using Godot;
using MagusWarrior.Cards;
using MagusWarrior.Cards.Effects;
using MagusWarrior.Core;
using MagusWarrior.Core.Types;

namespace MagusWarrior.UI;

public partial class CardExpanded : Control {
    [Signal] public delegate void PlayRequestedEventHandler(string cardId);
    [Signal] public delegate void PlaySidewaysRequestedEventHandler(string cardId);
    [Signal] public delegate void PowerRequestedEventHandler(string cardId);
    [Signal] public delegate void CancelRequestedEventHandler();

    private string _cardId = string.Empty;
    private Label _cardNameLabel = null!;
    private Label _effectTextLabel = null!;
    private Button _playButton = null!;
    private Button _playSidewaysButton = null!;
    private Button _powerButton = null!;

    public override void _Ready() {
        ZIndex = 3;
        Visible = false;

        var cardPanel = new PanelContainer();
        cardPanel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(cardPanel);

        var layout = new VBoxContainer();
        layout.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        cardPanel.AddChild(layout);

        var cancelButton = new Button();
        cancelButton.Text = "✕ Cancel";
        cancelButton.AddThemeFontSizeOverride("font_size", 28);
        layout.AddChild(cancelButton);

        _cardNameLabel = new Label();
        _cardNameLabel.AddThemeFontSizeOverride("font_size", 36);
        layout.AddChild(_cardNameLabel);

        _effectTextLabel = new Label();
        _effectTextLabel.AddThemeFontSizeOverride("font_size", 28);
        _effectTextLabel.AutowrapMode = TextServer.AutowrapMode.Word;
        layout.AddChild(_effectTextLabel);

        var actionBar = new HBoxContainer();
        actionBar.AddThemeConstantOverride("separation", 20);
        layout.AddChild(actionBar);

        _playButton = new Button();
        _playButton.Text = "Play";
        _playButton.AddThemeFontSizeOverride("font_size", 32);
        actionBar.AddChild(_playButton);

        _playSidewaysButton = new Button();
        _playSidewaysButton.Text = "Sideways";
        _playSidewaysButton.AddThemeFontSizeOverride("font_size", 32);
        actionBar.AddChild(_playSidewaysButton);

        _powerButton = new Button();
        _powerButton.Text = "Power";
        _powerButton.AddThemeFontSizeOverride("font_size", 32);
        _powerButton.Visible = false;
        actionBar.AddChild(_powerButton);

        cancelButton.Pressed += () => { EmitSignal(SignalName.CancelRequested); Close(); };
        _playButton.Pressed += () => {
            EmitSignal(SignalName.PlayRequested, _cardId);
            Close();
            Log.Debug("[UI]", $"Play tapped: {_cardId}");
        };
        _playSidewaysButton.Pressed += () => {
            EmitSignal(SignalName.PlaySidewaysRequested, _cardId);
            Close();
            Log.Debug("[UI]", $"Sideways tapped: {_cardId}");
        };
        _powerButton.Pressed += () => {
            EmitSignal(SignalName.PowerRequested, _cardId);
            Close();
            Log.Debug("[UI]", $"Power tapped: {_cardId}");
        };
    }

    public void Open(CardDefinition card, GamePhase currentPhase) {
        _cardId = card.Id;
        _cardNameLabel.Text = card.Name;
        _effectTextLabel.Text = card.Unpowered?.Text ?? "(no effect)";

        _playButton.Disabled = !(card.Unpowered != null &&
            PhaseGate.IsLegal(card.Unpowered.EffectType, currentPhase));
        _playSidewaysButton.Disabled = false;
        _powerButton.Visible = card.ManaCost.HasValue;
        _powerButton.Disabled = true;

        Visible = true;
        Log.Debug("[UI]", $"CardExpanded opened: {card.Id} phase={currentPhase}");
    }

    public void Close() {
        Visible = false;
        Log.Debug("[UI]", $"CardExpanded closed: {_cardId}");
    }
}
