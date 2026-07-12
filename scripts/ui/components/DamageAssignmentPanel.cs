using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using MagusWarrior.Combat;
using MagusWarrior.Core;
using MagusWarrior.Core.Types;
using MagusWarrior.Units;

namespace MagusWarrior.UI;

// Bottom-sheet panel for the Assign-Damage phase (story 3-4b). Structural mirror of
// BlockTargetingPanel's LAYOUT and lifecycle — NOT its declaration model. This panel is
// invoked once per decision step (the resolver's per-spill loop calls ShowAndAwait repeatedly)
// and each call returns exactly one DamageChoice: no multi-declaration accumulation, no
// ResourcesChanged subscription, and no "Pass" — every call must yield a choice.
public partial class DamageAssignmentPanel : Control {
    private VBoxContainer _choiceList = null!;
    private Label         _infoLabel  = null!;
    private InputLock?    _inputLock;
    private bool          _holdsLock;
    private TaskCompletionSource<DamageChoice>? _tcs;

    public void Initialize(InputLock inputLock) => _inputLock = inputLock;

    public override void _Ready() {
        // Full-rect root that does NOT intercept input — only the docked sheet below captures taps
        // (mirrors the block/ranged panels). NOTE: unlike those panels, pass-through here is NOT
        // desirable — assign-damage is resolution, not a play window, and MK rules permit no card
        // plays during it. The shared InputLock, held across ShowAndAwait, is what makes HandView
        // reject taps; the layout stays consistent with its siblings.
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

        var col = new VBoxContainer();
        col.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        sheet.AddChild(col);

        var title = new Label();
        title.Text = "ui.combat.damage.title";                          // static ⇒ key + auto-translate
        title.AutoTranslateMode = Node.AutoTranslateModeEnum.Always;
        title.AddThemeFontSizeOverride("font_size", 28);
        col.AddChild(title);

        _infoLabel = new Label();
        // Interpolated ⇒ composed via Strings.Format. Auto-translate MUST be off, or Godot would
        // look up the already-formatted result as a key and find nothing.
        _infoLabel.AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled;
        _infoLabel.AddThemeFontSizeOverride("font_size", 22);
        col.AddChild(_infoLabel);

        _choiceList = new VBoxContainer();
        col.AddChild(_choiceList);
    }

    // One decision step. `state` supplies hero.Units so unit labels can be numbered by their
    // STABLE roster position rather than by their index in the shrinking `eligible` list.
    public Task<DamageChoice> ShowAndAwait(
            DamageAssignment assignment, int remainingDamage,
            IReadOnlyList<UnitInstance> eligible, CombatState combat, GameState state) {
        // Re-entrancy guard: CANCEL any stranded awaiter rather than handing it a fabricated
        // HeroAbsorbs. A synthesised "hero takes it" would draw real Wounds for a decision the
        // player never made, silently. Cancelling surfaces the fault instead — the resolver's
        // await throws, OnDevCombatPressed logs it, and ResolveCombat's finally still tears down.
        _tcs?.TrySetCanceled();

        // Hold the shared InputLock for the duration of the decision so HandView rejects card
        // taps. TryAcquire rather than an unconditional set, so we never stomp another view's
        // lock; if it is somehow already held we still show the panel, because the resolver's
        // decision must always be answerable.
        _holdsLock = _inputLock?.TryAcquire() ?? false;

        _tcs = new TaskCompletionSource<DamageChoice>();
        RebuildRows(assignment, remainingDamage, eligible, state);
        Visible = true;
        return _tcs.Task;
    }

    private void RebuildRows(
            DamageAssignment assignment, int remainingDamage,
            IReadOnlyList<UnitInstance> eligible, GameState state) {
        _choiceList.ClearChildren();

        var note = "";
        if (assignment.Source.HasAbility(EnemyAbility.Poison))   note += Strings.Get("ui.combat.ability.poison");
        if (assignment.Source.HasAbility(EnemyAbility.Paralyze)) note += Strings.Get("ui.combat.ability.paralyze");
        if (assignment.Source.HasAbility(EnemyAbility.Brutal))   note += Strings.Get("ui.combat.ability.brutal");

        _infoLabel.Text = Strings.Format("ui.combat.damage.info",
            assignment.Source.Definition.Name, assignment.DamageType, remainingDamage, note);

        // Number units by their position in the hero's FULL roster, not in `eligible`. `eligible`
        // shrinks each spill step (the absorber is consumed), so an eligible-relative index would
        // rename the same unit between prompts — "Unit 2" becoming "Unit 1", or losing its number
        // entirely once it is the last one left — and the player would wound the wrong unit.
        bool numbered = state.Hero.Units.Count > 1;
        foreach (var unit in eligible) {
            int rosterIndex = state.Hero.Units.IndexOf(unit);
            var label = numbered && rosterIndex >= 0
                ? Strings.Format("ui.combat.damage.unit_numbered", rosterIndex + 1)
                : Strings.Get("ui.combat.damage.unit");
            var resistNote = unit.HasResistanceTo(assignment.DamageType)
                ? Strings.Format("ui.combat.damage.resists", assignment.DamageType)
                : "";

            var btn = new Button();
            btn.AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled;   // interpolated
            btn.Text = Strings.Format("ui.combat.damage.absorb_on", label, unit.Armor, resistNote);
            btn.AddThemeFontSizeOverride("font_size", 24);
            var capturedUnit = unit;
            btn.Pressed += () => Choose(new DamageChoice.AssignToUnit(capturedUnit));
            _choiceList.AddChild(btn);
        }

        var heroBtn = new Button();
        heroBtn.Text = "ui.combat.damage.hero_takes_it";                   // static ⇒ key + auto-translate
        heroBtn.AutoTranslateMode = Node.AutoTranslateModeEnum.Always;
        heroBtn.AddThemeFontSizeOverride("font_size", 24);
        heroBtn.Pressed += () => Choose(new DamageChoice.HeroAbsorbs());
        _choiceList.AddChild(heroBtn);
    }

    private void Choose(DamageChoice choice) {
        Visible = false;

        // Release the lock BEFORE completing the TCS: TrySetResult resumes the resolver INLINE
        // (no RunContinuationsAsynchronously), and its very next act may be another ShowAndAwait
        // that has to re-acquire this same lock.
        if (_holdsLock) {
            _inputLock?.Release();
            _holdsLock = false;
        }

        // Detach the TCS before completing it, so a second press in this frame (a stale row not
        // yet reaped, a double-tap) finds nothing to resolve and is inert, rather than feeding
        // the resolver a second — now ineligible — choice.
        var tcs = _tcs;
        _tcs = null;
        tcs?.TrySetResult(choice);
    }
}
