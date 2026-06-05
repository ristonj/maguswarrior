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
    private StagingManager _stagingManager = null!;
    private CardExpanded _expandedPanel = null!;
    private StagingAreaView _stagingAreaView = null!;
    private HBoxContainer _cardsContainer = null!;
    // Re-entrancy guard for OnCommitRequested. Today effects resolve synchronously
    // (Task.FromResult), so a second tap can't interleave — but once ResolveAll genuinely
    // awaits (UIBroker), an un-guarded second tap would re-enqueue the same staged cards
    // and double-apply every effect. The guard closes that window before async lands.
    private bool _committing;

    public override void _Ready() {
        AnchorLeft = 0f;
        AnchorRight = 1f;
        AnchorTop = 1f;
        AnchorBottom = 1f;
        OffsetTop = -350f;
        OffsetBottom = 0f;
        GrowHorizontal = GrowDirection.Both;
        GrowVertical = GrowDirection.Begin;

        _stagingAreaView = new StagingAreaView();
        _stagingAreaView.Name = "StagingAreaView";
        AddChild(_stagingAreaView);
        _stagingAreaView.AnchorLeft = 0f;
        _stagingAreaView.AnchorRight = 1f;
        _stagingAreaView.AnchorTop = 0f;
        _stagingAreaView.AnchorBottom = 1f;
        _stagingAreaView.OffsetBottom = -130f;
        _stagingAreaView.GrowHorizontal = GrowDirection.Both;
        _stagingAreaView.GrowVertical = GrowDirection.Both;
        _stagingAreaView.ZIndex = 1;

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

    public void Initialize(DeckManager deck, GameState state, EffectScheduler scheduler, StagingManager staging) {
        _deck = deck;
        _state = state;
        _scheduler = scheduler;
        _stagingManager = staging;
        _expandedPanel.PlayRequested += OnPlayRequested;
        _expandedPanel.PlaySidewaysRequested += OnPlaySidewaysRequested;
        _stagingAreaView.CommitRequested += OnCommitRequested;
        _stagingAreaView.UndoRequested += OnUndoRequested;
        _stagingAreaView.Initialize(staging);
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

    private void OnPlayRequested(string cardId) {
        var card = _deck.Hand.FirstOrDefault(c => c.Id == cardId);
        if (card is null) {
            Log.Debug("[UI]", $"OnPlayRequested: card '{cardId}' not found in hand — ignoring stale tap");
            return;
        }
        if (card.Unpowered is null) {
            Log.Warn("[UI]", $"OnPlayRequested: card '{cardId}' has no unpowered spec — cannot stage");
            return;
        }
        // Remove the card from hand FIRST — same atomicity discipline as OnPlaySidewaysRequested.
        var result = _deck.PlayCard(cardId);
        if (!result.IsSuccess) {
            Log.Warn("[UI]", $"OnPlayRequested: PlayCard failed for '{cardId}': {result.Error}");
            return;
        }
        _stagingManager.Stage(card, card.Unpowered.EffectType);
        Log.Debug("[UI]", $"Play staged: {cardId} → {card.Unpowered.EffectType}");
    }

    private void OnUndoRequested() {
        // Mirror OnCommitRequested's re-entrancy guard: while a commit is in flight, the
        // staged cards have been snapshotted and are being resolved. Unstaging during that
        // window would return a card to hand whose effect still commits from the snapshot
        // (double-state). Harmless today (ResolveAll is synchronous) — closes the window
        // before ResolveAll becomes genuinely awaitable (UIBroker). See 1b-3 commit guard.
        if (_committing) return;
        if (_stagingManager.StagedCards.Count == 0) return;
        var entry = _stagingManager.Unstage();
        if (entry is null) return;
        _deck.ReturnCard(entry.Card);
        Log.Debug("[UI]", $"Undo staged: {entry.Card.Id} returned to hand");
    }

    // async void is an accepted exception here: Godot signal handlers cannot return Task.
    // Safe because all current effects use Task.FromResult (synchronous path).
    private async void OnCommitRequested() {
        if (_committing || _stagingManager.StagedCards.Count == 0) return;
        _committing = true;
        try {
            foreach (var entry in _stagingManager.StagedCards.ToList()) {
                var effect = BuildEffect(entry);
                if (effect is null) {
                    Log.Warn("[UI]", $"OnCommitRequested: unsupported effect type {entry.EffectType} for '{entry.Card.Id}' — skipping");
                    continue;
                }
                var ctx = new EffectContext(entry.Card.Id, entry.EffectType, _state.CurrentPhase, false);
                _scheduler.Enqueue(effect, 0, ctx);
            }
            await _scheduler.ResolveAll(_state);
            _stagingManager.Clear();
            Log.Debug("[UI]", "Commit resolved");
        } finally {
            _committing = false;
        }
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

    private static IEffect? BuildEffect(StagingManager.StagedEntry entry) {
        var spec = entry.Card.Unpowered;
        if (spec is null) return null;
        return entry.EffectType switch {
            EffectType.Move         => new MoveEffect(spec.Move),
            EffectType.AttackMelee  => new AttackEffect(spec.Attack, EffectType.AttackMelee, AttackElement.Physical),
            EffectType.AttackRanged => new AttackEffect(spec.Attack, EffectType.AttackRanged, AttackElement.Physical),
            EffectType.Block        => new BlockEffect(spec.Block, AttackElement.Physical),
            EffectType.Influence    => new InfluenceEffect(spec.Influence),
            _                       => null
        };
    }
}
