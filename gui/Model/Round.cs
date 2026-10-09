using gui.Model.Managers.MarketManager;
using gui.Model.Managers.PlayerManager;
using gui.Model.Persistence;
using gui.Model.Phases;
using gui.Model.Phases.AuctionPhase;
using gui.Model.Phases.BureaucracyPhase;
using gui.Model.Phases.CityBuildingPhase;
using gui.Model.Phases.ResourceBuyingPhase;
using Serilog;

namespace gui.Model
{
    public class Round()
    {
        public event Action<Phase>? PhaseChanged;

        public event Action<PurchaseData>? PurchasedUpdated;
        
        public event Action<int, Player>? BuildUpdated;

        public Phase? CurrentPhase { get; set; }

        public async Task Run(PhaseKind from, bool skipFirstCheckpoint, CheckpointStore checkpoints)
        {
            PhaseKind[] order = [PhaseKind.Auction, PhaseKind.Buy, PhaseKind.Build, PhaseKind.Bureaucracy];
            var skipping = skipFirstCheckpoint;

            foreach (var kind in order)
            {
                if (kind < from)
                    continue;

                var skipCheckpoint = skipping && kind == from;
                skipping = false;
                await RunPhase(kind, skipCheckpoint, checkpoints);
            }
        }

        private async Task RunPhase(PhaseKind kind, bool skipCheckpoint, CheckpointStore checkpoints)
        {
            InputGate.Closed = true;
            try
            {
                PlayerManager.Instance.SetStateAll(Status.PlayerState.Wait);
                foreach (var player in PlayerManager.Instance.Players)
                    player.Clock.Stop();

                if (kind == PhaseKind.Buy && GameManager.Instance.IsRound(1) && !skipCheckpoint)
                    PlayerManager.Instance.Reorder();

                Phase phase = kind switch
                {
                    PhaseKind.Auction => new AuctionPhase(),
                    PhaseKind.Buy => new ResourceBuyingPhase(PurchasedUpdated),
                    PhaseKind.Build => new CityBuildingPhase(BuildUpdated),
                    _ => new BureaucracyPhase()
                };

                CurrentPhase = phase;
                PhaseChanged?.Invoke(phase);
                Log.Information($"Starting {phase.GetType().Name}");
                App.LogPanelViewModel.Add($"Starting {phase.GetType().Name}");

                if (!skipCheckpoint)
                    checkpoints.Commit(GameSnapshot.Capture(kind));

                var executing = phase.Execute();
                InputGate.Closed = false;
                await executing;
                Log.Information($"{phase.GetType().Name} completed");
            }
            finally
            {
                InputGate.Closed = false;
            }
        }

        public void UpdatePurchaseRecord(ResourceType type, int amount)
        {
            if (CurrentPhase is ResourceBuyingPhase phase)
            {
                Log.Information("UpdatePurchaseRecord when phase is ResourceBuyingPhase");
                phase.UpdatePurchaseRecord(type, amount);
            }
        }

        public void UpdateBuild(int amount)
        {
            if (CurrentPhase is CityBuildingPhase phase)
            {
                Log.Information("UpdatePurchaseRecord when phase is ResourceBuyingPhase");
                phase.UpdateBuild(amount);
            }
        }

        public void Done()
        {
            if (CurrentPhase is ResourceBuyingPhase buyPhase)
            {
                Log.Information($"Done purchase {CurrentPhase}");
                buyPhase.DonePurchase();
            }
            if(CurrentPhase is CityBuildingPhase buildPhase)
            {
                Log.Information($"Done build {CurrentPhase}");
                buildPhase.DoneBuild();
            }
            if(CurrentPhase is BureaucracyPhase bureaucracyPhase)
            {
                Log.Information($"Done bureacracy {CurrentPhase}");
                bureaucracyPhase.OnStartRoundPressed();
            }
        }

        public void Ready()
        {
            if (CurrentPhase is CityBuildingPhase buildPhase)
            {
                Log.Information($" Ready build {CurrentPhase}");
                buildPhase.ReadyBuild();
            }
        }
    }
}
