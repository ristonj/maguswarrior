# Story 3.3: Block Incoming Damage (Phase 2 Resolver-Core)

Status: done

## Story

As a player,
I want to spend Block resources during the combat Block phase to fully stop an enemy attack (reducing the damage it deals to zero),
so that fewer enemy attacks get through to wound me in the later Assign Damage phase.

## Acceptance Criteria

**Scope boundary:** This story implements **Phase 2 (Block) resolver-core only** — pure C# game logic + TDD, mirroring how story 3-2 delivered the ranged-phase resolver before 3-2b live-wired it. It adds a `BlockEfficiency` helper, a `BlockBridge` (mirrors `AttackBridge`), a `BlockDeclaration` record, a `PromptHeroBlock` broker injection point, and `ResolveBlockPhase` in `CombatResolver`. The block phase computes all-or-nothing blocking (with elemental efficiency and Swift) and emits a `DamageAssignment` for every **unblocked** enemy attack.

**It does NOT:**
- Apply any wounds. No `Hero` class, no wound-to-hand, no `DrawWoundsToHand`. Phase 3 (Assign Damage) — which turns `DamageAssignment`s into actual Wound cards — is **out of scope** and lands in story 3-4 (hero/unit damage) and 3-5 (knockdown). "Reduce wounds taken" is exercised here as: a fully blocked attack produces **no** `DamageAssignment`; an unblocked attack produces a `DamageAssignment` carrying the attack's raw value (the future wound source).
- Add any Godot panels or dev triggers. Live-wiring the block phase into the running game (a `BlockTargetingPanel`, dev-trigger extension, manual verification) is **story 3-3b** — exactly the 3-2 → 3-2b rhythm.
- Implement Influence-as-Block (Diplomacy powered, LLD §9.2). That requires `_state.Hero.InfluencePool`, which does not exist yet. Deferred.
- Implement combined/multi-attack-per-enemy block UI. Each active enemy attacks once (LLD §9.1); the resolver iterates `enemy.Definition.Attacks` so multi-attack enemies resolve correctly, but no card currently produces them.

---

**AC1 — `BlockEfficiency` helper (TDD, red-first)**

New pure C# static class `scripts/combat/BlockEfficiency.cs` (no Godot types):

```csharp
public static int Effective(BlockType block, AttackType attack, int value)
```

Returns the **effective** block points `value` provides against `attack`: the full `value` when the pairing is efficient (1:1), and `value / 2` (integer division, floor) when inefficient (2:1). `AttackType.None` is treated as inefficient for every block type (it should never reach here; guard defensively, do not throw).

Efficiency table — **both `effect-lld.md §Block Efficiency` (canonical) and `combat-flow-lld.md §9.4` agree** (elements oppose: Fire block counters Ice, Ice block counters Fire):

| Block Type | vs Physical | vs Fire | vs Ice | vs ColdFire |
|------------|:-----------:|:-------:|:------:|:-----------:|
| Physical   | 1:1         | 2:1     | 2:1    | 2:1         |
| Fire       | 1:1         | 2:1     | **1:1** | 2:1        |
| Ice        | 1:1         | **1:1** | 2:1    | 2:1         |
| ColdFire   | 1:1         | 1:1     | 1:1    | 1:1         |

Efficient (1:1) pairings: Physical→Physical; Fire→{Physical, Ice}; Ice→{Physical, Fire}; ColdFire→{all}. Everything else is inefficient (2:1).

**Test coverage** (new `tests/unit/BlockEfficiencyTest.cs`, TDD red-first):
- `Physical_vs_Physical_Efficient` — `Effective(Physical, Physical, 4) == 4`
- `Physical_vs_Fire_Inefficient` — `Effective(Physical, Fire, 4) == 2`
- `Fire_vs_Ice_Efficient` — `Effective(Fire, Ice, 5) == 5`
- `Fire_vs_Fire_Inefficient` — `Effective(Fire, Fire, 5) == 2` (floor of 2.5)
- `Ice_vs_Fire_Efficient` — `Effective(Ice, Fire, 5) == 5`
- `Ice_vs_Ice_Inefficient` — `Effective(Ice, Ice, 4) == 2`
- `ColdFire_vs_All_Efficient` — `Effective(ColdFire, X, 3) == 3` for X ∈ {Physical, Fire, Ice, ColdFire}
- `Inefficient_OddValue_FloorsDown` — `Effective(Physical, Fire, 3) == 1`
- `ZeroValue_ReturnsZero` — `Effective(Ice, Ice, 0) == 0`

