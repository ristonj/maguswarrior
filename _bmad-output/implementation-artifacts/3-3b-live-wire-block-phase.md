# Story 3.3b: Live-Wire Block Phase (Per-Attack Targeting) Against an Actual Enemy Token

Status: done

## Story

As a player,
I want to reach the Block phase during live dev combat, play Block cards from my hand, declare block against a specific enemy attack, and pass to end the phase,
so that the resolver-core block phase from story 3-3 is exercised end-to-end in the running game — and block is correctly consumed per enemy attack, not per enemy.

## Acceptance Criteria

**Scope boundary:** This story does two things, mirroring the 3-2 → 3-2b rhythm plus one baked-in model fix John approved:

1. **Live-wire the block phase** — add a `BlockTargetingPanel` (Godot `Control`, bottom-sheet, mirrors `RangedTargetingPanel`) and register `_broker.BlockProvider` in `PlaceholderMainMenu` so the already-running block phase (`ResolveCombat` already calls `ResolveBlockPhase`) becomes interactive: the player plays Block cards, sees block resources live, and declares block against an enemy attack.
2. **Bake in per-attack targeting** — change `BlockDeclaration` to target a specific **attack index** on an enemy (not the whole enemy), and update `ResolveBlockPhase` to allocate block **per attack**. This resolves the 3-3 deferred review finding ("one block allocation covers ALL of a multi-attack enemy's attacks") at the model level, so block allocated to one attack never covers another. (John's decision 2026-07-01.)

**It does NOT:**
- Apply any wounds or surface block outcome to the player. There is still no `Hero`/wound model — `DamageAssignment`s produced by the block phase are torn down at combat end (unchanged from 3-3). Turning unblocked attacks into Wound cards is **story 3-4 (assign damage to unit) / 3-5 (knockdown)**. Manual verification therefore exercises the *pipeline* (panel appears, block pool rises live, declare/pass work, phase transitions) — block-math *correctness* is proven by unit tests, exactly as 3-2b verified the ranged pipeline.
- Implement Influence-as-Block (Diplomacy). Needs `Hero.InfluencePool` (does not exist). Deferred, same as 3-3.
- Add any multi-attack enemy to `data/enemies.yaml`. All 30 enemy definitions are single-attack (verified); the per-attack model is proven by a **synthetic** two-attack `EnemyTokenInstance` in unit tests. The live dev flow fights a single-attack Brown token (attack index 0).
- Implement Phase 3 (Assign Damage) or Phase 4 (Melee), enemy piles, garrison reveal, or combined multi-enemy targeting.

---

**AC1 — `BlockDeclaration` carries an attack index; `ResolveBlockPhase` allocates block per attack (TDD, red-first)**

Change the `BlockDeclaration` record in `scripts/combat/CombatContributions.cs` to identify a single enemy **attack** rather than the whole enemy:

```csharp
// A single block declaration: a set of block contributions aimed at ONE specific attack
// of one enemy. Block is per-attack, all-or-nothing (LLD §9.3) — block allocated to one
// attack never covers another, even on the same enemy. AttackIndex indexes into
// Target.Definition.Attacks.
public record BlockDeclaration(
    IReadOnlyList<BlockContribution>  Contributions,
    EnemyTokenInstance                Target,
    int                               AttackIndex
);
```

Rewrite `ResolveBlockPhase` in `CombatResolver` so allocation is keyed by `(enemy, attackIndex)` — block declared against attack *i* is only summed against attack *i*:

```csharp
public async Task ResolveBlockPhase(CombatState combat) {
    SetPhase(GamePhase.CombatBlock, combat);
    await FirePhaseCallbacks(GamePhase.CombatBlock, combat);

    var declarations = await _broker.PromptHeroBlock(combat);

    foreach (var enemy in combat.ActiveEnemies.ToList()) {
        if (enemy.AttackCancelled || enemy.IsDestroyed) continue;

        var attacks = enemy.Definition.Attacks;
        for (int i = 0; i < attacks.Count; i++) {
            var attack = attacks[i];
            if (attack.Type == AttackType.None) continue;   // summon-only "attack"; no damage in Phase 2

            // Block allocated to THIS attack only (per-attack consumption — block declared
            // against attack i never covers attack j, LLD §9.3).
            var allocated = declarations
                .Where(d => d.Target == enemy && d.AttackIndex == i)
                .SelectMany(d => d.Contributions)
                .ToList();

            // Sum same-type block first, then apply efficiency once per type (avoids
            // per-contribution floor loss when same-type block is split across declarations).
            int effectiveBlock = allocated
                .GroupBy(b => b.Type)
                .Sum(g => BlockEfficiency.Effective(g.Key, attack.Type, g.Sum(b => b.Value)));

            int threshold = attack.Value * (enemy.HasAbility(EnemyAbility.Swift) ? 2 : 1);

            // All-or-nothing: block must fully meet the threshold or the FULL printed attack
            // gets through. RawValue is the PRINTED value (Swift only raises the threshold).
            if (effectiveBlock < threshold)
                combat.DamageAssignments.Add(
                    new DamageAssignment(enemy, attack.Type, attack.Value));
        }
    }
}
```

The only behavioral change vs 3-3 is per-attack allocation (`&& d.AttackIndex == i`). All 3-3 rules are preserved: all-or-nothing, sum-then-efficiency, Swift raises threshold only, `AttackCancelled`/`IsDestroyed`/`AttackType.None` skipped, printed `RawValue`.

**Test coverage** — update the existing 3-3 `CombatResolverTest` block-phase cases to the new `BlockDeclaration(contribs, enemy, attackIndex)` signature. For all current single-attack enemies the index is `0`, so each existing declaration gains a trailing `, 0` and its assertion is unchanged. Then add the case that proves the bake (TDD red-first — it fails against the 3-3 resolver because block leaks across attacks):

- `BlockPhase_MultiAttack_BlockConsumedPerAttack` — construct a synthetic enemy with **two** Physical attacks (value 4 each) via a purpose-built `EnemyTokenDefinition` (two `EnemyAttack(4, Physical)` entries). Declare Physical block 4 against **attack index 0 only**. Assert: attack 0 produces **no** `DamageAssignment` (fully blocked), attack 1 produces **one** `DamageAssignment(enemy, Physical, 4)` (the block did NOT leak to it). Under the 3-3 resolver both attacks would be blocked → this test fails red until the per-attack loop lands.
- `BlockPhase_MultiAttack_SeparateDeclarationsBlockBoth` — same two-attack enemy; declare Physical block 4 vs index 0 **and** Physical block 4 vs index 1 (two declarations). Assert: **no** `DamageAssignment`s (both attacks individually satisfied).

Keep the existing single-attack cases (`BlockPhase_FullyBlockedAttack_NoDamageAssignment`, `_PartialBlock_FullAttackGetsThrough`, `_SwiftDoublesThreshold`, `_MixedBlockTypes_Sum`, `_BlockForOneEnemy_DoesNotCoverAnother`, etc.) — they still pass with `AttackIndex = 0` appended.

---

**AC2 — `BlockTargetingPanel` shows each enemy attack and available block; player declares per attack or passes**

New `scripts/ui/components/BlockTargetingPanel.cs` (Godot `Control` subclass), a near-exact structural mirror of `RangedTargetingPanel` (bottom-sheet layout, `MouseFilter = Ignore` root so `HandView` stays tappable underneath, `ZIndex = 5`, docked in the 350–480px band above the hand, not visible on `_Ready`):

```csharp
public Task<IReadOnlyList<BlockDeclaration>> ShowAndAwait(CombatState combat, GameState state) {
    // Re-entrancy guards mirror RangedTargetingPanel: drop stale subscription, complete any
    // stranded awaiter, then re-arm.
    if (_state != null)
        _state.ResourcesChanged -= RefreshBlockDisplay;
    _tcs?.TrySetResult(_declarations);

    _combat = combat;
    _state  = state;
    _tcs    = new TaskCompletionSource<IReadOnlyList<BlockDeclaration>>();
    _declarations     = new List<BlockDeclaration>();
    _declaredContribs = new List<BlockContribution>();
    _state.ResourcesChanged += RefreshBlockDisplay;
    RefreshAttackRows();
    RefreshBlockDisplay();
    Visible = true;
    return _tcs.Task;
}

private void Complete() {
    if (_state != null)
        _state.ResourcesChanged -= RefreshBlockDisplay;   // always unsubscribe before resolving TCS
    Visible = false;
    _tcs?.TrySetResult(_declarations);
}
```

UI requirements:
- **Title:** `"Combat: Block Phase"`.
- **Available block label:** the current undeclared block pool, updated live via `ResourcesChanged`, computed by `AvailableContribs()` (see below). Shows `"No block available"` when empty.
- **One row per blockable enemy attack** — iterate `combat.ActiveEnemies`, and within each enemy iterate `Definition.Attacks` with index `i`, **skipping** `AttackType.None` attacks. Each row shows the enemy name, the attack (`"{Type} {Value}"`), a Swift/Fortification-style note if `enemy.HasAbility(EnemyAbility.Swift)` (e.g. `" [Swift: needs 2×]"`), and a `"Block {name} ({Type} {Value})"` button. The button captures the **enemy instance and the attack index** `i`.
- **Declare button disabled** when `AvailableContribs()` is empty (mirrors ranged panel's disable logic).
- **"Pass" button** — always enabled; calls `Complete()`.

`AvailableContribs()` mirrors `RangedTargetingPanel.AvailableContribs` exactly, but over the block pool (keyed by `AttackElement` only — no distance axis):

```csharp
private List<BlockContribution> AvailableContribs() {
    if (_state == null) return new List<BlockContribution>();
    var all = BlockBridge.ExtractBlockContributions(_state.BlockPool);

    // BlockPool aggregates by AttackElement, so `all` holds at most one entry per BlockType.
    // Subtract the total already declared per type by AMOUNT (never match discrete entries —
    // a second Block card raises the pool total and would break value-matching).
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
```

**Declare flow** (tap `"Block {name} ({Type} {Value})"` for attack index `i`):
1. `var available = AvailableContribs();`
2. If `available.Count == 0` → ignore (stale tap).
3. Otherwise: add `new BlockDeclaration(available, enemy, i)` to `_declarations`; append `available` to `_declaredContribs`; call `RefreshBlockDisplay()`.

**Note on partial block:** block is all-or-nothing, so a declaration that does not meet the attack's threshold is simply wasted (the resolver still emits the `DamageAssignment`). The panel does **not** compute block success — it just lets the player allocate; the resolver decides. This matches how `RangedTargetingPanel` does not compute enemy defeat.

---

**AC3 — `PlaceholderMainMenu` wires the block panel and registers `BlockProvider`**

In `PlaceholderMainMenu._Ready()`, alongside the existing interstitial/ranged panel wiring (create as child, register delegate):

```csharp
_blockTargetingPanel = new BlockTargetingPanel();
_blockTargetingPanel.Name = "BlockTargetingPanel";
AddChild(_blockTargetingPanel);
_broker.BlockProvider = c => _blockTargetingPanel.ShowAndAwait(c, _state);
```

Add the field `private BlockTargetingPanel _blockTargetingPanel = null!;` next to `_rangedTargetingPanel`.

**No change to `OnDevCombatPressed` is required** — `ResolveCombat` already runs `ResolveBlockPhase` when the Brown token survives the ranged phase (3-3 wired it). With `BlockProvider` now registered, that phase becomes interactive instead of auto-passing. The InputLock is already released before `ResolveCombat`, so Block card plays work during the block panel exactly as Ranged card plays work during the ranged panel. `TripUndoGate` is already called; `EffectType.Block` is already blessed in `GamePhase.CombatBlock` by `PhaseGate`, so Block cards play normally and add to `GameState.BlockPool` (which fires `ResourcesChanged` → panel refresh).

---

**AC4 — Full suite green + manual verification**

`dotnet test tests/maguswarrior.Tests.csproj` passes — all 275 existing tests (with the block-phase cases migrated to the 3-arg `BlockDeclaration`) plus the 2 new multi-attack cases. No regressions.

Manual verification (WSL desktop loop, mirrors 3-2b): launch game → "Combat: Dev (Brown Token)" → interstitial → "Begin Combat" → ranged phase → Pass (or play ranged and fail to defeat armor 5) → **Block phase panel appears as a bottom-sheet without covering the hand** → play a Block card (block pool label updates live) → tap "Block {enemy} (Physical 5)" (button enables once block is available) → the available-block label drops → "Pass" ends the phase → `[Combat] Dev combat LOST — 0 token(s) defeated` (block does not defeat enemies) → phase returns to Movement → no exceptions. Also verify: pressing "Pass" with no block declared ends the phase cleanly; the panel hides after Pass; a second "Combat: Dev" run works (re-entrancy).

---

**AC5 — Per-type amount picker so block can be split across attacks (added post-review 2026-07-01)**

Resolves the code-review decision finding. `BlockTargetingPanel` must let the player commit a **partial, chosen** amount of block to a specific attack instead of dumping the entire available pool. Design:

- On each blockable attack row (per enemy × attack index, `AttackType.None` still skipped), render **one `SpinBox` per block type currently available** (`BlockBridge.ExtractBlockContributions(_state.BlockPool)` types with remaining > 0). Each spin box: `MinValue = 0`, `MaxValue = remaining-of-that-type` (from `AvailableContribs()`), `Step = 1`, integer. Label each with its `BlockType`.
- The row's **"Block" button** commits a `BlockDeclaration(chosenContribs, enemy, attackIndex)` where `chosenContribs` is one `BlockContribution(type, spinBoxValue)` per spin box with value > 0. If every spin box is 0, the tap is ignored (no empty declaration).
- After a commit, append the chosen contribs to `_declaredContribs` (so `AvailableContribs()` subtracts them) and **rebuild the rows** so every attack's spin-box maxes reflect the reduced remaining pool. Spin-box maxes must also refresh on `ResourcesChanged` (a newly played Block card raises the pool).
- The "Available: …" summary label and the disable-when-empty behavior stay. When no block remains, spin boxes clamp to max 0 and the Block buttons disable (as today).
- **Preserve every mirror-of-`RangedTargetingPanel` invariant** the review verified: non-modal bottom-sheet, `MouseFilter=Ignore` root, `Complete()` unsubscribes before resolving the TCS, re-entrancy guards, amount-based subtraction. Do **not** regress those.

**Test coverage:** the resolver is unchanged, so its unit tests stand. Add panel-level coverage only if the existing test project already exercises panels (it does not — panels are Godot `Control`s verified manually). Therefore: **no new unit tests required**; correctness of split allocation is proven by the existing per-attack resolver tests plus manual verification. Confirm `dotnet test` stays green (277).

**Manual verification (John):** in the dev flow, play a Block card, set the attack's spin box **below** the attack value, tap Block → confirm the attack is NOT fully blocked (would produce a `DamageAssignment`); then set it **at/above** the (Swift-adjusted) value → fully blocked. Confirm committing a partial amount leaves the remainder available (spin-box max on any other control drops accordingly).

---

**AC6 — Panel UX refinements from manual verification (added 2026-07-01, John's playtest feedback)**

Two changes to `BlockTargetingPanel`, both driven by "don't add friction when there's no ambiguity, and auto-advance when the task is done":

**6a — Collapse the SpinBox when there is no allocation choice.** Count the total blockable attacks across all `ActiveEnemies` (attacks skipping `AttackType.None`). If that total is **exactly 1**, do NOT render SpinBoxes — render a plain "Block {name} ({Type} {Value})" button that, on tap, commits the **entire** `AvailableContribs()` to that single attack (the original dump-all behavior, which is correct when there is only one place block can go). If the total is **≥ 2**, keep the per-type SpinBox picker from AC5 (the split UI the review required). Determine this once per `RefreshAttackRows()` from the current `_combat`.

**6b — Auto-close the panel when every blockable attack is fully blocked.** After each successful commit in `OnBlockPressed`, evaluate whether **every** blockable attack of every active enemy is now fully blocked by the declarations collected so far. If so, call `Complete()` automatically — the player should not have to press "Pass" when there is nothing left to decide. "Pass" remains for the give-up case (an attack the player cannot or chooses not to block).

**6c — Shared block-outcome helper (no duplicated rule).** The "is this attack fully blocked?" test must NOT be re-implemented in the panel. Extract a pure-C# helper — e.g. `BlockOutcome.IsFullyBlocked(EnemyAttack attack, bool swift, IEnumerable<BlockContribution> allocated)` in `scripts/combat/` (no Godot types) — that computes `effectiveBlock >= attack.Value * (swift ? 2 : 1)` using the existing `BlockEfficiency.Effective` sum-then-per-type logic. **Refactor `ResolveBlockPhase` to use this same helper** (replacing its inline `if (effectiveBlock < threshold)` check) so the resolver and the panel share one definition of "fully blocked." The panel calls it per attack, summing the `Contributions` of all `_declarations` whose `Target`/`AttackIndex` match, to decide 6b.

**Tests:**
- Add a direct unit test file `tests/unit/BlockOutcomeTest.cs` (TDD) covering `IsFullyBlocked`: exact-threshold true, one-short false, Swift doubles threshold, efficient vs inefficient element, mixed-type sum, empty allocation false.
- The `ResolveBlockPhase` refactor must keep all existing block-phase resolver tests green (behavior unchanged — same threshold rule, just extracted). Confirm 277 + new `BlockOutcome` tests all pass.
- No panel unit tests (Godot Control, verified manually).

**Manual verification (John):** single-attack Minotaur → block panel shows a plain Block button (no SpinBox); generate ≥5 block, tap Block → attack fully blocked → **panel closes automatically** (no Pass needed). Also confirm: with only partial block, tapping Block does NOT auto-close and Pass still works.

---

## Tasks / Subtasks

- [ ] **Task 1 — `BlockDeclaration` attack-index + per-attack `ResolveBlockPhase` (TDD)** (AC: 1)
  - [ ] Add `int AttackIndex` to the `BlockDeclaration` record in `scripts/combat/CombatContributions.cs`; update the doc comment (per-attack, not per-enemy)
  - [ ] Migrate every existing 3-3 block-phase test declaration in `tests/unit/CombatResolverTest.cs` to `BlockDeclaration(contribs, enemy, 0)`; confirm they still assert the same outcomes
  - [ ] Write red tests `BlockPhase_MultiAttack_BlockConsumedPerAttack` and `BlockPhase_MultiAttack_SeparateDeclarationsBlockBoth` (synthetic two-attack `EnemyTokenDefinition`); confirm the first fails against the current resolver
  - [ ] Rewrite `ResolveBlockPhase` with the `for`-index loop and `&& d.AttackIndex == i` allocation filter
  - [ ] Confirm all block-phase tests green
  - [ ] Update the `TestBroker.PromptHeroBlock` override / canned-declaration plumbing in `CombatResolverTest.cs` for the 3-arg signature

- [ ] **Task 2 — `BlockTargetingPanel`** (AC: 2)
  - [ ] Implement `scripts/ui/components/BlockTargetingPanel.cs` mirroring `RangedTargetingPanel` — bottom-sheet root (`MouseFilter=Ignore`, `ZIndex=5`, docked 350–480px), title, live available-block label, per-attack rows (skip `AttackType.None`), disabled-when-empty declare buttons, Pass button, `_declaredByType` amount-subtraction in `AvailableContribs`, `Complete()` unsubscribing before TCS resolution, re-entrancy guards in `ShowAndAwait`
  - [ ] Not visible on `_Ready`

- [ ] **Task 3 — `PlaceholderMainMenu` wiring** (AC: 3)
  - [ ] Add `_blockTargetingPanel` field; in `_Ready()` create + add child + register `_broker.BlockProvider`
  - [ ] Confirm `OnDevCombatPressed` needs no change (block phase already runs; InputLock already released; undo gate already tripped)

- [ ] **Task 4 — Full regression + manual verification** (AC: 4)
  - [ ] `dotnet test tests/maguswarrior.Tests.csproj` → green (277 tests: 275 existing + 2 multi-attack)
  - [ ] Manual verification per AC4; record the run in Completion Notes

---

## Dev Notes

### Architecture rules (non-negotiable)
- **Pure C# game logic; scene tree is render-only.** `BlockDeclaration`, `ResolveBlockPhase`, `BlockBridge` contain **no Godot types**. `BlockTargetingPanel` is `partial class : Control` and must remain a minimal wiring node. **`Log` pulls in Godot (`GD.Print`) — do NOT add block-outcome logging inside `CombatResolver`** (it has no `using Godot` and must keep none). Any logging goes in the panel or the dev handler. [Source: docs/project-context.md#the-most-important-rule, scripts/core/Log.cs]
- **No per-card logic in the resolver.** Block resources flow card play → `GameState.BlockPool` → `BlockBridge` → `BlockContribution` → `BlockDeclaration`. The resolver never sees card IDs. [Source: docs/project-context.md#4, docs/combat-flow-lld.md#3.6]
- **InputLock released before `ResolveCombat`** (already true in `OnDevCombatPressed`) so `HandView.OnPlayRequested` can play Block cards during the block panel. The panel is a non-modal bottom-sheet (`MouseFilter=Ignore` root), so the hand stays tappable. [Source: docs/project-context.md#input-lock, scripts/ui/components/RangedTargetingPanel.cs]
- **Async/await; never `async void` for game logic.** `ResolveBlockPhase` stays `async Task`. Panel button handlers are Godot signal handlers (sync `void`); the only `async void` is the existing `OnDevCombatPressed`, already wrapped in try/catch. [Source: docs/project-context.md#1, #2]

### Why per-attack targeting (the bake)
3-3 shipped `BlockDeclaration.Target` = the enemy, and `ResolveBlockPhase` computed `allocated` once per enemy and re-applied the full pool against every attack — so a multi-attack enemy was fully blocked by one attack's worth of block (3-3 review deferred finding, LLD §9.3 violation). Latent today (all 30 YAML enemies are single-attack) but a real gap. John chose to fix it at the model level now (2026-07-01) rather than defer, because the panel we build here is the natural place to express "block *this* attack." Adding `AttackIndex` makes the panel and resolver agree on the unit of blocking, and the two synthetic multi-attack tests lock the behavior in before any multi-attack enemy exists. [Source: 3-3 Review Findings — deferred multi-attack finding; John decision 2026-07-01]

### Why block-success is not surfaced to the player yet
The block phase emits `DamageAssignment`s for unblocked attacks, but `TearDownCombatState` clears them at combat end and `CombatResult` does not carry them — there is no `Hero`/wound model to receive them. So the live flow cannot yet show "you took N wounds." That is Phase 3 (story 3-4 assign-damage / 3-5 knockdown). Manual verification here mirrors 3-2b: confirm the *pipeline* runs (panel, live pool, declare, pass, phase transitions), and trust the unit tests for the block *math*. Do not add a wound model or a results readout in this story. [Source: 3-3 scope boundary; project_next_session memory]

### `BlockTargetingPanel` mirrors `RangedTargetingPanel` — read it first
`scripts/ui/components/RangedTargetingPanel.cs` is the exact template: same bottom-sheet layout constants (anchors, `OffsetTop=-480`, `OffsetBottom=-350`, `MouseFilter=Ignore` root so the hand underneath stays tappable), same `TaskCompletionSource` + `ResourcesChanged` live-refresh pattern, same re-entrancy guards, same `Complete()` (unsubscribe before resolving TCS), same amount-based subtraction in `AvailableContribs` (do NOT match declared entries by exact value — a second Block card raises the pool total and value-matching breaks; this exact bug was a 3-2b review patch). The only differences: block pool is keyed by `AttackElement` only (no `(Distance, Element)`), and rows are per **attack** (enemy × attack index) rather than per enemy. [Source: scripts/ui/components/RangedTargetingPanel.cs, 3-2b Review Findings — AvailableContribs double-count patch]

### How Block cards feed the pool
Playing a Block card in `CombatBlock` phase runs `BlockEffect.Execute` → `GameState.AddBlockPoints(points, element)`, which fires `ResourcesChanged`. `PhaseGate` already blesses `(EffectType.Block, GamePhase.CombatBlock)`. `BlockBridge.ExtractBlockContributions(_state.BlockPool)` converts the pool to `BlockContribution`s. The panel tracks what it has already declared in `_declaredContribs` (local list; does NOT mutate `GameState.BlockPool`). `TearDownCombatState` → `ClearAttackAndBlockPools` clears the pool after combat. [Source: scripts/cards/effects/combat/BlockEffect.cs, scripts/core/GameState.cs:76, scripts/broker/BlockBridge.cs, scripts/cards/effects/PhaseGate.cs:17]

### Dev flow: which token, and reaching the block phase
`OnDevCombatPressed` fights `_enemyDefs.FirstOrDefault(e => e.Color == TokenColor.Brown)` — the first Brown in `data/enemies.yaml` (armor 5, fame 4, Physical 5, Brutal; single attack → index 0). To reach the block phase the Brown token must survive the ranged phase — with the default test hand there is likely no Ranged card, so "Pass" on the ranged panel leaves the enemy alive and the block phase runs. If the tester does defeat it in ranged, the block phase is correctly skipped (`!AllEnemiesDefeated` guard) — note that in the run log. [Source: data/enemies.yaml:194, scripts/ui/screens/PlaceholderMainMenu.cs:169]

### What NOT to do in this story
- Do **not** create a `Hero` class, apply wounds, or add a results readout. (Phase 3 = 3-4/3-5.)
- Do **not** add block-outcome logging inside `CombatResolver` (would pull Godot's `Log` into the pure resolver).
- Do **not** add a multi-attack enemy to `data/enemies.yaml` — the multi-attack model is proven by synthetic test enemies only.
- Do **not** implement Influence-as-Block / Diplomacy (needs `Hero.InfluencePool`).
- Do **not** make the block panel modal / full-rect opaque — it must not cover `HandView` (the 3-2b bottom-sheet decision applies identically here).
- Do **not** touch `ResolveRangedPhase`, `BlockEfficiency`, or `BlockBridge` logic — only the allocation filter in `ResolveBlockPhase` and the `BlockDeclaration` shape change.
- Do **not** use `@export` or `.tscn` for the panel — construct all UI in code.

### File list for this story
- `scripts/combat/CombatContributions.cs` — add `AttackIndex` to `BlockDeclaration`
- `scripts/combat/CombatResolver.cs` — rewrite `ResolveBlockPhase` with per-attack allocation loop
- `scripts/ui/components/BlockTargetingPanel.cs` — new file
- `scripts/ui/screens/PlaceholderMainMenu.cs` — add `_blockTargetingPanel` field + `_Ready()` wiring + `BlockProvider` registration
- `tests/unit/CombatResolverTest.cs` — migrate block-phase declarations to 3-arg; add 2 multi-attack cases; update `TestBroker` block plumbing

### Previous story intelligence (3-3 / 3-2b)
- `ResolveBlockPhase`, `BlockEfficiency`, `BlockBridge`, `BlockContribution`, `DamageAssignment`, `UIBroker.PromptHeroBlock` + `BlockProvider` all exist and are green (3-3). Only the allocation filter and `BlockDeclaration` shape change here.
- `UIBroker.BlockProvider` and `virtual PromptHeroBlock` already exist (3-3, AC3) — no broker change needed; just register the provider in `PlaceholderMainMenu`.
- `TestBroker` (inner class of `CombatResolverTest`) already overrides `virtual PromptHeroBlock` with canned declarations (3-3). Updating those canned `BlockDeclaration`s to the 3-arg form is the only broker-test change.
- `RangedTargetingPanel` (3-2b) is the panel template; the `AvailableContribs` amount-subtraction and non-modal bottom-sheet layout are both hardened 3-2b review patches — reuse them, don't re-derive.
- `GameState.BlockPool` is `IReadOnlyDictionary<AttackElement,int>`; `AddBlockPoints` fires `ResourcesChanged`; `ClearAttackAndBlockPools` (3-2b) clears it at teardown.
- `EnemyTokenInstance` is a plain class (reference equality) — `d.Target == enemy` is correct (3-3 verified). `Definition.Attacks` is `IReadOnlyList<EnemyAttack>`; `EnemyAttack` is `record EnemyAttack(int Value, AttackType Type)`.

### Project Context Rules
- **The most important rule:** pure C# game logic, scene tree render-only. Resolver + records have zero Godot deps; panel is a thin `Control`. [Source: docs/project-context.md]
- **TDD required:** red test → confirm fail → minimum implementation → green. The multi-attack test MUST be written first and confirmed failing against the 3-3 resolver. [Source: CLAUDE.md#Testing]
- **Effects/combat invariant:** `CombatState` holds no per-card flags; contributions flow through pools, behaviors through callbacks/modifiers. [Source: docs/combat-flow-lld.md#3.6]

### References
- [Source: docs/combat-flow-lld.md#9-phase-2-block-phase] — §9.1 which enemies attack, §9.3 all-or-nothing per-attack, §9.4 efficiency, §9.5 Swift
- [Source: _bmad-output/implementation-artifacts/3-3-block-incoming-damage.md] — resolver-core spec + Review Findings (the deferred multi-attack finding this story fixes)
- [Source: _bmad-output/implementation-artifacts/3-2b-live-wire-ranged-phase.md] — live-wire precedent; bottom-sheet + AvailableContribs review patches
- [Source: scripts/ui/components/RangedTargetingPanel.cs] — panel template to mirror
- [Source: scripts/combat/CombatResolver.cs#ResolveBlockPhase] — method to rewrite
- [Source: scripts/combat/CombatContributions.cs] — `BlockDeclaration`, `BlockContribution`, `DamageAssignment`
- [Source: scripts/broker/BlockBridge.cs] — pool→contribution bridge (unchanged)
- [Source: scripts/cards/effects/PhaseGate.cs] — `(Block, CombatBlock)` already blessed
- [Source: scripts/cards/effects/combat/BlockEffect.cs] — card play → `AddBlockPoints`
- [Source: scripts/ui/screens/PlaceholderMainMenu.cs] — combat wiring + dev trigger
- [Source: data/enemies.yaml:194] — first Brown token (dev-combat target)

---

## Dev Agent Record

### Agent Model Used
claude-sonnet-4-6

### Debug Log References
None — no in-resolver logging added (hard constraint: CombatResolver has no `using Godot`, must stay that way). All block-math correctness proven by unit tests.

### Completion Notes List
- Red-first confirmed: `BlockPhase_MultiAttack_BlockConsumedPerAttack` failed against the 3-3 resolver (empty DamageAssignments instead of 1) before `ResolveBlockPhase` was rewritten. All other 276 tests passed during the red phase.
- `BlockDecl` helper split into two overloads: the existing no-index overload defaults to `AttackIndex = 0` (all existing single-attack tests unchanged, no `, 0` append needed at call sites), and a new indexed overload `BlockDecl(target, attackIndex, ...contribs)` used by the two new multi-attack tests.
- `ResolveBlockPhase` rewritten: outer `foreach (enemy)` → inner `for (int i = 0; i < attacks.Count; i++)` loop with `&& d.AttackIndex == i` filter. All 3-3 rules preserved: `AttackCancelled`/`IsDestroyed` guard, `AttackType.None` skip, sum-then-efficiency GroupBy, Swift doubles threshold, `RawValue` is printed value.
- `BlockTargetingPanel.cs` created mirroring `RangedTargetingPanel.cs` exactly: same bottom-sheet layout constants, same `TaskCompletionSource` + `ResourcesChanged` live-refresh, same re-entrancy guards, same `Complete()` (unsubscribe before TCS), same amount-based `AvailableContribs` subtraction. Differences: block pool (`IReadOnlyDictionary<AttackElement,int>` → `BlockBridge.ExtractBlockContributions`), rows are per attack-index (skip `AttackType.None`), declare button captures both enemy and attack index.
- `PlaceholderMainMenu._Ready()` wired: `_blockTargetingPanel` field + create + `AddChild` + `_broker.BlockProvider` registration, immediately after the ranged panel wiring. `OnDevCombatPressed` left completely unchanged.
- Final test count: 277/277 passed (275 pre-existing + 2 new multi-attack cases). Zero regressions.
- AC5 (post-review patch, 2026-07-01): replaced dump-all `OnBlockPressed` with per-block-type `SpinBox` pickers. Each attack row now renders one `SpinBox` per block type in `AvailableContribs()` (`MinValue=0`, `MaxValue=remaining`, `Step=1`, `Rounded=true`). The "Block" button commits only the non-zero spin-box amounts as chosen `BlockContribution`s; all-zero tap is ignored. `RefreshBlockDisplay()` now calls `RefreshAttackRows()` internally, so SpinBox maxes refresh on both commit and `ResourcesChanged` (new Block card played). `SetDeclareButtonsDisabled` removed; button disabled state is set during row construction from `hasAnyBlock`. `ShowAndAwait` now calls `RefreshBlockDisplay()` only (covers row build). All hard constraints preserved: `Complete()` unsubscribes before TCS, re-entrancy guards untouched, `AvailableContribs` amount-based subtraction unchanged, non-modal bottom-sheet layout unchanged. 277/277 green after patch.
- AC6 (post-playtest UX refinements, 2026-07-01): Implemented 6c (shared helper) first per TDD: `scripts/combat/BlockOutcome.cs` — pure-C# static class with `IsFullyBlocked(EnemyAttack, bool swift, IEnumerable<BlockContribution>)` using the same GroupBy/sum-then-Effective logic as the original resolver. 10 red-first tests in `tests/unit/BlockOutcomeTest.cs` confirmed failure before implementation (compile error — class not found), then passed green. `ResolveBlockPhase` refactored: its inline `effectiveBlock`/`threshold` block replaced with `BlockOutcome.IsFullyBlocked(attack, enemy.HasAbility(EnemyAbility.Swift), allocated)` — behavior identical, all 277 existing tests stayed green. Then 6a: `RefreshAttackRows()` now counts `totalBlockable` attacks (skipping `AttackType.None`) before building rows. When `totalBlockable == 1`, renders a plain dump-all button (`OnBlockPressedDumpAll`) with no SpinBoxes; when `>= 2`, keeps AC5 per-type SpinBox picker. Then 6b: both `OnBlockPressed` (SpinBox path) and `OnBlockPressedDumpAll` (dump-all path) now call `AllBlockableAttacksFullyBlocked()` after each successful commit — mirrors the resolver's loop (skips `AttackCancelled`/`IsDestroyed`/`AttackType.None`), uses `BlockOutcome.IsFullyBlocked` per attack — and auto-call `Complete()` if every attack is fully blocked. "Pass" button unchanged. All AC6 hard constraints met: `BlockOutcome` and refactored resolver have zero Godot types; temporary `[Block]` diagnostic log line preserved; `Complete()` still unsubscribes before TCS resolution; re-entrancy guards untouched. Final: 287/287 green (277 + 10 new `BlockOutcomeTest`).

### File List
- `scripts/combat/CombatContributions.cs` — added `int AttackIndex` to `BlockDeclaration` record, updated doc comment
- `scripts/combat/CombatResolver.cs` — rewrote `ResolveBlockPhase` with `for`-index loop and `&& d.AttackIndex == i` filter; AC6: replaced inline effectiveBlock/threshold with `BlockOutcome.IsFullyBlocked`
- `scripts/combat/BlockOutcome.cs` — new file (AC6, pure-C#, no Godot): `public static bool IsFullyBlocked(EnemyAttack, bool swift, IEnumerable<BlockContribution>)`
- `scripts/ui/components/BlockTargetingPanel.cs` — new file (Godot `Control` bottom-sheet panel); AC5: per-block-type SpinBox pickers, rebuild-on-refresh pattern; AC6: single-attack detection + dump-all path (6a), auto-close on full coverage (6b), `AllBlockableAttacksFullyBlocked()` helper using `BlockOutcome`
- `scripts/ui/screens/PlaceholderMainMenu.cs` — added `_blockTargetingPanel` field, create/add-child, `BlockProvider` registration in `_Ready()`
- `tests/unit/CombatResolverTest.cs` — updated `BlockDecl` helpers (two overloads), added `TwoAttackEnemy` helper and 2 new multi-attack tests
- `tests/unit/BlockOutcomeTest.cs` — new file (AC6, TDD): 10 tests for `BlockOutcome.IsFullyBlocked` (exact-threshold, one-short, Swift doubles threshold ×2, efficient element, inefficient element ×2, mixed-type sum ×2, empty allocation)

### Change Log
- `BlockDeclaration` record: 2-arg → 3-arg (added `int AttackIndex`)
- `ResolveBlockPhase`: per-enemy block → per-attack-index block (`for` loop + `d.AttackIndex == i` predicate)
- New: `BlockTargetingPanel` (bottom-sheet, live block pool label, per-attack rows, declare + pass buttons)
- `PlaceholderMainMenu`: `_blockTargetingPanel` field + `_Ready()` wiring + `_broker.BlockProvider`
- AC5 `BlockTargetingPanel`: per-block-type `SpinBox` pickers on each attack row; `OnBlockPressed` reads SpinBox values (ignores all-zero); `RefreshBlockDisplay` now calls `RefreshAttackRows` so SpinBox maxes refresh on `ResourcesChanged` and commit; `SetDeclareButtonsDisabled` removed; `ShowAndAwait` calls `RefreshBlockDisplay` only
- AC6 `BlockOutcome.cs`: new pure-C# static helper `IsFullyBlocked(EnemyAttack, bool swift, IEnumerable<BlockContribution>)` with GroupBy/sum-then-Effective logic
- AC6 `CombatResolver.ResolveBlockPhase`: inline effectiveBlock/threshold block replaced by `BlockOutcome.IsFullyBlocked` call
- AC6 `BlockTargetingPanel`: `RefreshAttackRows` counts `totalBlockable` attacks → single-attack mode renders plain dump-all button (no SpinBoxes), multi-attack mode keeps SpinBoxes; `OnBlockPressedDumpAll` handler for single-attack path; `AllBlockableAttacksFullyBlocked()` helper; both commit handlers auto-call `Complete()` when all blockable attacks fully covered
- AC6 `BlockOutcomeTest.cs`: 10 TDD tests (red-first confirmed, then green)

### Review Findings

Adversarial code review (Opus 4.8, 3 parallel cold-context layers — Blind Hunter, Edge Case Hunter, Acceptance Auditor) on 2026-07-01. 1 decision, 0 patch, 3 deferred, 0 dismissed. Acceptance Auditor: **all four ACs fully satisfied** — record change, per-attack resolver, red-first tests, panel mirror, wiring, and every hard constraint (zero Godot in resolver, no wound model, no `OnDevCombatPressed` change, non-modal bottom-sheet) verified; 277/277 green.

- [ ] [Review][Decision → PATCH] **Block panel cannot split one block pool across a multi-attack enemy's attacks** — RESOLVED 2026-07-01: John chose **patch now**. See AC5 below (per-type amount picker). Original finding: — `OnBlockPressed` declares the ENTIRE `AvailableContribs()` against the tapped attack index, then disables the remaining declare buttons ("No block available"). For an enemy with two Physical-4 attacks and a Physical-8 pool, tapping "Block attack 0" consumes all 8; attack 1 becomes unblockable and the resolver (correctly) emits 4 damage — the hero wanted 4/4. Splitting is only possible by interleaving card-plays between declarations. The per-attack MODEL (the story's actual goal) is done and unit-proven; this is a PANEL-affordance gap. AC2's declare-flow was specified as "add `new BlockDeclaration(available, enemy, i)`" — so the code faithfully implements the spec, but the spec inherited the ranged panel's dump-all behavior, which doesn't fit per-attack blocking. NOT exercisable today (all 30 enemies are single-attack). [scripts/ui/components/BlockTargetingPanel.cs:OnBlockPressed] (blind+edge, major)
- [x] [Review][Defer] **Synchronous TCS continuation in the re-entrancy guard** — `_tcs` has no `RunContinuationsAsynchronously`; the guard's `_tcs?.TrySetResult(_declarations)` fires before `_combat`/`_state`/`_declarations` are reassigned, so a stranded awaiter would resume the OLD block phase mid-re-arm on half-updated state. Defensive-only path (guarded by `_combatInProgress`); identical pattern in `RangedTargetingPanel`. [scripts/ui/components/BlockTargetingPanel.cs:ShowAndAwait] — deferred, pre-existing template pattern; fix both panels together (blind)
- [x] [Review][Defer] **Out-of-range / mismatched `AttackIndex` silently drops the hero's block** — a `BlockDeclaration` whose `AttackIndex` matches no real attack (out of range, or a `None` slot the loop skips) is silently ignored → that attack takes full damage with no warning. Not reachable from `BlockTargetingPanel` (emits only valid indices); the record has no validation, so synthetic providers/tests could construct one. [scripts/combat/CombatResolver.cs:ResolveBlockPhase] — deferred, controlled construction; panel only emits valid indices (edge)
- [x] [Review][Defer] **Subscription survives abnormal scene teardown → disposed-node callback** — `ResourcesChanged` is unsubscribed only in `Complete()`/`ShowAndAwait` re-arm. `GameState` is a plain C# object (not freed with the tree); if the panel is freed mid-await, a later `ResourcesChanged` hits the disposed panel → `ObjectDisposedException`. No `_ExitTree`/`NOTIFICATION_PREDELETE` unsubscribe. Mirrors `RangedTargetingPanel`; cross-cutting scene-teardown concern logged in prior reviews (3-2b). [scripts/ui/components/BlockTargetingPanel.cs] — deferred, cross-cutting teardown; app-lifetime singleton today (edge)

**Doc-tracking (auditor, non-code):** Task/Subtask checkboxes above are still `[ ]` despite the work being done — tick after manual verification. AC4 manual-verification run (WSL desktop: panel appears as bottom-sheet, live pool, declare/pass, phase transition) is John's step and is **not yet recorded** — required before `done`.

### Manual Verification (John, WSL desktop, 2026-07-01)

Verified on device across several runs (clean `dotnet build` before each launch — a stale Godot build initially masked the block panel; rebuilding fixed it):

- **AC1–AC4 pipeline:** dev combat → interstitial (Minotaur, Armor 5, Physical 5, Brutal) → Begin → ranged Pass → **block panel appears as a bottom-sheet, hand still reachable underneath** → played Improvisation (Block 3) + 2 cards sideways (Block 1 each) → block pool rose live → returned to Movement, zero exceptions. Ran multiple times, re-entrancy clean.
- **AC5 SpinBox picker:** confirmed the per-type SpinBox appeared and its max climbed live with the pool; could allocate a chosen amount and commit.
- **AC6a (no-friction):** single-attack Minotaur showed a **plain "Block" button, no SpinBox**. ✓
- **AC6b (auto-close):** committing enough block to fully block the attack **closed the panel automatically — no Pass needed**; partial block left it open. ✓

John: "Yep, it worked perfectly." Temporary `[Block]` diagnostic log (added to localise the stale-build issue) removed after verification. Final suite: **287/287 green**.
