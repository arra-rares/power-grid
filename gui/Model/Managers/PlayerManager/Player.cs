using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using gui.Model.Managers.CardManager;
using gui.Model.Managers.MarketManager;

namespace gui.Model.Managers.PlayerManager
{

    /// <summary>
    /// Represents a player in the game, tracking their state, cards, and cities.
    /// </summary>
    public class Player(string name) : INotifyPropertyChanged
    {
        // ====== PROPERTIES ======

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Player's name.
        /// </summary>
        public int Id { get; private set; }

        public void AssignId(int id) => Id = id;

        private string _name = name;
        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
                }
            }
        }

        /// <summary>
        /// Number of cities built by the player.
        /// </summary>
        public int CitiesCount { get; set; } = 0;

        /// <summary>
        /// The last card that was removed from the player's hand.
        /// </summary>
        public Card? LastRemovedCard { get; private set; } = null;

        public void SetLastRemovedCard(Card? card) => LastRemovedCard = card;

        /// <summary>
        /// Indicates whether the player holds the "Bureaucrat" special card.
        /// A player with this status swaps places with the player directly below them
        /// in the ranking order after reordering.
        /// </summary>
        public bool IsBureaucrat { get; set; } = false;

        public Clock Clock { get; private set; } = new Clock();

        public Status Status { get; set; } = new Status();

        /// <summary>
        /// Player's collection of cards, indexed by card ID.
        /// </summary>
        public HashSet<Card> Cards = [];

        // ====== CARD MANAGEMENT ======

        /// <summary>
        /// Adds a card to the player's collection.
        /// </summary>
        public void AddCard(Card card)
        {
            App.LogPanelViewModel.Add($"{Name}: bought card {card.Rank}");
            Cards.Add(card);

            // this is a fancy way to check if card is bureaucrat
            IsBureaucrat |= card.Bureaucrat;
        }

        /// <summary>
        /// Removes a card from the player's collection and tracks it as the last removed card.
        /// </summary>
        public void BurnCard(Card card)
        {
            if (Cards.Remove(card))
            {
                LastRemovedCard = card;
            }
        }

        /// <summary>
        /// Gets the highest-ranked card owned by the player.
        /// </summary>
        public Card? HighestCard()
        {
            return Cards.OrderByDescending(card => card.Rank).FirstOrDefault();
        }

        /// <summary>
        /// Calculates the total rank sum of all cards owned by the player.
        /// </summary>
        public int CardRank()
        {
            return Cards.Sum(card => card.Rank);
        }

        // ====== GAMEPLAY ACTIONS ======

        /// <summary>
        /// Increases the number of cities owned by the player.
        /// </summary>
        public void BuildCities(int count)
        {
            App.LogPanelViewModel.Add($"{Name}: built {count} cities");
            CitiesCount += count;
        }

        /// <summary>
        /// Toggles whether the player has the coin.
        /// </summary>
        public void ToggleCoin()
        {
            Status.HasCoin = !Status.HasCoin;
            App.LogPanelViewModel.Add($"{Name}: hascoin {Status.HasCoin}");
        }

        public void DestroyCity()
        {
            if(CitiesCount > 0)
                App.LogPanelViewModel.Add($"{Name}: destroyed one city");
                CitiesCount--;
        }

        public List<ResourceType> SupportedResources()
        {
            return [.. Cards
                .SelectMany(card => card.SupportedResources
                    .Select((hasResource, index) => new { hasResource, index })
                    .Where(x => x.hasResource)
                    .Select(x => (ResourceType)x.index))
                .Distinct()
                .OrderBy(r => (int)r)];
        }

        public int CountPowerPlantCards()
        {
            return Cards.Count(card => card.Type == CardType.PowerPlant);
        }
    }

    /// <summary>
    /// Compares players based on game rules for ranking order.
    /// </summary>
    public class PlayerComparer : IComparer<Player>
    {
        public int Compare(Player? x, Player? y)
        {
            if (x == null || y == null) return 0;

            int result = y.CitiesCount.CompareTo(x.CitiesCount);
            if (result != 0) return result;

            result = y.CardRank().CompareTo(x.CardRank());
            if (result != 0) return result;

            result = (y.HighestCard()?.Rank ?? 0).CompareTo(x.HighestCard()?.Rank ?? 0);
            if (result != 0) return result;

            result = (y.HighestCard()?.Plus == true).CompareTo(x.HighestCard()?.Plus == true);
            if (result != 0) return result;

            return (y.LastRemovedCard?.Rank ?? 0).CompareTo(x.LastRemovedCard?.Rank ?? 0);
        }
    }
}
