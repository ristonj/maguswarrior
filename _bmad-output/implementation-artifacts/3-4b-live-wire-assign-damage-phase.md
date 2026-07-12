# Story 3.4b: Live-Wire the Assign-Damage Phase (Unit-Choice Panel + Wound Readout)

Status: done

<!-- 2026-07-12: code review complete (3 Opus layers) — 4 decisions resolved, 14 patches applied,
     6 deferred, 3 dismissed. 324 tests green, build 0/0.
     AC5 on-device run PERFORMED AND PASSED across ~8 combats on the WSL desktop loop:
       - damage panel appears per spill with correct remaining damage (10 → 7 → 4)
       - wound math exact on three paths: unit+unit+hero = 2, unit+hero = 4, hero-only = 5
       - "Unit 2" label stays stable across spill steps (the eligible-index bug is gone)
       - all strings render in English (translation import verified); no raw ui.* keys
       - card plays REJECTED during assign-damage, ALLOWED during block
       - fully-blocked combat correctly yields 0 wounds
       - units reseed on repeat runs; no wedged button; zero exceptions
     On-device also surfaced three bugs NOT in the original review, all fixed and re-verified:
     the silent card-tap rejection, the block button clickable with insufficient block, and the
     block/ranged panel misalignment. See Review Findings. -->


<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a player,
I want the Assign-Damage phase to be interactive during live dev combat — choosing, per unblocked attack, whether one of my units absorbs the damage or my hero takes it, and then seeing how many Wounds my hero drew,
so that the resolver-core assign-damage phase from story 3-4 is exercised end-to-end in the running game, and unblocked damage produces a visible outcome instead of landing invisibly in `Hero.Hand`.

## Acceptance Criteria

**Scope boundary.** This is the **live-wire** counterpart to story 3-4 (assign-damage resolver-core), mirroring the 3-2→3-2b and 3-3→3-3b rhythm. It does three things, one of which (the seam reshape) is a model change John approved because 3-4's code review explicitly deferred two broker-seam gaps to this story:

