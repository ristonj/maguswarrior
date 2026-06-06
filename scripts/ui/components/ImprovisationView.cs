using System.Linq;
using Godot;
using MagusWarrior.Cards;
using MagusWarrior.Cards.Effects;
using MagusWarrior.Core;
using MagusWarrior.Core.Types;
using MagusWarrior.Deck;

namespace MagusWarrior.UI;

public partial class ImprovisationView : Control {
    private DeckManager _deck = null!;
    private GameState _state = null!;
    private StagingManager _stagingManager = null!;
    private Label _statusLabel = null!;
    private VBoxContainer _discardPanel = null!;
    private HBoxContainer _resourcePanel = null!;

    private CardDefinition? _improvCard;
    private CardDefinition? _discardedCard;

    public override void _Ready() {
        AnchorLeft = 0f;
        AnchorRight = 1f;
        AnchorTop = 0f;
        AnchorBottom = 1f;
        GrowHorizontal = GrowDirection.Both;
        GrowVertical = GrowDirection.Both;
        ZIndex = 5;
        Visible = false;

        var background = new PanelContainer();
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var layout = new VBoxContainer();
        layout.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        background.AddChild(layout);

        _statusLabel = new Label();
        _statusLabel.AddThemeFontSizeOverride("font_size", 28);
        _statusLabel.AutowrapMode = TextServer.AutowrapMode.Word;
        layout.AddChild(_statusLabel);

        _discardPanel = new VBoxContainer();
        layout.AddChild(_discardPanel);

        _resourcePanel = new HBoxContainer();
        _resourcePanel.AddThemeConstantOverride("separation", 16);
        _resourcePanel.Visible = false;
        layout.AddChild(_resourcePanel);
    }

    public void Initialize(DeckManager deck, GameState state, StagingManager staging) {
        _deck = deck;
        _state = state;
        _stagingManager = staging;
    }

    public void Activate(CardDefinition improvCard) {
        bool anyOptionLegal =
            PhaseGate.IsLegal(EffectType.Move, _state.CurrentPhase)
            || PhaseGate.IsLegal(EffectType.AttackMelee, _state.CurrentPhase)
            || PhaseGate.IsLegal(EffectType.Block, _state.CurrentPhase)
            || PhaseGate.IsLegal(EffectType.Influence, _state.CurrentPhase);
        if (!anyOptionLegal) {
            Log.Warn("[UI]", $"ImprovisationView.Activate: no legal resource option in {_state.CurrentPhase} — cannot activate");
            return;
        }

        var playResult = _deck.PlayCard(improvCard.Id);
        if (!playResult.IsSuccess) {
            Log.Warn("[UI]", $"ImprovisationView.Activate: PlayCard failed: {playResult.Error}");
            return;
        }

        if (!_deck.Hand.Any(c => c.Type != CardType.Wound)) {
            Log.Warn("[UI]", "ImprovisationView.Activate: no non-Wound card to discard — cannot activate");
            _deck.ReturnCard(improvCard);
            return;
        }

        _improvCard = improvCard;
        _discardedCard = null;
        _statusLabel.Text = "Improvisation: discard a card to activate";
        BuildDiscardPanel();
        _resourcePanel.Visible = false;
        Visible = true;
        Log.Debug("[UI]", $"ImprovisationView activated in {_state.CurrentPhase}");
    }

    private void BuildDiscardPanel() {
        foreach (Node child in _discardPanel.GetChildren())
            child.QueueFree();

        foreach (var card in _deck.Hand) {
            if (card.Type == CardType.Wound)
                continue;
            var row = new HBoxContainer();

            var nameLabel = new Label();
            nameLabel.Text = card.Name;
            nameLabel.AddThemeFontSizeOverride("font_size", 28);
            nameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            row.AddChild(nameLabel);

            var discardBtn = new Button();
            discardBtn.Text = "Discard";
            discardBtn.AddThemeFontSizeOverride("font_size", 28);
            var capturedCard = card;
            discardBtn.Pressed += () => OnDiscardSelected(capturedCard);
            row.AddChild(discardBtn);

            _discardPanel.AddChild(row);
        }
    }

    private void OnDiscardSelected(CardDefinition card) {
        var r = _deck.DiscardCard(card.Id);
        if (!r.IsSuccess) {
            Log.Warn("[UI]", $"ImprovisationView: DiscardCard failed: {r.Error}");
            return;
        }
        _discardedCard = card;
        Log.Debug("[UI]", $"ImprovisationView: {card.Id} discarded, building resource panel in {_state.CurrentPhase}");

        foreach (Node child in _discardPanel.GetChildren())
            child.QueueFree();

        _statusLabel.Text = "Choose your resource:";
        BuildResourcePanel();
        _resourcePanel.Visible = true;
    }

    private void BuildResourcePanel() {
        foreach (Node child in _resourcePanel.GetChildren())
            child.QueueFree();

        (EffectType type, string label)[] options = {
            (EffectType.Move,        $"Move {ImprovisationRule.GetAmount(false)}"),
            (EffectType.AttackMelee, $"Attack {ImprovisationRule.GetAmount(false)}"),
            (EffectType.Block,       $"Block {ImprovisationRule.GetAmount(false)}"),
            (EffectType.Influence,   $"Influence {ImprovisationRule.GetAmount(false)}"),
        };

        foreach (var (effectType, labelText) in options) {
            var btn = new Button();
            btn.Text = labelText;
            btn.AddThemeFontSizeOverride("font_size", 32);
            btn.Disabled = !PhaseGate.IsLegal(effectType, _state.CurrentPhase);
            var capturedType = effectType;
            var capturedAmount = ImprovisationRule.GetAmount(false);
            btn.Pressed += () => OnResourceSelected(capturedType, capturedAmount);
            _resourcePanel.AddChild(btn);
        }
    }

    private void OnResourceSelected(EffectType effectType, int amount) {
        if (_improvCard is null) return;
        Log.Debug("[UI]", $"Improvisation: staging {effectType} {amount} in {_state.CurrentPhase}");
        _stagingManager.Stage(_improvCard, effectType, costCard: _discardedCard, overrideAmount: amount);
        _improvCard = null;
        _discardedCard = null;
        Visible = false;
        Log.Debug("[UI]", "ImprovisationView closed");
    }
}
