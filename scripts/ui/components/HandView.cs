using System;
using System.Linq;
using Godot;
using MagusWarrior.Cards;
using MagusWarrior.Cards.Effects;
using MagusWarrior.Cards.Effects.Combat;
using MagusWarrior.Cards.Effects.Influence;
using MagusWarrior.Cards.Effects.Movement;
using MagusWarrior.Cards.Effects.Special;
using MagusWarrior.Core;
using MagusWarrior.Core.Types;
using MagusWarrior.Deck;
using MagusWarrior.Map;

namespace MagusWarrior.UI;

public partial class HandView : Control {
    private DeckManager _deck = null!;
    private GameState _state = null!;
    private EffectScheduler _scheduler = null!;
    private WorldMap _map = null!;
    private CardExpanded _expandedPanel = null!;
    private StagingAreaView _stagingAreaView = null!;
    private HBoxContainer _cardsContainer = null!;
    private InputLock _lock = null!;
    private ImprovisationView? _improvView;

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

    public void Initialize(DeckManager deck, GameState state, EffectScheduler scheduler, InputLock inputLock, WorldMap map) {
        _deck = deck;
        _state = state;
        _scheduler = scheduler;
        _map = map;
        _lock = inputLock;
        _expandedPanel.PlayRequested += OnPlayRequested;
        _expandedPanel.PlaySidewaysRequested += OnPlaySidewaysRequested;
        _stagingAreaView.UndoRequested += OnUndoRequested;
        _stagingAreaView.Initialize(state);
        deck.HandChanged += RefreshHand;
        state.PhaseChanged += RefreshHand;
        RefreshHand();
    }

    public void SetImprovisationView(ImprovisationView view) {
        _improvView = view;
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
        if (card.Type == CardType.Wound) {
            Log.Debug("[UI]", $"OnCardTapped: '{cardId}' is a Wound — not tappable, ignoring");
            return;
        }
        if (_state.CurrentPhase == GamePhase.Rest) {
            bool hasLegalPlay = card.Unpowered != null
                && PhaseGate.IsLegal(card.Unpowered.EffectType, GamePhase.Rest);
            if (!hasLegalPlay) {
                Log.Debug("[UI]", $"OnCardTapped: '{cardId}' — no legal play in Rest phase; use rest controls to discard");
                return;
            }
        }
        _expandedPanel.Open(card, _state.CurrentPhase);
    }

    // async void accepted: Godot signal handler cannot return Task. InputLock prevents re-entry.
    private async void OnPlayRequested(string cardId) {
        if (!_lock.TryAcquire()) return;
        try {
            var card = _deck.Hand.FirstOrDefault(c => c.Id == cardId);
            if (card is null) {
                Log.Debug("[UI]", $"OnPlayRequested: card '{cardId}' not found in hand — ignoring stale tap");
                return;
            }
            if (card.Type == CardType.Wound) {
                Log.Warn("[UI]", $"OnPlayRequested: '{cardId}' is a Wound — cannot be played");
                return;
            }
            // Defense-in-depth Rest guard: a CardExpanded panel opened in a play phase can stay
            // open across a Rest declaration, so Play can still be tapped during Rest.
            if (_state.CurrentPhase == GamePhase.Rest
                && !(card.Unpowered != null && PhaseGate.IsLegal(card.Unpowered.EffectType, GamePhase.Rest))) {
                Log.Warn("[UI]", $"OnPlayRequested: '{cardId}' has no legal play in Rest phase");
                return;
            }
            // Intercept multi-step cards before the normal play path. Activate handles PlayCard internally.
            if (cardId == "improvisation" && _improvView != null) {
                _improvView.Activate(card);
                return;
            }
            if (card.Unpowered is null) {
                Log.Warn("[UI]", $"OnPlayRequested: card '{cardId}' has no unpowered spec");
                return;
            }
            var result = _deck.PlayCard(cardId);
            if (!result.IsSuccess) {
                Log.Warn("[UI]", $"OnPlayRequested: PlayCard failed for '{cardId}': {result.Error}");
                return;
            }
            var effect = BuildEffect(card, card.Unpowered.EffectType);
            if (effect is null) {
                Log.Warn("[UI]", $"OnPlayRequested: unsupported effect type {card.Unpowered.EffectType} for '{cardId}'");
                return;
            }
            var stateBefore = _state.TakeSnapshot();   // capture BEFORE enqueue
            var ctx = new EffectContext(card.Id, card.Unpowered.EffectType, _state.CurrentPhase, false);
            _scheduler.Enqueue(effect, 0, ctx);
            await _scheduler.ResolveAll(_state);
            _state.UndoController.PushCardPlay(card.Id, null, stateBefore);
            Log.Debug("[UI]", $"Play resolved: {cardId} → {card.Unpowered.EffectType}");
        } finally {
            _lock.Release();
        }
    }

    private void OnUndoRequested() {
        if (_lock.IsLocked) return;
        var result = _state.UndoController.ExecuteUndo(_state, _deck, _map);
        if (result.IsSuccess)
            Log.Debug("[UI]", $"Undo: {result.Value}");
        else
            Log.Debug("[UI]", $"OnUndoRequested: {result.Error}");
    }

    // async void accepted: Godot signal handler cannot return Task. InputLock prevents re-entry.
    private async void OnPlaySidewaysRequested(string cardId) {
        if (!_lock.TryAcquire()) return;
        try {
            // Wounds cannot be played in any way through the normal hand flow (rulebook p4).
            var card = _deck.Hand.FirstOrDefault(c => c.Id == cardId);
            if (card is not null && card.Type == CardType.Wound) {
                Log.Warn("[UI]", $"OnPlaySidewaysRequested: '{cardId}' is a Wound — cannot be played sideways");
                return;
            }
            var sideways = SidewaysRule.GetEffect(_state.CurrentPhase);
            if (sideways is null) {
                Log.Debug("[UI]", $"PlaySideways: no sideways effect in {_state.CurrentPhase}");
                return;
            }
            // Remove card from hand FIRST to guard against double-resolve.
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
            var stateBefore = _state.TakeSnapshot();   // capture BEFORE enqueue
            var ctx = new EffectContext(cardId, effectType, _state.CurrentPhase, false);
            _scheduler.Enqueue(effect, 0, ctx);
            await _scheduler.ResolveAll(_state);
            _state.UndoController.PushCardPlay(cardId, null, stateBefore);
            Log.Debug("[UI]", $"PlaySideways: {cardId} → {effectType} {amount} applied");
        } finally {
            _lock.Release();
        }
    }

    private static IEffect? BuildEffect(CardDefinition card, EffectType effectType) {
        var spec = card.Unpowered;
        if (spec is null) return null;
        return effectType switch {
            EffectType.Move         => new MoveEffect(spec.Move),
            EffectType.AttackMelee  => new AttackEffect(spec.Attack, EffectType.AttackMelee, AttackElement.Physical),
            EffectType.AttackRanged => new AttackEffect(spec.Attack, EffectType.AttackRanged, AttackElement.Physical),
            EffectType.AttackSiege  => new AttackEffect(spec.Attack, EffectType.AttackSiege, AttackElement.Physical),
            EffectType.Block        => new BlockEffect(spec.Block, AttackElement.Physical),
            EffectType.Influence    => new InfluenceEffect(spec.Influence),
            _                       => null
        };
    }
}