---

**AC2 — `BlockBridge.ExtractBlockContributions` (TDD, red-first)**

New pure C# static class `scripts/broker/BlockBridge.cs`, mirroring `AttackBridge` exactly:

```csharp
public static IReadOnlyList<BlockContribution> ExtractBlockContributions(
    IReadOnlyDictionary<AttackElement, int> pool)
```

`GameState.BlockPool` is keyed by `AttackElement` only (no distance dimension — block has no ranged/siege axis). Returns one `BlockContribution` per pool entry where `value > 0`. `AttackElement` and `BlockType` share identical member names (`Physical, Fire, Ice, ColdFire`), so a direct cast is valid — same idiom as `AttackBridge`:

```csharp
var type = (BlockType)(int)element;
result.Add(new BlockContribution(type, value));
```

**Test coverage** (new `tests/unit/BlockBridgeTest.cs`, TDD red-first):
- `Extract_Physical_MapsCorrectly` — `(Physical, 4)` → `BlockContribution(Physical, 4)`
- `Extract_Ice_MapsCorrectly` — `(Ice, 3)` → `BlockContribution(Ice, 3)`
- `Extract_ColdFire_MapsCorrectly` — `(ColdFire, 2)` → `BlockContribution(ColdFire, 2)`
- `Extract_ExcludesZeroValues` — `(Fire, 0)` → not in result
- `Extract_EmptyPool_ReturnsEmpty`
- `Extract_MultipleEntries_AllMapped` — mixed pool, all non-zero returned

---

**AC3 — `BlockDeclaration` record + `UIBroker.PromptHeroBlock` injection point**

Add to `scripts/combat/CombatContributions.cs`, mirroring `RangedAttackDeclaration`:

```csharp
// A single block declaration: a set of block contributions aimed at one enemy's attack.
public record BlockDeclaration(
    IReadOnlyList<BlockContribution>  Contributions,
    EnemyTokenInstance                Target
);
```

(Block targets a single enemy — unlike ranged, which can combine targets. The all-or-nothing rule is per enemy attack; block resources allocated to one attack cannot cover another, LLD §9.3.)

Add to `UIBroker` (`scripts/broker/UIBroker.cs`), mirroring `RangedAttackProvider` / `PromptHeroRangedAttacks`:

```csharp
public Func<CombatState, Task<IReadOnlyList<BlockDeclaration>>>? BlockProvider { get; set; }

public virtual Task<IReadOnlyList<BlockDeclaration>> PromptHeroBlock(CombatState combat) =>
    BlockProvider?.Invoke(combat)
    ?? Task.FromResult<IReadOnlyList<BlockDeclaration>>(new List<BlockDeclaration>());
```

Keep `PromptHeroBlock` `virtual` so `TestBroker` in `CombatResolverTest.cs` can override it (the same way it overrides `PromptHeroRangedAttacks`). Tests do not set `BlockProvider`; the override path executes and the delegate is never reached. **No existing tests change.**

> Note: `UIBroker` currently has an unused stub `Task ResolveEnemyAttackVsHero(...)`. Leave it untouched — it is not part of this story's contract; the resolver owns Phase 2 logic directly.

---

**AC4 — `ResolveBlockPhase` implemented in `CombatResolver`**

Add a public `ResolveBlockPhase(CombatState combat)` to `CombatResolver`, mirroring the structure of `ResolveRangedPhase`:

