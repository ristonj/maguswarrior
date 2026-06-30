# Story 3.2b: Live-Wire Ranged Phase Against an Actual Enemy Token

Status: done

## Story

As a player,
I want to tap a dev button to trigger combat against an actual enemy token, see its stats before fighting, play Ranged/Siege cards from my hand, declare an attack, and see whether the token was defeated,
so that the resolver-core ranged phase from story 3-2 is exercised end-to-end in the live game.

## Acceptance Criteria

**Scope boundary:** This story wires the live game end-to-end for the ranged phase only. It adds `EnemyLoader` (reads `data/enemies.yaml` into typed `EnemyTokenDefinition`s), `AttackBridge` (converts `GameState.AttackPool` ranged/siege entries into `RangedAttackDeclaration` contributions), replaces the fake "Combat: Start/Leave" dev buttons with a `ResolveCombat` call on an actual `CombatGroup`, implements `ShowStartOfCombatInterstitial` and `PromptHeroRangedAttacks` as minimal Godot panels, and adds `TearDownCombatState` to the resolver. It does NOT implement Block (Phase 2), Assign Damage (Phase 3), or Melee (Phase 4) — those are stories 3-3 through 3-5. It does NOT implement enemy piles, garrison reveal, or rampaging enemy provocation — those are Epic 4. Multi-enemy combined targeting UI is deferred; the resolver already supports it (3-2 AC3) but the panel exposes single-target declarations only.

---

**AC1 — `TokenColor.White` added; `EnemyLoader` loads `data/enemies.yaml` (TDD, red-first)**

`TokenColor.White` is added to `scripts/core/types/TokenColor.cs`. The YAML contains `white` enemies (city defenders / ruins encounters) and the enemy-token LLD §1.1 lists it.

`EnemyLoader` is a new pure C# static class in `scripts/combat/EnemyLoader.cs`. It follows the `CardLoader` pattern exactly:
- `LoadAll(string yamlPath)` reads from the filesystem — used by tests with an absolute path
- `ParseAll(string yamlContent)` deserializes from a string — used at runtime via `Godot.FileAccess`
- Internal YAML DTO (`YamlEnemyEntry`) with mutable properties matching YAML key names; `UnderscoredNamingConvention`; `IgnoreUnmatchedProperties()` (YAML `count` and any future fields must not throw)
- Maps DTO → `EnemyTokenDefinition`

Required mappings:

| YAML field | Maps to |
|---|---|
| `id`, `name`, `armor` | direct |
| `fame` | `FameValue` |
| `color` string | `Enum.Parse<TokenColor>(value, ignoreCase: true)` — YAML values now match enum names exactly (`green`, `gray`, `brown`, `red`, `violet`, `white`) |
| `attacks[*]` | `IReadOnlyList<EnemyAttack>`: `value→Value`, `attack_type` string → `AttackType` via case-insensitive parse except `cold_fire→ColdFire` (underscore) and `summon→None` (name mismatch) |
| `resistances` list | Append to `Abilities`: `physical→PhysicalResistance`, `fire→FireResistance`, `ice→IceResistance` (explicit map — suffix differs) |
| `fortified: true` | Append `EnemyAbility.Fortified` to `Abilities` |
| `attacks[*].modifiers` | Append to `Abilities` (deduped) via `Enum.Parse<EnemyAbility>(value, ignoreCase: true)` — YAML values now match enum names (`brutal`, `poison`, `swift`, `paralyze`) |
| any attack with `attack_type: summon` | Append `EnemyAbility.Summon` to `Abilities` |
| `is_rampaging` | `IsRampaging` (defaults `false` — field absent from current YAML; handle gracefully) |
| `summon:` block | `SummonBehavior` if present; `null` otherwise |
| `count` | Ignored |

Throw `InvalidOperationException` on unknown `color`, `attack_type`, or modifier string. The `Abilities` list must be deduped (two attacks each with `brutal` must not yield two `Brutal` entries).

