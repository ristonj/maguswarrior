using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MagusWarrior.Broker;
using MagusWarrior.Combat;
using MagusWarrior.Core;
using MagusWarrior.Core.Types;

namespace MagusWarrior.UI;

public partial class BlockTargetingPanel : Control {
    private CombatState?  _combat;
    private GameState?    _state;
    private VBoxContainer _attackList = null!;
    private Label         _blockLabel = null!;
    private TaskCompletionSource<IReadOnlyList<BlockDeclaration>>? _tcs;

    private List<BlockDeclaration>  _declarations     = new();
    private List<BlockContribution> _declaredContribs = new();

    public override void _Ready() {
        // Full-rect root that does NOT intercept input — only the docked sheet below
        // captures taps, so HandView underneath stays reachable for card plays during
        // the block phase (the whole point of this panel).
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
        title.Text = "ui.combat.block.title";                     // static ⇒ key + auto-translate
        title.AutoTranslateMode = Node.AutoTranslateModeEnum.Always;
        title.AddThemeFontSizeOverride("font_size", 28);
        leftCol.AddChild(title);

        _blockLabel = new Label();
        // Takes both a static and an interpolated value depending on branch, so it is composed
        // via Strings and auto-translate stays off (see Strings.cs for the two mechanisms).
        _blockLabel.AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled;
        _blockLabel.AddThemeFontSizeOverride("font_size", 22);
        leftCol.AddChild(_blockLabel);

        _attackList = new VBoxContainer();
        leftCol.AddChild(_attackList);

        var passBtn = new Button();
        passBtn.Text = "ui.common.pass";                          // static ⇒ key + auto-translate
        passBtn.AutoTranslateMode = Node.AutoTranslateModeEnum.Always;
        passBtn.AddThemeFontSizeOverride("font_size", 28);
        passBtn.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        passBtn.Pressed += OnPassPressed;
        layout.AddChild(passBtn);
    }

    public Task<IReadOnlyList<BlockDeclaration>> ShowAndAwait(CombatState combat, GameState state) {
        // Re-entrancy guards: drop any stale subscription and complete any stranded awaiter
        // before re-arming. (The dev trigger's _combatInProgress guard normally prevents this,
        // but a defensive panel keeps a second show from leaking a subscription or hanging a Task.)
        if (_state != null)
            _state.ResourcesChanged -= RefreshBlockDisplay;
        _tcs?.TrySetResult(_declarations);

        _combat = combat;
        _state  = state;
        _tcs    = new TaskCompletionSource<IReadOnlyList<BlockDeclaration>>();
        _declarations     = new List<BlockDeclaration>();
        _declaredContribs = new List<BlockContribution>();
        _state.ResourcesChanged += RefreshBlockDisplay;
        RefreshBlockDisplay();   // Rebuilds rows (including SpinBoxes) and updates label.
        Visible = true;
        return _tcs.Task;
    }

    private void Complete() {
        if (_state != null)
            _state.ResourcesChanged -= RefreshBlockDisplay;   // always unsubscribe before resolving TCS
        Visible = false;
        _tcs?.TrySetResult(_declarations);
    }

