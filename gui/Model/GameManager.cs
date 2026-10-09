using System.IO;
using System.Windows;
using gui.Model.Managers.CardManager;
using gui.Model.Managers.MarketManager;
using gui.Model.Managers.PlayerManager;
using gui.Model.Managers.RemoteManager;
using gui.Model.Managers.ResupplyManager;
using gui.Model.Persistence;
using gui.Model.Phases;
using gui.Model.Phases.ResourceBuyingPhase;
using Serilog;

namespace gui.Model
{
    public class GameManager
    {
        public static GameManager Instance { get; } = new();

        private readonly List<Round> _rounds = [];
        private readonly CheckpointStore _checkpoints = new();
        private int _roundNumber;
        private bool _loopRunning;
        private bool _saveFailed;

        public event Action<Phase>? PhaseChanged;

        public event Action<PurchaseData>? PurchaseRecordUpdated;

        public event Action<int, Player>? BuildUpdated;

        public int RoundNumber => _roundNumber;

        public bool IsRoundRunning => _loopRunning;

        private GameManager()
        {
        }

        public void SetRoundNumber(int roundNumber) => _roundNumber = roundNumber;

        public void NewGame()
        {
            _checkpoints.RetireAll();
            _roundNumber = 0;
            _rounds.Clear();
            _saveFailed = false;

            CardManager.Instance.ClearSession();
            PlayerManager.Instance.Clear();
            RemoteManager.Instance.Clear();
            MarketManager.Instance.Reload();
            ResupplyManager.Instance.Level = 1;
            LoadPlayersFromCsv();
            GameSession.Begin(Guid.NewGuid().ToString("N"));
        }

        public void StartGame(PhaseKind? resumePhase = null)
        {
            if (_loopRunning || _saveFailed)
                return;

            _loopRunning = true;
            _ = RunLoop(resumePhase);
        }

        public void AddPlayer(string name, int remoteId)
        {
            Player player = PlayerManager.Instance.AddPlayer(name);
            RemoteManager.Instance.AssignRemote(remoteId, player);
        }

        private void LoadPlayersFromCsv()
        {
            string csvPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Players.csv");

            Log.Information("csvPath: {csvPath}", csvPath);

            var lines = File.ReadAllLines(csvPath);

            foreach (var line in lines.Skip(1))
            {
                var parts = line.Split(',');

                if (parts.Length < 3)
                {
                    Log.Warning("Skipping malformed line: {line}", line);
                    continue;
                }

                string playerName = parts[0].Trim('"').Trim();
                bool isActive = parts[1].Trim('"').Trim() == "1";
                bool parsedRemoteId = int.TryParse(parts[2].Trim('"').Trim(), out int remoteId);

                if (isActive && parsedRemoteId)
                {
                    AddPlayer(playerName, remoteId);
                }
                else
                {
                    Log.Warning("Skipping inactive player or invalid remote ID: {line}", line);
                }
            }
        }

        private async Task RunLoop(PhaseKind? resumePhase)
        {
            try
            {
                var phase = resumePhase ?? PhaseKind.Auction;
                var resumed = resumePhase != null;
                if (!resumed)
                    _roundNumber = 1;

                await RunRound(phase, skipFirstCheckpoint: resumed);

                while (true)
                {
                    _roundNumber++;
                    await RunRound(PhaseKind.Auction, skipFirstCheckpoint: false);
                }
            }
            catch (Exception ex)
            {
                _saveFailed = true;
                Log.Error(ex, "Game loop stopped");
                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher == null)
                    return;

                dispatcher.Invoke(() => MessageBox.Show(
                    "Saving failed. Restart the app to resume the last successful checkpoint.",
                    "Power Grid"));
            }
            finally
            {
                _loopRunning = false;
            }
        }

        private async Task RunRound(PhaseKind from, bool skipFirstCheckpoint)
        {
            Round round = new();
            _rounds.Add(round);

            Log.Information("Round number {RoundCount} started!", _roundNumber);
            App.LogPanelViewModel.Add($"Round {_roundNumber} started");

            round.PhaseChanged += OnPhaseChanged;
            round.PurchasedUpdated += OnPurchaseRecordUpdated;
            round.BuildUpdated += OnBuildUpdated;
            try
            {
                await round.Run(from, skipFirstCheckpoint, _checkpoints);
            }
            finally
            {
                round.PhaseChanged -= OnPhaseChanged;
                round.PurchasedUpdated -= OnPurchaseRecordUpdated;
                round.BuildUpdated -= OnBuildUpdated;
            }
        }

        public void OnPhaseChanged(Phase phase)
        {
            PhaseChanged?.Invoke(phase);
        }

        public void OnPurchaseRecordUpdated(PurchaseData purchaseRecord)
        {
            Log.Information("OnPurchaseRecordUpdated");
            PurchaseRecordUpdated?.Invoke(purchaseRecord);
        }

        public void OnBuildUpdated(int amount, Player player)
        {
            Log.Information("OnBuildUpdated");
            BuildUpdated?.Invoke(amount, player);
        }

        public void UpdatePurchaseRecord(ResourceType type, int amount)
        {
            _rounds.Last().UpdatePurchaseRecord(type, amount);
        }

        public void UpdateBuild(int amount)
        {
            _rounds.Last().UpdateBuild(amount);
        }

        public void Done()
        {
            if (_rounds.Count == 0) return;

            _rounds.Last().Done();
        }

        public void Ready()
        {
            _rounds.Last().Ready();
        }

        public void ReloadPlayers()
        {
            PlayerManager.Instance.Clear();
            RemoteManager.Instance.Clear();
            LoadPlayersFromCsv();
        }

        public bool IsRound(int round)
        {
            return _roundNumber == round;
        }
    }
}
