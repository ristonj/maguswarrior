using Godot;
using MagusWarrior.Core;

namespace MagusWarrior.UI;

public partial class EffectEventLogPanel : Panel {
    private GameState? _state;
    private VBoxContainer _entriesContainer = null!;

    public override void _Ready() {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Visible = false;

        var layout = new VBoxContainer();
        layout.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(layout);

        var titleLabel = new Label();
        titleLabel.Text = "Effect Event Log";
        titleLabel.AddThemeFontSizeOverride("font_size", 36);
        layout.AddChild(titleLabel);

        var scroll = new ScrollContainer();
        scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
        layout.AddChild(scroll);

        _entriesContainer = new VBoxContainer();
        _entriesContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(_entriesContainer);

        var actionBar = new HBoxContainer();
        layout.AddChild(actionBar);

        var undoLastButton = new Button();
        undoLastButton.Text = "Undo Last";
        undoLastButton.AddThemeFontSizeOverride("font_size", 32);
        undoLastButton.Pressed += OnUndoLastPressed;
        actionBar.AddChild(undoLastButton);

        var closeButton = new Button();
        closeButton.Text = "Close";
        closeButton.AddThemeFontSizeOverride("font_size", 32);
        closeButton.Pressed += () => Visible = false;
        actionBar.AddChild(closeButton);
    }

    public void Initialize(GameState state) {
        _state = state;
    }

    public void RefreshDisplay() {
        _entriesContainer.ClearChildren();

        if (_state == null || _state.EventLog.Events.Count == 0) {
            _entriesContainer.AddChild(new Label { Text = "(no events)" });
            return;
        }

        for (int i = 0; i < _state.EventLog.Events.Count; i++) {
            var e = _state.EventLog.Events[i];
            _entriesContainer.AddChild(new Label {
                Text = $"[{i}]: {e.SourceCardId} {e.EffectType} phase={e.Phase} powered={e.Powered}"
            });
        }
        Log.Debug("[UI]", $"EffectEventLogPanel: refreshed with {_state.EventLog.Events.Count} entries");
    }

    private void OnUndoLastPressed() {
        if (_state == null) return;
        GameDebug.UndoLastEvent(_state);
        // Debug surgery on EventLog (PopLast + RestoreSnapshot) bypasses UndoController,
        // so its card-play/hero-move stack is now stale relative to EventLog. A later
        // player Undo would double-refund off that stale stack. Collapse to a known-clean
        // state — the structural equivalent of the old _map.ClearMovePath() defense.
        _state.UndoController.Clear();
        RefreshDisplay();
    }
}
