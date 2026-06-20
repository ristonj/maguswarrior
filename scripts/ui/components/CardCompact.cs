using Godot;
using MagusWarrior.Cards;
using MagusWarrior.Cards.Effects;
using MagusWarrior.Core;
using MagusWarrior.Core.Types;

namespace MagusWarrior.UI;

public partial class CardCompact : Control {
    [Signal]
    public delegate void CardTappedEventHandler(string cardId);

    private string _cardId = string.Empty;
    private Label _cardName = null!;
    private Label _manaCostLabel = null!;
    private Button _tapTarget = null!;
    private PanelContainer _cardPanel = null!;

    public override void _Ready() {
        CustomMinimumSize = new Vector2(120, 120);

        _cardPanel = new PanelContainer();
        _cardPanel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_cardPanel);

        var layout = new VBoxContainer();
        _cardPanel.AddChild(layout);

        _cardName = new Label();
        _cardName.AddThemeFontSizeOverride("font_size", 24);
        _cardName.AutowrapMode = TextServer.AutowrapMode.Word;
        layout.AddChild(_cardName);

        _manaCostLabel = new Label();
        _manaCostLabel.AddThemeFontSizeOverride("font_size", 18);
        layout.AddChild(_manaCostLabel);

        _tapTarget = new Button();
        _tapTarget.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _tapTarget.Flat = true;
        _tapTarget.ZIndex = 1;
        _tapTarget.Pressed += () => EmitSignal(SignalName.CardTapped, _cardId);
        AddChild(_tapTarget);
    }

    public void Initialize(CardDefinition card, GamePhase currentPhase) {
        // Reset per-card state unconditionally so re-initialization with any card type is safe.
        _tapTarget.Disabled = false;
        _cardPanel.RemoveThemeStyleboxOverride("panel");

        _cardId = card.Id;
        _cardName.Text = card.Name;

        if (card.Type == CardType.Wound) {
            // Wound: full red background (UX color.state.wound = #8B1A1A), untappable.
            // A disabled Button emits no Pressed, so CardTapped never fires — the wound
            // is inert regardless of phase. Full opacity: the red IS the signal, no dimming.
            _cardPanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color("8B1A1A") });
            _tapTarget.Disabled = true;
            _manaCostLabel.Text = string.Empty;
            Modulate = new Color(1, 1, 1, 1f);
            Log.Debug("[UI]", $"CardCompact wound rendered (untappable): {card.Id}");
            return;
        }

        _manaCostLabel.Text = card.ManaCost.HasValue ? card.ManaCost.ToString()! : string.Empty;
        Modulate = IsCardPlayable(card, currentPhase)
            ? new Color(1, 1, 1, 1f)
            : new Color(1, 1, 1, 0.5f);
        Log.Debug("[UI]", $"CardCompact initialized: {card.Id}");
    }

    private static bool IsCardPlayable(CardDefinition card, GamePhase phase) {
        if (card.Unpowered != null && PhaseGate.IsLegal(card.Unpowered.EffectType, phase))
            return true;
        if (card.Powered != null && PhaseGate.IsLegal(card.Powered.EffectType, phase))
            return true;
        foreach (var alt in card.AlternateEffectTypes)
            if (PhaseGate.IsLegal(alt, phase)) return true;
        return false;
    }
}