Note: `cold_fire` and `summon` still need explicit handling (`cold_fire` has an underscore that `Enum.Parse` won't handle for `ColdFire`; `summon` maps to `AttackType.None` not `AttackType.Summon`). Everything else parses cleanly.

**Test coverage** (new `tests/unit/EnemyLoaderTest.cs`, TDD red-first):
- `LoadAll_LoadsAtLeastOneEnemy` — file loads, result non-empty
- `LoadAll_ProwlersStats` — id=prowlers, name="Prowlers", color=Green, armor=3, fame=2, one attack (Physical, value=4), Abilities empty
- `LoadAll_IroncladsPhysicalResistance` — `resistances:[physical]` → `PhysicalResistance` in Abilities
- `LoadAll_DiggersFortified` — `fortified:true` → `EnemyAbility.Fortified` in Abilities
- `LoadAll_CursedHagsPoison` — attack `modifiers:[poison]` → `EnemyAbility.Poison` in Abilities
- `LoadAll_WolfRidersSwift` — attack `modifiers:[swift]` → `EnemyAbility.Swift` in Abilities
- `LoadAll_OrcSummonersSummonAbility` — `attack_type:summon` → `EnemyAbility.Summon` in Abilities and `AttackType.None` on the attack
- `LoadAll_WhiteColorToken` — `color:white` → `TokenColor.White` (verify against `freezers` or `gunners`)
- `LoadAll_VioletColorToken` — `color:violet` → `TokenColor.Violet` (verify against `monks` or `ice_mages`)
- `LoadAll_IceGolems_DualResistance` — `resistances:[physical,ice]` → both `PhysicalResistance` and `IceResistance` in Abilities

---

**AC2 — `AttackBridge.ExtractRangedContributions` (TDD, red-first)**

`AttackBridge` is a new pure C# static class in `scripts/broker/AttackBridge.cs`:

```csharp
public static IReadOnlyList<AttackContribution> ExtractRangedContributions(
    IReadOnlyDictionary<(EffectType Distance, AttackElement Element), int> pool)
```

Returns one `AttackContribution` per pool entry where `Distance ∈ {AttackRanged, AttackSiege}` **and** `value > 0`. Mappings:
- `AttackElement` → `AttackType`: Physical→Physical, Fire→Fire, Ice→Ice, ColdFire→ColdFire (direct name match)
- `EffectType.AttackRanged → AttackDelivery.Ranged`, `EffectType.AttackSiege → AttackDelivery.Siege`
- `EffectType.AttackMelee` keys: excluded

**Test coverage** (new `tests/unit/AttackBridgeTest.cs`, TDD red-first):
- `Extract_RangedPhysical_MapsCorrectly` — `(AttackRanged, Physical, 4)` → `AttackContribution(Physical, Ranged, 4)`
- `Extract_SiegeFire_MapsCorrectly` — `(AttackSiege, Fire, 3)` → `AttackContribution(Fire, Siege, 3)`
- `Extract_IgnoresMeleeEntries` — `(AttackMelee, Physical, 5)` → not in result
- `Extract_ExcludesZeroValues` — `(AttackRanged, Ice, 0)` → not in result
- `Extract_EmptyPool_ReturnsEmpty`
- `Extract_MultipleEntries_AllMapped` — mixed pool with ranged, siege, and melee entries → only ranged/siege non-zero returned

---

**AC3 — `UIBroker` extended with delegate injection points for Godot panels**

Add two nullable delegate properties to `UIBroker` (`scripts/broker/UIBroker.cs`):

```csharp
public Func<CombatState, Task>?                                           InterstitialProvider  { get; set; }
public Func<CombatState, Task<IReadOnlyList<RangedAttackDeclaration>>>?   RangedAttackProvider  { get; set; }
```

Update the two broker methods:

```csharp
public Task ShowStartOfCombatInterstitial(CombatState combat) =>
    InterstitialProvider?.Invoke(combat) ?? Task.CompletedTask;

public virtual Task<IReadOnlyList<RangedAttackDeclaration>> PromptHeroRangedAttacks(CombatState combat) =>
    RangedAttackProvider?.Invoke(combat)
    ?? Task.FromResult<IReadOnlyList<RangedAttackDeclaration>>(new List<RangedAttackDeclaration>());
```

`PromptHeroRangedAttacks` stays `virtual` so `TestBroker` in `CombatResolverTest.cs` can still override it. Tests do not set `RangedAttackProvider`, so the override path executes and the delegate is never reached. No existing tests change.

`ShowStartOfCombatInterstitial` was previously non-virtual `Task.CompletedTask`; the new form preserves stub behavior when no provider is registered.

---

**AC4 — `GameState.ClearAttackAndBlockPools()` added**

```csharp
public void ClearAttackAndBlockPools() {
    _attackPool.Clear();
    _blockPool.Clear();
    ResourcesChanged?.Invoke();
}
```

`TearDownCombatState` (AC5) calls this so ranged/siege attack points spent during combat do not carry into subsequent turns. `StagingAreaView` refreshes automatically via `ResourcesChanged`.

---

**AC5 — `TearDownCombatState` implemented and wired into `ResolveCombat`**

Add private `TearDownCombatState(CombatState combat)` to `CombatResolver` per LLD §12.4:

```csharp
private void TearDownCombatState(CombatState combat) {
    combat.AttackPool.Clear();
    combat.BlockPool.Clear();
    combat.DamageAssignments.Clear();
    combat.AttackModifiers.Clear();
    combat.PhaseCallbacks.Clear();
    combat.ActiveInfluenceConversion = null;
    combat.ActiveMoveConversion      = null;
    foreach (var e in combat.Group.Enemies)
        e.ClearCombatModifiers();
    _state.ClearAttackAndBlockPools();
    _state.SetPhase(GamePhase.EndOfTurn);
}
```

Update `ResolveCombat`:

```csharp
public async Task<CombatResult> ResolveCombat(CombatState combat) {
    await ResolveStartOfCombat(combat);
    if (!combat.AllEnemiesDefeated) await ResolveRangedPhase(combat);
    var result = BuildResult(combat);
    TearDownCombatState(combat);
    return result;
}
```

All 227 existing `CombatResolverTest` cases must remain green after this change.

---

**AC6 — `CombatInterstitialPanel` shows the enemy token lineup and awaits "Begin Combat"**

New `scripts/ui/components/CombatInterstitialPanel.cs` (Godot `Control` subclass). Follows the `ImprovisationView` `TaskCompletionSource` pattern:

```csharp
public Task ShowAndAwait(CombatState combat) {
    _tcs = new TaskCompletionSource<bool>();
    PopulateEnemyDisplay(combat.ActiveEnemies);
    Visible = true;
    return _tcs.Task;
}

private void OnBeginCombatPressed() {
    Visible = false;
    _tcs?.TrySetResult(true);
}
```

UI (code-constructed, no `.tscn`): full-screen semi-transparent backdrop (ZIndex 5), title label "Combat: Prepare", per-enemy row showing name / armor / attack type + value / abilities (comma-separated, or "—" if empty), and a "Begin Combat" button. Not visible on `_Ready`.

`PlaceholderMainMenu._Ready()` creates the panel, adds it as a child, and sets:
```csharp
_broker.InterstitialProvider = c => _combatInterstitialPanel.ShowAndAwait(c);
```

---

**AC7 — `RangedTargetingPanel` shows the enemy token and available attacks; player declares or passes**

New `scripts/ui/components/RangedTargetingPanel.cs` (Godot `Control` subclass):

```csharp
public Task<IReadOnlyList<RangedAttackDeclaration>> ShowAndAwait(CombatState combat, GameState state) {
    _combat = combat; _state = state;
    _tcs = new TaskCompletionSource<IReadOnlyList<RangedAttackDeclaration>>();
    _declarations     = new List<RangedAttackDeclaration>();
    _declaredContribs = new List<AttackContribution>();
    _state.ResourcesChanged += RefreshAttackDisplay;
    RefreshEnemyDisplay();
    RefreshAttackDisplay();
    Visible = true;
    return _tcs.Task;
}

private void Complete() {
    _state.ResourcesChanged -= RefreshAttackDisplay;   // always unsubscribe before resolving TCS
    Visible = false;
    _tcs?.TrySetResult(_declarations);
}
```

UI: enemy list (name, armor, fortification note if level > 0), current undeclared ranged/siege resources updated live via `ResourcesChanged`, one "Declare vs [name]" button per active enemy (disabled when no undeclared resources remain), and a "Pass" button (always enabled). Not visible on `_Ready`.

**Declare flow:** player taps "Declare vs Enemy X":
1. Compute `available = AttackBridge.ExtractRangedContributions(_state.AttackPool)` minus already-declared (match by type+delivery+value)
2. If `available` is empty → ignore (stale tap)
3. Otherwise: add `new RangedAttackDeclaration(available, [enemyX])` to `_declarations`; append `available` to `_declaredContribs`; call `RefreshAttackDisplay`

**Pass / phase end:** "Pass" calls `Complete()` immediately.

`PlaceholderMainMenu._Ready()` creates the panel, adds it as a child, and sets:
```csharp
_broker.RangedAttackProvider = c => _rangedTargetingPanel.ShowAndAwait(c, _state);
```

---

**AC8 — Dev trigger wired: actual `CombatGroup` constructed, undo gate tripped, `ResolveCombat` called**

In `PlaceholderMainMenu._Ready()`:
1. Load enemy tokens: `var content = Godot.FileAccess.GetFileAsString("res://data/enemies.yaml"); _enemyDefs = EnemyLoader.ParseAll(content);`
2. Create `_broker = new UIBroker()`.
3. Create and add `_combatInterstitialPanel` and `_rangedTargetingPanel` as children; register broker delegates (see AC6, AC7).
4. Create `_combatResolver = new CombatResolver(_state, _broker, _effectScheduler, new EffectHookRegistry())`.

Replace the existing `#if DEBUG` "Combat: Start" / "Combat: Leave" button row with a single "Combat: Dev (Brown Token)" button. Handler:

```csharp
private async void OnDevCombatPressed() {
    if (!_inputLock.TryAcquire()) return;
    _inputLock.Release();   // release immediately — panels and HandView share the lock;
                            // holding it across ResolveCombat would block card plays during targeting
    var def = _enemyDefs.FirstOrDefault(e => e.Color == TokenColor.Brown)
              ?? throw new InvalidOperationException("No Brown enemy tokens loaded");
    var group  = new CombatGroup { Enemies = new List<EnemyTokenInstance> { new(def) }, IsAtFortifiedSite = false };
    var combat = new CombatState { Group = group };

    _state.TripUndoGate();   // enemy token is being revealed — undo cannot go past this point

    var result = await _combatResolver.ResolveCombat(combat);

    // TearDownCombatState left phase at EndOfTurn; return to Movement for continued dev play
    _state.SetPhase(GamePhase.Movement);

    Log.Debug("[Combat]", result.HeroWon
        ? $"Dev combat WON — {result.DefeatedEnemies.Count} token(s) defeated"
        : $"Dev combat LOST — {result.DefeatedEnemies.Count} token(s) defeated");
}
```

---

**AC9 — Full suite green**

`dotnet test tests/maguswarrior.Tests.csproj` passes — 227 existing tests + new `EnemyLoaderTest` and `AttackBridgeTest` tests. No regressions.

---

## Tasks / Subtasks

- [x] **Task 1 — `TokenColor.White` + `EnemyLoader` (TDD)** (AC: 1)
  - [x] Add `White` to `TokenColor` enum (`scripts/core/types/TokenColor.cs`)
  - [x] Write red tests in `tests/unit/EnemyLoaderTest.cs` (all 10 cases) — confirm each fails
  - [x] Implement `YamlEnemyEntry` DTO + `EnemyLoader.ParseAll` / `LoadAll` in `scripts/combat/EnemyLoader.cs` with all YAML→record mappings
  - [x] Confirm all 10 tests green

- [x] **Task 2 — `AttackBridge` (TDD)** (AC: 2)
  - [x] Write red tests in `tests/unit/AttackBridgeTest.cs` (all 6 cases)
  - [x] Implement `AttackBridge.ExtractRangedContributions` in `scripts/broker/AttackBridge.cs`
  - [x] Confirm all 6 tests green

- [x] **Task 3 — `UIBroker` delegates; `GameState.ClearAttackAndBlockPools`** (AC: 3, 4)
  - [x] Add `InterstitialProvider` and `RangedAttackProvider` to `UIBroker`; update two methods
  - [x] Add `ClearAttackAndBlockPools()` to `GameState`
  - [x] Run full suite — all 227 existing tests must remain green

- [x] **Task 4 — `TearDownCombatState` in `CombatResolver`** (AC: 5)
  - [x] Implement `TearDownCombatState(CombatState)` per AC5 spec; wire into `ResolveCombat`
  - [x] Run existing `CombatResolverTest` — all 227 tests pass

- [x] **Task 5 — `CombatInterstitialPanel`** (AC: 6)
  - [x] Implement `CombatInterstitialPanel.cs` in `scripts/ui/components/` — backdrop, enemy labels, "Begin Combat" button, TCS pattern; not visible on `_Ready`

- [x] **Task 6 — `RangedTargetingPanel`** (AC: 7)
  - [x] Implement `RangedTargetingPanel.cs` in `scripts/ui/components/` — enemy list, live attack resource display via `ResourcesChanged`, "Declare vs X" buttons (disabled when no undeclared resources), "Pass" button, `_declaredContribs` tracking, `Complete()` with event unsubscribe before TCS resolution

- [x] **Task 7 — `PlaceholderMainMenu` wiring + dev trigger** (AC: 8)
  - [x] Add `_enemyDefs`, `_broker`, `_combatResolver`, `_combatInterstitialPanel`, `_rangedTargetingPanel` fields
  - [x] In `_Ready()`: load enemy tokens, create broker, create panels, register delegates, create resolver
  - [x] Replace "Combat: Start/Leave" button row with "Combat: Dev (Brown Token)" button + `OnDevCombatPressed` handler per AC8 spec

- [x] **Task 8 — Full regression** (AC: 9)
  - [x] `dotnet test tests/maguswarrior.Tests.csproj` → green (243 tests)
  - [x] Manual verification (2026-06-30, WSL desktop loop): launched game → "Combat: Dev (Brown Token)" → interstitial showed Minotaur (Armor 5, Physical, Brutal) on semi-transparent backdrop → "Begin Combat" → targeting panel docked as bottom-sheet without covering hand (card tap during panel logged `phase=CombatRanged`) → no Ranged/Siege card in test hand, so "Pass" with 0 resources → `[Combat] Dev combat LOST — 0 token(s) defeated` → phase returned to Movement, staging cleared (HandView refreshed 5 cards). Ran twice; re-entrancy guard kept runs sequential. Zero exceptions.

---

## Dev Notes

### Architecture rules (non-negotiable)
- **Pure C# game logic; scene tree is render-only.** `EnemyLoader`, `AttackBridge`, `UIBroker` — no Godot types. `CombatInterstitialPanel` and `RangedTargetingPanel` are `partial class : Control` and must remain minimal wiring nodes. [Source: docs/project-context.md#the-most-important-rule]
- **Async/await; never `async void` for game logic.** `OnDevCombatPressed` is a Godot signal handler — `async void` accepted there with `InputLock` acquired-and-released at the top, exactly as `HandView.OnPlayRequested`. [Source: docs/project-context.md#1, #2]
- **InputLock release before `ResolveCombat`:** The lock must be released before `await _combatResolver.ResolveCombat(combat)`. Holding it for the full call would block `HandView.OnPlayRequested` — which the player needs in order to play Ranged cards and build attack resources during the targeting panel. The panels are modal by ZIndex; they do not need the shared lock during their TCS await. [Source: docs/project-context.md#input-lock]
- **No per-card logic in the resolver.** Attack resources flow from card play → `GameState.AttackPool` → `AttackBridge` → `RangedAttackDeclaration`. The resolver never sees card IDs. [Source: docs/project-context.md#4]

### YAML schema observations (actual `data/enemies.yaml`)
- Color and modifier strings now match enum names exactly — use `Enum.Parse` with `ignoreCase: true`
- Exceptions requiring explicit handling: `cold_fire` (underscore → `ColdFire`), `summon` attack type (→ `AttackType.None`), `resistances` strings (suffix differs: `physical` → `PhysicalResistance`)
- `color: white` → `TokenColor.White` (new enum member added in AC1)
- No `is_rampaging` field in current YAML — default `false`
- No `reputation_value` field — deferred; `EnemyTokenDefinition` does not have it yet
- All abilities are derived from `resistances`, `fortified`, attack `modifiers`, and `attack_type: summon` — there is no explicit `abilities:` key in the YAML
- `orc_summoners` (green) and `illusionists` (purple) both use `attack_type: summon`

### AttackPool bridge and declaring resources
`GameState.AttackPool` is the card-staging pool. When the player plays a Ranged card in `CombatRanged` phase, `HandView.OnPlayRequested` fires `AttackEffect.Execute` → `GameState.AddAttackPoints`. `RangedTargetingPanel` reads this live via `ResourcesChanged`. When the player declares against an enemy token, `AttackBridge.ExtractRangedContributions` converts undeclared pool entries into contributions for the `RangedAttackDeclaration`. The panel tracks what it has already declared in `_declaredContribs` (local list; does NOT mutate `GameState.AttackPool`). After combat ends, `TearDownCombatState` clears the pool via `ClearAttackAndBlockPools`.

### InputLock and card plays during the targeting phase
The targeting panel is visible and the game is in `CombatRanged` phase. `HandView` is also visible. The player can tap cards — `PhaseGate` already blesses `AttackRanged` and `AttackSiege` in `CombatRanged`, so Ranged/Siege cards play normally and add resources. The targeting panel refreshes via `ResourcesChanged`. The "Declare" buttons enable. This interleaving is the desired flow.

`HandView.OnPlayRequested` acquires `InputLock` on each play and releases it in `finally` — these are short synchronous acquisitions. The dev trigger handler already released the lock before calling `ResolveCombat`, so HandView's acquisitions succeed freely.

### Dev trigger: which enemy token is used
`_enemyDefs.FirstOrDefault(e => e.Color == TokenColor.Brown)` returns `minotaur` (Armor 5, Physical + Brutal). With the default test hand (`improvisation` + 3 other non-Wound cards + 1 Wound), the player may not have Ranged cards available. Tapping "Pass" with 0 resources still exercises the pipeline correctly — the resolver receives empty declarations, no token is defeated, `HeroWon = false`, log shows LOST. That is a valid first run. A real Ranged card play + declare + win path can be verified with whatever cards are in the test hand.

### Undo gate during the ranged phase
`TripUndoGate` clears `UndoController`. Subsequent card plays in the ranged phase push onto the now-empty stack. If the player taps Undo during the ranged phase, the pre-play snapshot is restored — the resource disappears from the targeting panel (via `ResourcesChanged`). Blocking post-gate undo (e.g., hiding the Undo button during combat) is deferred.

### What NOT to do in this story
- Do **not** implement Phases 2–4. `ResolveCombat` still ends after the ranged phase; `BuildResult.HeroWon` is only true if all tokens are defeated in Phase 1.
- Do **not** implement enemy token piles. The dev trigger constructs `EnemyTokenInstance` directly from a definition. Pile management is Epic 4.
- Do **not** implement combined multi-enemy targeting in `RangedTargetingPanel`. The resolver supports it but the panel exposes single-target declarations only.
- Do **not** add `ReputationValue` to `EnemyTokenDefinition` — deferred until rampaging enemies are wired in Epic 4.
- Do **not** use `@export` or scene files for the new panels — construct all UI in code.

### File list for this story
- `scripts/core/types/TokenColor.cs` — add `White`
- `scripts/combat/EnemyLoader.cs` — new file
- `scripts/broker/AttackBridge.cs` — new file
- `scripts/broker/UIBroker.cs` — add delegate properties, update two methods
- `scripts/core/GameState.cs` — add `ClearAttackAndBlockPools()`
- `scripts/combat/CombatResolver.cs` — add `TearDownCombatState`, update `ResolveCombat`
- `scripts/ui/components/CombatInterstitialPanel.cs` — new file
- `scripts/ui/components/RangedTargetingPanel.cs` — new file
- `scripts/ui/screens/PlaceholderMainMenu.cs` — add fields, wiring, replace dev trigger button
- `tests/unit/EnemyLoaderTest.cs` — new file (10 tests)
- `tests/unit/AttackBridgeTest.cs` — new file (6 tests)

### Previous story intelligence (3-2)
All resolver-core logic (AC1–AC10) is implemented and green. `ResolveRangedPhase` handles fortification, union-resistance, modifier chain, all-or-nothing defeat, empty-target guard, and phantom-defeat guard. Do not reimplement any of it.

`TestBroker` (inner class of `CombatResolverTest`) overrides `virtual PromptHeroRangedAttacks`. Adding `RangedAttackProvider` must not break this — because `TestBroker` overrides the virtual method, the delegate check in the base is never reached from tests.

`HandView.BuildEffect` already handles `AttackSiege` (3-2 AC9). Playing Siege cards in `CombatRanged` phase will correctly add to `GameState.AttackPool` and appear in `AttackBridge.ExtractRangedContributions`.

### References
- [Source: docs/combat-flow-lld.md#7-phase-0-start-of-combat] — `ShowStartOfCombatInterstitial` contract
- [Source: docs/combat-flow-lld.md#8-phase-1-ranged-attack-phase] — `PromptHeroRangedAttacks` contract
- [Source: docs/combat-flow-lld.md#12.4-teardowncombatstate] — `TearDownCombatState` full spec
- [Source: docs/enemy-token-lld.md#1-data-model] — `TokenColor` (§1.1), `EnemyAbility` (§1.2), `EnemyTokenDefinition` (§1.4)
- [Source: data/enemies.yaml] — actual YAML schema (purple→Violet, white→White, swiftness→Swift)
- [Source: scripts/cards/CardLoader.cs] — `ParseAll`/`LoadAll` split, YamlDotNet DTO pattern
- [Source: scripts/ui/components/ImprovisationView.cs] — `TaskCompletionSource` panel pattern
- [Source: scripts/ui/components/HandView.cs] — `InputLock` acquire-release pattern, `async void` handler
- [Source: scripts/broker/UIBroker.cs] — current broker; `virtual PromptHeroRangedAttacks`
- [Source: docs/project-context.md#input-lock] — shared lock semantics

---

## Dev Agent Record

### Agent Model Used
claude-sonnet-4-6

### Debug Log References
- Build error: `AppContext` missing `using System;` in test file — added.
- YAML path had 5 `..` instead of 4 — corrected to match `EffectSystemTest.cs` pattern.
- `TokenColor.White` already present from a pre-story patch — skipped enum add, kept test.

### Completion Notes List
- Task 1: `EnemyLoader` uses `Enum.Parse ignoreCase` for colors and modifiers. Explicit switch only for `cold_fire` (underscore) and `summon → AttackType.None`. Resistances need explicit map (`physical → PhysicalResistance`). 10 tests green.
- Task 2: `AttackBridge` uses direct enum cast `(AttackType)(int)element` — `AttackElement` and `AttackType` share identical integer ordinates. 6 tests green.
- Task 3: `UIBroker` now has `InterstitialProvider` / `RangedAttackProvider` nullable delegates. `PromptHeroRangedAttacks` stays `virtual` — `TestBroker` override path in `CombatResolverTest` is unaffected because it never reaches the base-class delegate check. `ClearAttackAndBlockPools` fires `ResourcesChanged` so `StagingAreaView` clears automatically.
- Task 4: `TearDownCombatState` wired into `ResolveCombat` after `BuildResult`. All 227 prior tests still green.
- Task 5+6: Panels constructed in code (no .tscn). TCS completed by button handlers. `RangedTargetingPanel` unsubscribes `ResourcesChanged` before resolving TCS to prevent double-firing after panel hides.
- Task 7: `PlaceholderMainMenu` fully wired. `OnDevCombatPressed` releases InputLock immediately before `await ResolveCombat` so HandView card plays work during the targeting panel. `TripUndoGate` called before `ResolveCombat`; phase restored to Movement after.
- Task 8: 243 tests green (227 existing + 10 EnemyLoader + 6 AttackBridge). Manual verification pending.

### File List
- `scripts/core/types/TokenColor.cs` — `White` already present (pre-story patch)
- `scripts/combat/EnemyLoader.cs` — new file
- `scripts/broker/AttackBridge.cs` — new file
- `scripts/broker/UIBroker.cs` — added delegate properties, updated two methods
- `scripts/core/GameState.cs` — added `ClearAttackAndBlockPools()`
- `scripts/combat/CombatResolver.cs` — added `TearDownCombatState`, updated `ResolveCombat`
- `scripts/ui/components/CombatInterstitialPanel.cs` — new file
- `scripts/ui/components/RangedTargetingPanel.cs` — new file
- `scripts/ui/screens/PlaceholderMainMenu.cs` — added fields, combat wiring, replaced dev trigger
- `tests/unit/EnemyLoaderTest.cs` — new file (10 tests)
- `tests/unit/AttackBridgeTest.cs` — new file (6 tests)

### Change Log
- 2026-06-28: Implemented story 3-2b — EnemyLoader, AttackBridge, UIBroker delegates, GameState.ClearAttackAndBlockPools, TearDownCombatState, CombatInterstitialPanel, RangedTargetingPanel, PlaceholderMainMenu combat wiring. 243 tests green.

### Review Findings

Adversarial code review (Opus 4.8, 3 parallel layers — Blind Hunter, Edge Case Hunter, Acceptance Auditor) on 2026-06-28. 1 decision, 6 patch, 5 deferred, 5 dismissed as noise.

- [x] [Review][Patch] RangedTargetingPanel must not cover/block HandView — render as a bottom-sheet — (Resolved from Decision, 2026-06-28: John chose the **bottom-sheet** layout.) The panel builds a full-rect `PanelContainer` at `ZIndex=5` (opaque, default `mouse_filter=Stop`), exactly like the intentionally-modal `ImprovisationView`. HandView sits at `ZIndex=0` (staging at 1). The panel therefore renders over and intercepts input across the hand, so the player cannot play Ranged/Siege cards while targeting — which is the entire point of AC7/AC8 and the Dev-Notes "card plays during the targeting phase" flow (and why `OnDevCombatPressed` releases InputLock immediately). Fix: dock the panel as a strip between the map and HandView (anchored above the bottom hand region), not a full-rect modal — HandView and the panel both visible and tappable. The root Control must not eat input over the hand (no opaque full-rect backdrop; `mouse_filter=Ignore` on any background). [scripts/ui/components/RangedTargetingPanel.cs] (auditor)
- [x] [Review][Patch] AvailableContribs double-counts when AttackPool aggregates by key — `_state.AttackPool` sums by `(Distance, Element)`, so playing a second ranged-physical card turns a declared `(Physical,Ranged,4)` into a single `(Physical,Ranged,7)` pool entry. `AvailableContribs` matches declared by exact `(Type,Delivery,Value)`, so 4≠7 fails to subtract and the full 7 is re-offered — the declared amount is double-counted. Fix: track declared as summed amount per `(Type,Delivery)` and subtract amounts, not match discrete entries. [scripts/ui/components/RangedTargetingPanel.cs:AvailableContribs] (blind+edge)
- [x] [Review][Patch] FileAccess.GetFileAsString open failure unchecked → misleading error — `PlaceholderMainMenu` reads `res://data/enemies.yaml` via `GetFileAsString`, which returns `""` on open failure instead of throwing; the empty string deserializes to null and throws "enemies.yaml deserialized to null" far from the real cause. Use `FileAccess.Open` + `GetOpenError()` like `GameState.LoadCardsOrThrow`. [scripts/ui/screens/PlaceholderMainMenu.cs:~232] (blind)
- [x] [Review][Patch] OnDevCombatPressed async void can crash on unobserved exceptions — `async void` handler throws on `ResolveCombat` faults (and the `?? throw` if no Brown token); an async-void exception is unobservable and propagates as an unhandled crash, leaving phase stuck at EndOfTurn. Wrap the body in try/catch with `Log.Error` + phase reset. [scripts/ui/screens/PlaceholderMainMenu.cs:OnDevCombatPressed] (blind+edge)
- [x] [Review][Patch] Dev combat trigger is re-entrant; panel TCS overwritten on re-show — Lock is released immediately (per spec), so a second "Combat: Dev" press spawns a concurrent `ResolveCombat` + second interstitial, and `ShowAndAwait` reassigns `_tcs` without completing the prior, stranding the first awaiter. Add a `_combatInProgress` guard around the dev handler and guard TCS overwrite in both panels. [scripts/ui/screens/PlaceholderMainMenu.cs, scripts/ui/components/*Panel.cs] (blind+edge)
- [x] [Review][Patch] Interstitial backdrop opaque, AC6 says semi-transparent — `CombatInterstitialPanel` uses a default-themed opaque `PanelContainer`; AC6 specifies "full-screen semi-transparent backdrop". Set `Modulate` alpha or a transparent stylebox. (The targeting panel's backdrop is handled by the bottom-sheet re-layout above.) [scripts/ui/components/CombatInterstitialPanel.cs] (auditor)
- [x] [Review][Patch] EnemyLoader.BuildAbilities has a dead `attacks` parameter — `BuildAbilities` takes `IReadOnlyList<EnemyAttack> attacks` but only ever reads `rawAttacks`; the caller computes and passes `attacks` needlessly. Remove the parameter and the call-site argument. [scripts/combat/EnemyLoader.cs:BuildAbilities] (blind)
- [x] [Review][Defer] Modifier parse accepts any EnemyAbility name, not just modifiers — `Enum.TryParse<EnemyAbility>` on attack `modifiers` will silently accept `Fortified`/`Summon`/`PhysicalResistance` or other non-modifier names; restrict to `{poison,brutal,swift,paralyze}`. Controlled startup YAML, low risk. [scripts/combat/EnemyLoader.cs:BuildAbilities] — deferred, controlled data (blind+edge)
- [x] [Review][Defer] No validation of required numeric fields (armor/fame default to 0) — A YAML entry omitting `armor`/`fame` silently yields a 0-stat enemy; unlike `id`/`color` these are never validated. Controlled startup YAML. [scripts/combat/EnemyLoader.cs] — deferred, controlled data (blind+edge)
- [x] [Review][Defer] summon attack emitted as a None-typed EnemyAttack in the Attacks list — `attack_type: summon` maps to `AttackType.None` but `BuildAttacks` still constructs `EnemyAttack(value, None)`; summoner attack semantics belong to combat phases 2–4 (deferred). [scripts/combat/EnemyLoader.cs] — deferred, phases 2–4 out of scope (blind)
- [x] [Review][Defer] Loaded enemy defs live on the UI node, not GameState — `PlaceholderMainMenu._enemyDefs` holds the parsed list; project-context "Data Access" says runtime data lives in `GameState`. Sanctioned dev scaffolding (enemy piles are Epic 4). [scripts/ui/screens/PlaceholderMainMenu.cs] — deferred, dev scaffolding; piles are Epic 4 (auditor)
- [x] [Review][Defer] ResourcesChanged unsubscribe only on Pass; no _ExitTree safety net — `RangedTargetingPanel` unsubscribes only in `Complete()`; a freed panel would leave a live delegate into a disposed Control. Panels are app-lifetime singletons in scaffolding so unreachable today; part of the codebase-wide scene-teardown concern logged in prior reviews. [scripts/ui/components/RangedTargetingPanel.cs] — deferred, cross-cutting teardown (blind+edge)
