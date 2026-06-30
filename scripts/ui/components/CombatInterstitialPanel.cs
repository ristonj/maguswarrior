using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MagusWarrior.Combat;
using MagusWarrior.Core.Types;

namespace MagusWarrior.UI;

public partial class CombatInterstitialPanel : Control {
    private VBoxContainer _enemyList = null!;
    private TaskCompletionSource<bool>? _tcs;

    public override void _Ready() {
        AnchorLeft   = 0f;
        AnchorRight  = 1f;
        AnchorTop    = 0f;
        AnchorBottom = 1f;
        GrowHorizontal = GrowDirection.Both;
        GrowVertical   = GrowDirection.Both;
        ZIndex  = 5;
        Visible = false;

        var backdrop = new PanelContainer();
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        // Semi-transparent dark backdrop (AC6) — the map stays faintly visible behind the
        // interstitial. Styling the panel box (not Modulate) keeps the child text fully opaque.
        var bg = new StyleBoxFlat();
        bg.BgColor = new Color(0f, 0f, 0f, 0.6f);
        backdrop.AddThemeStyleboxOverride("panel", bg);
        AddChild(backdrop);

        var layout = new VBoxContainer();
        layout.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        backdrop.AddChild(layout);

        var title = new Label();
        title.Text = "Combat: Prepare";
        title.AddThemeFontSizeOverride("font_size", 36);
        layout.AddChild(title);

        _enemyList = new VBoxContainer();
        layout.AddChild(_enemyList);

        var beginBtn = new Button();
        beginBtn.Text = "Begin Combat";
        beginBtn.AddThemeFontSizeOverride("font_size", 32);
        beginBtn.Pressed += OnBeginCombatPressed;
        layout.AddChild(beginBtn);
    }

    public Task ShowAndAwait(CombatState combat) {
        _tcs?.TrySetResult(true);   // complete any stranded prior awaiter before re-arming
        _tcs = new TaskCompletionSource<bool>();
        PopulateEnemyDisplay(combat.ActiveEnemies);
        Visible = true;
        return _tcs.Task;
    }

    private void PopulateEnemyDisplay(List<EnemyTokenInstance> enemies) {
        foreach (Node child in _enemyList.GetChildren())
            child.QueueFree();

        foreach (var e in enemies) {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 12);

            var nameLabel = new Label();
            nameLabel.Text = e.Definition.Name;
            nameLabel.AddThemeFontSizeOverride("font_size", 28);
            nameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            row.AddChild(nameLabel);

            var armorLabel = new Label();
            armorLabel.Text = $"Armor:{e.EffectiveArmor}";
            armorLabel.AddThemeFontSizeOverride("font_size", 28);
            row.AddChild(armorLabel);

            var atksLabel = new Label();
            atksLabel.Text = string.Join(", ", e.Definition.Attacks.Select(a => $"{a.Type} {a.Value}"));
            atksLabel.AddThemeFontSizeOverride("font_size", 28);
            row.AddChild(atksLabel);

            var abilStr = e.Definition.Abilities.Any()
                ? string.Join(", ", e.Definition.Abilities)
                : "—";
            var abilLabel = new Label();
            abilLabel.Text = abilStr;
            abilLabel.AddThemeFontSizeOverride("font_size", 24);
            row.AddChild(abilLabel);

            _enemyList.AddChild(row);
        }
    }

    private void OnBeginCombatPressed() {
        Visible = false;
        _tcs?.TrySetResult(true);
    }
}