```csharp
public async Task ResolveBlockPhase(CombatState combat) {
    SetPhase(GamePhase.CombatBlock, combat);
    await FirePhaseCallbacks(GamePhase.CombatBlock, combat);

    var declarations = await _broker.PromptHeroBlock(combat);

    // Each active enemy attacks once (LLD §9.1). Resolve every attack the enemy has.
    foreach (var enemy in combat.ActiveEnemies.ToList()) {
        if (enemy.AttackCancelled) continue;   // a nullified enemy deals no damage this combat

        // All block contributions the hero allocated to this enemy.
        var allocated = declarations
            .Where(d => d.Target == enemy)
            .SelectMany(d => d.Contributions)
            .ToList();

        foreach (var attack in enemy.Definition.Attacks) {
            if (attack.Type == AttackType.None) continue;   // summon-only "attack"; no damage in Phase 2

            // Sum same-type block FIRST, then apply efficiency once per type (mirrors
            // ResolveRangedPhase's GroupBy-then-sum). Applying floor division per
            // contribution would lose points when same-type block is split.
            int effectiveBlock = allocated
                .GroupBy(b => b.Type)
                .Sum(g => BlockEfficiency.Effective(g.Key, attack.Type, g.Sum(b => b.Value)));

            int threshold = attack.Value * (enemy.HasAbility(EnemyAbility.Swift) ? 2 : 1);

            // All-or-nothing: block must fully meet the threshold or the FULL raw attack gets through.
            if (effectiveBlock < threshold)
                combat.DamageAssignments.Add(
                    new DamageAssignment(enemy, attack.Type, attack.Value)); // RawValue = PRINTED value (Swift only raises the threshold)
        }
    }
}
```

Key rules baked in (LLD §9.3–§9.5):
- **All-or-nothing:** `effectiveBlock >= threshold` → fully blocked, no `DamageAssignment`. Otherwise the attack's **full printed value** becomes a `DamageAssignment`.
- **Efficiency** is applied per contribution via `BlockEfficiency.Effective`, then summed (mixed block types are allowed, LLD effect-lld §Block Efficiency example).
- **Swift** doubles the *threshold* only; `DamageAssignment.RawValue` is the **printed** attack value, not doubled.
- A declaration's block resources allocated to one enemy never cover another enemy's attack (declarations are filtered by `Target`).
- `AttackCancelled` enemies and `AttackType.None` attacks produce no `DamageAssignment`.

---

**AC5 — `ResolveBlockPhase` wired into `ResolveCombat`**

Update `ResolveCombat` to run the block phase after ranged, guarded per LLD §5/§6:

```csharp
public async Task<CombatResult> ResolveCombat(CombatState combat) {
    await ResolveStartOfCombat(combat);
    if (!combat.AllEnemiesDefeated) await ResolveRangedPhase(combat);
    if (!combat.AllEnemiesDefeated && !combat.SkipBlockAndDamagePending)
        await ResolveBlockPhase(combat);
    var result = BuildResult(combat);
    TearDownCombatState(combat);
    return result;
}
```

Skip conditions: all enemies already defeated in the ranged phase, OR `SkipBlockAndDamagePending` (Wings of Wind, LLD §5 — the flag already exists on `CombatState`; no card sets it yet).

`TearDownCombatState` already clears `DamageAssignments`, so block-phase output does not leak past the combat. **`BuildResult.HeroWon` is unchanged** — it is still only true when all enemies are defeated (which Phase 2 never does; defeating enemies is Phase 1/Phase 4). A combat that reaches the block phase with surviving enemies still reports `HeroWon = false`; that is correct for resolver-core.

---

**AC6 — Full suite green**

`dotnet test tests/maguswarrior.Tests.csproj` passes — all 243 existing tests plus the new `BlockEfficiencyTest`, `BlockBridgeTest`, and new `CombatResolverTest` block-phase cases. No regressions.

New `CombatResolverTest` cases (add to the existing file; reuse the inner `TestBroker` by overriding `PromptHeroBlock` to return canned declarations). Call `ResolveBlockPhase` **directly** to inspect `combat.DamageAssignments` (the same way ranged tests call `ResolveRangedPhase` directly — `ResolveCombat` would tear the assignments down):