1. **Reshape the `PromptHeroDamageTarget` broker seam** to resolve the two deferred findings — a **typed `DamageChoice` result** (kills the `null`-vs-ineligible-unit ambiguity at the type level) and a **`remainingDamage` parameter** (so the panel can show "N damage still to assign" without recomputing resolver math). (John's decisions 2026-07-11.)
2. **Add a `DamageAssignmentPanel`** (Godot `Control`, bottom-sheet, mirrors `BlockTargetingPanel`) and register `_broker.DamageTargetProvider` in `PlaceholderMainMenu`, so the already-running assign-damage phase becomes interactive: per unblocked attack, the player picks an eligible unit to absorb it or lets the hero take it.
3. **Surface the outcome** — add `WoundsDrawn` to `CombatResult`, tracked via a `CombatState.WoundsToHand` counter, and show an on-screen post-combat summary (defeated + wounds taken). **Seed two dev units** so the panel, the unit-absorption path, and the resistance/spill path are all live-verifiable (the dev hero has no recruited units — recruitment is Epic 5).

**John's three scope decisions (2026-07-11), baked into the ACs below:**
1. **Typed result + remaining-d param** for the seam reshape (not the minimal "add int only" and not panel-drives-allocation). The resolver keeps driving the per-spill loop; only the seam's shape changes.
2. **`WoundsDrawn` on `CombatResult` + an on-screen readout line** (not a dedicated results panel, not log-only).
3. **Seed two dev units — one plain (armor 3), one Fire-resistant (armor 3) —** in the dev combat flow so the unit-vs-hero choice, absorption, and resistance-then-spill are all exercisable on-device.

**It does NOT:**
- **Implement knockout / knockdown / Paralyze-vs-hero hand-emptying.** Still **story 3-5**. Option B stops at `DrawWoundsToHand` (+ Poison discard), exactly as 3-4 shipped. Do **not** add `IsKnockedOut`/`HeroKnockedOut` or a hand-size threshold. `WoundsDrawn` is a plain count for the readout — it carries no knockout semantics.
- **Unify `Hero.Hand` with `DeckManager`.** The two hands still coexist by design; the on-screen `DeckManager` hand still does not show combat Wounds. The wound readout reads the count from `CombatResult.WoundsDrawn` (sourced from `Hero.Hand`), NOT from the `DeckManager` hand. Unification remains deferred.
- **Recruit real units.** The two dev units are seeded **only** in the `#if`-guarded dev combat handler, exactly like the dev Brown token — no recruitment UI, no `data/units.yaml`, no unit identity/abilities/ready-state (all Epic 5). `UnitInstance` stays the minimal damage-sink from 3-4.
- **Touch Melee (Phase 4), enemy piles, Into-the-Heat, Diplomacy/Influence, or mid-combat serialization (3-6).**

---

**AC1 — Reshape the broker seam: typed `DamageChoice` result + `remainingDamage` param (TDD, red-first)**

Introduce a typed result so "hero absorbs" and "assign to unit" are distinct types (resolving deferred finding #1 — the silent `null`/ineligible-unit collapse). Add it to `scripts/combat/CombatContributions.cs` next to `DamageAssignment` (add `using MagusWarrior.Units;` there):

```csharp
// The hero's Assign-Damage decision for one (spill) step. Replaces the old UnitInstance? return,
// which conflated "hero absorbs" (null) with "provider returned an ineligible unit" (also null-branch).
// Now the two are distinct types: HeroAbsorbs is an explicit choice, and an AssignToUnit carrying an
// ineligible unit is a caller (panel) bug the resolver asserts on rather than silently eating.
public abstract record DamageChoice {
    public sealed record HeroAbsorbs()                 : DamageChoice;
    public sealed record AssignToUnit(UnitInstance Unit) : DamageChoice;
}
```

Reshape the seam in `scripts/broker/UIBroker.cs` — add the `remainingDamage` parameter (resolving deferred finding #2) and return `DamageChoice`; default (no provider) is `HeroAbsorbs`:

```csharp
public Func<DamageAssignment, int, IReadOnlyList<UnitInstance>, CombatState, Task<DamageChoice>>?
    DamageTargetProvider { get; set; }

// Returns the hero's choice for THIS decision step. `remainingDamage` is the running, post-armor
// damage still to assign (NOT the printed RawValue) so the panel can show "N still to assign" without
// re-deriving the resolver's math. Default (no provider): HeroAbsorbs — the correct fallback when the
// panel is not registered (all unblocked damage lands on the hero).
public virtual Task<DamageChoice> PromptHeroDamageTarget(
        DamageAssignment assignment, int remainingDamage,
        IReadOnlyList<UnitInstance> eligibleUnits, CombatState combat) =>
    DamageTargetProvider?.Invoke(assignment, remainingDamage, eligibleUnits, combat)
    ?? Task.FromResult<DamageChoice>(new DamageChoice.HeroAbsorbs());
```

Update `ApplyOneDamageAssignment` in `scripts/combat/CombatResolver.cs` to switch on the typed result, pass the running `d`, and **assert** on an ineligible unit (finding #1 — an `AssignToUnit` whose unit is not in `eligible` is a panel bug, not a hero-absorb):

```csharp
DamageChoice choice = eligible.Count == 0
    ? new DamageChoice.HeroAbsorbs()                       // nothing to choose ⇒ hero
    : await _broker.PromptHeroDamageTarget(a, d, eligible, combat);   // pass running d, not RawValue

if (choice is DamageChoice.AssignToUnit assign) {
    var target = assign.Unit;
    if (!eligible.Contains(target))
        throw new System.InvalidOperationException(
            "DamageTargetProvider returned an ineligible unit; the panel must only offer eligible units.");
    // Option A — assign to a unit. Marked "used" regardless of outcome.
    alreadyAssigned.Add(target);
    if (target.HasResistanceTo(a.DamageType)) d -= target.Armor;
    if (d <= 0) break;
    d -= target.Armor;
    if (a.Source.HasAbility(EnemyAbility.Paralyze)) {
        target.Destroy();
    } else {
        target.TakeWound();
        if (a.Source.HasAbility(EnemyAbility.Poison))
            target.TakeWound();
    }
} else {
    // Option B — hero absorbs the remaining damage.
    int wounds = (int)System.Math.Ceiling((double)d / System.Math.Max(1, hero.Armor));
    hero.DrawWoundsToHand(wounds);
    combat.WoundsToHand += wounds;                          // AC3 — for the readout
    if (a.Source.HasAbility(EnemyAbility.Poison))
        hero.AddWoundsToDiscard(wounds);
    // NOTE: knockout threshold + Paralyze-vs-hero hand-emptying are story 3-5 — not here.
    d = 0;
}
```

**`CombatResolver` still has no `using Godot` and must keep none** — no `Log`/`GD.Print` anywhere (same hard constraint as 3-3/3-4). The only behavioral change vs 3-4 is: (a) the typed result, (b) the ineligible-unit throw (was a silent hero-branch), (c) passing running `d` to the seam, (d) the `WoundsToHand` counter. The deterministic math (Brutal doubling, double-armor-on-resistance, Paralyze-destroys-no-wound, Poison-second-wound / Poison-extra-discard, per-combat `alreadyAssigned` HashSet, `UnitDamageLocked` gate, armor-floor guard) is **unchanged**.

**Test coverage — migrate + extend `tests/unit/CombatResolverTest.cs`:**
- **Migrate** the `TestBroker` damage-choice seam and `MakeResolverWithDamageChoices` from `params UnitInstance?[]` to `params DamageChoice[]`, and the overridden `PromptHeroDamageTarget` to the new 4-arg signature returning `DamageChoice` (dequeue → default `HeroAbsorbs` when empty). Every existing 3-4 assign-damage test migrates: a scripted `null` (hero) becomes `new DamageChoice.HeroAbsorbs()`, a scripted `unit` becomes `new DamageChoice.AssignToUnit(unit)`. All existing assertions stay identical (same outcomes).
- Add `AssignDamage_IneligibleUnitChoice_Throws` (TDD red-first) — script `AssignToUnit(u)` where `u` is a wounded/foreign unit not in `eligible`; assert `ResolveAssignDamagePhase` throws `InvalidOperationException`. Proves finding #1 is resolved (no silent hero-branch).
- Add `AssignDamage_SeamReceivesRunningDamage_NotRawValue` — a spill case (unit armor 3, Physical 8, choose unit then hero) where the `TestBroker` records the `remainingDamage` it was passed on each call; assert the **second** call received `2` (the post-armor running `d`), not `8`. Proves finding #2 is resolved.
- Keep every other 3-4 assign-damage case green under the migrated signature (no outcome change).

---

**AC2 — `WoundsDrawn` on `CombatResult`, tracked via `CombatState.WoundsToHand` (TDD)**

Add a per-combat counter on `scripts/combat/CombatState.cs` (next to `FameEarned`):
```csharp
public int WoundsToHand { get; set; }   // Wounds drawn to Hero.Hand this combat (for the post-combat readout)
```
Increment it in the Option-B hero branch (AC1). Add `WoundsDrawn` to `CombatResult` in `scripts/combat/CombatGroup.cs` and populate it in `BuildResult`:
```csharp
public record CombatResult(
    bool                     HeroWon,
    List<EnemyTokenInstance> DefeatedEnemies,
    int                      FameEarned,
    int                      ReputationEarned,
    int                      WoundsDrawn                 // NEW
);
```
```csharp
private CombatResult BuildResult(CombatState combat) =>
    new(
        HeroWon:          combat.ActiveEnemies.Count == 0,
        DefeatedEnemies:  new List<EnemyTokenInstance>(combat.DefeatedEnemies),
        FameEarned:       combat.FameEarned,
        ReputationEarned: combat.ReputationEarned,
        WoundsDrawn:      combat.WoundsToHand           // NEW
    );
```
`BuildResult` runs **before** `TearDownCombatState`, so `WoundsToHand` is still populated. `WoundsToHand` counts wounds drawn to **hand** only (the Poison extra-to-discard wounds are a separate mechanic and are NOT counted in the readout — the readout reflects what entered the hand). It is a plain count with **no** knockout meaning (knockout is 3-5).

**Test coverage** — add to `CombatResolverTest.cs`:
- `AssignDamage_ResultReportsWoundsDrawn` — no units, Physical 5, hero armor 2 ⇒ resolve **via `ResolveCombat`** (or set `WoundsToHand` through the phase and call `BuildResult` if it is reachable in tests; prefer end-to-end `ResolveCombat` mirroring the existing `AssignDamage_SkippedWhenAllEnemiesDefeated` case) ⇒ `result.WoundsDrawn == 3`.
- `AssignDamage_UnitAbsorbs_WoundsDrawnZero` — unit fully absorbs ⇒ `result.WoundsDrawn == 0`.
- Confirm the existing `CombatResult` construction sites (any test asserting on the record, plus `BuildResult` callers) compile with the new 5-arg record — update any positional `new CombatResult(...)` in tests.

---

**AC3 — `DamageAssignmentPanel` shows the current attack + remaining damage; player picks a unit or the hero (per decision step)**

New `scripts/ui/components/DamageAssignmentPanel.cs` (Godot `Control` subclass), a structural mirror of `BlockTargetingPanel`'s **layout and lifecycle** (bottom-sheet, `MouseFilter = Ignore` root so nothing underneath is blocked, `ZIndex = 5`, docked in the 350–480px band, not visible on `_Ready`). **Key difference from the block/ranged panels:** this panel is invoked **once per decision step** (the resolver's per-spill loop calls it repeatedly), and each call returns exactly **one `DamageChoice`** — there is no multi-declaration accumulation and **no "Pass"** (every call must yield a choice).

```csharp
public Task<DamageChoice> ShowAndAwait(
        DamageAssignment assignment, int remainingDamage,
        IReadOnlyList<UnitInstance> eligible, CombatState combat) {
    // Re-entrancy guard: complete any stranded awaiter before re-arming. (The resolver awaits each
    // call sequentially, so this is defensive — but a second show must never hang a Task.)
    _tcs?.TrySetResult(new DamageChoice.HeroAbsorbs());

    _tcs = new TaskCompletionSource<DamageChoice>();
    RebuildRows(assignment, remainingDamage, eligible);
    Visible = true;
    return _tcs.Task;
}

private void Choose(DamageChoice choice) {
    Visible = false;
    _tcs?.TrySetResult(choice);
}
```

UI requirements (`RebuildRows`):
- **Title:** `"Combat: Assign Damage"`.
- **Info label:** the enemy, the attack element, and the **remaining** damage — e.g. `"{enemy.Definition.Name}: {assignment.DamageType} — {remainingDamage} damage to assign"`. This uses the passed `remainingDamage` directly (the whole point of the seam reshape — the panel never recomputes the resolver's running total). If the enemy has Poison/Paralyze/Brutal, append a short note (e.g. `" [Poison]"`) so the player understands the stakes — read from `assignment.Source.HasAbility(...)`.
- **One "Absorb on {name}" button per eligible unit** — for each `UnitInstance` in `eligible`, a button labelled with its armor and any resistance to **this** attack type, e.g. `"Absorb on Unit (Armor 3)"` or `"Absorb on Unit (Armor 3, resists {DamageType})"` when `unit.HasResistanceTo(assignment.DamageType)`. On tap: `Choose(new DamageChoice.AssignToUnit(unit))`. (Units have no name/identity yet — Epic 5 — so "Unit" + armor + resistance is the disambiguator. If two units are indistinguishable, index them `"Unit 1"/"Unit 2"`.)
- **"Hero takes it" button** — always present; on tap: `Choose(new DamageChoice.HeroAbsorbs())`.
- The panel does **not** compute wounds or absorption outcome — it only returns the choice; the resolver does the math (mirrors how `BlockTargetingPanel` does not compute block success).

**No live `ResourcesChanged` subscription** — unlike block/ranged, this panel shows no card-play-driven pool (the player is not playing cards during assign-damage resolution), so it does not subscribe to `ResourcesChanged` and needs no unsubscribe-before-TCS dance. Keep the re-entrancy `TrySetResult` guard only.

---

**AC4 — `PlaceholderMainMenu` wiring: register the provider, seed two dev units, show the readout**

In `PlaceholderMainMenu._Ready()`, alongside the existing panel wiring:
```csharp
_damageAssignmentPanel = new DamageAssignmentPanel();
_damageAssignmentPanel.Name = "DamageAssignmentPanel";
AddChild(_damageAssignmentPanel);
_broker.DamageTargetProvider = (a, rem, eligible, c) => _damageAssignmentPanel.ShowAndAwait(a, rem, eligible, c);
```
Add the field `private DamageAssignmentPanel _damageAssignmentPanel = null!;` next to `_blockTargetingPanel`.

In `OnDevCombatPressed` (inside the `#if` dev block), **seed two units on the hero before `ResolveCombat`** so the panel is exercisable, and **clear them after** so repeated dev runs start clean and no state leaks into non-combat play:
```csharp
// Dev-only: give the hero two units so the assign-damage panel has real choices.
// Recruitment is Epic 5 — these are throwaway damage-sinks for live verification only.
_state.Hero.Units.Add(new UnitInstance(armor: 3));
_state.Hero.Units.Add(new UnitInstance(armor: 3, resistances: new[] { AttackType.Fire }));
```
Add the clear in the `finally` block (next to the phase reset): `_state.Hero.Units.Clear();`. (Leaving `Hero.Hand` wounds in place across runs is fine — they accumulate, which is realistic; only the transient dev units are cleared. If a run wounds a unit, clearing the list next run reseeds fresh units — acceptable for a dev harness.)

Replace the existing result `Log.Debug` with a readout that includes wounds **and** an on-screen summary. Use a Godot `AcceptDialog` (built-in modal with an OK button — no new component file, honest to the "small on-screen summary" decision):
```csharp
var summary = result.HeroWon
    ? $"Combat WON — {result.DefeatedEnemies.Count} defeated, took {result.WoundsDrawn} Wound(s)"
    : $"Combat LOST — {result.DefeatedEnemies.Count} defeated, took {result.WoundsDrawn} Wound(s)";
Log.Debug("[Combat]", summary);
var dlg = new AcceptDialog { DialogText = summary };
AddChild(dlg);
dlg.PopupCentered();
dlg.Confirmed += () => dlg.QueueFree();     // clean up the dialog after OK
```
Add `using MagusWarrior.Units;` and (if not present) the `AttackType` namespace to `PlaceholderMainMenu.cs`.

**No change to the resolver call site or the InputLock handling is required** — `ResolveCombat` already runs `ResolveAssignDamagePhase` after Block (3-4 wired it); with `DamageTargetProvider` now registered, that phase becomes interactive. The InputLock is already released before `ResolveCombat`.

---

**AC5 — Full suite green + manual verification (live-wire, on-device)**

`dotnet test tests/maguswarrior.Tests.csproj` passes — all 317 existing tests (with the assign-damage cases migrated to `DamageChoice`) plus the new AC1/AC2 cases. No regressions. A full Godot-aware `dotnet build maguswarrior.csproj` is clean (0/0), confirming no Godot-type leakage into `DamageChoice`/`UIBroker`/`CombatResolver`.

**Manual verification (WSL desktop loop, mirrors 3-2b/3-3b — this is the live-wire, so an on-device run IS required, unlike 3-4's resolver-core smoke):**
- Launch game → "Combat: Dev (Brown Token)" → interstitial (Minotaur, Armor 5, Physical 5, Brutal) → Begin → ranged Pass → block panel → **Pass without fully blocking** (let the Physical 5 attack through) →
- **Assign-Damage panel appears** as a bottom-sheet showing `"Minotaur: Physical — 10 damage to assign"` (Brutal doubled 5→10) with **"Absorb on Unit (Armor 3)"**, **"Absorb on Unit (Armor 3, resists Physical?)"** (the Fire-resistant unit does **not** resist Physical, so it shows no resist note here — good, it exercises the non-resistant path against a Physical attack), and **"Hero takes it"**.
- Tap **"Absorb on Unit (Armor 3)"** → damage 10−3 = 7 remains → panel **re-appears** showing `"...7 damage to assign"` with the remaining eligible unit + hero → tap **"Hero takes it"** → `⌈7/2⌉ = 4` Wounds drawn →
- **Post-combat summary dialog** shows `"Combat LOST — 0 defeated, took 4 Wound(s)"`; OK dismisses it; phase returns to Movement; **no exceptions**.
- Also verify: choosing **"Hero takes it"** immediately on the first prompt draws `⌈10/2⌉ = 5` Wounds; a second "Combat: Dev" run reseeds fresh units and works (re-entrancy + unit-clear).
- (Correctness of the absorption math is proven by unit tests; the manual run verifies the *pipeline* — panel appears per spill, remaining-damage readout is correct, choices route, summary shows.)

Record the on-device run in Completion Notes.

---

## Tasks / Subtasks

- [x] **Task 1 — Seam reshape: `DamageChoice` + `remainingDamage` (TDD)** (AC: 1)
  - [x] Add the `DamageChoice` record to `scripts/combat/CombatContributions.cs` (+ `using MagusWarrior.Units;`)
  - [x] Reshape `UIBroker.DamageTargetProvider` + `virtual PromptHeroDamageTarget` to the 4-arg / `DamageChoice`-returning signature; default `HeroAbsorbs`
  - [x] Migrate `TestBroker` damage seam + `MakeResolverWithDamageChoices` to `params DamageChoice[]` and the new override signature; migrate every existing 3-4 assign-damage test to `HeroAbsorbs()` / `AssignToUnit(unit)` (assertions unchanged)
  - [x] Update `ApplyOneDamageAssignment` to switch on `DamageChoice`, pass running `d`, and throw on an ineligible `AssignToUnit`
  - [x] Write red-first `AssignDamage_IneligibleUnitChoice_Throws` and `AssignDamage_SeamReceivesRunningDamage_NotRawValue`; confirm green
  - [x] Confirm `CombatResolver` still has zero `using Godot` / no `Log`

- [x] **Task 2 — `WoundsDrawn` readout data (TDD)** (AC: 2)
  - [x] Add `CombatState.WoundsToHand`; increment in the Option-B hero branch
  - [x] Add `WoundsDrawn` to `CombatResult`; populate in `BuildResult`; fix all positional `new CombatResult(...)` sites
  - [x] Write `AssignDamage_ResultReportsWoundsDrawn` + `AssignDamage_UnitAbsorbs_WoundsDrawnZero`; confirm green

- [x] **Task 3 — `DamageAssignmentPanel`** (AC: 3)
  - [x] Implement `scripts/ui/components/DamageAssignmentPanel.cs` mirroring `BlockTargetingPanel`'s bottom-sheet layout/lifecycle; per-call `ShowAndAwait` returning one `DamageChoice`; info label using `remainingDamage`; one "Absorb on {name}" button per eligible unit (armor + resistance-to-this-attack note); "Hero takes it" button; re-entrancy `TrySetResult` guard; no `Pass`, no `ResourcesChanged` subscription; not visible on `_Ready`

- [x] **Task 4 — `PlaceholderMainMenu` wiring + dev units + readout** (AC: 4)
  - [x] Add `_damageAssignmentPanel` field; create + add child + register `DamageTargetProvider` in `_Ready()`
  - [x] Seed two dev units before `ResolveCombat`; clear them in `finally`
  - [x] Replace the result log with the wounds-inclusive summary + `AcceptDialog` on-screen readout

- [x] **Task 5 — Full regression + on-device manual verification** (AC: 5)
  - [x] `dotnet test tests/maguswarrior.Tests.csproj` → green (317 migrated + new AC1/AC2 cases)
  - [x] `dotnet build maguswarrior.csproj` → 0/0
  - [ ] On-device WSL desktop run per AC5; record in Completion Notes — **pending the human** (not performed by the dev agent)
  - [x] Update deferred-work.md: strike the two 3-4 broker-seam defers (now resolved) and the "assign-damage phase has no live UI" entry; note the DeckManager↔Hero.Hand unification remains open

### Review Findings

Three-layer adversarial review (Blind Hunter / Edge Case Hunter / Acceptance Auditor), all on Opus, 2026-07-12.
**All 5 ACs pass clean; scope is clean; the Dev Agent Record's factual claims all verified true.** Findings below are
about the edges of the change, not the change itself. Two findings were reached independently by two layers (marked ††).

**All 4 decisions resolved and all 12 patches applied 2026-07-12** — 324 tests green (321 + 3 new), `dotnet build maguswarrior.csproj` 0/0, no Godot leakage into `scripts/combat|broker|units`, `project.godot` unmodified, `ui_strings.en.translation` re-imported. **Still gated on AC5's on-device run.**

**Decisions (John, 2026-07-12) — all resolved:**

- [x] [Review][Decision] **Poison wound readout under-reports by half** †† — `WoundsToHand` counts only hand wounds; the Poison extra-to-discard wounds are equal in number and uncounted. A Poison enemy dealing 4 absorbed damage puts 2 Wounds in hand + 2 in discard, and the dialog says "took 2 Wound(s)". AC2 *explicitly authorized* hand-only counting ("the readout reflects what entered the hand") — so the code is spec-compliant and the **UI copy is what lies**. Unreachable today (dev combat hardcodes the Brown/Minotaur token; no Brown enemy has Poison), reachable the moment Epic 4 draws green tokens. Options: (a) reword the readout to "N Wound(s) to hand", (b) add a `WoundsToDiscard` counter and report both, (c) accept as-is and revisit in 3-5.
  **RESOLVED — option (b), with a correction.** John's proposed mechanism ("hand-wounds always equal discard-wounds for a Poison enemy, so just print the same N twice") is true **per assignment** but false **per combat**: `WoundsDrawn` is a combat total, and a mixed group draws hand-wounds from every enemy but discard-wounds only from the Poison ones. Implemented as a separate `CombatState.WoundsToDiscard` → `CombatResult.WoundsToDiscard` counter, incremented on the line beside `AddWoundsToDiscard`, and appended to the readout as `" (also N Wound(s) straight to discard)"` when non-zero. Same on-screen outcome John asked for, correct under every group composition. Pinned by `AssignDamage_MixedPoisonGroup_WoundsToDiscardDiffersFromWoundsDrawn` (5 to hand, 3 to discard).
- [x] [Review][Decision] **`docs/project-context.md` error-handling rule is now factually contradicted by the code** — the doc admits exactly two failure categories (expected → `Result<T>`; unrecoverable startup → throw) and states *"Normal game flow never throws."* The new `InvalidOperationException` in `ApplyOneDamageAssignment` is a third thing: an internal invariant violation by a caller. The Auditor judged the throw **substantively correct** (a player cannot produce it; `Result<T>` would be strictly worse, since the only sane response to "the UI lied to me" is to stop) and the **doc stale**. Options: (a) amend project-context.md with a third category ("internal invariant violation → throw immediately; never for player-reachable states"), (b) convert the throw to `Result<T>`, (c) leave both standing. Leaving both standing means the next agent either reverts this or cites it as precedent for throwing on a player-reachable path.
  **RESOLVED — option (a).** `docs/project-context.md#error-handling` now documents **three** categories (expected player failure → `Result<T>`; unrecoverable startup → throw; **internal invariant violation → throw**), with the discriminating test spelled out — *can the player produce this by playing the game?* — plus the requirement that a category-3 throw must still clean up (`finally`) and must be logged in full (`$"{ex}"`, never `ex.Message`).
- [x] [Review][Decision] **Card plays are not blocked during assign-damage** — the panel root is `MouseFilter.Ignore` (copied from `BlockTargetingPanel`, where pass-through is the *point* — you play cards to block), and `OnDevCombatPressed` releases the InputLock for the whole of `ResolveCombat`. `PhaseGate` grants `Mana`/`Crystal`/`Special` at `GamePhase.Any`, so all three are legal while `CurrentPhase == CombatAssignDamage`. The player can tap a card in `HandView` mid-spill-loop; the panel never rebuilds (no `ResourcesChanged` subscription, by design). Impact is bounded today (mana/crystal gain only), but assign-damage is resolution, not a play window — MK rules do not permit card plays here. Options: (a) hold the InputLock for the duration of `ShowAndAwait`, (b) give this panel a full-rect `MouseFilter.Stop` blocker, (c) accept as-is.
  **RESOLVED — option (a).** `DamageAssignmentPanel.Initialize(InputLock)` added; `ShowAndAwait` holds the shared `InputLock` for the duration of each decision (via `TryAcquire`, so it never stomps another view's lock) and `Choose` releases it **before** completing the TCS — necessary because the resolver's continuation resumes inline and its next `ShowAndAwait` must be able to re-acquire. HandView now rejects card taps during damage resolution.
- [x] [Review][Decision] **i18n: the project has a wired translation pipeline that exactly one label uses — decide whether to adopt or delete it** — `project.godot` registers `res://data/strings/ui_strings.en.translation` (imported from `data/strings/ui_strings.csv`, which holds **one** row: `ui.placeholder_menu.title`), and the established pattern is *key-in-`Text` + `AutoTranslateMode.Always`* (`PlaceholderMainMenu.cs:77-78`) — **not** `tr()`. Every combat panel since (Ranged, Block, and now DamageAssignment) hardcodes English literals and ignores it. Not a 3-4b regression, but 3-4b adds a third offender. Three tiers of work if adopted: (1) static strings (`"Hero takes it"`) are mechanical — key + auto-translate; (2) **interpolated strings are the real blocker** — `$"{name}: {DamageType} — {remainingDamage} damage to assign"` is unique at runtime and can never match a translation key, so it needs a translated *format template* + `string.Format(TranslationServer.Translate(key), args)` with `AutoTranslateMode` **off**, which means two mechanisms and therefore a shared helper (natural home: `scripts/core/`, alongside `Log.cs`, which already depends on Godot); (3) game data — enemy names from `data/enemies.yaml` and `AttackType`/`EnemyAbility` enum `ToString()`s are untranslated, so real i18n is a data-model change, not just a UI one. **Half-built i18n is the worst state**: it generates review findings, implies a convention nothing follows, and delivers nothing. Options: (a) **declare English-only** — delete `ui_strings.csv`, drop `locale/translations`, inline the one title, and it stops being a finding forever (recommended for a solo English Android build); (b) **commit to the convention now** — add the `Strings.Get(key, args)` helper and retrofit the three combat panels, before more retrofit debt accrues; (c) leave as-is and keep re-finding it every review.

**Patches — all 12 applied 2026-07-12:**

- [x] [Review][Patch] Stale `QueueFree`d buttons stay live for the rest of the frame and can feed an already-assigned unit back to the resolver, tripping the new `throw` †† — **FIXED**: `RemoveChild` before `QueueFree`, and `Choose` detaches `_tcs` (sets it null) before completing it, so a second press this frame is inert [scripts/ui/components/DamageAssignmentPanel.cs]
- [x] [Review][Patch] The `throw` unwinds past `TearDownCombatState`, leaving attack/block pools and enemy combat modifiers dirty into the next combat — **FIXED**: `ResolveCombat` now wraps its phase sequence in `try/finally { TearDownCombatState(combat); }`. `BuildResult` is evaluated before the `finally` runs, so the counters are still live. Pinned by `ResolveCombat_AssignDamageThrows_StillTearsDownCombatState` [scripts/combat/CombatResolver.cs]
- [x] [Review][Patch] The catch logs `ex.Message` only — so "fails fast and loud" did not hold end-to-end — **FIXED**: logs `$"{ex}"` (full type + stack trace) [scripts/ui/screens/PlaceholderMainMenu.cs]
- [x] [Review][Patch] `AcceptDialog` freed only on `Confirmed`, leaking a node per Escape-dismissed dialog — **FIXED**: `dlg.Canceled += () => dlg.QueueFree();` added alongside `Confirmed` [scripts/ui/screens/PlaceholderMainMenu.cs]
- [x] [Review][Patch] Unit button labels renamed the same unit between spill steps — **FIXED**: labels now index the hero's **full roster** (`state.Hero.Units.IndexOf(unit)`), which is stable, instead of the shrinking `eligible` list. `ShowAndAwait` takes `GameState` for this (mirroring `BlockTargetingPanel.ShowAndAwait(combat, state)`) [scripts/ui/components/DamageAssignmentPanel.cs]
- [x] [Review][Patch] `DamageChoice` open hierarchy + catch-all `else` — **FIXED**: hierarchy closed with a `private` constructor (only the nested cases can derive), and the resolver now matches `is HeroAbsorbs` explicitly with a `throw` on the unhandled case. Deliberately kept as `if`/`else if`/`else` rather than a `switch` — inside a `switch`, the existing `if (d <= 0) break;` would exit the switch instead of the `while (d > 0)` loop [scripts/combat/CombatContributions.cs, scripts/combat/CombatResolver.cs]
- [x] [Review][Patch] Re-entrancy guard fabricated a damaging `HeroAbsorbs` for a stranded awaiter — **FIXED**: now `TrySetCanceled()`, so the fault surfaces (resolver's await throws, handler logs it, `finally` still tears down) instead of silently drawing Wounds for a decision the player never made [scripts/ui/components/DamageAssignmentPanel.cs]
- [x] [Review][Patch] Dev teardown called `Hero.Units.Clear()` indiscriminately — **FIXED**: the two seeded instances are captured in a `devUnits` list and removed individually in the `finally`, so an Epic-5 recruited roster survives a dev-combat press [scripts/ui/screens/PlaceholderMainMenu.cs]
- [x] [Review][Patch] **(D1)** `CombatState.WoundsToDiscard` + `CombatResult.WoundsToDiscard` + readout line — see decision 1 above [scripts/combat/CombatState.cs, CombatGroup.cs, CombatResolver.cs, PlaceholderMainMenu.cs]
- [x] [Review][Patch] **(D2)** `docs/project-context.md` error-handling amended with the third category — see decision 2 above [docs/project-context.md]
- [x] [Review][Patch] **(D3)** `InputLock` held across `ShowAndAwait` — see decision 3 above [scripts/ui/components/DamageAssignmentPanel.cs, PlaceholderMainMenu.cs]
- [x] [Review][Patch] **(D4)** i18n convention adopted — see decision 4 and the scope note below [scripts/core/Strings.cs (new), data/strings/ui_strings.csv, all four combat panels]

- [x] [Review][Patch] **(13th — found on the on-device run, 2026-07-12)** **Card plays were correctly blocked during assign-damage, but blocked SILENTLY** — `HandView.OnCardTapped` (the card-detail opener) had **no lock check at all**, so during `CombatAssignDamage` a card would still open its detail panel with live-looking Play/Sideways/Power buttons; tapping them hit `if (!_lock.TryAcquire()) return;` in the play handlers and did **nothing** — no feedback, no disabled state. John hit this immediately on device and tried three cards in a row (`CardExpanded opened: threaten|stamina|improvisation phase=CombatAssignDamage`, then `Sideways tapped: improvisation` → silently rejected), which is exactly what a player does when a button looks broken. Violates the "no silent no-ops" UX principle, **and** `OnCardTapped` was the one path in `HandView` that ignored the documented InputLock contract ("while any view holds the lock, all other views reject user input"). **FIXED**: `if (_lock.IsLocked) return;` guard added at the top of `OnCardTapped`, so the card does not open at all while another view owns the interaction. Block/ranged phases are unaffected — those panels deliberately do not take the lock, because playing cards *is* the point there. [scripts/ui/components/HandView.cs:107]

- [x] [Review][Patch] **(14th — found on the on-device run)** **Block declare button was clickable with insufficient block, silently eating the pool** — `declareBtn.Disabled = !hasAnyBlock` enabled the button whenever the player had *any* block at all. With Block 3 vs the Minotaur's Physical 5, clicking dumped the whole pool into a declaration that can never meet the all-or-nothing threshold (LLD §9.3): "Available" dropped to zero with no sign the block had gone anywhere, and the player stayed in the panel. **FIXED**: the button now gates on the real rule via the shared `BlockOutcome.IsFullyBlocked` helper (the same one the resolver uses, so UI and rules cannot diverge), testing *already-declared-against-this-attack ∪ still-in-pool* so the incremental declaration path stays valid. Both commit handlers (`OnBlockPressed`, `OnBlockPressedDumpAll`) also reject a doomed declaration defensively and log it. Verified on device: built 3 → 4 → 5, button enabled only at 5, full block → 0 wounds. [scripts/ui/components/BlockTargetingPanel.cs]
- [x] [Review][Patch] **(15th — found on the on-device run, via screenshot)** **Block/Ranged declare button collided with the Pass button** — `nameLabel.SizeFlagsHorizontal = ExpandFill` made the enemy-name label eat the row's spare width, flinging the declare button to the far right of the column where it landed beside Pass — which is `ShrinkCenter`'d vertically in the sheet, so the two sat at different heights and read as misaligned. Pass was also clipping against the viewport edge. **FIXED**: label is `Fill` (not `ExpandFill`); a trailing spacer absorbs the spare width so name + button stay grouped left; a `MarginContainer` (24px sides) inside the sheet stops Pass clipping. Applied to **both** panels — Ranged had the identical structure and would have shown the same defect. Verified by screenshot. **Note:** my first diagnosis of this (stale `QueueFree`d rows growing the layout) was WRONG — the `rows=2` I measured was an artifact of `CallDeferred` running before Godot flushes the free-queue. The `ClearChildren` work is still correct on its own merits (the stale-input hazard is real), but it was not the cause. [scripts/ui/components/BlockTargetingPanel.cs, RangedTargetingPanel.cs]

**i18n scope actually delivered (D4).** `scripts/core/Strings.cs` (new, Godot-dependent, sits beside `Log.cs` — and deliberately absent from the test project's per-file `scripts/core` includes, which is why that project lists `scripts/core` file-by-file). It documents the **two** mechanisms and when each applies: *static* strings assign the KEY to `Text` with `AutoTranslateMode.Always`; *interpolated* strings can never match a key, so the TEMPLATE is translated and then filled via `Strings.Format(...)` with `AutoTranslateMode.Disabled`. `data/strings/ui_strings.csv` grew from 1 key to 30 and was re-imported (`godot --headless --import`) — **the `.translation` is a build artifact; editing the CSV alone leaves the old keys live and the new ones rendering as raw `ui.foo.bar` text.** All four combat panels retrofitted (Interstitial, Ranged, Block, DamageAssignment). **NOT converted:** `RestView`, `ImprovisationView`, `CardExpanded`, `CardCompact`, `StagingAreaView`, `HexMapView`, `TileCountView` — ~30 further strings, logged as a follow-up chore in deferred-work.md. Debug-only UI (`EffectEventLogPanel`, the dev-combat button) is intentionally exempt. Enemy names and `AttackType`/`EnemyAbility` enum `ToString()`s remain untranslated **data** — a data-model change, also deferred.

**Deferred (real, but not caused by this change or not actionable now):**

- [x] [Review][Defer] `WoundsToHand` is a hand-maintained mirror of `DrawWoundsToHand`, incremented at one call site — story 3-5 adds more wound-drawing paths (knockout discard-all, Paralyze-vs-hero) and nothing in the type system, tests, or code forces whoever writes it to also bump the counter [scripts/combat/CombatResolver.cs:146] — deferred to 3-5
- [x] [Review][Defer] A panel freed mid-`await` never completes its `TaskCompletionSource`, hanging the resolver forever and permanently wedging `_combatInProgress` — pre-existing shape shared by `BlockTargetingPanel` and `RangedTargetingPanel`; this story adds a third instance [scripts/ui/components/DamageAssignmentPanel.cs:66-74] — deferred, pre-existing; story 3-6 territory
- [x] [Review][Defer] Zero test coverage for panel components — `scripts/ui/components/**` is deliberately outside the pure-C# test project's globs, so three of this review's findings (stale buttons, unstable labels, fabricated `HeroAbsorbs`) are exactly the class no existing test could catch. Needs a scene-tree/`GutTest` integration harness [scripts/ui/components/] — deferred, pre-existing
- [x] [Review][Defer] `Hero.cs` lives in `scripts/core/` but `project-context.md`'s File Placement table says `scripts/hero/` (which does not exist) — pre-existing from 3-4, but 3-4b's `WoundsDrawn` work now leans on `Hero` from the resolver, so the placement is load-bearing [scripts/core/Hero.cs] — deferred, pre-existing
- [x] [Review][Defer] `eligible.Contains(target)` and the `alreadyAssigned` HashSet are load-bearing on `UnitInstance` **reference identity**, and nothing states that invariant — converting `UnitInstance` to a `record` (plausible; every type in `CombatContributions.cs` is one) would silently break both [scripts/units/UnitInstance.cs:12] — deferred, document the invariant when Epic 5 gives units identity

**Dismissed as noise (3):** `WoundsToHand` not reset in `TearDownCombatState` (a fresh `CombatState` is constructed per combat and `BuildResult` runs before teardown; consistent with `FameEarned`/`ReputationEarned`); missing `tr()`/i18n on panel strings (no panel in the project uses `tr()` — pre-existing project-wide pattern, not a 3-4b regression); the "vacuous" first assertion in `AssignDamage_SeamReceivesRunningDamage_NotRawValue` (it documents the first call's value; harmless).

**Gate:** AC5's on-device manual run remains outstanding and is correctly disclosed as such. Do not move this story to `done` on the strength of "321 green / build 0-0" — the live-wire pipeline (panel appears per spill, remaining-damage readout correct, choices route, summary shows, second run reseeds) is precisely what tests cannot cover.

---

## Dev Notes

### Architecture rules (non-negotiable)
- **Pure C# game logic; scene tree render-only.** `DamageChoice`, the `UIBroker` additions, `ApplyOneDamageAssignment`, `CombatState.WoundsToHand`, and `CombatResult.WoundsDrawn` contain **zero Godot types**. `DamageAssignmentPanel` is `partial class : Control` and must stay a thin wiring node. **`Log` pulls in Godot — do NOT add any logging inside `CombatResolver`** (it has no `using Godot` and must keep none). Readout logging goes in `OnDevCombatPressed`. [Source: docs/project-context.md#the-most-important-rule, scripts/core/Log.cs]
- **No per-card / per-enemy logic in the resolver.** The Option-A-vs-B choice flows through the broker seam; the resolver reads only `EnemyAbility` flags off `DamageAssignment.Source` and `UnitInstance`/`Hero` primitives. It never sees card IDs. [Source: docs/project-context.md#4, docs/combat-flow-lld.md#3.6]
- **Async/await; never `async void` for game logic.** `ApplyOneDamageAssignment` stays `async Task`. Panel button handlers are sync Godot signal handlers; the only `async void` remains `OnDevCombatPressed` (already try/catch-wrapped). [Source: docs/project-context.md#1, #2]
- **TDD required.** Red test → confirm fail → minimum implementation → green, for the seam reshape (ineligible-throw + running-d), the readout data, and the migrated cases. [Source: CLAUDE.md#Testing]

### Why the typed `DamageChoice` result (John's decision, 2026-07-11)
3-4's code review deferred two seam gaps to this story (deferred-work.md): (1) `target == null || !eligible.Contains(target)` collapsed a deliberate hero-absorb and a stale/foreign/ineligible unit into the same silent hero-branch — a `DamageTargetProvider` bug would surface as unexplained hero wounds with no error; (2) the seam passed the full `a.RawValue` every loop iteration, so a panel could not show "N still to assign" without recomputing the resolver's running `d` (which `feedback_ux_no_friction` warns against — never duplicate rule-based math in the UI). The typed `DamageChoice` makes hero-absorb an explicit case and turns an ineligible unit into a **caught bug** (throw), and the `remainingDamage` param hands the panel the exact running total it needs. John chose this over the minimal "add int only" (which leaves the null-ambiguity open) and over panel-drives-allocation (which would pull the spill math into the UI, against "resolver owns the math"). [Source: AskUserQuestion 2026-07-11; deferred-work.md 3-4 broker-seam defers; feedback_ux_no_friction memory]

### Why an ineligible `AssignToUnit` throws rather than re-prompts
`DamageAssignmentPanel` only ever offers `eligible` units, so a non-eligible `AssignToUnit` cannot arise from the real panel — it would only come from a provider bug or a malformed test. Throwing (`InvalidOperationException`) fails fast and loud, which is correct for a programming error; re-prompting would mask the bug. `OnDevCombatPressed` already wraps `ResolveCombat` in try/catch and logs, so a thrown seam error is visible, not a silent crash. [Source: deferred-work.md finding #1; 3-4 Review Findings]

### Why `WoundsDrawn` carries no knockout meaning
The readout count is deliberately just an `int` for display. Knockout — the `newWoundsInHand ≥ hand size` threshold, discard-all-non-Wounds, Paralyze-vs-hero hand-emptying, and any `HeroKnockedOut` flag on `CombatResult` — is **story 3-5** and must not be introduced here. Adding `WoundsDrawn` now gives 3-5 a natural place to also read from, but this story ships it as a plain readout only. [Source: combat-flow-lld §10.1 Option B tail; project_next_session story split]

### `DamageAssignmentPanel` mirrors `BlockTargetingPanel` layout, NOT its declaration model
Reuse the bottom-sheet layout constants (`MouseFilter=Ignore` root, `ZIndex=5`, `OffsetTop=-480`/`OffsetBottom=-350`, not visible on `_Ready`) and the `TaskCompletionSource` re-entrancy guard. But this panel is **one-choice-per-call** (the resolver's per-spill loop drives it), so there is NO `_declarations`/`_declaredContribs` accumulation, NO `AvailableContribs` amount-subtraction, NO `ResourcesChanged` subscription (hence no unsubscribe-before-TCS), and NO "Pass" button — every invocation must return exactly one `DamageChoice`. Do not copy the block panel's SpinBox/declare machinery. [Source: scripts/ui/components/BlockTargetingPanel.cs]

### Dev unit seeding is throwaway and dev-only
The two seeded units live only inside the `#if`-guarded `OnDevCombatPressed`, exactly like the dev Brown token — no recruitment, no data file, no identity/abilities/ready-state (all Epic 5). One is plain (armor 3); one resists Fire (armor 3) so a Fire attack could be routed to it to exercise the double-armor path, though the dev Brown token is Physical (so against it the "resistant" unit behaves as a plain armor-3 unit — the resistance path is fully covered by unit tests, and the second unit still exercises the multi-unit spill/second-prompt path live). Clear `Hero.Units` in the `finally` so runs are independent and no unit state leaks into non-combat play. [Source: project_story34_unit_model memory; 3-4 scope — dev hero has no recruited units]

### Previous story intelligence (3-4 / 3-3b)
- 3-4 shipped `ResolveAssignDamagePhase` + `ApplyOneDamageAssignment` (resolver-core), the minimal `Hero`/`UnitInstance` models, `GameState.Hero` (excluded from the undo snapshot — do NOT change that), and the `DamageTargetProvider`/`PromptHeroDamageTarget` seam with a `UnitInstance?` return. This story reshapes that seam and live-wires it. [Source: scripts/combat/CombatResolver.cs:91-148, scripts/broker/UIBroker.cs:15-35]
- The `TestBroker` (inner class of `CombatResolverTest`) already scripts damage choices via a `Queue<UnitInstance?>` fed through `MakeResolverWithDamageChoices(state, params UnitInstance?[])` — migrate both to `DamageChoice`. [Source: tests/unit/CombatResolverTest.cs:805-808]
- `DamageAssignment` is `record DamageAssignment(EnemyTokenInstance Source, AttackType DamageType, int RawValue)`; Poison/Brutal/Paralyze are derived from `Source.HasAbility(...)`. [Source: scripts/combat/CombatContributions.cs]
- The dev flow fights the first Brown token (Minotaur: Armor 5, Physical 5, Brutal, single attack). Brutal doubles 5→10 before assignment. To reach the assign-damage phase, the Physical 5 attack must get through Block — Pass the block panel without fully blocking. [Source: scripts/ui/screens/PlaceholderMainMenu.cs:175, data/enemies.yaml Brown]
- `GamePhase.CombatAssignDamage` already exists; `ResolveCombat` already calls `ResolveAssignDamagePhase` under `!AllEnemiesDefeated && !SkipBlockAndDamagePending`. No `ResolveCombat` structural change. [Source: scripts/combat/CombatResolver.cs:47-56]
- `PlaceholderMainMenu._Ready()` registers `InterstitialProvider`/`RangedAttackProvider`/`BlockProvider` in the same shape this story adds `DamageTargetProvider`. `OnDevCombatPressed` already releases the InputLock before `ResolveCombat` and guards re-entry with `_combatInProgress`. [Source: scripts/ui/screens/PlaceholderMainMenu.cs:137-201]

### Project Structure Notes
- New file: `scripts/ui/components/DamageAssignmentPanel.cs` (namespace `MagusWarrior.UI`, matching the other panels). `DamageChoice` goes in the existing `scripts/combat/CombatContributions.cs` (namespace `MagusWarrior.Combat`). No new folders, no `.tscn`, no `@export`.

### What NOT to do in this story
- Do **not** add knockout/knockdown state, `IsKnockedOut`, hand-size thresholds, Paralyze-vs-hero hand-emptying, or a `HeroKnockedOut` field (all 3-5).
- Do **not** unify `DeckManager` with `Hero.Hand` or make combat Wounds appear in the on-screen `DeckManager` hand (later refactor; the readout reads the count, not the hand view).
- Do **not** recruit real units, add `data/units.yaml`, or give `UnitInstance` identity/abilities (Epic 5). The dev units are throwaway.
- Do **not** add `Hero` to `GameStateSnapshot`/`TakeSnapshot`/`RestoreSnapshot` (3-4's deliberate exclusion stands).
- Do **not** add any logging inside `CombatResolver` (would pull Godot's `Log` into pure code).
- Do **not** make the panel modal/full-rect-opaque, use `@export`, or use `.tscn` — construct UI in code, `MouseFilter=Ignore` root.
- Do **not** implement Melee (Phase 4), enemy piles, Into-the-Heat, or Diplomacy/Influence.

### References
- [Source: docs/combat-flow-lld.md#10-phase-3-assign-damage-phase] — §10.1 algorithm, §10.2 key-rules table
- [Source: _bmad-output/implementation-artifacts/3-4-assign-damage-to-unit.md] — resolver-core this story live-wires; the two deferred broker-seam findings
- [Source: _bmad-output/implementation-artifacts/3-3b-live-wire-block-phase.md] — live-wire precedent; panel mirror + wiring pattern
- [Source: _bmad-output/implementation-artifacts/deferred-work.md] — the two 3-4 broker-seam defers + "assign-damage has no live UI" entry this story resolves
- [Source: scripts/ui/components/BlockTargetingPanel.cs] — bottom-sheet layout/lifecycle to mirror (NOT the declaration model)
- [Source: scripts/combat/CombatResolver.cs] — `ApplyOneDamageAssignment` (to reshape), `BuildResult`, `ResolveCombat`
- [Source: scripts/broker/UIBroker.cs] — the seam to reshape
- [Source: scripts/combat/CombatContributions.cs] — `DamageAssignment`; where `DamageChoice` lands
- [Source: scripts/combat/CombatGroup.cs] — `CombatResult` (add `WoundsDrawn`)
- [Source: scripts/combat/CombatState.cs] — add `WoundsToHand`
- [Source: scripts/ui/screens/PlaceholderMainMenu.cs] — panel wiring + dev combat handler
- [Source: tests/unit/CombatResolverTest.cs:800-940] — assign-damage cases + `TestBroker` seam to migrate

---

## Dev Agent Record

### Agent Model Used
claude-sonnet

### Debug Log References
- **Task 1 red confirmation**: added `AssignDamage_IneligibleUnitChoice_Throws` and `AssignDamage_SeamReceivesRunningDamage_NotRawValue` against the new `DamageChoice` API before it existed. `dotnet test` failed to compile with `CS0246: The type or namespace name 'DamageChoice' could not be found` (x3) and `CS1061: 'TestBroker' does not contain a definition for 'RemainingDamageSeen'` (x3) — confirmed red. Implemented `DamageChoice` + seam reshape + `TestBroker`/migration; `dotnet test` → 319/319 green.
- **Task 2 red confirmation**: added `AssignDamage_ResultReportsWoundsDrawn` and `AssignDamage_UnitAbsorbs_WoundsDrawnZero` referencing `result.WoundsDrawn` before the field existed. `dotnet test` failed with `CS1061: 'CombatResult' does not contain a definition for 'WoundsDrawn'` (x2) — confirmed red. Implemented `CombatState.WoundsToHand` + `CombatResult.WoundsDrawn` + `BuildResult` wiring; `dotnet test` → 321/321 green.
- Final verification: `dotnet test tests/maguswarrior.Tests.csproj` → 321 passed, 0 failed. `dotnet build maguswarrior.csproj` → 0 warnings, 0 errors. `grep` for `using Godot`/`GD.`/`Log.` in `CombatResolver.cs`, `UIBroker.cs`, `CombatContributions.cs`, `CombatState.cs`, `CombatGroup.cs` → no matches (pure C# constraint holds).

### Completion Notes List
- AC1: `DamageChoice` (abstract record with `HeroAbsorbs`/`AssignToUnit` cases) added to `CombatContributions.cs`. `UIBroker.DamageTargetProvider`/`PromptHeroDamageTarget` reshaped to the 4-arg, `DamageChoice`-returning signature (default `HeroAbsorbs`). `ApplyOneDamageAssignment` now switches on the typed choice, throws `InvalidOperationException` on an `AssignToUnit` whose unit isn't in `eligible`, and passes the running `d` (not `RawValue`) to the seam. `TestBroker` and every pre-existing 3-4 assign-damage test migrated from `UnitInstance?` to `DamageChoice`; all outcomes unchanged. Two new red-first tests added and confirmed green (see Debug Log).
- AC2: `CombatState.WoundsToHand` (incremented in the Option-B hero branch) and `CombatResult.WoundsDrawn` (populated in `BuildResult` from `combat.WoundsToHand`, which runs before `TearDownCombatState`) added. Two new tests added and confirmed green.
- AC3: `DamageAssignmentPanel` (new file, `scripts/ui/components/DamageAssignmentPanel.cs`) implemented as a Godot `Control` mirroring `BlockTargetingPanel`'s bottom-sheet layout/lifecycle (`MouseFilter=Ignore` root, `ZIndex=5`, 350–480px docked band, not visible on `_Ready`). One-choice-per-call `ShowAndAwait`, re-entrancy `TrySetResult` guard, no `ResourcesChanged` subscription, no "Pass". This file is Godot-only and is intentionally NOT included in the pure-C# test project's `.csproj` globs (verified: `scripts/ui/components/**` is not globbed there, only `scripts/ui/InputLock.cs` is).
- AC4: `PlaceholderMainMenu` wired with the `_damageAssignmentPanel` field, panel construction/registration in `_Ready()`, two throwaway dev units (`armor:3` plain, `armor:3` Fire-resistant) seeded onto `_state.Hero.Units` before `ResolveCombat` in `OnDevCombatPressed` and cleared in the existing `finally` block, and the `Log.Debug` result line replaced with a `WoundsDrawn`-inclusive summary string plus an `AcceptDialog` popup (kept the `Log.Debug` call too, per the story's code sketch — logging lives in `OnDevCombatPressed`, not the resolver).
- AC5 (automated): `dotnet test tests/maguswarrior.Tests.csproj` → **321 passed**, 0 failed (317 pre-existing + 4 new: 2 AC1 + 2 AC2). `dotnet build maguswarrior.csproj` → **0 warnings, 0 errors**. `CombatResolver`/`UIBroker`/`CombatContributions`/`CombatState`/`CombatGroup` confirmed to have zero `using Godot` and no `Log`/`GD.Print` calls.
- AC5 (manual/on-device): **NOT performed by this dev agent** — the WSL desktop on-device verification (launch game → Dev Combat → let the Physical 5/Brutal-doubled-10 attack through Block → verify the `DamageAssignmentPanel` bottom-sheet appears with the correct remaining-damage readout, unit-absorb path, resistance-vs-Physical path, and multi-run re-entrancy → verify the post-combat `AcceptDialog` summary) is left for the human, per instructions. The manual-verification checkbox under Task 5 is left unticked.
- Deviations from the story's code sketches: none of substance. The `DamageChoice`/`UIBroker`/`ApplyOneDamageAssignment`/`DamageAssignmentPanel`/`PlaceholderMainMenu` code matches the story's sketches essentially verbatim. The two new AC1 tests required constructing scenarios not spelled out literally in the AC prose (see inline test comments): `AssignDamage_SeamReceivesRunningDamage_NotRawValue` needed a SECOND eligible unit purely to force the resolver to consult the broker a second time (with only one eligible unit, the resolver short-circuits to `HeroAbsorbs` locally once that unit is used/wounded, bypassing the broker entirely) — the AC's "unit armor 3, Physical 8" prose is a simplification of this two-unit mechanic. `AssignDamage_IneligibleUnitChoice_Throws` similarly needs one genuinely-eligible unit present (so the broker is actually consulted) plus a second, never-added "foreign" unit scripted as the (ineligible) choice.
- Did not touch: knockout/knockdown, `HeroKnockedOut`, DeckManager↔Hero.Hand unification, real unit recruitment/`data/units.yaml`, Melee/enemy piles/Into-the-Heat/Diplomacy — all correctly out of scope per the story.

### File List
- `scripts/combat/CombatContributions.cs` — added `DamageChoice` record (+ `using MagusWarrior.Units;`)
- `scripts/broker/UIBroker.cs` — reshaped `DamageTargetProvider`/`PromptHeroDamageTarget` to 4-arg/`DamageChoice`
- `scripts/combat/CombatResolver.cs` — `ApplyOneDamageAssignment` switches on `DamageChoice`, throws on ineligible unit, passes running `d`, increments `combat.WoundsToHand`; `BuildResult` populates `WoundsDrawn`
- `scripts/combat/CombatState.cs` — added `WoundsToHand` counter
- `scripts/combat/CombatGroup.cs` — added `WoundsDrawn` to `CombatResult`
- `scripts/ui/components/DamageAssignmentPanel.cs` — new file (Godot `Control`, bottom-sheet panel)
- `scripts/ui/screens/PlaceholderMainMenu.cs` — panel field/wiring, dev unit seed/clear, wounds-inclusive summary + `AcceptDialog`
- `tests/unit/CombatResolverTest.cs` — migrated `TestBroker`/`MakeResolverWithDamageChoices`/all existing assign-damage tests to `DamageChoice`; added `AssignDamage_IneligibleUnitChoice_Throws`, `AssignDamage_SeamReceivesRunningDamage_NotRawValue`, `AssignDamage_ResultReportsWoundsDrawn`, `AssignDamage_UnitAbsorbs_WoundsDrawnZero`
- `_bmad-output/implementation-artifacts/deferred-work.md` — resolved the two 3-4 broker-seam defers + the "assign-damage has no live UI" entry; noted DeckManager↔Hero.Hand unification remains open
- `_bmad-output/implementation-artifacts/3-4b-live-wire-assign-damage-phase.md` — this file (task checkboxes, Dev Agent Record)

### Change Log
- 2026-07-11: Implemented story 3-4b end-to-end (Tasks 1–5, TDD red-first for the seam reshape and the `WoundsDrawn` readout data). Full suite green (321 tests); Godot build clean (0/0). On-device manual verification (AC5) intentionally left for the human.
