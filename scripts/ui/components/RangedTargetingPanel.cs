using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MagusWarrior.Broker;
using MagusWarrior.Combat;
using MagusWarrior.Core;
using MagusWarrior.Core.Types;

namespace MagusWarrior.UI;

public partial class RangedTargetingPanel : Control {
    private CombatState?  _combat;
    private GameState?    _state;
    private VBoxContainer _enemyList    = null!;
    private Label         _attacksLabel = null!;
    private TaskCompletionSource<IReadOnlyList<RangedAttackDeclaration>>? _tcs;

    private List<RangedAttackDeclaration> _declarations     = new();
    private List<AttackContribution>      _declaredContribs = new();

    public override void _Ready() {
        // Full-rect root that does NOT intercept input — only the docked sheet below
        // captures taps, so HandView underneath stays reachable for card plays during
        // the ranged phase (the whole point of this panel).
        AnchorLeft   = 0f;
        AnchorRight  = 1f;
        AnchorTop    = 0f;
        AnchorBottom = 1f;
        GrowHorizontal = GrowDirection.Both;
        GrowVertical   = GrowDirection.Both;
        MouseFilter = MouseFilterEnum.Ignore;
        ZIndex  = 5;
        Visible = false;

        // Bottom-sheet strip docked directly above HandView (the hand occupies the bottom
        // 350px). This strip sits in the 350–480px band so it never overlaps the hand.
        var sheet = new PanelContainer();
        sheet.AnchorLeft   = 0f;
        sheet.AnchorRight  = 1f;
        sheet.AnchorTop    = 1f;
        sheet.AnchorBottom = 1f;
        sheet.OffsetTop    = -480f;
        sheet.OffsetBottom = -350f;
        sheet.GrowHorizontal = GrowDirection.Both;
        sheet.GrowVertical   = GrowDirection.Begin;
        AddChild(sheet);

        // Inner margins: without these the Pass button sits flush against (and clips at) the
        // right edge of the viewport, because the sheet is anchored edge-to-edge.
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left",   24);
        margin.AddThemeConstantOverride("margin_right",  24);
        margin.AddThemeConstantOverride("margin_top",    8);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        sheet.AddChild(margin);

        var layout = new HBoxContainer();
        layout.AddThemeConstantOverride("separation", 16);
        margin.AddChild(layout);

        var leftCol = new VBoxContainer();
        leftCol.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        layout.AddChild(leftCol);

        var title = new Label();
        title.Text = "ui.combat.ranged.title";                    // static ⇒ key + auto-translate
        title.AutoTranslateMode = Node.AutoTranslateModeEnum.Always;
        title.AddThemeFontSizeOverride("font_size", 28);
        leftCol.AddChild(title);

        _attacksLabel = new Label();
        // Takes both a static and an interpolated value depending on branch, so it is composed
        // via Strings and auto-translate stays off (see Strings.cs for the two mechanisms).
        _attacksLabel.AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled;
        _attacksLabel.AddThemeFontSizeOverride("font_size", 22);
        leftCol.AddChild(_attacksLabel);

        _enemyList = new VBoxContainer();
        leftCol.AddChild(_enemyList);

        var passBtn = new Button();
        passBtn.Text = "ui.common.pass";                          // static ⇒ key + auto-translate
        passBtn.AutoTranslateMode = Node.AutoTranslateModeEnum.Always;
        passBtn.AddThemeFontSizeOverride("font_size", 28);
        passBtn.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        passBtn.Pressed += OnPassPressed;
        layout.AddChild(passBtn);
    }

    public Task<IReadOnlyList<RangedAttackDeclaration>> ShowAndAwait(CombatState combat, GameState state) {
        // Re-entrancy guards: drop any stale subscription and complete any stranded awaiter
        // before re-arming. (The dev trigger's _combatInProgress guard normally prevents this,
        // but a defensive panel keeps a second show from leaking a subscription or hanging a Task.)
        if (_state != null)
            _state.ResourcesChanged -= RefreshAttackDisplay;
        _tcs?.TrySetResult(_declarations);

        _combat = combat;
        _state  = state;
        _tcs    = new TaskCompletionSource<IReadOnlyList<RangedAttackDeclaration>>();
        _declarations     = new List<RangedAttackDeclaration>();
        _declaredContribs = new List<AttackContribution>();
        _state.ResourcesChanged += RefreshAttackDisplay;
        RefreshEnemyDisplay();
        RefreshAttackDisplay();
        Visible = true;
        return _tcs.Task;
    }

