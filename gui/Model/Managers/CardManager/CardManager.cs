using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using gui.Model.Managers.MarketManager;
using gui.Model.Managers.PlayerManager;
using gui.Model.Persistence;
using gui.Model.Phases;
using gui.View;
using gui.View.CardEditor;
using Serilog;

namespace gui.Model.Managers.CardManager
{
    public class CardManager
    {
        private static readonly CardManager _instance = new();
        public static CardManager Instance => _instance;

        private readonly Dictionary<int, Card> _cards = [];
        private readonly Dictionary<Card, Player> _assignedCards = [];
        private const string CardsFilePath = "cards.json";

        public event Action<Card>? NewCardScanned;
        public event Action<int>? UnknownCardScanned;

        public bool IsLevel3 { get; private set; }

        private CardManager()
        {
            LoadCards();
        }

        // Card Management
        public void AddCard(Card card)
        {
            _cards[card.Id] = card;
            SaveCards();
        }

        public Card GetCard(int cardId)
        {
            return _cards[cardId];
        }

        public void AssignCard(Player player, Card? card)
        {
            if (card == null) return;
            _assignedCards[card] = player;
            player.AddCard(card);
        }

        public void BurnCard(Card card)
        {
            if (_assignedCards.Remove(card, out var assignedPlayer))
            {
                App.LogPanelViewModel.Add($"Card Burned: {card.Rank}, player: {assignedPlayer.Name}");
                assignedPlayer.BurnCard(card);
            }
        }

        // Card Scanning
        public bool Contains(int cardId) => _cards.ContainsKey(cardId);

        public List<CardFlagSnapshot> ExportEndsTurn()
        {
            return _cards.Values
                .Select(card => new CardFlagSnapshot { Id = card.Id, EndsTurn = card.EndsTurn })
                .ToList();
        }

        public void ApplyEndsTurn(IEnumerable<CardFlagSnapshot> flags)
        {
            foreach (var flag in flags)
            {
                if (_cards.TryGetValue(flag.Id, out var card))
                    card.EndsTurn = flag.EndsTurn;
            }
        }

        public void SetLevel3(bool value) => IsLevel3 = value;

        public void ClearAssignments() => _assignedCards.Clear();

        public void Attach(Player player, Card card)
        {
            _assignedCards[card] = player;
            player.Cards.Add(card);
        }

        public void ClearSession()
        {
            _assignedCards.Clear();
            IsLevel3 = false;
            LoadCards();
        }

        public void OnCardScanned(int cardId)
        {
            if (InputGate.Closed)
                return;

            Log.Information($"OnCardScanned, id: {cardId}");

            if (!_cards.TryGetValue(cardId, out var card))
            {
                Log.Information($"Unknown card scanned: {cardId}");
                UnknownCardScanned?.Invoke(cardId);
                return;
            }

            HandleCardScanned(card);
        }

        private void HandleCardScanned(Card card)
        {
            if (card.Type == CardType.Event)
            {
                HandleEventCard(card);
            }

            App.LogPanelViewModel.Add($"Card scanned: {card.Id}, {card.Type}, {card.Rank}");
            BurnCard(card);
            NewCardScanned?.Invoke(card);
        }

        private void HandleEventCard(Card card)
        {
            card.MarketEffect
                .Select((effect, index) => (effect, index))
                .ToList()
                .ForEach(x => MarketManager.MarketManager.Instance.Update((ResourceType)x.index, x.effect));

            MarketManager.MarketManager.Instance.UpdateMostLimitedResource(card.MarketEffectLowest);
            MarketManager.MarketManager.Instance.UpdateMostAvaialableResource(card.MarketEffectHighest);
            IsLevel3 = card.Level3;
        }

        // Persistence
        private void LoadCards()
        {
            if (!File.Exists(CardsFilePath))
            {
                File.WriteAllText(CardsFilePath, "[]");
            }

            try
            {
                var loadedCards = JsonSerializer.Deserialize<List<Card>>(File.ReadAllText(CardsFilePath)) ?? [];
                _cards.Clear();
                loadedCards.ToList().ForEach(card => _cards[card.Id] = card);
            }
            catch (Exception ex)
            {
                Log.Error($"Error loading cards.json: {ex.Message}");
            }
        }

        private void SaveCards()
        {
            Log.Information($"Save Cards: {CardsFilePath}");
            try
            {
                var json = JsonSerializer.Serialize(_cards.Values, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(CardsFilePath, json);
            }
            catch (Exception ex)
            {
                Log.Error($"Error saving cards.json: {ex.Message}");
            }
        }
    }
}
