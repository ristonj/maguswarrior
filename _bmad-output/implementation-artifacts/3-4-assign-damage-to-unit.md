# Story 3.4: Assign Damage to a Unit (Phase 3 Resolver-Core + Minimal Hero/Unit Models)

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a player,
I want unblocked enemy attacks resolved in an Assign Damage phase where I can absorb the damage on a unit instead of my hero,
so that damage that got through the Block phase lands somewhere — on a unit (wounding it) or on my hero (drawing Wounds into my hand) — instead of vanishing at combat end.

## Acceptance Criteria

**Scope boundary.** This is a **resolver-core** story, mirroring 3-2 and 3-3 (pure C#, TDD-by-direct-construction, no Godot panel, no live UI readout). It builds the two data models the Assign Damage phase needs and wires the phase into `ResolveCombat`. A later live-wire story (the "3-4b" analogue) adds the unit-choice panel and the wound readout; this story does **not**.

**John's two scope decisions (2026-07-05), baked into the ACs below:**
1. **Full phase, not unit-only.** Implement `ResolveAssignDamagePhase` end-to-end: both **Option A** (assign to a unit — full absorption math) **and Option B** (assign remaining to the hero — draw `⌈d / heroArmor⌉` Wounds to hand, plus Poison's extra Wounds to discard). This makes the phase functionally complete so the `DamageAssignment`s that Block already emits stop vanishing at teardown.
2. **Minimal `Hero` owning a real card hand.** The hero wound-sink is a new minimal pure-C# `Hero` on `GameState.Hero` that owns `Armor`, a `Units` collection, and a **real** `Hand`/`DiscardPile` of `CardDefinition` (Wounds are `WoundCard.Create()`), not an integer counter. This starts the recorded Hero-owns-hand model so 3-5 inherits real hand semantics.

**It does NOT (all deferred, called out so the dev does not over-build):**
- **Knockout / knockdown state.** No `IsKnockedOut`, no cross-assignment `newWoundsInHand ≥ handSize` threshold, no "discard all non-Wound cards" on KO, no **Paralyze-vs-hero** hand-emptying. That is **story 3-5** (LLD §10.1 Option B tail + §10.2 Knockout/Paralyze-vs-hero rows). This story draws Wounds and stops. **Poison-vs-hero** (extra Wounds to discard) IS in scope — it is damage math, not knockdown state.
- **Unify `Hero.Hand` with `DeckManager`.** The existing UI-side hand (`DeckManager`, instantiated in `PlaceholderMainMenu`) is left untouched. `Hero.Hand` is the **combat-authoritative** hand for wound-drawing; the two are reconciled in a later refactor (John accepted the temporary two-hands state; log a deferred-work note). Do **not** move `DeckManager` under `Hero` in this story.
- **Any live UI.** No damage-target panel, no wound readout, no `PlaceholderMainMenu` panel. The default (no provider registered) sends **all** unblocked damage to the hero — which is exactly the correct fallback for the not-yet-live-wired dev flow.
- **Melee (Phase 4), enemy piles, Into-the-Heat card, Diplomacy/Influence, mid-combat serialization.** Untouched.

---

**AC1 — Minimal `UnitInstance` damage-sink model (new, pure C#, TDD)**

New file `scripts/units/UnitInstance.cs` (namespace `MagusWarrior.Units`; `scripts/units/` currently holds only `.gitkeep`). A minimal pure-C# damage-sink that mirrors `EnemyTokenInstance`'s wound/destroy conventions. **No Godot types.** The absorption *math* lives in the resolver (AC4), never in the Unit — the Unit only exposes primitives:

```csharp
using System.Collections.Generic;
using System.Linq;
using MagusWarrior.Core.Types;

namespace MagusWarrior.Units;

public class UnitInstance {
    public int  Armor       { get; }
    public int  WoundCount  { get; private set; } = 0;
    public bool IsDestroyed { get; private set; } = false;

    // Resistances as a SET. Cold Fire resistance is DERIVED (must resist both Fire and Ice),
    // exactly as EnemyTokenInstance.HasResistanceTo derives it — do not store a ColdFire entry.
    private readonly HashSet<AttackType> _resistances;

    public UnitInstance(int armor, IEnumerable<AttackType>? resistances = null) {
        Armor        = armor;
        _resistances = new HashSet<AttackType>(resistances ?? Enumerable.Empty<AttackType>());
    }

    public bool HasResistanceTo(AttackType type) => type switch {
        AttackType.ColdFire => _resistances.Contains(AttackType.Fire) && _resistances.Contains(AttackType.Ice),
        _                   => _resistances.Contains(type),
    };

    // A wounded OR destroyed unit cannot absorb damage. (The "not already assigned this combat"
    // condition is enforced by the resolver's per-combat HashSet, NOT by a field on the unit —
    // so there is no per-combat flag to reset here.)
    public bool CanAbsorbDamage => WoundCount == 0 && !IsDestroyed;

    public void TakeWound() { WoundCount++; }                 // wounds ACCUMULATE and never destroy
    public void Heal()      { if (WoundCount > 0) WoundCount--; } // one wound at a time
    public void Destroy()   { IsDestroyed = true; }           // wound-independent (Paralyze only)
}
```

**Critical model rules (from `project_story34_unit_model` memory + combat-flow-lld §10.2), each a test:**
- Wounds are a **count that heals down**, not a bool. `TakeWound()` twice → `WoundCount == 2`; **accumulating wounds never destroys** (`IsDestroyed` stays false).
- `Heal()` removes **one** wound; `Heal()` at `WoundCount == 0` is a no-op (stays 0).
- `Destroy()` is the **only** thing that sets `IsDestroyed` (Paralyze), and it is wound-independent.
- `HasResistanceTo(ColdFire)` is true **iff** the unit resists both Fire and Ice; a unit resisting only Fire is **not** ColdFire-resistant. A unit with no resistances returns false for every type.
- `CanAbsorbDamage` is false when `WoundCount > 0` (wounded, incl. wounds carried from a prior combat — this is the cross-combat inert rule) and false when `IsDestroyed`.

**Tests** — new `tests/unit/UnitInstanceTest.cs` (TDD red-first): construction stores Armor; `TakeWound` accumulates and never destroys; `Heal` decrements one-at-a-time and floors at 0; `Destroy` sets the flag independent of wounds; `HasResistanceTo` for single element / ColdFire-needs-both / none; `CanAbsorbDamage` true/wounded-false/destroyed-false.

---

**AC2 — Minimal `Hero` model owning a real card hand (new, pure C#, TDD)**

New file `scripts/core/Hero.cs` (namespace `MagusWarrior.Core`, next to `GameState`). Minimal pure-C# Hero — **only** what Phase 3's Option B needs. **No Godot types.**

```csharp
using System.Collections.Generic;
using MagusWarrior.Cards;
using MagusWarrior.Units;

namespace MagusWarrior.Core;

// Minimal combat damage-sink introduced in story 3-4. Owns the hero's Armor, deployed Units,
// and a REAL card Hand/DiscardPile (Wounds are WoundCard.Create() cards, not counters) so 3-5
// (knockdown) inherits real hand semantics. This is the first slice of the Hero-owns-hand model
// (see project_hero_model_decision). It deliberately does NOT yet subsume DeckManager — the two
// hands coexist until a later unification refactor. Knockout / Paralyze-vs-hero / hand-size are
// story 3-5 and are intentionally absent here.
public class Hero {
    public int                   Armor       { get; }
    public List<UnitInstance>    Units       { get; } = new();
    public List<CardDefinition>  Hand        { get; } = new();
    public List<CardDefinition>  DiscardPile { get; } = new();

    public Hero(int armor) { Armor = armor; }

    public void DrawWoundsToHand(int n) {
        for (int i = 0; i < n; i++) Hand.Add(WoundCard.Create());
    }

    // Poison: for every Wound drawn to hand, one extra Wound goes straight to the discard pile.
    public void AddWoundsToDiscard(int n) {
        for (int i = 0; i < n; i++) DiscardPile.Add(WoundCard.Create());
    }
}
```

Add `HeroBaseArmor = 2` to `scripts/core/GameConstants.cs` (standard Mage Knight hero armor; `data/heroes.yaml` carries no armor field — verified). `Hero(int armor)` takes it so the value is not hard-coded inside `Hero`.

**Tests** — new `tests/unit/HeroTest.cs`: `DrawWoundsToHand(3)` → `Hand.Count == 3` and every added card has `Type == CardType.Wound`; `AddWoundsToDiscard(2)` → `DiscardPile.Count == 2` (all Wounds); a fresh `Hero(2)` has `Armor == 2`, empty `Units`, empty `Hand`.

---

**AC3 — `GameState.Hero`, constructed at startup, deliberately NOT in the undo snapshot**

Add to `scripts/core/GameState.cs`:
```csharp
public Hero Hero { get; } = new Hero(GameConstants.HeroBaseArmor);
```
(Construct in the field initializer or the constructor — it needs no card catalog.)

**Do NOT add `Hero` to `GameStateSnapshot` / `TakeSnapshot` / `RestoreSnapshot`.** This is deliberate and must be stated in a code comment, because the existing LOCKSTEP comment says "Add Hand … the moment they land in GameState." The rationale that overrides it:

> Combat resolves **entirely after an undo gate** — moving into an enemy hex trips the gate (`project_undo_gate_principle`), and the assault/combat is the post-gate action. There is no undo gate *inside* combat, so combat wound state is never subject to undo; snapshotting it would falsely imply combat is undoable. Mid-combat **persistence** is a different mechanism (full serialization) owned by **story 3-6**, not the undo snapshot. Therefore `Hero` is intentionally excluded from `GameStateSnapshot` in this story.

Add exactly that reasoning as a comment next to the `Hero` property and in the LOCKSTEP block, so a reviewer does not "fix" it. (If 3-6 later needs Hero in a serialized blob, that is a separate serialization path, not `TakeSnapshot`.)

---

**AC4 — `ResolveAssignDamagePhase` + `ApplyOneDamageAssignment` in `CombatResolver` (TDD, the core of the story)**

Add to `scripts/combat/CombatResolver.cs`. **`CombatResolver` has no `using Godot` and must keep none** — no `Log`/`GD.Print` anywhere in this method (the same hard constraint 3-3 held).

Phase entry (mirrors combat-flow-lld §10):
```csharp
public async Task ResolveAssignDamagePhase(CombatState combat) {
    SetPhase(GamePhase.CombatAssignDamage, combat);
    await FirePhaseCallbacks(GamePhase.CombatAssignDamage, combat);

    var alreadyAssigned = new HashSet<UnitInstance>(); // a unit may be assigned damage only ONCE per combat
    foreach (var assignment in combat.DamageAssignments)
        await ApplyOneDamageAssignment(assignment, alreadyAssigned, combat);
}
```

Per-assignment algorithm — a faithful transcription of combat-flow-lld §10.1, with the hero's Option-A-vs-B **choice** expressed through a broker seam (AC5). Everything except that choice is deterministic:
```csharp
private async Task ApplyOneDamageAssignment(
        DamageAssignment a, HashSet<UnitInstance> alreadyAssigned, CombatState combat) {
    var hero = _state.Hero;

    int d = a.RawValue;
    if (a.Source.HasAbility(EnemyAbility.Brutal)) d *= 2;   // Brutal doubles BEFORE any assignment

    while (d > 0) {
        // Eligible units: not wounded, not destroyed, not already used this combat — and only if
        // units are not damage-locked (Into the Heat). Empty list ⇒ no prompt, damage falls to hero.
        var eligible = combat.UnitDamageLocked
            ? new List<UnitInstance>()
            : hero.Units.Where(u => u.CanAbsorbDamage && !alreadyAssigned.Contains(u)).ToList();

        UnitInstance? target = eligible.Count == 0
            ? null                                                   // nothing to choose ⇒ hero
            : await _broker.PromptHeroDamageTarget(a, eligible, combat);

        if (target == null || !eligible.Contains(target)) {
            // Option B — assign remaining damage to the hero.
            int wounds = (int)System.Math.Ceiling((double)d / hero.Armor);
            hero.DrawWoundsToHand(wounds);
            if (a.Source.HasAbility(EnemyAbility.Poison))
                hero.AddWoundsToDiscard(wounds);
            // NOTE: knockout threshold + Paralyze-vs-hero hand-emptying are story 3-5 — not here.
            d = 0;
        } else {
            // Option A — assign to a unit. Marked "used" regardless of outcome.
            alreadyAssigned.Add(target);
            if (target.HasResistanceTo(a.DamageType)) d -= target.Armor; // resistant ⇒ armor twice total
            if (d <= 0) break;                                           // resistance absorbed all ⇒ no wound
            d -= target.Armor;                                           // base armor always subtracted
            if (a.Source.HasAbility(EnemyAbility.Paralyze)) {
                target.Destroy();                                        // destroyed WITHOUT a wound
            } else {
                target.TakeWound();
                if (a.Source.HasAbility(EnemyAbility.Poison))
                    target.TakeWound();                                 // Poison ⇒ 2nd wound
            }
            // loop continues with remaining d (may fall to hero or another eligible unit)
        }
    }
}
```

**Every rule below is a test in `tests/unit/CombatResolverTest.cs` (see AC6):**
- Brutal doubles `RawValue` first.
- Unit resistance subtracts armor a **second** time; if `d ≤ 0` after the resistance subtraction, the unit is **used up but takes no wound**.
- Paralyze on a unit **destroys** it (no wound); Poison on a unit gives a **second** wound.
- A unit is added to `alreadyAssigned` and cannot be re-selected in this combat **even if it took no wound**.
- Wounded (`WoundCount > 0`) / destroyed / already-assigned units are **not eligible**; `UnitDamageLocked` makes **no** unit eligible.
- Hero fallback draws `⌈d / hero.Armor⌉` Wounds to `Hand`; Poison additionally draws that many to `DiscardPile`.

---

**AC5 — Broker seam for the Option-A-vs-B choice (`UIBroker`, pure C#)**

Add to `scripts/broker/UIBroker.cs` (keep it pure C# — no Godot):
```csharp
public Func<DamageAssignment, IReadOnlyList<UnitInstance>, CombatState, Task<UnitInstance?>>?
    DamageTargetProvider { get; set; }

// Returns the unit the hero chose to absorb this assignment, or null to assign to the hero.
// Default (no provider registered): null ⇒ hero takes the damage. This is the correct fallback
// for the not-yet-live-wired dev flow — all unblocked damage lands on the hero.
public virtual Task<UnitInstance?> PromptHeroDamageTarget(
        DamageAssignment assignment, IReadOnlyList<UnitInstance> eligibleUnits, CombatState combat) =>
    DamageTargetProvider?.Invoke(assignment, eligibleUnits, combat)
    ?? Task.FromResult<UnitInstance?>(null);
```
Add `using MagusWarrior.Units;` to `UIBroker.cs`. **No `PlaceholderMainMenu` registration in this story** — leaving `DamageTargetProvider` null is the intended resolver-core state (hero absorbs everything). The live panel that registers it is the deferred follow-up.

---

**AC6 — Wire the phase into `ResolveCombat`; resolver tests green (TDD)**

In `CombatResolver.ResolveCombat`, run Assign Damage after Block, under the same guard that gates Block (combat-flow-lld §5: Assign Damage is skipped if all enemies are already defeated, and `SkipBlockAndDamagePending` — Wings of Wind — skips both Block and Assign Damage):
```csharp
public async Task<CombatResult> ResolveCombat(CombatState combat) {
    await ResolveStartOfCombat(combat);
    if (!combat.AllEnemiesDefeated) await ResolveRangedPhase(combat);
    if (!combat.AllEnemiesDefeated && !combat.SkipBlockAndDamagePending) {
        await ResolveBlockPhase(combat);
        await ResolveAssignDamagePhase(combat);   // NEW — consumes the DamageAssignments Block emits
    }
    var result = BuildResult(combat);
    TearDownCombatState(combat);
    return result;
}
```
`TearDownCombatState` already clears `DamageAssignments` **after** `BuildResult`, so no teardown change is needed — the assignments are now consumed by Phase 3 before teardown instead of silently dropped. `BuildResult` / `CombatResult` are **unchanged** (no `HeroKnockedOut` field — that arrives with knockout in 3-5).

**Test-double seam** — extend the existing `TestBroker` (inner class of `CombatResolverTest`) to script damage-target choices, mirroring how it scripts ranged/block declarations. A `Queue<UnitInstance?>` consumed by an overridden `PromptHeroDamageTarget` is the simplest shape:
```csharp
public override Task<UnitInstance?> PromptHeroDamageTarget(
        DamageAssignment a, IReadOnlyList<UnitInstance> eligible, CombatState combat) =>
    Task.FromResult(_damageChoices.Count > 0 ? _damageChoices.Dequeue() : null);
```
(Add an optional `Queue<UnitInstance?>`/`IEnumerable<UnitInstance?>` ctor arg defaulting to empty, so all existing `TestBroker` call sites compile unchanged and default to "hero absorbs".)

**Assign-damage resolver cases** (drive through `ResolveAssignDamagePhase` directly, or end-to-end via `ResolveCombat` with a surviving enemy + scripted block that lets an attack through — prefer calling `ResolveAssignDamagePhase` directly with pre-seeded `combat.DamageAssignments` for unit clarity, following how block tests seed inputs). At minimum:
- `AssignDamage_NoUnits_HeroDrawsCeilWounds` — Physical 5, hero armor 2, no units ⇒ `Hero.Hand` has 3 Wounds.
- `AssignDamage_Brutal_DoublesBeforeHero` — Brutal, RawValue 3, armor 2 ⇒ 3 Wounds (`⌈6/2⌉`).
- `AssignDamage_UnitAbsorbs_NoHeroWound` — unit armor 3, Physical 3, choice = unit ⇒ unit `WoundCount == 1`, `Hero.Hand` empty.
- `AssignDamage_ResistantUnit_AbsorbsAll_NoWound` — unit armor 3 resists Fire, Fire 3, choice = unit ⇒ unit `WoundCount == 0`, `IsDestroyed == false`, but unit is now used up (see next case).
- `AssignDamage_ResistantUnit_ThenSpillsToHero` — unit armor 3 resists Fire, Fire 8, choice = unit then (auto) hero ⇒ unit `WoundCount == 1` (d: 8→5→2, wound), hero draws `⌈2/2⌉ = 1` Wound.
- `AssignDamage_Paralyze_DestroysUnit_NoWound` — Paralyze, choice = unit ⇒ `IsDestroyed`, `WoundCount == 0`.
- `AssignDamage_Poison_UnitTakesTwoWounds` — Poison, unit armor 3, Physical 3, choice = unit ⇒ `WoundCount == 2`, hero empty.
- `AssignDamage_Poison_HeroExtraWoundsToDiscard` — Poison, no units, Physical 4, armor 2 ⇒ `Hand` 2 Wounds, `DiscardPile` 2 Wounds.
- `AssignDamage_WoundedUnit_Ineligible_HeroAbsorbs` — unit pre-wounded (`TakeWound()`), Physical 4 ⇒ unit not offered/unchanged, hero draws 2.
- `AssignDamage_UnitUsedOncePerCombat` — one unit, **two** assignments both scripting "unit": first uses the unit, second finds it in `alreadyAssigned` ⇒ falls to hero. (Also covers the resistance-absorbed-no-wound "still used up" rule if the first assignment gives no wound.)
- `AssignDamage_UnitDamageLocked_AllToHero` — `UnitDamageLocked = true`, unit present, choice would be unit ⇒ unit untouched, hero absorbs.
- `AssignDamage_SkippedWhenAllEnemiesDefeated` (or `SkipBlockAndDamagePending`) — via `ResolveCombat`: enemy defeated in ranged ⇒ no wounds drawn, `Hero.Hand` empty.

All existing 287 tests stay green (the `TestBroker` ctor addition is source-compatible; `BuildResult`/`CombatResult` unchanged).

---

**AC7 — Full suite green + light manual smoke (resolver-core, no UI readout)**

`dotnet test tests/maguswarrior.Tests.csproj` passes — 287 existing + the new `UnitInstanceTest`, `HeroTest`, and assign-damage `CombatResolverTest` cases. No regressions.

**Manual verification is intentionally light**, exactly as 3-3 (resolver-core) was: there is no panel and no wound readout, and the dev hero has no deployed units, so the observable behavior is "unblocked damage now draws Wounds into `Hero.Hand` instead of vanishing." To smoke it: run dev combat → fail to block the Brown/Minotaur token's attack → the phase runs without exceptions and reaches `CombatAssignDamage`. Optionally add a **temporary** diagnostic in `OnDevCombatPressed` after `ResolveCombat` — e.g. `Log.Debug("[Combat]", $"Hero hand wounds: {_state.Hero.Hand.Count(c => c.Type == CardType.Wound)}")` — to confirm Wounds landed, then **remove it** after verifying (mirrors the temporary `[Block]` log 3-3b used). Correctness of the absorption math is proven by the unit tests, not the manual run. Record the run (or "resolver-core, unit-test-verified, smoke only") in Completion Notes.

---

## Tasks / Subtasks

- [x] **Task 1 — `UnitInstance` model (TDD)** (AC: 1)
  - [x] Write `tests/unit/UnitInstanceTest.cs` red-first (armor, wound-accumulate-never-destroy, heal-one-at-a-time-floor-0, destroy-independent, resistance incl ColdFire-needs-both, `CanAbsorbDamage`)
  - [x] Implement `scripts/units/UnitInstance.cs` (namespace `MagusWarrior.Units`, no Godot); confirm green

- [x] **Task 2 — `Hero` model + `GameConstants.HeroBaseArmor` (TDD)** (AC: 2)
  - [x] Add `HeroBaseArmor = 2` to `scripts/core/GameConstants.cs`
  - [x] Write `tests/unit/HeroTest.cs` red-first (DrawWoundsToHand adds N Wound cards; AddWoundsToDiscard; fresh-hero defaults)
  - [x] Implement `scripts/core/Hero.cs` (namespace `MagusWarrior.Core`, no Godot); confirm green

- [x] **Task 3 — `GameState.Hero` (excluded from undo snapshot)** (AC: 3)
  - [x] Add `public Hero Hero { get; } = new Hero(GameConstants.HeroBaseArmor);`
  - [x] Add the "combat is post-undo-gate ⇒ Hero deliberately not snapshotted; mid-combat persistence is 3-6" comment next to the property AND in the LOCKSTEP block
  - [x] Do NOT touch `GameStateSnapshot` / `TakeSnapshot` / `RestoreSnapshot`

- [x] **Task 4 — Broker seam** (AC: 5)
  - [x] Add `DamageTargetProvider` + `virtual PromptHeroDamageTarget` to `scripts/broker/UIBroker.cs` (+ `using MagusWarrior.Units;`); default returns null (hero)
  - [x] No `PlaceholderMainMenu` registration (intended resolver-core state)

- [x] **Task 5 — `ResolveAssignDamagePhase` + `ApplyOneDamageAssignment` (TDD)** (AC: 4, 6)
  - [x] Extend `TestBroker` with a scriptable damage-choice queue + `PromptHeroDamageTarget` override (source-compatible ctor default)
  - [x] Write the assign-damage resolver cases (AC6 list) red-first; confirm they fail before the phase exists
  - [x] Implement `ResolveAssignDamagePhase` + `ApplyOneDamageAssignment` per AC4 (no Godot/`Log` in `CombatResolver`)
  - [x] Wire the phase into `ResolveCombat` after Block, under the `!AllEnemiesDefeated && !SkipBlockAndDamagePending` guard
  - [x] Confirm all new cases green

- [x] **Task 6 — Full regression + manual smoke** (AC: 7)
  - [x] `dotnet test tests/maguswarrior.Tests.csproj` → green (287 existing + new)
  - [x] Manual verification per AC7 recorded as **resolver-core, unit-test-verified, no live Godot run** (AC7-sanctioned fallback); clean Godot-aware `dotnet build` stands in as compile-time verification. No temporary diagnostic added (no live run performed). See Completion Notes.
  - [x] Add a deferred-work note: DeckManager ↔ Hero.Hand unification; knockout/Paralyze-vs-hero (3-5); live damage-target panel + wound readout

---

## Dev Notes

### Architecture rules (non-negotiable)
- **Pure C# game logic; scene tree render-only.** `UnitInstance`, `Hero`, the `UIBroker` additions, and `ResolveAssignDamagePhase` contain **zero Godot types**. **`Log` pulls in Godot (`GD.Print`) — do NOT add any logging inside `CombatResolver`, `Hero`, or `UnitInstance`** (they have no `using Godot` and must keep none). Any smoke diagnostic goes in `OnDevCombatPressed` (the Godot side) and is temporary. [Source: docs/project-context.md#the-most-important-rule, scripts/core/Log.cs]
- **No per-card / per-enemy logic in the resolver.** The Option-A-vs-B choice flows through the broker seam; the resolver reads only `EnemyAbility` flags off `DamageAssignment.Source` (Brutal/Poison/Paralyze) and `UnitInstance`/`Hero` primitives. It never sees card IDs. [Source: docs/project-context.md#4, docs/combat-flow-lld.md#3.6]
- **Async/await; never `async void` for game logic.** `ResolveAssignDamagePhase`/`ApplyOneDamageAssignment` are `async Task`. The only `async void` remains the existing `OnDevCombatPressed` (already try/catch-wrapped). [Source: docs/project-context.md#1, #2]
- **TDD required.** Red test → confirm fail → minimum implementation → green, for all three new units of behavior (Unit model, Hero model, assign-damage phase). [Source: CLAUDE.md#Testing]

### Why a minimal `Hero` with a real hand (John's decision, 2026-07-05)
The resolver's Option B needs a hero wound-sink, but `GameState` had none (only `Fame`); the real hand lived in `DeckManager`, which is instantiated UI-side in `PlaceholderMainMenu` and never reaches the resolver. John chose to introduce a **minimal `Hero` on `GameState.Hero` owning a real `Hand`/`DiscardPile`** (not an int counter) so 3-5's "hand is all Wounds" knockdown inherits real card semantics, and so this becomes the first honest slice of the recorded Hero-owns-hand model (`project_hero_model_decision`). The **known cost**, which John accepted: `Hero.Hand` and `DeckManager.Hand` coexist as two hands until a later unification refactor — `Hero.Hand` is combat-authoritative for wound-drawing; the on-screen `DeckManager` hand is untouched this story (and shows no combat Wounds yet, which is fine for a resolver-core story with no readout). Log the unification as deferred work. [Source: AskUserQuestion 2026-07-05; project_hero_model_decision memory]

### Why `Hero` is excluded from the undo snapshot
The LOCKSTEP comment in `GameState` says to add new mutable fields to `GameStateSnapshot`. `Hero` is the deliberate exception: combat resolves **after** an undo gate (movement into the enemy hex trips it — `project_undo_gate_principle`), and there is no undo gate inside combat, so combat wound state is never undoable. Snapshotting `Hero` would imply combat is undoable, which is false. Mid-combat **persistence** (a serialization concern, not undo) is **story 3-6**. State the rationale in-code so a reviewer does not "correct" the omission. [Source: scripts/core/GameState.cs LOCKSTEP block; project_undo_gate_principle memory; combat-flow-lld §12/§15]

### The absorption math belongs in the resolver, not the Unit
`UnitInstance` exposes only primitives (`Armor`, `HasResistanceTo`, `TakeWound`, `Heal`, `Destroy`, `CanAbsorbDamage`). The double-armor-on-resistance, Paralyze-destroy, Poison-second-wound, and Brutal-doubling all live in `ApplyOneDamageAssignment`. This is the explicit `project_story34_unit_model` requirement so the same minimal Unit accommodates the full Epic-5 rules unchanged — the varying absorption is an algorithm concern, not a model-shape concern. The damage entry point is **element-aware from day one** (`DamageAssignment.DamageType` flows into `HasResistanceTo`), which the memory flags as the one choice that would force a rework if missed. [Source: project_story34_unit_model memory; combat-flow-lld §10.1]

### "Used up once per combat" is a resolver HashSet, not a Unit field
combat-flow-lld §10.1 tracks `alreadyAssigned` as a `HashSet<UnitInstance>` local to the phase. Keep it there — do **not** add a per-combat `bool` to `UnitInstance` (that would need a per-combat reset and duplicate the resolver's state). A unit is added to the set the moment it is chosen, **regardless of outcome** (even if resistance absorbed all damage and no wound was given), and cannot be re-selected. [Source: combat-flow-lld §10.1 "Unit assignment rule"]

### What is deferred to 3-5 (do NOT build here)
Knockout state (`IsKnockedOut`, cross-assignment `newWoundsInHand ≥ hand size`, discard-all-non-Wounds on KO) and **Paralyze-vs-hero** (hand-emptying) are the Option B *tail* the LLD shows but which belong with knockdown. This story's Option B stops after `DrawWoundsToHand` (+ Poison discard). Do not add `IsKnockedOut` to `Hero` or `CombatResult` here. **Poison-vs-hero (extra Wounds to discard) IS in scope** — it is damage math, present in the loop above. [Source: combat-flow-lld §10.1 Option B, §10.2 Knockout/Paralyze-vs-hero rows; story split in project_next_session memory]

### Previous story intelligence (3-3 / 3-3b)
- Block already emits `DamageAssignment(enemy, attack.Type, attack.Value)` per **attack** (3-3b per-attack bake) into `combat.DamageAssignments`, but nothing consumed them and `TearDownCombatState` cleared them — this story is the consumer. `DamageAssignment` is `record DamageAssignment(EnemyTokenInstance Source, AttackType DamageType, int RawValue)`; Poison/Brutal/Paralyze are derived from `Source` at resolution (exactly what `ApplyOneDamageAssignment` does). [Source: scripts/combat/CombatContributions.cs:10, scripts/combat/CombatResolver.cs:82]
- `EnemyTokenInstance.HasAbility(EnemyAbility)` and the `EnemyAbility` enum (Brutal, Poison, Paralyze, Swift, resistances) already exist and are green. `EnemyAttack` is `record EnemyAttack(int Value, AttackType Type)`. [Source: scripts/combat/EnemyToken.cs, scripts/core/types/EnemyAbility.cs]
- `UIBroker` is pure C# with a Provider-delegate + `virtual Prompt*` pattern; `TestBroker` (inner class of `CombatResolverTest`) overrides the virtuals with canned data. The damage seam follows that exact shape. [Source: scripts/broker/UIBroker.cs, tests/unit/CombatResolverTest.cs:831]
- `GamePhase.CombatAssignDamage` already exists in the enum (no enum change needed). `SetPhase`/`FirePhaseCallbacks` are the same helpers Block/Ranged use. [Source: scripts/core/types/GamePhase.cs, scripts/combat/CombatResolver.cs:30-43]
- `WoundCard.Create()` returns a `CardDefinition { Id="wound", Type=CardType.Wound }` — the real Wound card the hero hand draws. [Source: scripts/cards/WoundCard.cs]
- `combat.AllEnemiesDefeated` and `combat.SkipBlockAndDamagePending` already gate the Block phase; reuse the same guard for Assign Damage (LLD §5 skip-if-defeated). [Source: scripts/combat/CombatResolver.cs:47-49, combat-flow-lld §5]

### Project Structure Notes
- `scripts/units/` exists but is empty (`.gitkeep`) — `UnitInstance.cs` is the first file there; namespace `MagusWarrior.Units` (folder-matching; establishes the convention). `Hero.cs` sits in `scripts/core/` beside `GameState.cs` (namespace `MagusWarrior.Core`), since `GameState` owns it. `CombatResolver` and `Hero` add `using MagusWarrior.Units;`.
- No new folders beyond files in the existing `scripts/units/`. No `.tscn`, no `@export`, no Godot node work anywhere in this story.

### Project Context Rules
- **The most important rule:** pure C# game logic, scene tree render-only. `UnitInstance`, `Hero`, `UIBroker`, and the resolver phase are all Godot-free. [Source: docs/project-context.md]
- **Effects/combat invariant:** `CombatState` holds no per-card flags; contributions flow through pools, behaviors through callbacks/modifiers, choices through broker seams. [Source: docs/combat-flow-lld.md#3.6]
- **Wound cards** are code-defined, identical/fungible, never playable through the normal hand flow — drawing them to hand is a pure list append. [Source: scripts/cards/WoundCard.cs, wound_play_skill_exception memory]

### What NOT to do in this story
- Do **not** add knockout/knockdown state, `IsKnockedOut`, hand-size thresholds, Paralyze-vs-hero hand-emptying, or a `HeroKnockedOut` field on `CombatResult` (all 3-5).
- Do **not** touch `DeckManager` or wire it to `Hero.Hand` (unification is a later refactor; two hands coexist by design).
- Do **not** register `DamageTargetProvider` in `PlaceholderMainMenu` or build any panel/readout (resolver-core; live-wire is the deferred follow-up).
- Do **not** add `Hero` to `GameStateSnapshot`/`TakeSnapshot`/`RestoreSnapshot`.
- Do **not** add any logging inside `CombatResolver`, `Hero`, or `UnitInstance` (would pull Godot's `Log` into pure code); smoke diagnostics go in `OnDevCombatPressed`, temporary.
- Do **not** implement Melee (Phase 4), enemy piles, Into-the-Heat, or Diplomacy/Influence.
- Do **not** put a per-combat "already assigned" flag on `UnitInstance` — that state is the resolver's `HashSet`.

### References
- [Source: docs/combat-flow-lld.md#10-phase-3-assign-damage-phase] — §10.1 algorithm (Brutal/Option A/Option B), §10.2 key-rules table, §10.3 flowchart
- [Source: docs/combat-flow-lld.md#3.3] — `DamageAssignment` record shape; Poison/Brutal/Paralyze derived from `Source`
- [Source: docs/combat-flow-lld.md#5-phase-sequence] — Assign Damage skipped if all enemies defeated
- [Source: _bmad-output/implementation-artifacts/3-3b-live-wire-block-phase.md] — producer of the `DamageAssignment`s this story consumes; resolver-core/live-wire split precedent
- [Source: _bmad-output/implementation-artifacts/3-3-block-incoming-damage.md] — resolver-core + TestBroker seam precedent
- [Source: scripts/combat/CombatResolver.cs] — `ResolveCombat`, `SetPhase`, `FirePhaseCallbacks`, `ResolveBlockPhase` (the method to follow structurally), `TearDownCombatState`
- [Source: scripts/combat/CombatContributions.cs] — `DamageAssignment`
- [Source: scripts/combat/EnemyToken.cs] — `EnemyTokenInstance.HasAbility`/`HasResistanceTo`/`TakeWound`/`Destroy` conventions to mirror in `UnitInstance`
- [Source: scripts/broker/UIBroker.cs] — Provider-delegate + `virtual Prompt*` pattern for the damage seam
- [Source: scripts/core/GameState.cs] — where `Hero` is added + the LOCKSTEP snapshot block
- [Source: scripts/cards/WoundCard.cs] — `WoundCard.Create()`
- [Source: data/heroes.yaml] — no armor field ⇒ `GameConstants.HeroBaseArmor = 2`

---

## Dev Agent Record

### Agent Model Used

claude-sonnet-5

### Debug Log References

- `dotnet test tests/maguswarrior.Tests.csproj` — full suite: 315/315 green (287 baseline + 12 `UnitInstanceTest` + 4 `HeroTest` + 12 assign-damage `CombatResolverTest` cases).
- `dotnet build maguswarrior.csproj` — full Godot-aware build (GODOT symbol defined): 0 warnings, 0 errors. Confirms `UnitInstance`, `Hero`, the `UIBroker` additions, and `CombatResolver` compile cleanly under the real Godot SDK, not just the pure-C# test project.
- Red-first confirmations (each ran and inspected before implementing):
  - `UnitInstanceTest` → CS0234 (`MagusWarrior.Units` namespace didn't exist) before `UnitInstance.cs` was written.
  - `HeroTest` → CS0246 (`Hero` type not found) before `Hero.cs` was written.
  - Assign-damage `CombatResolverTest` cases (12) → CS1061 (`ResolveAssignDamagePhase` not defined on `CombatResolver`) before the phase method was implemented.

### Completion Notes List

- **Task 1 — `UnitInstance`:** New pure-C# `scripts/units/UnitInstance.cs`, exact per the story's code sketch. 12 tests in `tests/unit/UnitInstanceTest.cs` covering: armor storage, wound-accumulate-never-destroys, heal-one-at-a-time-floors-at-0, `Destroy()` independent of wound count, `HasResistanceTo` for a single element / ColdFire-needs-both / no-resistances-ever-false, and `CanAbsorbDamage` true/wounded-false/destroyed-false. Red confirmed (compile error, type didn't exist) before implementation.
- **Task 2 — `Hero`:** Added `GameConstants.HeroBaseArmor = 2`. New pure-C# `scripts/core/Hero.cs` owning `Armor`, `Units`, `Hand`, `DiscardPile` (real `CardDefinition` Wound cards via `WoundCard.Create()`, not counters). 4 tests in `tests/unit/HeroTest.cs`. Red confirmed before implementation.
- **Task 3 — `GameState.Hero`:** Added `public Hero Hero { get; } = new Hero(GameConstants.HeroBaseArmor);` with the full "combat is post-undo-gate, never undoable, persistence is 3-6" rationale as an inline comment, plus a matching exception note in the LOCKSTEP block so a reviewer doesn't "fix" the omission. Deliberately did NOT touch `GameStateSnapshot`/`TakeSnapshot`/`RestoreSnapshot`. No dedicated unit test (it's a property + comment); full suite green confirms no regression.
- **Task 4 — Broker seam:** Added `DamageTargetProvider` delegate + `virtual PromptHeroDamageTarget` to `scripts/broker/UIBroker.cs` (+ `using MagusWarrior.Units;`), default fallback returns `null` (hero absorbs). No registration added in `PlaceholderMainMenu` — intentional resolver-core state. Exercised indirectly via the Task 5 resolver tests (no dedicated broker unit test — none was specified for this seam in isolation).
- **Task 5 — `ResolveAssignDamagePhase` + `ApplyOneDamageAssignment`:** Implemented exactly per the story's code sketch in `scripts/combat/CombatResolver.cs`, wired into `ResolveCombat` right after `ResolveBlockPhase` under the existing `!AllEnemiesDefeated && !SkipBlockAndDamagePending` guard. Extended the inner `TestBroker` (in `CombatResolverTest.cs`) with a `Queue<UnitInstance?>` damage-choice seam and an overridden `PromptHeroDamageTarget`, with a new optional ctor param (`damageChoices`, defaults to empty) so every existing call site still compiles unchanged. Wrote and red-confirmed (CS1061, method didn't exist) 12 resolver cases covering every rule in AC6: no-units hero fallback, Brutal doubling, unit absorption, resistance-absorbs-all-no-wound, resistance-then-spills-to-hero, Paralyze destroys without a wound, Poison gives a unit 2 wounds, Poison gives the hero extra discard wounds, a pre-wounded unit is ineligible, a unit used once per combat (including the "used up but no wound" sub-case via a resistance-absorbed-all first assignment), `UnitDamageLocked` routes everything to the hero, and the phase is skipped entirely (via a `CombatAssignDamage` phase-callback probe) when all enemies are already defeated in Ranged.
- **Task 6 — Full regression + manual smoke:** Full suite green at 315/315. **Manual verification recorded as: resolver-core, unit-test-verified, smoke only** — plus a full Godot-aware `dotnet build` came back clean (0/0). Did not add the optional temporary `Log.Debug` in `OnDevCombatPressed` / launch the live Godot app for this pass, since (a) the story explicitly sanctions "resolver-core, unit-test-verified, smoke only" as the recorded outcome for this style of story (mirrors 3-3), (b) the dev hero has no deployed units so the only observable live-run behavior is "Wounds land in `Hero.Hand` instead of vanishing," which is exactly what the 12 resolver tests already prove byte-for-byte, and (c) the clean Godot-aware build independently confirms there is no Godot-type leakage or compile-time surprise in the three pure-C# files when built against the real SDK.
- **Deferred-work note** (per Task 6, not built in this story): (1) `DeckManager` ↔ `Hero.Hand` unification — the two hands coexist by design until a later refactor; (2) knockout / knockdown state and Paralyze-vs-hero hand-emptying — story 3-5; (3) the live damage-target panel + wound readout (the "3-4b" analogue) — not built here, mirrors the 3-2/3-2b and 3-3/3-3b resolver-core → live-wire split.
- **Deviation from story sketch:** none in the production code — `UnitInstance.cs`, `Hero.cs`, the `UIBroker` additions, `ResolveAssignDamagePhase`/`ApplyOneDamageAssignment`, and the `ResolveCombat` wiring are verbatim transcriptions of the story's code sketches. The only additions beyond the sketches were (a) the `tests/maguswarrior.Tests.csproj` `<Compile Include>` entries needed to make the new pure-C# files visible to the test project (`scripts/units/**/*.cs` glob; `scripts/core/Hero.cs` per-file, matching the existing per-file pattern for that directory) and (b) using `Assert.Single` instead of `Assert.Equal(1, ...)` in one spot to satisfy an xUnit analyzer warning (xUnit2013) surfaced during the build.

### File List

- `scripts/units/UnitInstance.cs` (new)
- `scripts/core/Hero.cs` (new)
- `scripts/core/GameConstants.cs` (modified — added `HeroBaseArmor`)
- `scripts/core/GameState.cs` (modified — added `Hero` property + LOCKSTEP exception comment)
- `scripts/broker/UIBroker.cs` (modified — added `DamageTargetProvider` + `PromptHeroDamageTarget`)
- `scripts/combat/CombatResolver.cs` (modified — added `ResolveAssignDamagePhase` + `ApplyOneDamageAssignment`; wired into `ResolveCombat`)
- `tests/unit/UnitInstanceTest.cs` (new)
- `tests/unit/HeroTest.cs` (new)
- `tests/unit/CombatResolverTest.cs` (modified — `TestBroker` damage-choice seam + 12 new assign-damage cases)
- `tests/maguswarrior.Tests.csproj` (modified — added `scripts/units/**/*.cs` glob and `scripts/core/Hero.cs` per-file `<Compile Include>` entries)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (modified — status → `review`, `last_updated` comment updated)
- `_bmad-output/implementation-artifacts/3-4-assign-damage-to-unit.md` (this file — task checkboxes, Dev Agent Record, Status)

### Change Log

- Added pure-C# `UnitInstance` damage-sink model (`MagusWarrior.Units`) with wound/heal/destroy/resistance primitives, TDD.
- Added pure-C# `Hero` model (`MagusWarrior.Core`) owning `Armor`, `Units`, a real `Hand`/`DiscardPile` of `CardDefinition`, and `HeroBaseArmor = 2` constant, TDD.
- Added `GameState.Hero`, deliberately excluded from the undo snapshot with an in-code rationale comment (combat is post-undo-gate; persistence is story 3-6).
- Added the `DamageTargetProvider`/`PromptHeroDamageTarget` broker seam to `UIBroker` (pure C#, default fallback = hero absorbs).
- Implemented `ResolveAssignDamagePhase` + `ApplyOneDamageAssignment` in `CombatResolver` (Brutal doubling, unit absorption with resistance-doubles-armor, Paralyze-destroys-no-wound, Poison-second-wound / Poison-extra-discard, per-combat "used once" `HashSet`, `UnitDamageLocked` gate) and wired it into `ResolveCombat` after Block, under the existing skip guard.
- Extended `TestBroker` with a scriptable `Queue<UnitInstance?>` damage-choice seam; added 12 new resolver tests covering every AC6 rule.
- Full suite: 315/315 green (287 existing + 28 new). Full Godot-aware build: 0/0.
- **Code review (2026-07-05, Opus 3-layer):** applied 4 patches — P1 armor-floor guard (`Math.Max(1, hero.Armor)`) in `ApplyOneDamageAssignment`; P2 resistant-band no-spill test; P3 multi-unit-spill Brutal+Poison test; P4 doc completeness (deferred-work.md knockout/live-panel entries + AC7 checkbox reword). 3 findings deferred (broker-seam disambiguation + remaining-`d` → 3-4b; unit-resistance input validation → Epic 5). Suite now **317/317 green**. See Review Findings.

### Review Findings

Adversarial code review (Opus, 3 cold-context layers — Blind Hunter, Edge Case Hunter, Acceptance Auditor) on 2026-07-05. **Acceptance Auditor: all 7 ACs SATISFIED**, faithful to combat-flow-lld §10.1, no over-building of 3-5/knockout/UI deferrals, no Godot leakage, no undo-snapshot change, 315/315 green. Triage: 0 decision, 4 patch, 3 defer, 4 dismissed.

**Patch (all applied 2026-07-05 — 317/317 green):**
- [x] [Review][Patch] Guard hero-wound division against `Armor == 0` (silent `int.MinValue` → hero takes zero wounds) — floor the divisor with `Math.Max(1, hero.Armor)`, mirroring `EnemyTokenInstance.EffectiveArmor`. Latent (Armor fixed at 2), but new code + nasty silent-wrong-output mode. [scripts/combat/CombatResolver.cs ApplyOneDamageAssignment]
- [x] [Review][Patch] Add test: resistant unit wounded in the `(armor, 2×armor]` band with NO hero spill (e.g. armor 3, Fire-resist, d=5 → wound, hero unharmed) — spec-correct per §10.2 but the whole band is untested. [tests/unit/CombatResolverTest.cs]
- [x] [Review][Patch] Add test: multi-unit spill in one assignment (≥2 units, Brutal+Poison, damage cascades unit→unit→hero) — §10.1-correct but zero regression coverage today. [tests/unit/CombatResolverTest.cs]
- [x] [Review][Patch] Doc completeness: log the knockout/Paralyze-vs-hero (3-5) and live damage-target-panel/readout (3-4b) deferrals in deferred-work.md per Task 6; reword the AC7 manual-smoke checkbox to match the honest "resolver-core, unit-test-verified, no live run (AC7-sanctioned)" outcome. [deferred-work.md, this story]

**Defer (tracked, not actionable now):**
- [x] [Review][Defer] Broker seam cannot distinguish "hero chose to absorb" (`null`) from "UI returned an ineligible/stale/foreign unit" — both collapse to the hero branch silently. Bites at live-wire; design the disambiguation (re-prompt or assert) with the panel. [scripts/combat/CombatResolver.cs ApplyOneDamageAssignment] — deferred to the 3-4b live-wire story
- [x] [Review][Defer] Broker seam never surfaces the *remaining* `d` on a spill (passes the original `a.RawValue` each iteration) and can prompt up to `Units.Count` times per assignment — the future panel can't show "N damage still to assign" without duplicating resolver math. Frozen API-shape gap to revisit when the seam is live-wired. [scripts/combat/CombatResolver.cs ApplyOneDamageAssignment] — deferred to the 3-4b live-wire story
- [x] [Review][Defer] `UnitInstance` resistance input is not normalized/validated — a literal `AttackType.ColdFire` entry resists nothing (invariant lives only in a comment), and `Armor` isn't floored at ≥1. Unreachable until unit definitions/data exist. [scripts/units/UnitInstance.cs] — deferred to Epic-5 unit recruitment/data

**Dismissed (false positive / non-defect):**
- Blind Hunter's headline "resistant unit wounded below 2×armor is a bug" — **false positive**: exactly correct per §10.2 (the second armor subtraction reduces damage, it does not gate the wound). Verified against the LLD the Blind Hunter could not see.
- `a.Source` null-deref — `DamageAssignment.Source` is a non-nullable `EnemyTokenInstance`; a null caller is a nullable-analysis violation, not a reachable path.
- `UnitInstance` `Armor == 0` "wastes" the unit — no armor-0 units exist in MK or data; loop still terminates; folded into the Epic-5 input-validation defer.
- Double-Poison on a split unit→hero assignment — explicitly spec-correct per §10.1 (Poison checked independently in Option A and B); a non-defect the Auditor flagged only to pre-empt confusion.