    private void Complete() {
        if (_state != null)
            _state.ResourcesChanged -= RefreshAttackDisplay;   // always unsubscribe before resolving TCS
        Visible = false;
        _tcs?.TrySetResult(_declarations);
    }

    private void RefreshEnemyDisplay() {
        _enemyList.ClearChildren();

        if (_combat == null) return;

        foreach (var e in _combat.ActiveEnemies) {
            var fortLevel = (_combat.IsAtFortifiedSite ? 1 : 0) + (e.HasAbility(EnemyAbility.Fortified) ? 1 : 0);
            var fortNote  = fortLevel > 0
                ? Strings.Format("ui.combat.ranged.fortified_note", fortLevel)
                : "";

            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 12);

            var nameLabel = new Label();
            nameLabel.AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled;   // interpolated
            nameLabel.Text = Strings.Format("ui.combat.ranged.enemy_row",
                e.Definition.Name, fortNote, e.EffectiveArmor);
            nameLabel.AddThemeFontSizeOverride("font_size", 24);
            // Fill, NOT ExpandFill — see BlockTargetingPanel. Expand flung the declare button to the
            // far right of the column, colliding with the vertically-centred Pass button.
            nameLabel.SizeFlagsHorizontal = SizeFlags.Fill;
            row.AddChild(nameLabel);

            var declareBtn = new Button();
            declareBtn.Name = $"DeclareBtn_{e.Definition.Id}";
            declareBtn.AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled;  // interpolated
            declareBtn.Text = Strings.Format("ui.combat.ranged.declare_vs", e.Definition.Name);
            declareBtn.AddThemeFontSizeOverride("font_size", 24);
            var capturedEnemy = e;
            declareBtn.Pressed += () => OnDeclarePressed(capturedEnemy);
            row.AddChild(declareBtn);

            // Trailing spacer absorbs the row's spare width, keeping the name + declare button
            // grouped on the left instead of the button being pushed against Pass on the right.
            var spacer = new Control();
            spacer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            row.AddChild(spacer);

            _enemyList.AddChild(row);
        }
    }

    private void RefreshAttackDisplay() {
        if (_state == null) { _attacksLabel.Text = ""; return; }
        var available = AvailableContribs();
        if (available.Count == 0) {
            _attacksLabel.Text = Strings.Get("ui.combat.ranged.none_available");
            SetDeclareButtonsDisabled(true);
        } else {
            _attacksLabel.Text = Strings.Format("ui.combat.ranged.available",
                string.Join(", ", available.Select(c => $"{c.Delivery} {c.Type} {c.Value}")));
            SetDeclareButtonsDisabled(false);
        }
    }

    private void SetDeclareButtonsDisabled(bool disabled) {
        foreach (Node child in _enemyList.GetChildren()) {
            if (child is HBoxContainer row) {
                foreach (Node grandchild in row.GetChildren()) {
                    if (grandchild is Button btn)
                        btn.Disabled = disabled;
                }
            }
        }
    }

    private List<AttackContribution> AvailableContribs() {
        if (_state == null) return new List<AttackContribution>();
        var all = AttackBridge.ExtractRangedContributions(_state.AttackPool);

        // AttackPool aggregates by (Distance, Element), so `all` holds at most one entry per
        // (Type, Delivery). Subtract the total already declared per key by AMOUNT — matching
        // declared entries by exact value breaks the moment another card raises the pool total
        // (e.g. declared 4, then a second card makes the pool 7 → the 4 must still be subtracted).
        var declaredByKey = new Dictionary<(AttackType, AttackDelivery), int>();
        foreach (var d in _declaredContribs) {
            var key = (d.Type, d.Delivery);
            declaredByKey[key] = declaredByKey.GetValueOrDefault(key) + d.Value;
        }

        var result = new List<AttackContribution>();
        foreach (var c in all) {
            int remaining = c.Value - declaredByKey.GetValueOrDefault((c.Type, c.Delivery));
            if (remaining > 0)
                result.Add(new AttackContribution(c.Type, c.Delivery, remaining));
        }
        return result;
    }

    private void OnDeclarePressed(EnemyTokenInstance target) {
        var available = AvailableContribs();
        if (available.Count == 0) return;
        _declarations.Add(new RangedAttackDeclaration(available, new List<EnemyTokenInstance> { target }));
        _declaredContribs.AddRange(available);
        RefreshAttackDisplay();
    }

    private void OnPassPressed() => Complete();
}
