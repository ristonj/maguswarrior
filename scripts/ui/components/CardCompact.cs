using Godot;
using MagusWarrior.Cards;
using MagusWarrior.Core;
using MagusWarrior.Core.Types;

namespace MagusWarrior.UI;

public partial class CardCompact : Control {
    [Signal]
    public delegate void CardTappedEventHandler(string cardId);

    private string _cardId = string.Empty;
    private Label _cardName = null!;
    private Label _manaCostLabel = null!;

    public override void _Ready() {
        CustomMinimumSize = new Vector2(120, 120);

        var cardPanel = new PanelContainer();
        cardPanel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(cardPanel);

        var layout = new VBoxContainer();
        cardPanel.AddChild(layout);

        _cardName = new Label();
        _cardName.AddThemeFontSizeOverride("font_size", 24);
        _cardName.AutowrapMode = TextServer.AutowrapMode.Word;
        layout.AddChild(_cardName);

        _manaCostLabel = new Label();
        _manaCostLabel.AddThemeFontSizeOverride("font_size", 18);
        layout.AddChild(_manaCostLabel);

        var tapTarget = new Button();
        tapTarget.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        tapTarget.Flat = true;
        tapTarget.ZIndex = 1;
        tapTarget.Pressed += () => EmitSignal(SignalName.CardTapped, _cardId);
        AddChild(tapTarget);
    }

    public void Initialize(CardDefinition card, GamePhase currentPhase) {
        _cardId = card.Id;
        _cardName.Text = card.Name;
        _manaCostLabel.Text = card.ManaCost.HasValue ? card.ManaCost.ToString()! : string.Empty;
        Modulate = IsCardPlayable(card, currentPhase)
            ? new Color(1, 1, 1, 1f)
            : new Color(1, 1, 1, 0.5f);
        Log.Debug("[UI]", $"CardCompact initialized: {card.Id}");
    }

    private static bool IsCardPlayable(CardDefinition card, GamePhase phase) {
        // 1b-1: return true for all cards — greying of native-play happens inside CardExpanded.
        // Wound card gating is Story 1b-5.
        return true;
    }
}
