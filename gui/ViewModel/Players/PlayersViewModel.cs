using gui.Helpers;
using gui.Model;
using gui.Model.Managers.PlayerManager;
using gui.Model.Persistence;
using Serilog;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace gui.ViewModel.Players
{
    class PlayersViewModel : ViewModelBase
    {
        // 2. Properties
        public ObservableCollection<PlayerPanelViewModel>? PlayerPanels { get; private set; }

        public ICommand ReloadCommand { get; }
        public ICommand ReorderCommand { get; }

        // 3. Constructors
        public PlayersViewModel()
        {
            PlayerManager.Instance.PlayersOrderChanged += OnPlayerOrderChange;

            // Load initial data
            LoadPlayerPanels();

            // Initialize ReloadCommand
            ReloadCommand = new RelayCommand(_ => ReloadPlayers());
            ReorderCommand = new RelayCommand(_ => ReorderPlayers());
        }

        // 4. Private Methods
        private void LoadPlayerPanels()
        {
            Log.Information("LoadPlayersPanel");
            var players = PlayerManager.Instance.GetPlayers();
            if (players == null)
            {
                Log.Warning("no players, this should not happen");
                return;
            }

            PlayerPanels?.Clear();

            PlayerPanels = new ObservableCollection<PlayerPanelViewModel>(
                players.Select(player =>
                {
                    var playerPanelViewModel = new PlayerPanelViewModel(player);
                    playerPanelViewModel.StatusClicked += OnStatusClicked;
                    playerPanelViewModel.ClockClicked += OnClockClicked;
                    return playerPanelViewModel;
                })
            );
        }


        private void ReorderPlayers()
        {
            PlayerManager.Instance.Reorder();
        }

        private void ReloadPlayers()
        {
            if (GameSession.Active)
            {
                Log.Information("Reload players is blocked while a session is active.");
                return;
            }

            GameManager.Instance.ReloadPlayers();

            LoadPlayerPanels();

            // Notify UI about changes
            OnPropertyChanged(nameof(PlayerPanels));
        }

        private void OnStatusClicked(object? sender, Status status)
        {
            PlayerManager.Instance.HandleStatusClick(status);
        }

        private void OnClockClicked(object? sender, Clock clock)
        {
            PlayerManager.Instance.HandleClockClick(clock);
        }

        private void OnPlayerOrderChange(object? sender, EventArgs e)
        {

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var players = PlayerManager.Instance.GetPlayers();
                var reorderedPanels = players
                    .Select(player => PlayerPanels?.FirstOrDefault(p => p.Name == player.Name)) // Corrected predicate
                    .Where(p => p != null)
                    .ToList();

                PlayerPanels?.Clear(); // Clear and refill to trigger UI updates
                foreach (var panel in reorderedPanels)
                {
                    if (panel != null)
                    {
                        PlayerPanels?.Add(panel);
                    }
                }
            });
        }

        public void MovePlayer(PlayerPanelViewModel source, PlayerPanelViewModel target)
        {
            if (source == null || target == null || source == target || PlayerPanels == null)
                return;

            int oldIndex = PlayerPanels.IndexOf(source);
            int newIndex = PlayerPanels.IndexOf(target);

            if (oldIndex < 0 || newIndex < 0 || oldIndex == newIndex)
                return;

            PlayerPanels.Move(oldIndex, newIndex);

            // Update PlayerManager order
            var newPlayerOrder = PlayerPanels.Select(p => p.Player).ToList();
            PlayerManager.Instance.Players = newPlayerOrder;
        }

    }
}
