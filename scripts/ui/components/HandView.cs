using System.Linq;
using Godot;
using MagusWarrior.Cards;
using MagusWarrior.Core;
using MagusWarrior.Core.Types;
using MagusWarrior.Deck;

namespace MagusWarrior.UI;

public partial class HandView : Control {
    private DeckManager _deck = null!;
    private GameState _state = null!;
    private CardExpanded _expandedPanel = null!;
    private HBoxContainer _cardsContainer = null!;

    public override void _Ready() {
        AnchorLeft = 0f;
        AnchorRight = 1f;
        AnchorTop = 1f;
        AnchorBottom = 1f;
        OffsetTop = -350f;
        OffsetBottom = 0f;
        GrowHorizontal = GrowDirection.Both;
        GrowVertical = GrowDirection.Begin;

        _expandedPanel = new CardExpanded();
        _expandedPanel.Name = "CardExpandedPanel";
        AddChild(_expandedPanel);
        _expandedPanel.AnchorLeft = 0f;
        _expandedPanel.AnchorRight = 1f;
        _expandedPanel.AnchorTop = 0f;
        _expandedPanel.AnchorBottom = 1f;
        _expandedPanel.OffsetBottom = -130f;
        _expandedPanel.GrowHorizontal = GrowDirection.Both;
        _expandedPanel.GrowVertical = GrowDirection.Both;

        _cardsContainer = new HBoxContainer();
        _cardsContainer.Name = "CardsContainer";
        AddChild(_cardsContainer);
        _cardsContainer.AnchorLeft = 0f;
        _cardsContainer.AnchorRight = 1f;
        _cardsContainer.AnchorTop = 1f;
        _cardsContainer.AnchorBottom = 1f;
        _cardsContainer.OffsetTop = -130f;
        _cardsContainer.OffsetBottom = 0f;
        _cardsContainer.GrowHorizontal = GrowDirection.Both;
        _cardsContainer.GrowVertical = GrowDirection.Begin;
        _cardsContainer.AddThemeConstantOverride("separation", 8);
    }

    public void Initialize(DeckManager deck, GameState state) {
        _deck = deck;
        _state = state;
        deck.HandChanged += RefreshHand;
        RefreshHand();
    }

    private void RefreshHand() {
        foreach (Node child in _cardsContainer.GetChildren())
            child.QueueFree();

        foreach (var card in _deck.Hand) {
            var node = new CardCompact();
            _cardsContainer.AddChild(node);
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
