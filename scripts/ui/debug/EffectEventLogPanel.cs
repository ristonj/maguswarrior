using Godot;
using MagusWarrior.Core;

namespace MagusWarrior.UI;

public partial class EffectEventLogPanel : Panel {
    private GameState? _state;

    public override void _Ready() {
        GetNode<Button>("Layout/ActionBar/UndoLastButton").Pressed += OnUndoLastPressed;
        GetNode<Button>("Layout/ActionBar/CloseButton").Pressed += () => Visible = false;
    }

    public void Initialize(GameState state) {
        _state = state;
    }

    public void RefreshDisplay() {
        var container = GetNode<VBoxContainer>("Layout/Scroll/EntriesContainer");
        foreach (Node child in container.GetChildren())
            child.QueueFree();

        if (_state == null || _state.EventLog.Events.Count == 0) {
            container.AddChild(new Label { Text = "(no events)" });
            return;
        }

        for (int i = 0; i < _state.EventLog.Events.Count; i++) {
            var e = _state.EventLog.Events[i];
            container.AddChild(new Label {
                Text = $"[{i}]: {e.SourceCardId} {e.EffectType} phase={e.Phase} powered={e.Powered}"
            });
        }
        Log.Debug("[UI]", $"EffectEventLogPanel: refreshed with {_state.EventLog.Events.Count} entries");
    }

    private void OnUndoLastPressed() {
        if (_state == null) return;
        GameDebug.UndoLastEvent(_state);
        RefreshDisplay();
    }
}
