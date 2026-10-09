using System.Text.Json.Serialization;
using gui.Model.Managers.CardManager;
using gui.Model.Managers.MarketManager;
using gui.Model.Managers.PlayerManager;
using gui.Model.Managers.RemoteManager;
using gui.Model.Managers.ResupplyManager;
using gui.Model;
using static gui.Model.Managers.PlayerManager.Status;

namespace gui.Model.Persistence
{
    public sealed class GameSnapshot
    {
        public int SchemaVersion { get; set; } = CheckpointStore.SchemaVersion;

        public string SessionId { get; set; } = "";

        public int Generation { get; set; }

        public DateTimeOffset SavedAt { get; set; }

        public int RoundNumber { get; set; }

        public string Phase { get; set; } = "";

        public bool IsLevel3 { get; set; }

        public int ResupplyLevel { get; set; }

        public List<PlayerSnapshot> Players { get; set; } = [];

        public List<CardFlagSnapshot> CardFlags { get; set; } = [];

        public List<MarketPileSnapshot> Market { get; set; } = [];

        [JsonIgnore]
        public PhaseKind PhaseKind { get; set; }

        public static GameSnapshot Capture(PhaseKind phase)
        {
            var snapshot = new GameSnapshot
            {
                SchemaVersion = CheckpointStore.SchemaVersion,
                SessionId = GameSession.SessionId,
                SavedAt = DateTimeOffset.UtcNow,
                RoundNumber = GameManager.Instance.RoundNumber,
                Phase = phase.ToString(),
                PhaseKind = phase,
                IsLevel3 = CardManager.Instance.IsLevel3,
                ResupplyLevel = ResupplyManager.Instance.Level,
                CardFlags = CardManager.Instance.ExportEndsTurn(),
                Market = MarketManager.Instance.ExportPiles(),
                Players = PlayerManager.Instance.Players.Select(player => new PlayerSnapshot
                {
                    Id = player.Id,
                    Name = player.Name,
                    RemoteId = RemoteManager.Instance.GetPlayerRemote(player.Name),
                    Cities = player.CitiesCount,
                    IsBureaucrat = player.IsBureaucrat,
                    HasCoin = player.Status.HasCoin,
                    State = player.Status.State.ToString(),
                    ElapsedMilliseconds = (long)player.Clock.TimePassed.TotalMilliseconds,
                    CardIds = player.Cards.Select(card => card.Id).ToList(),
                    LastRemovedCardId = player.LastRemovedCard?.Id
                }).ToList()
            };
            return snapshot;
        }

