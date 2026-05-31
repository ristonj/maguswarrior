using Godot;
using MagusWarrior.Cards;
using MagusWarrior.Core;
using MagusWarrior.Core.Types;

namespace MagusWarrior.UI;

#if GODOT_ANDROID
[ScriptPath("res://scripts/ui/components/CardCompact.cs")]
#endif
public partial class CardCompact : Control {
    [Signal]
    public delegate void CardTappedEventHandler(string cardId);

    private string _cardId = string.Empty;

    public void Initialize(CardDefinition card, GamePhase currentPhase) {
        _cardId = card.Id;
        GetNode<Label>("CardPanel/Layout/CardName").Text = card.Name;
        GetNode<Label>("CardPanel/Layout/ManaCostLabel").Text =
            card.ManaCost.HasValue ? card.ManaCost.ToString()! : string.Empty;
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

    public override void _Ready() {
        GetNode<Button>("TapTarget").Pressed += () => EmitSignal(SignalName.CardTapped, _cardId);
    }
}
