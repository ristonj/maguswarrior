using Godot;
using MagusWarrior.Core;
using MagusWarrior.Map;

namespace MagusWarrior.UI;

// HUD overlay showing how many tiles remain to be revealed (countryside + core).
// Display-only: observes TileStock.Changed and re-renders. Handles no input.
public partial class TileCountView : Control {
    private TileStock _stock = null!;
    private Label _label = null!;

    public void Initialize(TileStock stock) {
        _stock = stock;

        SetAnchorsAndOffsetsPreset(LayoutPreset.TopRight);

        _label = new Label();
        _label.Name = "TileCountLabel";
        _label.AddThemeFontSizeOverride("font_size", 28);
        _label.HorizontalAlignment = HorizontalAlignment.Right;
        _label.SetAnchorsAndOffsetsPreset(LayoutPreset.TopRight);
        _label.OffsetLeft = -360f;
        _label.OffsetTop = 20f;
        _label.OffsetRight = -20f;
        AddChild(_label);

        _stock.Changed += RefreshLabel;
        RefreshLabel();
        Log.Debug("[UI]", $"TileCountView initialized: countryside={_stock.CountrysideRemaining} core={_stock.CoreRemaining}");
    }

    private void RefreshLabel() {
        _label.Text = $"Countryside: {_stock.CountrysideRemaining}   Core: {_stock.CoreRemaining}";
    }
}
