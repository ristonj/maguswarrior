using Godot;
using MagusWarrior.Core;
using MagusWarrior.Deck;

namespace MagusWarrior.UI;

public partial class StagingAreaView : Control {
    [Signal] public delegate void CommitRequestedEventHandler();

    private StagingManager _stagingManager = null!;
    private Label _moveLabel = null!;
    private Label _attackLabel = null!;
    private Label _blockLabel = null!;
    private Label _influenceLabel = null!;
    private Button _commitButton = null!;

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

        _commitButton = new Button();
        _commitButton.Text = "Commit";
        _commitButton.AddThemeFontSizeOverride("font_size", 32);
        _commitButton.Disabled = true;
        _commitButton.Pressed += () => {
            EmitSignal(SignalName.CommitRequested);
            Log.Debug("[UI]", "Commit tapped");
        };
        container.AddChild(_commitButton);
    }

    public void Initialize(StagingManager staging) {
        _stagingManager = staging;
        staging.StagingChanged += Refresh;
        Refresh();
    }

    private void Refresh() {
        var totals = _stagingManager.GetTotals();
        _moveLabel.Text      = $"Move: {totals.Move}";
        _attackLabel.Text    = $"Attack: {totals.Attack}";
        _blockLabel.Text     = $"Block: {totals.Block}";
        _influenceLabel.Text = $"Influence: {totals.Influence}";
        _commitButton.Disabled = _stagingManager.StagedCards.Count == 0;
    }
}
