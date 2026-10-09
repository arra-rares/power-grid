using gui.Model.Persistence;
using gui.Model.Utils;
using Serilog;
using static gui.Model.Managers.PlayerManager.Status;

namespace gui.Model.Managers.PlayerManager
{
    /// <summary>
    /// Singleton manager responsible for handling players and their states.
    /// </summary>
    public class PlayerManager
    {
        // ====== SINGLETON INSTANCE ======

        private static readonly PlayerManager _instance = new();
        public static PlayerManager Instance => _instance;
        private PlayerManager() 
        {
        }


        // ====== PRIVATE FIELDS ======

        public event EventHandler? PlayersOrderChanged;

        /// <summary>
        /// Stores all players, ordered by rank at time of request.
        /// </summary>
        private int _nextId = 1;

        private List<Player> _players = [];
        public List<Player> Players
        {
            get => _players;
            set
            {
                if (_players != value)
                {
                    _players = value;
                    PlayersOrderChanged?.Invoke(this, EventArgs.Empty); // Notify listeners of state changes
                }
            }
        }

        // ====== PUBLIC METHODS ======

        /// <summary>
        /// Adds a new player to the game.
        /// </summary>
        /// <param name="name">The player's name.</param>
        /// <param name="remoteId">The player's remoteId.</param>
        public Player AddPlayer(string name)
        {
            Player p = new(name);
            p.AssignId(_nextId++);
            _players.Add(p);
            return p;
        }

        public void ReplaceAll(List<Player> players)
        {
            _nextId = players.Count == 0 ? 1 : players.Max(player => player.Id) + 1;
            Players = players;
        }

        public void Clear()
        {
            _nextId = 1;
            Players = [];
        }

        /// <summary>
        /// Counts how many players are in a specific state.
        /// </summary>
        /// <param name="state">The state to count players for.</param>
        public int CountByState(PlayerState state)
        {
            return Players.Count(player => player.Status.State == state);
        }

        /// <summary>
        /// Gets a list of all players.
        /// </summary>
        public List<Player> GetPlayers()
        {
            return Players;
        }

        /// <summary>
        /// Reorders the players based on ranking criteria using PlayerComparer.
        /// </summary>
        public void Reorder()
        {
            Log.Information("Reorder players");

            Players = [.. Players.OrderBy(p => p, new PlayerComparer())];

            ApplyBureaucrat();
        }

        public void SetStateAll(PlayerState state)
        {
            Players.ForEach(p => SetPlayerState(p, state));
        }

        public List<Player> GetPlayersByState(PlayerState state)
        {
            return [.. Players.FindAll(p => p.Status.State == state)];
        }

        public List<Player> GetPlayersExcept(List<Player> players)
        {
            return Players.Except(players).ToList();
        }

        public void ApplyBureaucrat()
        {
            Log.Information("Apply bureaucrat");
            int index = Players.FindIndex(p => p.IsBureaucrat);

            if (index >= 0 && index < Players.Count - 1)
            {
                Log.Information("Bureaucrat index is {Index}", index);
                ListUtils.Swap(Players, index, index + 1);
                Log.Information("Swapped positions {Index} <-> {NextIndex}", index, index + 1);

                Log.Information("Invoking PlayersOrderChanged...");
                Task.Run(() => PlayersOrderChanged?.Invoke(this, EventArgs.Empty));
                Log.Information("PlayersOrderChanged event finished execution.");
            }
        }

        public void HandleStatusClick(Status status)
        {
            status.HasCoin = !status.HasCoin;
        }

        public void HandleClockClick(Clock clock)
        {
            clock.ChangeState();
        }

        public void SetPlayerState(Player player, PlayerState state)
        {
            if(player.Status.State == PlayerState.Active)
            {
                player.Clock.Stop();
            }
            if (state == PlayerState.Active)
            {
                if (GameSession.TakeStartWithoutDelay())
                    player.Clock.Start();
                else
                    player.Clock.StartWithDelay();
            }
            player.Status.State = state;
        }
    }
}
