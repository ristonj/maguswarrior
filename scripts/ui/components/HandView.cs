using System;
using System.Linq;
using Godot;
using MagusWarrior.Cards;
using MagusWarrior.Cards.Effects;
using MagusWarrior.Cards.Effects.Combat;
using MagusWarrior.Cards.Effects.Influence;
using MagusWarrior.Cards.Effects.Movement;
using MagusWarrior.Core;
using MagusWarrior.Core.Types;
using MagusWarrior.Deck;

namespace MagusWarrior.UI;

public partial class HandView : Control {
    private DeckManager _deck = null!;
    private GameState _state = null!;
    private EffectScheduler _scheduler = null!;
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

    public void Initialize(DeckManager deck, GameState state, EffectScheduler scheduler) {
        _deck = deck;
        _state = state;
        _scheduler = scheduler;
        _expandedPanel.PlaySidewaysRequested += OnPlaySidewaysRequested;
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

    // async void is an accepted exception here: Godot signal handlers cannot return Task.
    // Safe because all current effects use Task.FromResult (synchronous path).
    private async void OnPlaySidewaysRequested(string cardId) {
        var sideways = SidewaysRule.GetEffect(_state.CurrentPhase);
        if (sideways is null) {
            Log.Debug("[UI]", $"PlaySideways: no sideways effect in {_state.CurrentPhase}");
            return;
        }
        // Remove the card from hand FIRST: the resource is granted only if the card
        // actually leaves the hand. This guards against double-resolve and orphaned
        // resources once ResolveAll becomes genuinely awaitable (UIBroker). Full atomic
        // rollback (card returns to hand if a future awaitable effect fails) is deferred
        // to the UIBroker story — Hand is not yet part of the GameState snapshot.
        var result = _deck.PlayCard(cardId);
        if (!result.IsSuccess) {
            Log.Warn("[UI]", $"PlaySideways: PlayCard failed for '{cardId}': {result.Error}");
            return;
        }
        var (effectType, amount) = sideways.Value;
        IEffect effect = effectType switch {
            EffectType.Move        => new MoveEffect(amount),
            EffectType.AttackMelee => new AttackEffect(amount, EffectType.AttackMelee, AttackElement.Physical),
            EffectType.Block       => new BlockEffect(amount, AttackElement.Physical),
            EffectType.Influence   => new InfluenceEffect(amount),
            _                      => throw new InvalidOperationException(
                                          $"SidewaysRule returned unexpected EffectType: {effectType}")
        };
        var ctx = new EffectContext(cardId, effectType, _state.CurrentPhase, false);
        _scheduler.Enqueue(effect, 0, ctx);
        await _scheduler.ResolveAll(_state);
        Log.Debug("[UI]", $"PlaySideways: {cardId} → {effectType} {amount} applied");
    }
}