        public bool TryValidate(Func<int, bool> cardExists, out string error)
        {
            error = "";
            if (SchemaVersion != CheckpointStore.SchemaVersion)
            {
                error = $"Unsupported schema {SchemaVersion}.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(SessionId) || Generation < 1 || RoundNumber < 1)
            {
                error = "Missing session, generation, or round.";
                return false;
            }
            if (!Enum.TryParse<PhaseKind>(Phase, out var phase))
            {
                error = $"Unknown phase '{Phase}'.";
                return false;
            }
            PhaseKind = phase;
            if (ResupplyLevel is < 1 or > 3)
            {
                error = "Resupply level is out of range.";
                return false;
            }
            if (Players.Count == 0)
            {
                error = "Checkpoint has no players.";
                return false;
            }
            if (Players.Select(player => player.Id).Distinct().Count() != Players.Count)
            {
                error = "Duplicate player id.";
                return false;
            }
            var remoteIds = Players.Select(player => player.RemoteId).Where(id => id != 0).ToList();
            if (remoteIds.Distinct().Count() != remoteIds.Count)
            {
                error = "Duplicate remote id.";
                return false;
            }

            var owned = new HashSet<int>();
            foreach (var player in Players)
            {
                if (string.IsNullOrWhiteSpace(player.Name))
                {
                    error = "Player name is empty.";
                    return false;
                }
                if (!Enum.TryParse<PlayerState>(player.State, out _))
                {
                    error = $"Unknown player state '{player.State}'.";
                    return false;
                }
                if (player.ElapsedMilliseconds < 0)
                {
                    error = "Negative clock.";
                    return false;
                }
                foreach (var cardId in player.CardIds)
                {
                    if (!cardExists(cardId) || !owned.Add(cardId))
                    {
                        error = $"Card {cardId} is missing or owned twice.";
                        return false;
                    }
                }
                if (player.LastRemovedCardId is int removed && !cardExists(removed))
                {
                    error = $"Last removed card {removed} is missing.";
                    return false;
                }
            }

            foreach (var flag in CardFlags)
            {
                if (!cardExists(flag.Id))
                {
                    error = $"Card flag {flag.Id} is missing from the catalog.";
                    return false;
                }
            }

            var types = new HashSet<string>();
            foreach (var pile in Market)
            {
                if (!Enum.TryParse<ResourceType>(pile.Type, out _))
                {
                    error = $"Unknown resource '{pile.Type}'.";
                    return false;
                }
                if (!types.Add(pile.Type))
                {
                    error = $"Duplicate market pile '{pile.Type}'.";
                    return false;
                }
                var ids = pile.Empty.Select(token => token.Id).Concat(pile.Filled.Select(token => token.Id)).ToList();
                if (ids.Distinct().Count() != ids.Count)
                {
                    error = $"Duplicate token id in {pile.Type}.";
                    return false;
                }
            }

            return true;
        }

        public void Apply()
        {
            CardManager.Instance.ApplyEndsTurn(CardFlags);
            CardManager.Instance.SetLevel3(IsLevel3);
            CardManager.Instance.ClearAssignments();

            RemoteManager.Instance.Clear();

            var players = new List<Player>();
            foreach (var saved in Players)
            {
                var player = new Player(saved.Name);
                player.AssignId(saved.Id);
                player.CitiesCount = saved.Cities;
                player.IsBureaucrat = saved.IsBureaucrat;
                player.Status.HasCoin = saved.HasCoin;
                player.Status.State = Enum.Parse<PlayerState>(saved.State);
                player.Clock.TimePassed = TimeSpan.FromMilliseconds(saved.ElapsedMilliseconds);

                foreach (var cardId in saved.CardIds)
                    CardManager.Instance.Attach(player, CardManager.Instance.GetCard(cardId));

                if (saved.LastRemovedCardId is int removed)
                    player.SetLastRemovedCard(CardManager.Instance.GetCard(removed));

                if (saved.RemoteId != 0)
                    RemoteManager.Instance.AssignRemote(saved.RemoteId, player);

                players.Add(player);
            }

            PlayerManager.Instance.ReplaceAll(players);
            MarketManager.Instance.ImportPiles(Market);
            ResupplyManager.Instance.Level = ResupplyLevel;
            GameManager.Instance.SetRoundNumber(RoundNumber);
            GameSession.Begin(SessionId);
            GameSession.ArmResumeClock();
        }
    }

    public sealed class PlayerSnapshot
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int RemoteId { get; set; }
        public int Cities { get; set; }
        public bool IsBureaucrat { get; set; }
        public bool HasCoin { get; set; }
        public string State { get; set; } = "";
        public long ElapsedMilliseconds { get; set; }
        public List<int> CardIds { get; set; } = [];
        public int? LastRemovedCardId { get; set; }
    }

    public sealed class CardFlagSnapshot
    {
        public int Id { get; set; }
        public bool EndsTurn { get; set; }
    }

    public sealed class MarketPileSnapshot
    {
        public string Type { get; set; } = "";
        public List<TokenSnapshot> Empty { get; set; } = [];
        public List<TokenSnapshot> Filled { get; set; } = [];
    }

    public sealed class TokenSnapshot
    {
        public int Id { get; set; }
        public int Price { get; set; }
    }
}