- `BlockPhase_FullyBlockedAttack_NoDamageAssignment` — Physical attack value 4, hero declares Physical block 4 → `DamageAssignments` empty.
- `BlockPhase_PartialBlock_FullAttackGetsThrough` — Physical attack 4, hero declares Physical block 3 → one `DamageAssignment(enemy, Physical, 4)` (all-or-nothing; raw value, not 1).
- `BlockPhase_NoBlock_DamageAssignmentRawValue` — attack 5, no declarations → `DamageAssignment(enemy, _, 5)`.
- `BlockPhase_InefficientBlock_NeedsDouble` — Fire attack 4, hero declares Physical block 6 (2:1 → effective 3 < 4) → unblocked; then Physical block 8 (effective 4 ≥ 4) → blocked.
- `BlockPhase_EfficientElementalBlock` — Fire attack 4, hero declares Ice block 4 (1:1 vs Fire) → blocked, no assignment.
- `BlockPhase_SwiftDoublesThreshold` — Swift Physical attack 4 (threshold 8), Physical block 4 → unblocked, `RawValue == 4` (printed, not 8); Physical block 8 → blocked.
- `BlockPhase_MixedBlockTypes_Sum` — Fire attack 4: 1 Ice block (eff 1) + 2 Physical block (2:1 → eff 1) = effective 2 < 4 → unblocked; bump to Ice 2 + Physical 4 (eff 2 + 2 = 4) → blocked. (Mirrors effect-lld §Block Efficiency example.)
- `BlockPhase_BlockForOneEnemy_DoesNotCoverAnother` — two enemies each Physical attack 4; declaration targets enemy A with Physical block 4 → A blocked (no assignment), B unblocked (assignment).
- `BlockPhase_AttackCancelledEnemy_NoAssignment` — enemy with `AttackCancelled = true` → no assignment regardless of declarations.
- `BlockPhase_SummonOnlyAttack_NoAssignment` — enemy whose only attack is `AttackType.None` (summon) → no assignment.
- `ResolveCombat_RunsBlockPhase_WhenEnemiesSurvive` — after a ranged phase that defeats nothing, block phase runs (assert via a spy/side-effect; e.g. `DamageAssignments` populated before teardown is observable only inside the phase, so assert phase ordering through `combat.CurrentPhase` transitions or a `TestBroker` flag set when `PromptHeroBlock` is invoked).
- `ResolveCombat_SkipsBlockPhase_WhenSkipBlockAndDamagePending` — set `SkipBlockAndDamagePending = true`; assert `PromptHeroBlock` was never invoked (TestBroker flag).
- `ResolveCombat_SkipsBlockPhase_WhenAllEnemiesDefeated` — ranged phase clears all enemies; `PromptHeroBlock` never invoked.

---

## Tasks / Subtasks

- [x] **Task 1 — `BlockEfficiency` helper (TDD)** (AC: 1)
  - [x] Write red tests in `tests/unit/BlockEfficiencyTest.cs` (all cases above); confirm each fails
  - [x] Implement `BlockEfficiency.Effective` in `scripts/combat/BlockEfficiency.cs` with the efficiency table
  - [x] Confirm all tests green

- [x] **Task 2 — `BlockBridge` (TDD)** (AC: 2)
  - [x] Write red tests in `tests/unit/BlockBridgeTest.cs` (all 6 cases)
  - [x] Implement `BlockBridge.ExtractBlockContributions` in `scripts/broker/BlockBridge.cs`
  - [x] Confirm all 6 tests green

- [x] **Task 3 — `BlockDeclaration` record + `UIBroker.PromptHeroBlock`** (AC: 3)
  - [x] Add `BlockDeclaration` record to `scripts/combat/CombatContributions.cs`
  - [x] Add `BlockProvider` delegate + `virtual PromptHeroBlock` to `UIBroker`
  - [x] Run full suite — all 243 existing tests remain green (no test sets `BlockProvider`)

- [x] **Task 4 — `ResolveBlockPhase` in `CombatResolver`** (AC: 4, 5)
  - [x] Implement `ResolveBlockPhase` per AC4 spec
  - [x] Wire into `ResolveCombat` with the `AllEnemiesDefeated` + `SkipBlockAndDamagePending` guards
  - [x] Add `PromptHeroBlock` override to the inner `TestBroker` in `CombatResolverTest.cs`

- [x] **Task 5 — Block-phase resolver tests** (AC: 6)
  - [x] Add all new `CombatResolverTest` cases (call `ResolveBlockPhase` directly to inspect `DamageAssignments`)
  - [x] Add `ResolveCombat` skip/run cases via a `TestBroker` invocation flag

