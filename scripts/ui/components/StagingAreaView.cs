using Godot;
using MagusWarrior.Core;
using MagusWarrior.Core.Types;

namespace MagusWarrior.UI;

public partial class StagingAreaView : Control {
    [Signal] public delegate void UndoRequestedEventHandler();

    private GameState _state = null!;
    private Label _phaseLabel = null!;
    private Label _moveLabel = null!;
    private Label _attackLabel = null!;
    private Label _blockLabel = null!;
    private Label _influenceLabel = null!;
    private Button _undoButton = null!;

    public override void _Ready() {
        var container = new HBoxContainer();
        container.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        container.AddThemeConstantOverride("separation", 20);
        AddChild(container);

        _phaseLabel = new Label();
        _phaseLabel.AddThemeFontSizeOverride("font_size", 28);
        container.AddChild(_phaseLabel);

        _moveLabel = new Label();
        _moveLabel.AddThemeFontSizeOverride("font_size", 28);
        container.AddChild(_moveLabel);

        _attackLabel = new Label();
        _attackLabel.AddThemeFontSizeOverride("font_size", 28);
        container.AddChild(_attackLabel);

        _blockLabel = new Label();
        _blockLabel.AddThemeFontSizeOverride("font_size", 28);
        container.AddChild(_blockLabel);

        _influenceLabel = new Label();
        _influenceLabel.AddThemeFontSizeOverride("font_size", 28);
        container.AddChild(_influenceLabel);

        _undoButton = new Button();
        _undoButton.Text = "Undo";
        _undoButton.AddThemeFontSizeOverride("font_size", 32);
        _undoButton.Pressed += () => {
            EmitSignal(SignalName.UndoRequested);
            Log.Debug("[UI]", "Undo tapped");
        };
        container.AddChild(_undoButton);
    }

    public void Initialize(GameState state) {
        _state = state;
        state.ResourcesChanged += Refresh;
        state.PhaseChanged += RefreshPhase;
        Refresh();
        RefreshPhase();
    }

    private void RefreshPhase() {
        _phaseLabel.Text = _state.CurrentPhase switch {
            GamePhase.CombatStart        => "Combat Start",
            GamePhase.CombatRanged       => "Ranged Attack",
            GamePhase.CombatBlock        => "Block",
            GamePhase.CombatAssignDamage => "Assign Damage",
            GamePhase.CombatMelee        => "Melee Attack",
            GamePhase.Rest               => "Rest",
            GamePhase.EndOfTurn          => "End of Turn",
            GamePhase.Movement           => "Movement",
            _                            => _state.CurrentPhase.ToString()
        };
    }

    private void Refresh() {
        _moveLabel.Text      = $"Move: {_state.MovePointsThisTurn}";
        _attackLabel.Text    = $"Attack: {_state.TotalAttackThisTurn}";
        _blockLabel.Text     = $"Block: {_state.TotalBlockThisTurn}";
        _influenceLabel.Text = $"Influence: {_state.InfluencePointsThisTurn}";
        _undoButton.Disabled = !_state.UndoController.CanUndo;
    }
}
