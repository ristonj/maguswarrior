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

    private Button PlayButton        => GetNode<Button>("CardPanel/Layout/ActionBar/PlayButton");
    private Button PlaySidewaysButton => GetNode<Button>("CardPanel/Layout/ActionBar/PlaySidewaysButton");
    private Button PowerButton        => GetNode<Button>("CardPanel/Layout/ActionBar/PowerButton");
    private Button CancelButton       => GetNode<Button>("CardPanel/Layout/CancelButton");

    public void Open(CardDefinition card, GamePhase currentPhase) {
        _cardId = card.Id;
        GetNode<Label>("CardPanel/Layout/CardNameLabel").Text = card.Name;
        GetNode<Label>("CardPanel/Layout/EffectTextLabel").Text = card.Unpowered?.Text ?? "(no effect)";

        PlayButton.Disabled = !(card.Unpowered != null &&
            PhaseGate.IsLegal(card.Unpowered.EffectType, currentPhase));
        PlaySidewaysButton.Disabled = false;
        PowerButton.Visible = card.ManaCost.HasValue;
        // ManaPool not implemented until Epic 6 stories — Power always greyed for now
        PowerButton.Disabled = true;

        Visible = true;
        Log.Debug("[UI]", $"CardExpanded opened: {card.Id} phase={currentPhase}");
    }

    public void Close() {
        Visible = false;
        Log.Debug("[UI]", $"CardExpanded closed: {_cardId}");
    }

    public override void _Ready() {
        CancelButton.Pressed += () => {
            EmitSignal(SignalName.CancelRequested);
            Close();
        };
        PlayButton.Pressed += () => {
            EmitSignal(SignalName.PlayRequested, _cardId);
            Close();
            Log.Debug("[UI]", $"Play tapped: {_cardId}");
        };
        PlaySidewaysButton.Pressed += () => {
            EmitSignal(SignalName.PlaySidewaysRequested, _cardId);
            Close();
            Log.Debug("[UI]", $"Sideways tapped: {_cardId}");
        };
        PowerButton.Pressed += () => {
            EmitSignal(SignalName.PowerRequested, _cardId);
            Close();
            Log.Debug("[UI]", $"Power tapped: {_cardId}");
        };
    }
}
