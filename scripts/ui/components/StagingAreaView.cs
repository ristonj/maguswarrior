using Godot;
using MagusWarrior.Core;

namespace MagusWarrior.UI;

public partial class StagingAreaView : Control {
    [Signal] public delegate void UndoRequestedEventHandler();

    private GameState _state = null!;
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
        Refresh();
    }

    private void Refresh() {
        _moveLabel.Text      = $"Move: {_state.MovePointsThisTurn}";
        _attackLabel.Text    = $"Attack: {_state.TotalAttackThisTurn}";
        _blockLabel.Text     = $"Block: {_state.TotalBlockThisTurn}";
        _influenceLabel.Text = $"Influence: {_state.InfluencePointsThisTurn}";
    }
}