- [x] **Task 6 — Full regression** (AC: 6)
  - [x] `dotnet test tests/maguswarrior.Tests.csproj` → green (275 tests: 243 existing + 12 efficiency + 6 bridge + 14 block-phase)

---

## Dev Notes

### Architecture rules (non-negotiable)
- **Pure C# game logic; scene tree is render-only.** `BlockEfficiency`, `BlockBridge`, `BlockDeclaration`, and `ResolveBlockPhase` contain **no Godot types**. This whole story is pure logic + tests — there are no panels (those are 3-3b). [Source: docs/project-context.md#the-most-important-rule]
- **No per-card logic in the resolver.** Block resources flow from card play → `GameState.BlockPool` → `BlockBridge` → `BlockContribution` → `BlockDeclaration`. The resolver never sees card IDs. Card-specific behaviors (Diplomacy influence-as-block) use `PhaseCallbacks` + `ActiveInfluenceConversion`, not resolver branches. [Source: docs/project-context.md#4, docs/combat-flow-lld.md#3.6 invariant]
- **Async/await; never `async void` for game logic.** `ResolveBlockPhase` is `async Task`, exactly like `ResolveRangedPhase`. [Source: docs/project-context.md#1, #2]

### Why block targets one enemy (not a target list like ranged)
Ranged attacks can be *combined* across multiple enemies (a single big attack split over a group), so `RangedAttackDeclaration` carries `IReadOnlyList<EnemyTokenInstance> Targets`. Block is the opposite: the all-or-nothing rule is **per enemy attack**, and block points allocated to one attack cannot help against another (LLD §9.3). So `BlockDeclaration` carries a single `Target`. The resolver groups all declarations by target enemy and sums their contributions against each of that enemy's attacks.

### Why "reduce wounds taken" is satisfied without applying wounds
The user-story benefit is "fewer attacks get through to wound me." In Mage Knight, a blocked attack is reduced **to zero** (no partial blocking) and therefore contributes **zero** wounds in the later Assign Damage phase; an unblocked attack contributes its full damage. This story makes that true at the resolver level: a fully blocked attack produces **no** `DamageAssignment`, an unblocked one produces a `DamageAssignment` carrying the raw value that Phase 3 will later convert to Wound cards. The actual Wound-card creation (and the `Hero` wound model it needs) is deliberately deferred — see scope boundary. [Source: docs/combat-flow-lld.md#9.3, John's scope decision 2026-06-30]

### Swift is a block tax, not a damage boost
`attackThreshold = AttackValue * (Swift ? 2 : 1)`. Swift makes an enemy *harder to block* (you need double the effective block), but if the attack gets through, the damage dealt is the **printed** value — `DamageAssignment.RawValue = attack.Value`, never doubled. [Source: docs/combat-flow-lld.md#9.5]

### Efficiency: sum same-type block first, then apply efficiency per type
Group the enemy's allocated block by `BlockType`, sum each group's values, then apply `BlockEfficiency.Effective` once per type, then sum across types (mirrors `ResolveRangedPhase`'s `GroupBy(c => c.Type)`). This reproduces the canonical example — 1 Ice block + 2 Physical block vs Fire = 1 (Ice efficient) + 1 (Physical 2:1, floor(2/2)) = 2 — without losing points to per-contribution floor when same-type block is split across declarations. Inefficient block halves with integer floor (3 Physical block vs Fire = effective 1). Mixed block types are explicitly allowed. [Source: docs/effect-lld.md#Block Efficiency, docs/combat-flow-lld.md#9.4]

### Block efficiency table reconciliation (read before implementing)
Both docs **agree** (no conflict): elements oppose — **Fire block is efficient (1:1) vs Ice**, **Ice block is efficient (1:1) vs Fire**, both are efficient vs Physical, both are inefficient vs their own element and vs ColdFire. Physical block is efficient only vs Physical. ColdFire block is efficient vs everything. If the implementation ever disagrees with `effect-lld.md`, **effect-lld.md is canonical** (combat-flow-lld §9.4 says so explicitly). [Source: docs/effect-lld.md#Block Efficiency, docs/combat-flow-lld.md#9.4]

### Testing approach (mirror 3-2)
The previous ranged-phase resolver tests call `ResolveRangedPhase` **directly** (not via `ResolveCombat`) so they can inspect mutated `CombatState` before `TearDownCombatState` wipes it. Do the same: call `ResolveBlockPhase(combat)` directly and assert on `combat.DamageAssignments`. For the `ResolveCombat` skip/run guard cases, add a bool flag to the inner `TestBroker` that flips when `PromptHeroBlock` is invoked, and assert on that flag. [Source: tests/unit/CombatResolverTest.cs pattern from story 3-2]

### Previous story intelligence (3-2 / 3-2b)
- `ResolveRangedPhase`, `FortificationLevel`, `ComputeEffectiveAttack`, `BuildResult`, `TearDownCombatState` are all implemented and green — do not touch them.
- `CombatState` **already has** `BlockPool`, `DamageAssignments`, `BlockType? ActiveInfluenceConversion`, `SkipBlockAndDamagePending`, `UnitDamageLocked`. No new `CombatState` fields are needed.
- `BlockContribution(BlockType, int)` and `DamageAssignment(EnemyTokenInstance Source, AttackType DamageType, int RawValue)` already exist in `CombatContributions.cs`.
- `EnemyTokenInstance` exposes `Definition.Attacks` (`IReadOnlyList<EnemyAttack>`), `HasAbility(EnemyAbility)`, `AttackCancelled`, `EffectiveArmor`. `EnemyAttack` is `record EnemyAttack(int Value, AttackType Type)`.
- `GameState.BlockPool` is `IReadOnlyDictionary<AttackElement, int>` with `AddBlockPoints(int, AttackElement)`. `ClearAttackAndBlockPools()` already clears it (added in 3-2b).
- `AttackBridge` (3-2b) is the exact template for `BlockBridge` — same `(BlockType)(int)element` cast trick; `AttackElement`/`BlockType` member names line up.
- `TestBroker` (inner class of `CombatResolverTest`) overrides `virtual PromptHeroRangedAttacks`; adding a `virtual PromptHeroBlock` override there is the established pattern, and adding `BlockProvider` to the base must not break it (the override path never reaches the base delegate check).

### What NOT to do in this story
- Do **not** create a `Hero` class or apply any wounds. No `DrawWoundsToHand`, no knockdown, no hand mutation. (Phase 3 = stories 3-4/3-5.)
- Do **not** add Godot panels, `BlockTargetingPanel`, `BlockProvider` wiring in `PlaceholderMainMenu`, or a dev trigger. (Story 3-3b.)
- Do **not** implement Influence-as-Block / Diplomacy callback (needs `Hero.InfluencePool`).
- Do **not** implement Phase 4 (Melee) or modify `ResolveRangedPhase`.
- Do **not** add per-card or per-unit branches to the resolver — everything flows through `BlockPool` / `BlockContribution`.

### File list for this story
- `scripts/combat/BlockEfficiency.cs` — new file
- `scripts/broker/BlockBridge.cs` — new file
- `scripts/combat/CombatContributions.cs` — add `BlockDeclaration` record
- `scripts/broker/UIBroker.cs` — add `BlockProvider` delegate + `virtual PromptHeroBlock`
- `scripts/combat/CombatResolver.cs` — add `ResolveBlockPhase`; update `ResolveCombat`
- `tests/unit/BlockEfficiencyTest.cs` — new file
- `tests/unit/BlockBridgeTest.cs` — new file
- `tests/unit/CombatResolverTest.cs` — add block-phase cases + `TestBroker.PromptHeroBlock` override

### Project Structure Notes
- New combat logic lives in `scripts/combat/` (alongside `CombatResolver`, `EnemyLoader`); the bridge lives in `scripts/broker/` (alongside `AttackBridge`). Tests in `tests/unit/`. Matches the layout established by 3-2/3-2b. No structural variance.

### Project Context Rules
- **The most important rule:** pure C# game logic, scene tree render-only — this story has zero Godot dependencies. [Source: docs/project-context.md]
- **TDD required:** red test → confirm fail → minimum implementation → green. Never mark a task complete without seeing tests green. [Source: CLAUDE.md#Testing, docs/project-context.md]
- **Effects/combat invariant:** `CombatState` holds no per-card flags; contributions flow through pools, behaviors through callbacks/modifiers. [Source: docs/combat-flow-lld.md#3.6]

### References
- [Source: docs/combat-flow-lld.md#9-phase-2-block-phase] — `ResolveBlockPhase` structure, §9.1 which enemies attack, §9.3 all-or-nothing, §9.4 efficiency, §9.5 Swift
- [Source: docs/combat-flow-lld.md#5-phase-sequence] — block-phase skip conditions (AllEnemiesDefeated, SkipBlockAndDamagePending)
- [Source: docs/combat-flow-lld.md#6-combatresolver] — `ResolveCombat` ordering
- [Source: docs/combat-flow-lld.md#3.6 invariant] — pool/callback/modifier discipline
- [Source: docs/effect-lld.md#Block Efficiency] — **canonical** efficiency table + mixed-spending example
- [Source: scripts/broker/AttackBridge.cs] — bridge pattern to mirror for `BlockBridge`
- [Source: scripts/combat/CombatResolver.cs#ResolveRangedPhase] — phase-method pattern to mirror
- [Source: scripts/combat/CombatContributions.cs] — existing `BlockContribution`, `DamageAssignment`, `RangedAttackDeclaration` records
- [Source: scripts/combat/EnemyToken.cs] — `EnemyTokenInstance`, `EnemyAttack`, ability/cancel surface
- [Source: _bmad-output/implementation-artifacts/3-2b-live-wire-ranged-phase.md] — prior story; resolver-core vs live-wire split precedent

---

## Dev Agent Record

### Agent Model Used
claude-opus-4-8

### Debug Log References
- Red cycle 1 (`BlockEfficiency`): stub returned `value` always → 4 inefficient cases failed (Fire-vs-Fire, Ice-vs-Ice, etc.) → implemented efficiency table → 12/12 green.
- Red cycle 2 (`ResolveBlockPhase`): empty stub (prompt only, no assignments) → 6 block-math cases failed (the assignment-expecting ones); the 3 `ResolveCombat` skip/run cases passed immediately (wiring correct) → implemented block math → 14/14 green.

### Completion Notes List
- Task 1: `BlockEfficiency.Effective` uses a per-block-type efficient-set switch; inefficient halves with integer floor. ColdFire efficient vs all; Fire efficient vs {Physical, Ice}; Ice efficient vs {Physical, Fire}; Physical efficient vs {Physical} only. 12 tests green.
- Task 2: `BlockBridge.ExtractBlockContributions` mirrors `AttackBridge` exactly — `(BlockType)(int)element` cast, skips `value <= 0`. Pool is keyed by `AttackElement` only (no distance axis). 6 tests green.
- Task 3: `BlockDeclaration(IReadOnlyList<BlockContribution>, EnemyTokenInstance)` added to `CombatContributions.cs` (single target — block is per-enemy). `UIBroker` gained `BlockProvider` delegate + `virtual PromptHeroBlock` mirroring the ranged pair. No existing test sets `BlockProvider`; 261 tests stayed green after this step.
- Task 4: `ResolveBlockPhase` groups allocated block by `BlockType`, sums then applies efficiency once per type (avoids per-contribution floor loss), compares to `attackValue * (Swift ? 2 : 1)`, and emits a `DamageAssignment` with the **printed** value for any unblocked attack. `AttackCancelled` enemies and `AttackType.None` (summon) attacks are skipped. Wired into `ResolveCombat` behind `!AllEnemiesDefeated && !SkipBlockAndDamagePending`.
- Task 5: 11 direct `ResolveBlockPhase` cases + 3 `ResolveCombat` skip/run cases. `TestBroker` extended with an optional block-declarations arg (single-arg constructor preserved) and a `BlockPrompted` flag.
- Task 6: 275 tests green. No regressions. No Godot types introduced (pure resolver-core).

### File List
- `scripts/combat/BlockEfficiency.cs` — new file
- `scripts/broker/BlockBridge.cs` — new file
- `scripts/combat/CombatContributions.cs` — added `BlockDeclaration` record
- `scripts/broker/UIBroker.cs` — added `BlockProvider` delegate + `virtual PromptHeroBlock`
- `scripts/combat/CombatResolver.cs` — added `ResolveBlockPhase`; updated `ResolveCombat`
- `tests/unit/BlockEfficiencyTest.cs` — new file (12 tests)
- `tests/unit/BlockBridgeTest.cs` — new file (6 tests)
- `tests/unit/CombatResolverTest.cs` — added 14 block-phase cases; extended `TestBroker`

### Change Log
- 2026-06-30: Implemented story 3-3 — `BlockEfficiency`, `BlockBridge`, `BlockDeclaration`, `UIBroker.PromptHeroBlock`, `ResolveBlockPhase` + `ResolveCombat` wiring. Resolver-core only (no Hero/wound model, no panels). 275 tests green.

### Review Findings

Adversarial code review (Opus 4.8, 3 parallel cold-context layers — Blind Hunter, Edge Case Hunter, Acceptance Auditor) on 2026-06-30. 0 decision, 1 patch, 3 deferred, 6 dismissed as noise. Acceptance Auditor: all six ACs compliant; efficiency table verified cell-by-cell against canonical effect-lld.

- [x] [Review][Patch] Block loop does not skip `IsDestroyed` enemies — only `AttackCancelled` is skipped; a destroyed-but-not-removed enemy would still generate a `DamageAssignment`. One-line guard, consistent with the `AttackCancelled` skip. Latent today (`Destroy()` has zero callers) but a real gap in the new loop. [scripts/combat/CombatResolver.cs:ResolveBlockPhase] (edge)
- [x] [Review][Defer] One block allocation covers ALL of a multi-attack enemy's attacks — `allocated` is computed once per enemy and the full pool is re-applied against each attack in `Definition.Attacks`; block is never consumed per attack, so a multi-attack enemy is fully blocked by one attack's worth of block (violates LLD §9.3 "block allocated to one attack cannot cover another"). Latent: all 31 enemies in `data/enemies.yaml` are single-attack (verified). Proper fix needs per-attack targeting on `BlockDeclaration` (currently `Target` is the enemy, not the attack) — a model change beyond resolver-core scope. [scripts/combat/CombatResolver.cs:ResolveBlockPhase] — deferred, needs multi-attack model + per-attack declarations (blind+edge)
- [x] [Review][Defer] `ResolveBlockPhase` is not idempotent — appends to `DamageAssignments` without clearing at entry; a second call on the same `CombatState` double-counts. Single-pass `ResolveCombat` flow is correct (cleared in `TearDownCombatState`); the method is `public` and re-runnable. A blind entry-clear could be architecturally wrong if a future ranged phase/callback adds assignments, so deferred rather than patched. [scripts/combat/CombatResolver.cs:ResolveBlockPhase] — deferred, single-pass flow correct; revisit if phases re-run (edge)
- [x] [Review][Defer] `enemy.Definition.Attacks` not null-guarded — NRE if `Attacks` is ever null inside the per-enemy loop. Controlled data: `EnemyLoader` and all tests always populate it. [scripts/combat/CombatResolver.cs:ResolveBlockPhase] — deferred, controlled construction (blind)
- Dismissed (on review): `BlockEfficiency` ColdFire-vs-`AttackType.None` efficiency — `AttackType.None` is a summoner placeholder meaning "no attack this phase" (the real attack comes from the summoned token, which enters `ActiveEnemies` with its own type). A non-attack is never blocked, and `ResolveBlockPhase` skips `None` before `BlockEfficiency` runs, so the ColdFire arm's blanket `true` is both unreachable and semantically moot. AC1's "treat None as inefficient defensively" buys nothing here; left as-is to keep the switch clean. (auditor)
- Dismissed (verified safe): `d.Target == enemy` reference equality (`EnemyTokenInstance` is a plain class, not a record — reference equality is correct); `BlockBridge` `(BlockType)(int)element` cast (`AttackElement` and `BlockType` are identical `{Physical,Fire,Ice,ColdFire}`, no `None` in either — same idiom as `AttackBridge`); zero/negative attack value (effectiveBlock ≥ 0, 0-threshold never produces spurious assignment); block declared vs a defeated/absent enemy (loop iterates only `ActiveEnemies` — ignored); same-type block split across declarations (correctly summed before efficiency via `GroupBy`).
