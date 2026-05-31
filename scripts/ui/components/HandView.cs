using System.Linq;
using Godot;
using MagusWarrior.Cards;
using MagusWarrior.Core;
using MagusWarrior.Core.Types;
using MagusWarrior.Deck;

namespace MagusWarrior.UI;

public partial class HandView : Control {
    [Export] public PackedScene CardCompactScene { get; set; } = null!;

    private DeckManager _deck = null!;
    private GameState _state = null!;
    private CardExpanded _expandedPanel = null!;

    public void Initialize(DeckManager deck, GameState state) {
        _deck = deck;
        _state = state;
        _expandedPanel = GetNode<CardExpanded>("CardExpandedPanel");
        deck.HandChanged += RefreshHand;
        RefreshHand();
    }

    private void RefreshHand() {
        var container = GetNode<HBoxContainer>("CardsContainer");
        foreach (Node child in container.GetChildren())
            child.QueueFree();

        foreach (var card in _deck.Hand) {
            var node = CardCompactScene.Instantiate<CardCompact>();
            container.AddChild(node);
            node.Initialize(card, _state.CurrentPhase);
            node.CardTapped += OnCardTapped;
        }
        Log.Debug("[UI]", $"HandView refreshed: {_deck.Hand.Count} cards");
    }

    private void OnCardTapped(string cardId) {
        if (_expandedPanel.Visible)
            _expandedPanel.Close();
        var card = _deck.Hand.FirstOrDefault(c => c.Id == cardId);
        if (card is null) {
            Log.Debug("[UI]", $"OnCardTapped: card '{cardId}' not found in hand — ignoring stale tap");
            return;
        }
        _expandedPanel.Open(card, _state.CurrentPhase);
    }
}