    // Rebuilds all attack rows reflecting the current available (undeclared) pool.
    // Called on every ResourcesChanged event and after every declaration commit so
    // SpinBox maxes (or dump-all button state) always track the remaining pool.
    //
    // UI mode selection (6a): if there is exactly 1 blockable attack across all active
    // enemies, render a plain button that dumps the entire pool to that attack — no
    // SpinBoxes, no split needed.  If there are ≥ 2 blockable attacks, keep the AC5
    // per-type SpinBox picker so the player can split the pool across attacks.
    private void RefreshAttackRows() {
        _attackList.ClearChildren();

        if (_combat == null) return;

        var available = AvailableContribs();

        // Count total blockable attacks (skip AttackType.None) to choose UI mode.
        int totalBlockable = 0;
        foreach (var e in _combat.ActiveEnemies)
            foreach (var atk in e.Definition.Attacks)
                if (atk.Type != AttackType.None)
                    totalBlockable++;
        bool singleAttack = totalBlockable == 1;

        foreach (var e in _combat.ActiveEnemies) {
            var attacks = e.Definition.Attacks;
            for (int i = 0; i < attacks.Count; i++) {
                var attack = attacks[i];
                if (attack.Type == AttackType.None) continue;  // summon-only; skip

                var swiftNote = e.HasAbility(EnemyAbility.Swift)
                    ? Strings.Get("ui.combat.block.swift_note")
                    : "";

                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 12);

                var nameLabel = new Label();
                nameLabel.AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled;   // interpolated
                nameLabel.Text = Strings.Format("ui.combat.block.attack_row",
                    e.Definition.Name, attack.Type, attack.Value, swiftNote);
                nameLabel.AddThemeFontSizeOverride("font_size", 24);
                // Fill, NOT ExpandFill. With Expand the label ate all the row's spare width and
                // flung the declare button to the far right edge of the column — where it collided
                // with the Pass button (which is ShrinkCenter'd vertically in the sheet, so the two
                // sat at different heights and read as misaligned). The button belongs next to the
                // attack it blocks; the spare width goes in a spacer AFTER it (see below).
                nameLabel.SizeFlagsHorizontal = SizeFlags.Fill;
                row.AddChild(nameLabel);

                // Block is ALL-OR-NOTHING (LLD §9.3): a declaration that cannot reach the attack's
                // threshold blocks nothing, so offering the button before the pool can actually
                // cover the attack is offering a move that can only hurt. Previously this was
                // `Disabled = !hasAnyBlock`, so Block 1 vs Physical 5 was clickable — it consumed
                // the whole pool into a declaration that could never succeed, dropped "Available"
                // to zero with no sign the block had gone anywhere, and left the player in the panel.
                //
                // Gate on the real rule instead, via the same shared helper the resolver uses.
                // Declarations AGGREGATE per (enemy, attackIndex), so the test is "everything already
                // declared against THIS attack, plus everything still in the pool" — that keeps an
                // incremental declaration path valid rather than assuming the pool is the whole story.
                var declaredHere = _declarations
                    .Where(d => d.Target == e && d.AttackIndex == i)
                    .SelectMany(d => d.Contributions);
                bool canFullyBlock = BlockOutcome.IsFullyBlocked(
                    attack, e.HasAbility(EnemyAbility.Swift), declaredHere.Concat(available));

                var declareBtn = new Button();
                declareBtn.Name     = $"BlockBtn_{e.Definition.Id}_{i}";
                declareBtn.AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled;  // interpolated
                declareBtn.Text     = Strings.Format("ui.combat.block.declare_vs",
                    e.Definition.Name, attack.Type, attack.Value);
                declareBtn.Disabled = !canFullyBlock;
                declareBtn.AddThemeFontSizeOverride("font_size", 24);
                var capturedEnemy  = e;
                var capturedIndex  = i;
                var capturedAttack = attack;

                if (singleAttack) {
                    // No split possible: one attack gets the whole available pool.
                    // Plain button — no SpinBoxes. Disabled until the pool can fully block.
                    declareBtn.Pressed += () => OnBlockPressedDumpAll(capturedEnemy, capturedIndex, capturedAttack);
                } else {
                    // Multiple attacks: per-block-type SpinBoxes let the player split
                    // the pool across attacks. Button commits only non-zero amounts.
                    var spinBoxes = new List<(BlockType type, SpinBox box)>();
                    foreach (var contrib in available) {
                        var typeLabel = new Label();
                        typeLabel.Text = $"{contrib.Type}:";
                        typeLabel.AddThemeFontSizeOverride("font_size", 22);
                        row.AddChild(typeLabel);

                        var spinBox = new SpinBox();
                        spinBox.MinValue = 0;
                        spinBox.MaxValue = contrib.Value;
                        spinBox.Step     = 1;
                        spinBox.Rounded  = true;
                        spinBox.Value    = 0;
                        row.AddChild(spinBox);
                        spinBoxes.Add((contrib.Type, spinBox));
                    }

                    var capturedSpinBoxes = spinBoxes;
                    declareBtn.Pressed += () =>
                        OnBlockPressed(capturedEnemy, capturedIndex, capturedAttack, capturedSpinBoxes);
                }

                row.AddChild(declareBtn);

                // Trailing spacer absorbs the row's spare width, keeping the name + declare button
                // grouped on the left instead of the button being pushed against Pass on the right.
                var spacer = new Control();
                spacer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                row.AddChild(spacer);

                _attackList.AddChild(row);
            }
        }
    }

    // Called on ResourcesChanged (new Block card played) and after every declaration commit.
    // Updates the "Available: …" summary label and rebuilds attack rows so SpinBox maxes
    // and button disabled state stay consistent with the current undeclared pool.
    private void RefreshBlockDisplay() {
        if (_state == null) { _blockLabel.Text = ""; return; }
        var available = AvailableContribs();
        if (available.Count == 0) {
            _blockLabel.Text = Strings.Get("ui.combat.block.none_available");
        } else {
            _blockLabel.Text = Strings.Format("ui.combat.block.available",
                string.Join(", ", available.Select(c => $"{c.Type} {c.Value}")));
        }
        RefreshAttackRows();  // Rebuild so SpinBox maxes and button disabled states reflect updated pool.
    }

    private List<BlockContribution> AvailableContribs() {
        if (_state == null) return new List<BlockContribution>();
        var all = BlockBridge.ExtractBlockContributions(_state.BlockPool);

        // BlockPool aggregates by AttackElement, so `all` holds at most one entry per BlockType.
        // Subtract the total already declared per type by AMOUNT — matching declared entries by
        // exact value breaks the moment another card raises the pool total (e.g. declared 4, then
        // a second card makes the pool 7 → the 4 must still be subtracted).
        var declaredByType = new Dictionary<BlockType, int>();
        foreach (var d in _declaredContribs)
            declaredByType[d.Type] = declaredByType.GetValueOrDefault(d.Type) + d.Value;

        var result = new List<BlockContribution>();
        foreach (var c in all) {
            int remaining = c.Value - declaredByType.GetValueOrDefault(c.Type);
            if (remaining > 0)
                result.Add(new BlockContribution(c.Type, remaining));
        }
        return result;
    }

    // Reads SpinBox values for each block type; builds a BlockDeclaration from the
    // chosen (non-zero) amounts only. Ignores taps where every SpinBox is zero
    // (no block committed). After a valid commit, rebuilds rows so SpinBox maxes
    // reflect the reduced remaining pool, then auto-closes if all attacks are
    // fully blocked (6b).
    private void OnBlockPressed(EnemyTokenInstance enemy, int attackIndex, EnemyAttack attack,
            List<(BlockType type, SpinBox box)> spinBoxes) {
        var chosen = spinBoxes
            .Where(sb => sb.box.Value > 0)
            .Select(sb => new BlockContribution(sb.type, (int)sb.box.Value))
            .ToList();
        if (chosen.Count == 0) return;  // all zeros; ignore tap

        // All-or-nothing: reject a commit that still cannot reach the threshold, rather than
        // silently swallowing the block into a declaration that blocks nothing. The button's
        // Disabled state already covers "the pool can never cover this attack"; this covers
        // "the pool could, but the SpinBox amounts the player picked don't".
        var declaredHere = _declarations
            .Where(d => d.Target == enemy && d.AttackIndex == attackIndex)
            .SelectMany(d => d.Contributions);
        if (!BlockOutcome.IsFullyBlocked(attack, enemy.HasAbility(EnemyAbility.Swift),
                declaredHere.Concat(chosen))) {
            Log.Debug("[UI]", $"BlockTargetingPanel: {enemy.Definition.Name} attack {attackIndex} — " +
                "chosen block does not fully cover the attack (all-or-nothing); ignoring tap");
            return;
        }

        _declarations.Add(new BlockDeclaration(chosen, enemy, attackIndex));
        _declaredContribs.AddRange(chosen);
        RefreshBlockDisplay();  // Updates label + rebuilds rows with updated SpinBox maxes.
        if (AllBlockableAttacksFullyBlocked())
            Complete();
    }

    // Single-attack dump-all handler (6a): commits the entire available pool to the
    // one attack. Used when there is no allocation decision to make (totalBlockable == 1).
    // Ignores taps when no block is available. Auto-closes when fully blocked (6b).
    private void OnBlockPressedDumpAll(EnemyTokenInstance enemy, int attackIndex, EnemyAttack attack) {
        var available = AvailableContribs();
        if (available.Count == 0) return;  // no block; ignore tap

        // Defence in depth: the button is Disabled unless this can fully block, but a stale row
        // (or a future caller) must not be able to dump the pool into a doomed declaration.
        var declaredHere = _declarations
            .Where(d => d.Target == enemy && d.AttackIndex == attackIndex)
            .SelectMany(d => d.Contributions);
        if (!BlockOutcome.IsFullyBlocked(attack, enemy.HasAbility(EnemyAbility.Swift),
                declaredHere.Concat(available))) {
            Log.Debug("[UI]", $"BlockTargetingPanel: {enemy.Definition.Name} attack {attackIndex} — " +
                "available block cannot fully cover the attack (all-or-nothing); ignoring tap");
            return;
        }

        _declarations.Add(new BlockDeclaration(available, enemy, attackIndex));
        _declaredContribs.AddRange(available);
        RefreshBlockDisplay();
        if (AllBlockableAttacksFullyBlocked())
            Complete();
    }

    // Returns true when every blockable attack of every active enemy is fully
    // blocked by the declarations collected so far (6b auto-close condition).
    // Mirrors the resolver's enemy/attack loop: skips AttackCancelled, IsDestroyed,
    // and AttackType.None — the same attacks that would produce DamageAssignments.
    // Uses BlockOutcome.IsFullyBlocked (the shared helper from 6c).
    private bool AllBlockableAttacksFullyBlocked() {
        if (_combat == null) return false;
        foreach (var e in _combat.ActiveEnemies) {
            if (e.AttackCancelled || e.IsDestroyed) continue;
            var attacks = e.Definition.Attacks;
            for (int i = 0; i < attacks.Count; i++) {
                var attack = attacks[i];
                if (attack.Type == AttackType.None) continue;
                var allocated = _declarations
                    .Where(d => d.Target == e && d.AttackIndex == i)
                    .SelectMany(d => d.Contributions);
                if (!BlockOutcome.IsFullyBlocked(attack, e.HasAbility(EnemyAbility.Swift), allocated))
                    return false;
            }
        }
        return true;
    }

    private void OnPassPressed() => Complete();
}
