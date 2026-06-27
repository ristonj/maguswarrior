# Story 3.2: Use Ranged/Siege Attack in Phase 1

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a player,
I want to use Ranged and Siege attacks in the first combat phase to defeat enemies before they can strike,
so that I can soften the opposition (remove weak enemies from the fight) before the Block and Melee phases.

## Acceptance Criteria

**Scope boundary (read first):** This story is **resolver-core, TDD-driven**. It implements the Phase 1 (Ranged Attack) resolution logic in `CombatResolver` plus the supporting type changes, driven entirely through unit tests with a test-double `UIBroker`. It does **NOT** wire the live game: no `EnemyLoader`, no live `CombatGroup` from the dev trigger, no on-screen targeting UI, no start-of-combat interstitial panel. Those live-wiring concerns are deferred to a follow-up story (**3-2b**). See "What NOT to do in this story."

**AC1 — `ResolveRangedPhase` implemented and wired into `ResolveCombat` (TDD, red-first)**
`CombatResolver.ResolveRangedPhase(CombatState)` exists and is invoked by `ResolveCombat` after `ResolveStartOfCombat`, guarded by the skip rule: `if (!combat.AllEnemiesDefeated) await ResolveRangedPhase(combat);`. Phase is set to `GamePhase.CombatRanged`, phase callbacks fire (`FirePhaseCallbacks(GamePhase.CombatRanged, combat)`), then hero ranged attacks are prompted and resolved. [Source: docs/combat-flow-lld.md#6-combatresolver, #8-phase-1-ranged-attack-phase]

**AC2 — All-or-nothing defeat, single target**
A ranged/siege declaration against one enemy defeats it **iff** total effective attack ≥ that enemy's `EffectiveArmor`. Below the threshold deals **no** damage (no partial damage to enemies, ever). A defeated enemy is removed from `combat.ActiveEnemies` and added to `combat.DefeatedEnemies`. [Source: docs/combat-flow-lld.md#8.1-all-or-nothing-hero-attacks, #8.6-defeating-enemies]

**AC3 — Combined (multi-target) declaration with union-resistance**
A declaration against multiple enemies defeats **all** targeted enemies iff total effective attack ≥ the **sum** of their `EffectiveArmor`. Union-resistance applies per attack type: for each `AttackType` in the declaration, if **any** targeted enemy `HasResistanceTo` that type, the summed value of that type is halved (round down) before totalling. Mixed types are halved independently then summed. [Source: docs/enemy-token-lld.md#4.2-physical-resistance, #6.2-attack-targeting-in-ranged-phase; docs/combat-flow-lld.md#8.4-targeting-and-union-resistance]

**AC4 — Fortification gates delivery type per declaration**
`FortificationLevel(enemy, combat) = (combat.IsAtFortifiedSite ? 1 : 0) + (enemy.HasAbility(EnemyAbility.Fortified) ? 1 : 0)`. For a declaration, the governing level is the **max** over all targeted enemies. A `Ranged` contribution contributes 0 when governing level ≥ 1; a `Siege` contribution contributes 0 when governing level == 2. Blocked contributions silently contribute nothing (no throw) — a correct UI would not submit them, but the resolver is defensive. [Source: docs/combat-flow-lld.md#8.3-fortification-and-attack-delivery; docs/enemy-token-lld.md#6.2-attack-targeting-in-ranged-phase]

**AC5 — Attack modifier chain applied before effective computation**
`ComputeEffectiveAttack(AttackContribution, CombatState)` applies every `ICombatAttackModifier` in `combat.AttackModifiers` in registration order, then returns the resulting value. Effective-attack totals use the post-modifier value. With an empty modifier list the value is unchanged. [Source: docs/combat-flow-lld.md#8.5-applying-attack-modifiers]

**AC6 — `EnemyTokenInstance.EffectiveArmor` added**
`EffectiveArmor => Math.Max(1, Definition.Armor + ArmorModifier)`. Defeat thresholds use `EffectiveArmor`, never `BaseArmor` directly. (Reconciliation: `enemy-token-lld.md §176` is canonical and clamps to a floor of 1; the `combat-flow-lld.md §8.6` snippet that reads `BaseArmor + ArmorModifier` predates the clamp — use the clamped `EffectiveArmor`.) [Source: docs/enemy-token-lld.md#L176]

**AC7 — `UIBroker.PromptHeroRangedAttacks` returns hero declarations**
The stub method changes signature from `Task` to `Task<IReadOnlyList<RangedAttackDeclaration>>`. A new record `RangedAttackDeclaration(IReadOnlyList<AttackContribution> Contributions, IReadOnlyList<EnemyTokenInstance> Targets)` couples attack chunks with their target group. The resolver consumes the returned declarations; an empty list means the hero passed (no enemies defeated this phase). Contributions are also appended to `combat.AttackPool` for record/consistency, but **targeting lives in the declaration** (the flat `AttackPool` carries no target info). [Source: docs/combat-flow-lld.md#8.2-hero-ranged-attack-input; docs/project-context.md#testing]

**AC8 — Re-entry hardening of `ResolveStartOfCombat`**
`ResolveStartOfCombat` calls `combat.ActiveEnemies.Clear()` before `AddRange(combat.Group.Enemies)` so a second invocation does not duplicate enemies (deferred-work item #194). All existing 3-1 `CombatResolverTest` cases remain green. [Source: _bmad-output/implementation-artifacts/deferred-work.md#194]

**AC9 — `HandView.BuildEffect` handles `AttackSiege` (data-loss fix, #94)**
`BuildEffect` maps `EffectType.AttackSiege => new AttackEffect(spec.Attack, EffectType.AttackSiege, AttackElement.Physical)` so a Siege card no longer falls through to `null` and gets silently dropped. `PhaseGate` already blesses `AttackSiege` in `CombatRanged`/`CombatMelee`. (Live combat card-play is still 3-2b; this is a defensive coverage fix only.) [Source: _bmad-output/implementation-artifacts/deferred-work.md#94]

**AC10 — Full suite green**
`dotnet test tests/maguswarrior.Tests.csproj` passes (202 existing + new ranged-phase tests). No regressions.

## Tasks / Subtasks

- [x] **Task 1 — `EnemyTokenInstance.EffectiveArmor`** (AC: 6)
  - [x] Write red test: `EffectiveArmor_IsBaseArmorPlusModifier` and `EffectiveArmor_FloorsAtOne` (e.g. base 2, modifier −5 → 1).
  - [x] Add `public int EffectiveArmor => System.Math.Max(1, Definition.Armor + ArmorModifier);` to `scripts/combat/EnemyToken.cs`.

- [x] **Task 2 — `RangedAttackDeclaration` + broker signature** (AC: 7)
  - [x] Add `public record RangedAttackDeclaration(IReadOnlyList<AttackContribution> Contributions, IReadOnlyList<EnemyTokenInstance> Targets);` to `scripts/combat/CombatContributions.cs`.
  - [x] Change `UIBroker.PromptHeroRangedAttacks` to `public Task<IReadOnlyList<RangedAttackDeclaration>> PromptHeroRangedAttacks(CombatState combat) => Task.FromResult<IReadOnlyList<RangedAttackDeclaration>>(new List<RangedAttackDeclaration>());` (stub still resolves immediately, now with an empty-pass default).
  - [x] In `CombatResolverTest`, add a test double broker (subclass `UIBroker`, override `PromptHeroRangedAttacks` to return scripted declarations).

- [x] **Task 3 — `ComputeEffectiveAttack` modifier chain** (AC: 5)
  - [x] Red test: empty modifiers returns input value; a registered `PhysicalAttackDoublerModifier`-style stub doubles a Physical contribution.
  - [x] Implement `int ComputeEffectiveAttack(AttackContribution contrib, CombatState combat)` applying `combat.AttackModifiers` in order (per LLD §8.5).

- [x] **Task 4 — `FortificationLevel` + delivery gating** (AC: 4)
  - [x] Red tests: unfortified site + plain enemy → ranged & siege allowed; fortified site OR Fortified enemy (level 1) → ranged blocked, siege allowed; site AND Fortified enemy (level 2) → both blocked.
  - [x] Implement `int FortificationLevel(EnemyTokenInstance enemy, CombatState combat)`.
  - [x] Implement a per-declaration delivery filter using the **max** fortification level across targets.

- [x] **Task 5 — `ResolveRangedPhase` resolution core** (AC: 1, 2, 3)
  - [x] Red tests (single target): exact-armor kill; over-armor kill; under-armor no-op (enemy stays in `ActiveEnemies`).
  - [x] Red tests (combined target): total ≥ summed armor defeats all; below defeats none; union-resistance halves the resisted type (e.g. Physical 6 vs two enemies armor 3+3=6 where one resists Physical → effective 3 < 6 → no defeat).
  - [x] Red test: mixed-type union (Physical 4 + Fire 4 vs enemy resisting Physical only → floor(4/2)+4 = 6).
  - [x] Red test: hero passes (empty declarations) → no enemies defeated.
  - [x] Implement `ResolveRangedPhase`: `SetPhase(CombatRanged)` → `FirePhaseCallbacks` → `var decls = await _broker.PromptHeroRangedAttacks(combat)` → for each declaration: append contributions to `AttackPool`, filter by fortification, group surviving contributions by `AttackType`, apply `ComputeEffectiveAttack`, apply union-resistance halving per type, sum, compare to summed `EffectiveArmor`, on success move all targets `ActiveEnemies → DefeatedEnemies`.

- [x] **Task 6 — Wire `ResolveCombat` + harden `ResolveStartOfCombat`** (AC: 1, 8)
  - [x] Add `combat.ActiveEnemies.Clear();` before `AddRange` in `ResolveStartOfCombat`.
  - [x] Update `ResolveCombat` to: `await ResolveStartOfCombat(combat); if (!combat.AllEnemiesDefeated) await ResolveRangedPhase(combat); return BuildResult(combat);`
  - [x] Confirm existing 3-1 `CombatResolverTest` cases still pass unchanged.

- [x] **Task 7 — `AttackSiege` BuildEffect fix** (AC: 9)
  - [x] Add the `EffectType.AttackSiege` arm to `HandView.BuildEffect`. Do not touch the sideways path (rulebook p9: sideways cannot produce Ranged/Siege).

- [x] **Task 8 — Full regression** (AC: 10)
  - [x] `dotnet test tests/maguswarrior.Tests.csproj` → green. Fix fallout.
  - [x] Update `deferred-work.md`: mark #94 resolved; update #194 to note re-entry `Clear()` landed (the `ToList()` snapshot and stub `HeroWon` notes remain open for later phases).

### Review Findings

_Code review 2026-06-26 (Blind Hunter + Edge Case Hunter + Acceptance Auditor). All 10 ACs confirmed satisfied. 2 defensive-hardening patches, 2 deferred, 8 dismissed (spec-mandated behavior or unreachable malformed-input)._

- [x] [Review][Patch] Empty `Targets` list crashes the ranged phase via `Enumerable.Max` [scripts/combat/CombatResolver.cs:~57] — `decl.Targets.Max(...)` throws `InvalidOperationException` on an empty target list, hard-aborting `ResolveRangedPhase`/`ResolveCombat`. Violates the spec's "resolver is defensive, no throw" principle. Guard: skip declarations with no targets. (blind+edge, High) — FIXED: `if (decl.Targets.Count == 0) continue;` after AttackPool append; covered by `ResolveRangedPhase_EmptyTargets_SkipsDeclarationWithoutThrowing`.
- [x] [Review][Patch] Defeat loop adds to `DefeatedEnemies` without confirming the target was active [scripts/combat/CombatResolver.cs:~73-76] — `ActiveEnemies.Remove(target)` returns false silently when the target is already gone (same enemy across two declarations, or a stale/foreign reference), but `DefeatedEnemies.Add(target)` runs unconditionally → duplicate/phantom entries in `DefeatedEnemies` (inflates downstream Fame). Fix: `if (combat.ActiveEnemies.Remove(target)) combat.DefeatedEnemies.Add(target);`. (blind+edge, High/Med) — FIXED: guard applied; covered by `ResolveRangedPhase_SameEnemyDefeatedTwice_NoPhantomDefeatEntry`.
- [x] [Review][Defer] Type/Delivery-changing attack modifiers not reflected in fortification filter, type-grouping, or resistance keys [scripts/combat/CombatResolver.cs:~59-68] — deferred, no such modifier exists today; element-conversion modeling is a future design decision.
- [x] [Review][Defer] `AttackPool` carries raw (pre-modifier) contributions including fortification-blocked ones [scripts/combat/CombatResolver.cs:~52-53] — deferred; appending all raw contributions is spec-mandated by AC7 ("for record/consistency"), nothing consumes `AttackPool` yet. Revisit the raw-vs-effective semantics when a later phase reads the pool.

## Dev Notes

### Architecture rules (non-negotiable)
- **Pure C#, no Godot.** `CombatResolver`, `CombatState`, `EnemyTokenInstance`, `UIBroker`, `RangedAttackDeclaration` all stay Godot-free so the test project compiles them via the `scripts/combat/**` and `scripts/broker/**` globs. [Source: docs/project-context.md#the-most-important-rule]
- **Async/await, never `async void`.** `PromptHeroRangedAttacks` returns `Task<...>`; `ResolveRangedPhase` returns `Task`. The phase awaits the hero declaration — no flags, no polling. [Source: docs/project-context.md#1-every-player-decision-uses-asyncawait, #2-never-async-void]
- **No per-card logic in the resolver.** Card-specific value transforms go through `combat.AttackModifiers` (an `ICombatAttackModifier`); card-specific phase entry goes through `combat.PhaseCallbacks`. Do not add `if (card.Id == ...)` anywhere in `CombatResolver`. [Source: docs/project-context.md#4-never-hardcode-per-card-logic-in-resolvers; docs/combat-flow-lld.md#3.4-icombatattackmodifier]
- **DI via constructor.** `CombatResolver` already receives `GameState`, `UIBroker`, `EffectScheduler`, `EffectHookRegistry`. The scheduler/hooks are not needed for ranged resolution — leave them injected and unused. No service locator. [Source: docs/project-context.md#dependency-injection]
- **Logging:** `Log.Debug("[Combat]", ...)` only at phase entry/exit, not inside the per-declaration loop. [Source: docs/project-context.md#logging]

### The targeting model (key design decision)
`combat.AttackPool` is `List<AttackContribution>` — it carries **no target information**, so it cannot by itself express "this attack hits enemies A+B combined." Ranged resolution needs per-declaration targeting (fortification and union-resistance are computed per target group). Therefore:
- The **declaration** (`RangedAttackDeclaration`) is the unit of resolution: a bundle of `AttackContribution`s aimed at a `Targets` list (one enemy = single target; many = combined target).
- `PromptHeroRangedAttacks` returns the hero's full set of declarations for the phase. In this story a **test-double broker supplies them**; the live targeting UI that builds them is 3-2b.
- Contributions are still appended to `combat.AttackPool` (keeps the LLD's "sources add to AttackPool" invariant for downstream modifiers/inspection), but the resolver reads targeting from the declaration, not the pool.

### Effective-attack algorithm (per declaration)
```
1. surviving = contributions where delivery is NOT blocked by fortification:
   govLevel = max(FortificationLevel(t, combat) for t in Targets)
   keep Ranged contrib  iff govLevel == 0
   keep Siege  contrib  iff govLevel <= 1
   (Melee contribs are illegal in this phase — drop them too)
2. for each AttackType T present in surviving:
     valueT = sum(ComputeEffectiveAttack(c, combat) for c in surviving where c.Type == T)
     if any t in Targets HasResistanceTo(T):  valueT = valueT / 2   // integer floor
3. effectiveTotal = sum of valueT over all T
4. threshold = sum(t.EffectiveArmor for t in Targets)
5. if effectiveTotal >= threshold: defeat ALL Targets (ActiveEnemies -> DefeatedEnemies)
   else: no damage (all-or-nothing)
```
`EnemyTokenInstance.HasResistanceTo(AttackType)` already exists and derives ColdFire resistance correctly (Fire AND Ice). Do not reimplement resistance logic. [Source: scripts/combat/EnemyToken.cs:44; docs/enemy-token-lld.md#4.4-cold-fire-resistance-derived]

### What NOT to do in this story
- Do **not** build `EnemyLoader` or read `data/enemies.yaml` — tests construct `EnemyTokenDefinition`/`EnemyTokenInstance` inline (see `CombatResolverTest.TestEnemy()`). Live enemy loading is 3-2b.
- Do **not** wire the dev "Combat: Start" button to `ResolveCombat`, build a live `CombatGroup`, targeting UI, or the start-of-combat interstitial — all 3-2b.
- Do **not** implement Block (Phase 2), Assign Damage (Phase 3), or Melee (Phase 4) — stories 3-3, 3-4, 3-5.
- Do **not** add Fame/Reputation accrual to the ranged phase. `BuildResult` stays as-is; end-of-combat Fame/Reputation accrual (combat-flow-lld §13) is a later story. Defeated enemies only need to land in `combat.DefeatedEnemies`.
- Do **not** bridge `AttackElement` (card running totals in `GameState._attackPool`) to `AttackType` here — the test double supplies `AttackContribution` directly. The live `AttackElement → AttackType` mapping is 3-2b.

### Stub semantics being hardened / left open (deferred-work #194)
- **Fixed here:** `ResolveStartOfCombat` re-entry (`Clear()` before `AddRange`).
- **Left open (acceptable until later phases):** `FirePhaseCallbacks` snapshots via `ToList()` (a callback enqueuing another for the same phase is skipped); `BuildResult.HeroWon` is only meaningful once all phases run. Note these in `deferred-work.md`; do not fix in 3-2.

### Previous story intelligence (3-1)
- 3-1 built all the types this story extends: `CombatState`, `CombatResolver`, `EnemyTokenInstance`, `AttackContribution`, `ICombatAttackModifier`, `UIBroker` stub, plus the `GamePhase` combat values and `PhaseChanged` HUD wiring. Reuse them — do **not** recreate.
- `CombatResolver.SetPhase` is `public` for testability — keep it public; tests call it directly.
- `AttackType` has a `None` member (for Summon enemies with no attack) that `AttackElement` lacks — they are deliberately separate types. Do not merge them. [Source: _bmad-output/implementation-artifacts/3-1-see-current-combat-phase.md#L63-L65]
- Test patterns to mirror: `CombatResolverTest.cs` already has `EmptyState()`, `MakeResolver()`, `TestEnemy()`, `SingleEnemyGroup()`, and a `TestCallback` inner class. Add a `TestBroker` inner class and `MultiEnemyGroup()`/parameterised enemy helpers alongside them rather than starting a new file.

### Project Structure Notes
- `EnemyTokenInstance.EffectiveArmor` → `scripts/combat/EnemyToken.cs`.
- `RangedAttackDeclaration` → `scripts/combat/CombatContributions.cs` (combat domain record, next to `AttackContribution`). `UIBroker` already references the `MagusWarrior.Combat` namespace, so returning it from `scripts/broker/UIBroker.cs` needs no new dependency direction.
- `ResolveRangedPhase`, `ComputeEffectiveAttack`, `FortificationLevel` → `scripts/combat/CombatResolver.cs`.
- `AttackSiege` arm → `scripts/ui/components/HandView.cs` `BuildEffect`.
- Tests → `tests/unit/CombatResolverTest.cs` (extend existing) + `tests/unit/EnemyTokenTest.cs` if a focused armor test reads cleaner separately.
- No files belong in `scripts/ui/` for this story except the one-line `HandView.BuildEffect` fix.

### Project Context Rules
- Pure C# game logic; scene tree render-only. Combat types never inherit Godot types. [docs/project-context.md#the-most-important-rule]
- Every player decision uses `await` on the broker; never `async void`. [#1, #2]
- Never hardcode per-card logic in `CombatResolver`; use `AttackModifiers`/`PhaseCallbacks`. [#4]
- Return `Result<T>` for expected failures elsewhere, but combat resolution here has no player-invalid-action path (the test double supplies well-formed declarations); blocked-by-fortification contributions are silently zeroed, not errors. [#error-handling]
- `[Combat]` log tag, entry/exit only. [#logging]
- TDD required: red test first, watch it fail, implement minimum to green. `dotnet test tests/maguswarrior.Tests.csproj` may run anytime without asking. [CLAUDE.md#testing]

### References
- [Source: docs/combat-flow-lld.md#8-phase-1-ranged-attack-phase] — full ranged-phase spec (§8.1 all-or-nothing, §8.2 input, §8.3 fortification table, §8.4 union-resistance, §8.5 modifiers, §8.6 defeat).
- [Source: docs/combat-flow-lld.md#6-combatresolver] — `ResolveCombat` skip-logic and method map (§16).
- [Source: docs/enemy-token-lld.md#4-resistances] — halving rule (round down), ColdFire derivation; #6.2 ranged-phase combined targeting; #L176 `EffectiveArmor`.
- [Source: _bmad-output/epics.md#epic-3-combat-system] — story list and "soften enemies before melee" intent.
- [Source: _bmad-output/implementation-artifacts/deferred-work.md] — #94 (AttackSiege drop), #194 (CombatResolver stub semantics).
- [Source: scripts/combat/CombatResolver.cs, CombatState.cs, EnemyToken.cs, CombatContributions.cs; scripts/broker/UIBroker.cs] — current code being extended.

## Dev Agent Record

### Agent Model Used
Claude Sonnet 4.6

### Debug Log References
- `Log.cs` excluded from test project (uses Godot GD.Print) — removed `Log.Debug` calls from `ResolveRangedPhase`; all other combat scripts follow the same pattern, so this is correct behavior.
- Existing 3-1 test `ResolveStartOfCombat_TransitionsToCombatRangedAfterPhase0` updated to `ResolveStartOfCombat_LeavesPhaseAtCombatStart` — the phase-handoff to `CombatRanged` moved from `ResolveStartOfCombat` into `ResolveRangedPhase` as intended by 3-2's scope.

### Completion Notes List
- **AC6**: `EnemyTokenInstance.EffectiveArmor` added with `Math.Max(1, ...)` floor; 3 tests in new `EnemyTokenTest.cs`.
- **AC7**: `RangedAttackDeclaration` record added to `CombatContributions.cs`; `UIBroker.PromptHeroRangedAttacks` upgraded from `Task` → `Task<IReadOnlyList<RangedAttackDeclaration>>` with `virtual` so `TestBroker` in tests can override it. `TestBroker` + `MakeResolverWith` helper added to `CombatResolverTest`.
- **AC5**: `ComputeEffectiveAttack` chains `combat.AttackModifiers` in registration order; 2 tests.
- **AC4**: `FortificationLevel` = site(0/1) + Fortified ability(0/1); per-declaration max governs delivery filter (Ranged blocked ≥ 1, Siege blocked = 2); 4 tests.
- **AC1/2/3**: `ResolveRangedPhase` implements the full algorithm — phase set, callbacks fired, broker awaited, contributions appended to `AttackPool`, delivery filtered, per-type union-resistance halved (floor), summed against `EffectiveArmor` threshold, all-or-nothing defeat; 13 tests covering single-target, combined, union-resistance (single and mixed type), fortification levels 0/1/2, empty-pass, phase-set assertion, and `AttackPool` population.
- **AC8**: `ResolveStartOfCombat` now calls `Clear()` before `AddRange`; re-entry hardening test added.
- **AC9**: `BuildEffect` `AttackSiege` arm added.
- **AC10**: 227 tests green (202 prior + 23 story + 2 review-patch). No regressions.
- **deferred-work.md**: item #94 marked RESOLVED; item #194 updated to note `Clear()` fix landed, remaining open items documented.

### File List
- `scripts/combat/EnemyToken.cs` — added `EffectiveArmor` property
- `scripts/combat/CombatContributions.cs` — added `RangedAttackDeclaration` record, added `using System.Collections.Generic`
- `scripts/combat/CombatResolver.cs` — added `ResolveRangedPhase`, `FortificationLevel`, `ComputeEffectiveAttack`; hardened `ResolveStartOfCombat` (`Clear()`); wired `ResolveCombat`
- `scripts/broker/UIBroker.cs` — `PromptHeroRangedAttacks` upgraded to `virtual Task<IReadOnlyList<RangedAttackDeclaration>>`
- `scripts/ui/components/HandView.cs` — `BuildEffect` `AttackSiege` arm added
- `tests/unit/EnemyTokenTest.cs` — new file, 3 `EffectiveArmor` tests
- `tests/unit/CombatResolverTest.cs` — added `TestBroker`, `MakeResolverWith`, `MultiEnemyGroup`, `FortifiedSiteGroup`, `TestEnemyWithArmor` helpers; 20 new tests
- `_bmad-output/implementation-artifacts/deferred-work.md` — items #94 resolved, #194 updated
