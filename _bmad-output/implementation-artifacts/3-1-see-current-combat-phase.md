# Story 3-1 — See Current Combat Phase

**Epic:** 3 — Combat System
**Story ID:** 3-1
**Status:** done
**Created:** 2026-06-20
**Dependencies:** 3-0b (UndoController, pure-C# game logic pattern — done)
**Reviewer model:** Use Opus 4.8 for code review (per project feedback convention)

---

## User Story

As a player entering combat, I can see the current combat phase name in the HUD and my
hand greys out cards whose primary effect is not legal in that phase, so I know at a
glance what I can play without opening each card.

---

## Context

### What this story delivers

Epic 3 is the combat system. Before any phase resolution (3-2 ranged attack, 3-3 block,
etc.), the infrastructure must exist and the phase display must work. This story:

1. Creates all combat data types (`CombatState`, `CombatGroup`, `EnemyTokenInstance`, etc.)
2. Creates `CombatResolver` skeleton with phase transition logic
3. Creates minimal `UIBroker` stub (resolves immediately — real UI comes in later stories)
4. Wires a dev trigger to start/leave a test "combat" in the game
5. Shows the current phase name in the HUD
6. Greys out cards whose primary effect is illegal in the current phase

### What NOT to do in this story

- Do NOT implement Phase 1 (Ranged Attack) resolution — that is story 3-2
- Do NOT implement Phase 2–4 resolution — stories 3-3, 3-4, 3-5
- Do NOT load enemies from `data/enemies.yaml` — inline test data is sufficient for 3-1
- Do NOT implement `UnitDamageLocked`, ruins context, or Summon — out of scope
- Do NOT create `data/enemies.yaml` or `EnemyFactory` yet
- Do NOT remove `StagingManager` — confirmed for reuse in combat (see deferred-work.md)

### How combat phase display fits into the existing game loop

- `GameState.CurrentPhase` already exists and is part of `GameStateSnapshot`
- `PhaseGate.IsLegal(effectType, phase)` already covers all combat phases
- `CardCompact.IsCardPlayable` is currently a stub returning `true` for all cards — this story fixes it
- `SidewaysRule.GetEffect(phase)` returns `null` for `CombatRanged` and `CombatAssignDamage` — no sideways play in those phases
- `CardCompact` controls visual opacity (grey = primary effect not useful); cards remain tappable for sideways play in phases where it is available

### Dev trigger design (story 3-1 only)

Two placeholder buttons on `PlaceholderMainMenu`:
- **"Combat: Start"** → `_state.SetPhase(GamePhase.CombatRanged)` — skips CombatStart, goes straight to Ranged Attack phase for demonstration
- **"Combat: Leave"** → `_state.SetPhase(GamePhase.Movement)` — exits back to movement

This is intentionally a `SetPhase` shortcut, NOT a full `CombatResolver.ResolveCombat` call. The resolver is tested exclusively via TDD in this story. Wiring the resolver to the game flow starts in story 3-2 when actual ranged attack resolution is implemented.

### `PhaseChanged` event pattern

`GameState` gains a `PhaseChanged` event following the exact same pattern as the existing `DayNightChanged` event. `SetPhase` fires it (with a no-op guard when phase doesn't change). `RestoreSnapshot` fires it when the restored phase differs from the current phase. Both `StagingAreaView` and `HandView` subscribe to `PhaseChanged`.

### `AttackElement` vs. `AttackType` — two separate types

`AttackElement` (`scripts/core/types/AttackElement.cs`) is the existing enum used by the card effect system's running totals in `GameState._attackPool` and `AttackEffect`. `AttackType` (new, from `combat-flow-lld.md` and `enemy-token-lld.md`) is the combat system's contribution model enum. They have the same base values (Physical/Fire/Ice/ColdFire) but `AttackType` adds `None` (for Summon enemies). They serve different domains and must stay separate. The bridging between card effects → combat contributions is story 3-2's concern.

---

## Acceptance Criteria

**AC1 — New combat infrastructure types compile and are pure C# (TDD, red-first)**
All new files in `scripts/combat/`, `scripts/broker/`, and `scripts/core/types/` compile
cleanly with no Godot inheritance. Test project builds after adding Compile globs for
`scripts/combat/**` and `scripts/broker/**`.

**AC2 — `GameState.PhaseChanged` event fires on phase transitions**
`SetPhase(phase)` fires `PhaseChanged` unless phase is already the current value (no-op guard).
`RestoreSnapshot` fires `PhaseChanged` when restored phase differs from current.
TDD: `SetPhase_FiresPhaseChangedEvent`, `SetPhase_NoOp_WhenSamePhase_DoesNotFirePhaseChanged`,
`RestoreSnapshot_FiresPhaseChangedWhenPhaseChanges`.

**AC3 — Phase indicator in HUD shows current phase**
`StagingAreaView` adds a phase label. Subscribes to `state.PhaseChanged` and updates the
label on every transition. In `CombatRanged` the label reads "Ranged Attack". In `Movement`
it reads "Movement" (or is hidden — designer's choice). In all other phases a readable name.

**AC4 — `CardCompact.IsCardPlayable` greys cards with no legal primary effect**
`IsCardPlayable` now checks `PhaseGate.IsLegal` for the card's primary, powered, and
alternate effect types. Returns `true` if ANY of those types is legal in the current phase.
Wounds are exempt (they return early in `Initialize`). Cards remain tappable even when grey.
In `CombatRanged`: Move cards grey, Attack Melee cards grey, Attack Ranged cards bright.
In `Movement`: Attack and Block cards grey, Move cards bright.

**AC5 — `HandView` re-renders cards on phase change**
`HandView.Initialize` subscribes `RefreshHand` to `state.PhaseChanged`. When the dev
trigger fires, cards immediately re-render at the new phase's legality.

**AC6 — `CombatResolver` TDD: phase management tested**
`tests/unit/CombatResolverTest.cs` exists, written RED before implementation. Minimum tests:
1. `AllEnemiesDefeated_TrueWhenActiveEnemiesEmpty`
2. `AllEnemiesDefeated_FalseWhenEnemiesPresent`
3. `ResolveStartOfCombat_SetsPhaseToStart`
4. `ResolveStartOfCombat_PopulatesActiveEnemiesFromGroup`
5. `ResolveStartOfCombat_TransitionsToCombatRangedAfterPhase0`
6. `SetPhase_UpdatesGameStateCurrentPhase`
7. `SetPhase_UpdatesCombatStateCurrentPhase`
8. `FirePhaseCallbacks_ExecutesAndRemovesMatchingCallback`

**AC7 — UIBroker stub compiles and resolves immediately**
`UIBroker.ShowStartOfCombatInterstitial(CombatState)` returns `Task.CompletedTask`.
No Godot dependency. Test project can compile `UIBroker` via the `scripts/broker/**` glob.

**AC8 — Dev trigger works: "Combat: Start" → HUD shows "Ranged Attack", cards update**
Tapping "Combat: Start" button sets phase to `CombatRanged`. HUD shows "Ranged Attack". 
Move cards visually grey. Tapping "Combat: Leave" returns to `Movement`.

**AC9 — All tests pass**
`dotnet test tests/maguswarrior.Tests.csproj` — 190 baseline + ≥11 new = **≥201 green**.

---

## Implementation Tasks

### Task 0 — Add compile globs to test project (do FIRST)

**File:** `tests/maguswarrior.Tests.csproj`

Add after the existing `scripts/map/**` entry:
```xml
<!-- scripts/combat: pure C# — glob picks up all future combat files -->
<Compile Include="../scripts/combat/**/*.cs" />
<!-- scripts/broker: pure C# — glob picks up all future broker files -->
<Compile Include="../scripts/broker/**/*.cs" />
```

Verify: `dotnet build tests/maguswarrior.Tests.csproj` passes (no new files yet, so nothing added yet — but the glob is ready for when combat files land).

---

### Task 1 — New enum types in `scripts/core/types/` (auto-included by existing glob)

These files are auto-included by `<Compile Include="../scripts/core/types/**/*.cs" />`.

**`scripts/core/types/AttackType.cs`**
```csharp
namespace MagusWarrior.Core.Types;

// Combat system contribution type — distinct from AttackElement (card effect running totals).
// None is used only for Summon enemies that have no personal attack value.
public enum AttackType { Physical, Fire, Ice, ColdFire, None }
```

**`scripts/core/types/AttackDelivery.cs`**
```csharp
namespace MagusWarrior.Core.Types;

public enum AttackDelivery { Melee, Ranged, Siege }
```

**`scripts/core/types/BlockType.cs`**
```csharp
namespace MagusWarrior.Core.Types;

public enum BlockType { Physical, Fire, Ice, ColdFire }
```

**`scripts/core/types/MoveConversionMode.cs`**
```csharp
namespace MagusWarrior.Core.Types;

public enum MoveConversionMode { AgilityUnpowered, AgilityPowered }
```

**`scripts/core/types/TokenColor.cs`**
```csharp
namespace MagusWarrior.Core.Types;

public enum TokenColor { Green, Red, Brown, Gray, Violet, White }  // White = city defenders + ruins encounters
```

**`scripts/core/types/EnemyAbility.cs`**
```csharp
namespace MagusWarrior.Core.Types;

public enum EnemyAbility {
    Fortified,
    PhysicalResistance,
    FireResistance,
    IceResistance,
    // Cold fire resistance is derived: IceResistance && FireResistance; no enum value
    Swift,
    Brutal,
    Poison,
    Paralyze,
    Summon,
    ArcaneImmunity,  // no base game tokens; stable hook point for expansion
}
```

Confirm: `dotnet build tests/maguswarrior.Tests.csproj` still passes.

---

### Task 2 — Enemy token types (TDD red-first)

**Write failing test stubs first in `tests/unit/CombatResolverTest.cs`** — just enough to
confirm compilation fails on missing types. Then create the types.

**`scripts/combat/EnemyToken.cs`**

```csharp
using System.Collections.Generic;
using MagusWarrior.Core.Types;

namespace MagusWarrior.Combat;

public record EnemyTokenDefinition(
    string                    Id,
    string                    Name,
    TokenColor                Color,
    int                       Armor,
    int                       Attack,
    AttackType                AttackType,
    int                       FameValue,
    IReadOnlyList<EnemyAbility> Abilities,
    bool                      IsRampaging,
    SummonBehavior?           Summon
);

public record SummonBehavior(TokenColor Color, int Count);

public class EnemyTokenInstance {
    public EnemyTokenDefinition Definition { get; }
    public int  BaseArmor    => Definition.Armor;
    public int  ArmorModifier { get; set; } = 0;
    public int  AttackModifier { get; set; } = 0;
    public bool AttackCancelled { get; set; } = false;
    public int  WoundCount    { get; private set; } = 0;
    public bool IsDestroyed   { get; private set; } = false;

    private readonly List<EnemyAbility> _strippedAbilities = new();

    public EnemyTokenInstance(EnemyTokenDefinition definition) {
        Definition = definition;
    }

    public bool HasAbility(EnemyAbility ability) {
        if (_strippedAbilities.Contains(ability)) return false;
        return Definition.Abilities.Contains(ability);
    }

    public bool HasResistanceTo(AttackType type) => type switch {
        AttackType.Physical  => HasAbility(EnemyAbility.PhysicalResistance),
        AttackType.Fire      => HasAbility(EnemyAbility.FireResistance),
        AttackType.Ice       => HasAbility(EnemyAbility.IceResistance),
        AttackType.ColdFire  => HasAbility(EnemyAbility.FireResistance) && HasAbility(EnemyAbility.IceResistance),
        _                    => false
    };

    public void TakeWound()    { WoundCount++; }
    public void Destroy()      { IsDestroyed = true; }
    public void StripAbility(EnemyAbility ability) => _strippedAbilities.Add(ability);

    public void ClearCombatModifiers() {
        ArmorModifier     = 0;
        AttackModifier    = 0;
        AttackCancelled   = false;
        _strippedAbilities.Clear();
    }
}
```

---

### Task 3 — Combat contribution types

**`scripts/combat/CombatContributions.cs`**

```csharp
using System.Threading.Tasks;
using MagusWarrior.Core;
using MagusWarrior.Core.Types;

namespace MagusWarrior.Combat;

public record AttackContribution(AttackType Type, AttackDelivery Delivery, int Value);
public record BlockContribution(BlockType Type, int Value);
public record DamageAssignment(EnemyTokenInstance Source, AttackType DamageType, int RawValue);

public interface ICombatAttackModifier {
    AttackContribution Modify(AttackContribution contrib);
}

public interface ICombatPhaseCallback {
    GamePhase TriggerPhase { get; }
    Task Execute(CombatState combat, UIBroker broker);
}
```

**Note:** `CombatState` and `UIBroker` are forward references — they will be in the same
namespace and assembly. Order of file compilation doesn't matter for a single project.

---

### Task 4 — `CombatGroup` and `CombatResult`

**`scripts/combat/CombatGroup.cs`**

```csharp
using System.Collections.Generic;

namespace MagusWarrior.Combat;

public class CombatGroup {
    public IReadOnlyList<EnemyTokenInstance> Enemies           { get; init; } = new List<EnemyTokenInstance>();
    public bool                              IsAtFortifiedSite { get; init; } = false;
}

public record CombatResult(
    bool                     HeroWon,
    List<EnemyTokenInstance> DefeatedEnemies,
    int                      FameEarned,
    int                      ReputationEarned
);
```

---

### Task 5 — `CombatState`

**`scripts/combat/CombatState.cs`**

```csharp
using System.Collections.Generic;
using MagusWarrior.Core.Types;

namespace MagusWarrior.Combat;

public class CombatState {
    public CombatGroup Group        { get; init; } = new();
    public bool   IsAtFortifiedSite => Group.IsAtFortifiedSite;
    public GamePhase CurrentPhase   { get; set; }

    public List<EnemyTokenInstance>    ActiveEnemies    { get; } = new();
    public List<EnemyTokenInstance>    DefeatedEnemies  { get; } = new();
    public List<AttackContribution>    AttackPool       { get; } = new();
    public List<BlockContribution>     BlockPool        { get; } = new();
    public List<DamageAssignment>      DamageAssignments { get; } = new();
    public List<ICombatAttackModifier> AttackModifiers  { get; } = new();
    public List<ICombatPhaseCallback>  PhaseCallbacks   { get; } = new();

    public bool           UnitDamageLocked         { get; set; }
    public bool           SkipBlockAndDamagePending { get; set; }
    public MoveConversionMode? ActiveMoveConversion { get; set; }
    public BlockType?     ActiveInfluenceConversion { get; set; }

    public int FameEarned       { get; set; }
    public int ReputationEarned { get; set; }

    public bool AllEnemiesDefeated => ActiveEnemies.Count == 0;
}
```

---

### Task 6 — `UIBroker` (stub for this story)

**`scripts/broker/UIBroker.cs`**

```csharp
using System.Threading.Tasks;
using MagusWarrior.Combat;

namespace MagusWarrior.Broker;

// Minimal stub — methods return immediately; real UI panels wired in later stories.
// All methods must remain pure C# (no Godot types) so the test project can compile this file.
public class UIBroker {
    public Task ShowStartOfCombatInterstitial(CombatState combat) => Task.CompletedTask;
    // Placeholder stubs for future stories:
    public Task PromptHeroRangedAttacks(CombatState combat)  => Task.CompletedTask;
    public Task ResolveEnemyAttackVsHero(EnemyTokenInstance enemy, CombatState combat) => Task.CompletedTask;
    public Task PromptHeroMeleeAttacks(CombatState combat)   => Task.CompletedTask;
}
```

---

### Task 7 — `CombatResolver` skeleton (TDD — write tests BEFORE implementation)

**Test file (write RED first):** `tests/unit/CombatResolverTest.cs`

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;
using MagusWarrior.Broker;
using MagusWarrior.Cards;
using MagusWarrior.Cards.Effects;
using MagusWarrior.Combat;
using MagusWarrior.Core;
using MagusWarrior.Core.Types;
using Xunit;

namespace MagusWarrior.Tests;

public class CombatResolverTest {
    private static GameState EmptyState() => new(new List<CardDefinition>());
    private static UIBroker  StubBroker() => new();

    private static CombatResolver MakeResolver(GameState state) =>
        new(state, StubBroker(), new EffectScheduler(), new EffectHookRegistry());

    private static EnemyTokenInstance TestEnemy() =>
        new(new EnemyTokenDefinition("test", "Test Enemy", TokenColor.Brown,
            4, 2, AttackType.Physical, 1, new List<EnemyAbility>(), false, null));

    private static CombatGroup SingleEnemyGroup() =>
        new() { Enemies = new List<EnemyTokenInstance> { TestEnemy() } };

    [Fact]
    public void AllEnemiesDefeated_TrueWhenActiveEnemiesEmpty() {
        var combat = new CombatState { Group = SingleEnemyGroup() };
        Assert.True(combat.AllEnemiesDefeated);
    }

    [Fact]
    public void AllEnemiesDefeated_FalseWhenEnemiesPresent() {
        var combat = new CombatState { Group = SingleEnemyGroup() };
        combat.ActiveEnemies.Add(TestEnemy());
        Assert.False(combat.AllEnemiesDefeated);
    }

    [Fact]
    public async Task ResolveStartOfCombat_SetsPhaseToStart() {
        var state = EmptyState();
        var resolver = MakeResolver(state);
        var combat = new CombatState { Group = SingleEnemyGroup() };

        await resolver.ResolveStartOfCombat(combat);

        // After ResolveStartOfCombat, GameState is in CombatRanged
        // (Phase0 transitions to CombatStart then CombatRanged in this stub)
        Assert.Equal(GamePhase.CombatStart, combat.CurrentPhase);
    }

    [Fact]
    public async Task ResolveStartOfCombat_PopulatesActiveEnemiesFromGroup() {
        var state = EmptyState();
        var resolver = MakeResolver(state);
        var combat = new CombatState { Group = SingleEnemyGroup() };

        await resolver.ResolveStartOfCombat(combat);

        Assert.Single(combat.ActiveEnemies);
    }

    [Fact]
    public void SetPhase_UpdatesGameStateCurrentPhase() {
        var state = EmptyState();
        var resolver = MakeResolver(state);
        var combat = new CombatState { Group = new CombatGroup() };

        resolver.SetPhase(GamePhase.CombatRanged, combat);

        Assert.Equal(GamePhase.CombatRanged, state.CurrentPhase);
    }

    [Fact]
    public void SetPhase_UpdatesCombatStateCurrentPhase() {
        var state = EmptyState();
        var resolver = MakeResolver(state);
        var combat = new CombatState { Group = new CombatGroup() };

        resolver.SetPhase(GamePhase.CombatBlock, combat);

        Assert.Equal(GamePhase.CombatBlock, combat.CurrentPhase);
    }

    [Fact]
    public async Task FirePhaseCallbacks_ExecutesAndRemovesMatchingCallback() {
        var state = EmptyState();
        var resolver = MakeResolver(state);
        var combat = new CombatState { Group = new CombatGroup() };
        bool executed = false;
        combat.PhaseCallbacks.Add(new TestCallback(GamePhase.CombatMelee, () => executed = true));

        await resolver.FirePhaseCallbacks(GamePhase.CombatMelee, combat);

        Assert.True(executed);
        Assert.Empty(combat.PhaseCallbacks);  // callback removed after firing
    }

    [Fact]
    public async Task FirePhaseCallbacks_IgnoresNonMatchingCallbacks() {
        var state = EmptyState();
        var resolver = MakeResolver(state);
        var combat = new CombatState { Group = new CombatGroup() };
        bool executed = false;
        combat.PhaseCallbacks.Add(new TestCallback(GamePhase.CombatBlock, () => executed = true));

        await resolver.FirePhaseCallbacks(GamePhase.CombatMelee, combat);  // wrong phase

        Assert.False(executed);
        Assert.Single(combat.PhaseCallbacks);  // not removed
    }

    private class TestCallback : ICombatPhaseCallback {
        private readonly Action _action;
        public GamePhase TriggerPhase { get; }
        public TestCallback(GamePhase phase, Action action) { TriggerPhase = phase; _action = action; }
        public Task Execute(CombatState combat, UIBroker broker) { _action(); return Task.CompletedTask; }
    }
}
```

Confirm RED: these tests fail because `CombatResolver`, `SetPhase`, etc. don't exist yet.

**`scripts/combat/CombatResolver.cs`**

```csharp
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MagusWarrior.Broker;
using MagusWarrior.Cards.Effects;
using MagusWarrior.Core;
using MagusWarrior.Core.Types;

namespace MagusWarrior.Combat;

public class CombatResolver {
    private readonly GameState          _state;
    private readonly UIBroker           _broker;
    private readonly EffectScheduler    _scheduler;
    private readonly EffectHookRegistry _hooks;

    public CombatResolver(GameState state, UIBroker broker,
                          EffectScheduler scheduler, EffectHookRegistry hooks) {
        (_state, _broker, _scheduler, _hooks) = (state, broker, scheduler, hooks);
    }

    // Phase 0 — Start of Combat
    public async Task ResolveStartOfCombat(CombatState combat) {
        SetPhase(GamePhase.CombatStart, combat);
        combat.ActiveEnemies.AddRange(combat.Group.Enemies);
        await FirePhaseCallbacks(GamePhase.CombatStart, combat);
        await _broker.ShowStartOfCombatInterstitial(combat);
    }

    public void SetPhase(GamePhase phase, CombatState combat) {
        combat.CurrentPhase = phase;
        _state.SetPhase(phase);
    }

    public async Task FirePhaseCallbacks(GamePhase phase, CombatState combat) {
        var callbacks = combat.PhaseCallbacks
            .Where(cb => cb.TriggerPhase == phase)
            .ToList();
        foreach (var cb in callbacks) {
            combat.PhaseCallbacks.Remove(cb);
            await cb.Execute(combat, _broker);
        }
    }

    // Stubs — to be filled in stories 3-2 through 3-5
    public async Task<CombatResult> ResolveCombat(CombatState combat) {
        await ResolveStartOfCombat(combat);
        // Phase 1–4 resolution added per-story in 3-2, 3-3, 3-4, 3-5
        return BuildResult(combat);
    }

    private CombatResult BuildResult(CombatState combat) =>
        new(
            HeroWon:          combat.ActiveEnemies.Count == 0,
            DefeatedEnemies:  new List<EnemyTokenInstance>(combat.DefeatedEnemies),
            FameEarned:       combat.FameEarned,
            ReputationEarned: combat.ReputationEarned
        );
}
```

Run GREEN: all tests in `CombatResolverTest.cs` pass.

**Note on `SetPhase` visibility:** `SetPhase` is `public` here so `CombatResolverTest` can call it
directly to verify both state updates. The architecture docs show it as package-private-equivalent,
but pure C# has no package scope — `public` is the correct choice for testability.

---

### Task 8 — Add `PhaseChanged` event to `GameState`

**File:** `scripts/core/GameState.cs`

Add event declaration (after `UndoGateCrossed`):
```csharp
public event Action? PhaseChanged;
```

Update `SetPhase` to fire it with a no-op guard:
```csharp
public void SetPhase(GamePhase phase) {
    if (CurrentPhase == phase) return;
    CurrentPhase = phase;
    PhaseChanged?.Invoke();
}
```

Update `RestoreSnapshot` to fire `PhaseChanged` when phase changes (same pattern as `DayNightChanged`):
```csharp
public void RestoreSnapshot(GameStateSnapshot snapshot) {
    var dayChanged   = IsDay != snapshot.IsDay;
    var phaseChanged = CurrentPhase != snapshot.CurrentPhase;
    CurrentPhase = snapshot.CurrentPhase;
    MovePointsThisTurn = snapshot.MovePointsThisTurn;
    InfluencePointsThisTurn = snapshot.InfluencePointsThisTurn;
    _attackPool = new Dictionary<(EffectType, AttackElement), int>(snapshot.AttackPool);
    _blockPool = new Dictionary<AttackElement, int>(snapshot.BlockPool);
    IsDay = snapshot.IsDay;
    ResourcesChanged?.Invoke();
    if (dayChanged)   DayNightChanged?.Invoke();
    if (phaseChanged) PhaseChanged?.Invoke();
}
```

Add to `tests/unit/GameStateTest.cs`:
```csharp
[Fact]
public void SetPhase_FiresPhaseChangedEvent() {
    var state = new GameState(new List<CardDefinition>());
    bool fired = false;
    state.PhaseChanged += () => fired = true;

    state.SetPhase(GamePhase.CombatRanged);

    Assert.True(fired);
}

[Fact]
public void SetPhase_NoOp_WhenSamePhase_DoesNotFirePhaseChanged() {
    var state = new GameState(new List<CardDefinition>());
    // Default phase is Movement
    int count = 0;
    state.PhaseChanged += () => count++;

    state.SetPhase(GamePhase.Movement);  // same as default

    Assert.Equal(0, count);
}

[Fact]
public void RestoreSnapshot_FiresPhaseChangedWhenPhaseChanges() {
    var state = new GameState(new List<CardDefinition>());
    var snap = state.TakeSnapshot();   // captures Movement
    state.SetPhase(GamePhase.CombatRanged);
    int count = 0;
    state.PhaseChanged += () => count++;

    state.RestoreSnapshot(snap);       // restores to Movement

    Assert.Equal(1, count);
}
```

Run all tests — should be GREEN.

---

### Task 9 — Phase indicator in `StagingAreaView`

**File:** `scripts/ui/components/StagingAreaView.cs`

Add `_phaseLabel` field:
```csharp
private Label _phaseLabel = null!;
```

In `_Ready()`, before `container.AddChild(_undoButton)`:
```csharp
_phaseLabel = new Label();
_phaseLabel.AddThemeFontSizeOverride("font_size", 28);
container.AddChild(_phaseLabel);
```

In `Initialize(GameState state)`:
```csharp
public void Initialize(GameState state) {
    _state = state;
    state.ResourcesChanged += Refresh;
    state.PhaseChanged += RefreshPhase;
    Refresh();
    RefreshPhase();
}
```

Add `RefreshPhase()`:
```csharp
private void RefreshPhase() {
    _phaseLabel.Text = _state.CurrentPhase switch {
        GamePhase.CombatStart       => "Combat Start",
        GamePhase.CombatRanged      => "Ranged Attack",
        GamePhase.CombatBlock       => "Block",
        GamePhase.CombatAssignDamage => "Assign Damage",
        GamePhase.CombatMelee       => "Melee Attack",
        GamePhase.Rest              => "Rest",
        GamePhase.EndOfTurn         => "End of Turn",
        GamePhase.Movement          => "Movement",
        _                           => _state.CurrentPhase.ToString()
    };
}
```

---

### Task 10 — `HandView` re-renders on phase change

**File:** `scripts/ui/components/HandView.cs`

In `Initialize(...)`, add one line after `deck.HandChanged += RefreshHand`:
```csharp
state.PhaseChanged += RefreshHand;
```

No other changes — `RefreshHand()` already reads `_state.CurrentPhase` fresh on each call.

---

### Task 11 — Fix `CardCompact.IsCardPlayable`

**File:** `scripts/ui/components/CardCompact.cs`

Replace the stub:
```csharp
// Before:
private static bool IsCardPlayable(CardDefinition card, GamePhase phase) {
    // 1b-1: return true for all cards...
    return true;
}

// After:
private static bool IsCardPlayable(CardDefinition card, GamePhase phase) {
    if (card.Unpowered != null && PhaseGate.IsLegal(card.Unpowered.EffectType, phase))
        return true;
    if (card.Powered != null && PhaseGate.IsLegal(card.Powered.EffectType, phase))
        return true;
    foreach (var alt in card.AlternateEffectTypes)
        if (PhaseGate.IsLegal(alt, phase)) return true;
    return false;
}
```

`PhaseGate` is already imported via `MagusWarrior.Cards.Effects` (check existing usings in file).
The Wound early-return above `IsCardPlayable` handles Wounds — they never reach this method.
Cards remain tappable even when grey (opacity only; `_tapTarget.Disabled` stays false).

---

### Task 12 — Dev trigger buttons on `PlaceholderMainMenu`

**File:** `scripts/ui/screens/PlaceholderMainMenu.cs`

Find where other debug buttons are added (search for existing button additions in `_Ready`).
Add two buttons in a `HBoxContainer` near the top of the screen:

```csharp
// Dev combat trigger — remove/replace when CombatResolver.ResolveCombat is wired in 3-2
var combatRow = new HBoxContainer();
combatRow.AddThemeConstantOverride("separation", 10);
// ... add to the scene tree at an appropriate position

var startCombatBtn = new Button();
startCombatBtn.Text = "Combat: Start";
startCombatBtn.AddThemeFontSizeOverride("font_size", 24);
startCombatBtn.Pressed += () => {
    _state.SetPhase(GamePhase.CombatRanged);
    Log.Debug("[UI]", "Dev: entered CombatRanged phase");
};
combatRow.AddChild(startCombatBtn);

var leaveCombatBtn = new Button();
leaveCombatBtn.Text = "Combat: Leave";
leaveCombatBtn.AddThemeFontSizeOverride("font_size", 24);
leaveCombatBtn.Pressed += () => {
    _state.SetPhase(GamePhase.Movement);
    Log.Debug("[UI]", "Dev: returned to Movement phase");
};
combatRow.AddChild(leaveCombatBtn);
```

`_state` is already available in `PlaceholderMainMenu`. Look at how other buttons are wired
(e.g., RestView buttons) and follow the same layout pattern.

---

### Task 13 — Run full test suite and fix fallout

```bash
dotnet test tests/maguswarrior.Tests.csproj
```

**Expected count: 190 baseline + ≥11 new = ≥201 green**

New tests added:
- `CombatResolverTest.cs`: 8 tests
- `GameStateTest.cs`: 3 new PhaseChanged tests

Watch for:
- Any existing test that calls `state.SetPhase(GamePhase.Movement)` when already in Movement will no-op now (the new guard). That's correct behavior — verify no test relied on the event firing when phase doesn't change.
- `RestoreSnapshot` now fires `PhaseChanged` in addition to `ResourcesChanged` — verify `UndoGateTest.cs` and `GameStateTest.cs` tests still pass cleanly.

---

## Dev Notes

### Architecture rules (non-negotiable)

- All new files in `scripts/combat/`, `scripts/broker/`, and `scripts/core/types/` must be **pure C# — no `Node` inheritance, no Godot API calls, no `#if GODOT` guards**.
- `CombatResolver` has no Godot dependency — it takes `GameState`, `UIBroker`, `EffectScheduler`, `EffectHookRegistry` via constructor. No `Initialize` pattern.
- `UIBroker` has no Godot dependency in this story. Real Godot signal wiring happens when each phase's actual UI panel is built.
- **Never register hooks inside `Execute()`** — the hook registry is startup-only. (No hooks are registered in 3-1, but don't add any ad-hoc registrations.)
- `Log.Debug` with system tag `[Combat]` in `CombatResolver`, `[UI]` in view handlers.

### `SetPhase` guard and LOCKSTEP discipline

`GameState.SetPhase` now skips the event if the phase is unchanged. This is intentional — 
it matches the `SetIsDay` pattern. Any code that previously relied on `SetPhase` firing 
unconditionally (e.g., as a "notify all observers" call with the same value) was already 
relying on incorrect behavior.

`CombatResolver.SetPhase` calls `_state.SetPhase(phase)`. If `GameState.CurrentPhase` is
already `CombatStart` and the resolver calls `SetPhase(CombatStart, combat)` again, the
`GameState` event does not fire. This is correct — the event is for transitions, not
confirmations.

### `PhaseChanged` vs `ResourcesChanged` — subscribe to the right one

`StagingAreaView.Refresh()` handles resources (Move/Attack/Block/Influence/Undo button). It
subscribes to `ResourcesChanged`. The new `RefreshPhase()` handles only the phase label and
subscribes to `PhaseChanged`. **Do NOT merge them** — `Refresh()` is cheap, but firing it on
phase changes alongside `RefreshPhase()` would be redundant. Keep them separate.

`HandView.RefreshHand()` subscribes to BOTH `deck.HandChanged` (card count changes) AND
`state.PhaseChanged` (legality changes). It does NOT subscribe to `ResourcesChanged` — that
would cause RefreshHand to fire on every move point spent, which is unnecessary and adds node
recreations on hot paths.

### `CardCompact.IsCardPlayable` — sideways is not a reason to show "playable"

`SidewaysRule.GetEffect(phase)` returns `null` for `CombatRanged` and `CombatAssignDamage`.
During those phases, a Move card has no primary effect AND no sideways play. It should grey out.
During `CombatBlock`, sideways gives 1 Block — but a Move card still has no primary Block effect,
so it greys. The grey is a hint: "your primary effect isn't useful here." Tapping still opens
`CardExpanded` and sideways is offered there if available. Do NOT check sideways in `IsCardPlayable`.

### `CombatResolver.SetPhase` is `public` for testability

The LLD shows it as private. In pure C# there is no package scope — making it `public` is the
correct trade-off. If it becomes a concern later, it can be marked `internal` with
`[assembly: InternalsVisibleTo("maguswarrior.Tests")]`.

### Test helper — `EnemyTokenInstance` construction

Tests construct `EnemyTokenDefinition` inline (no YAML, no file I/O). Pattern established
in 3-0b: use inline data, never `CardLoader.LoadAll("data/cards.yaml")` in tests
(wrong CWD, Godot-only file path).

### `ICombatPhaseCallback.Execute` signature

```csharp
Task Execute(CombatState combat, UIBroker broker);
```

The `broker` parameter is intentional — callbacks like `BurningShieldCallback` need to show
a choice prompt. In 3-1, `TestCallback` ignores `broker`. Don't simplify the interface to
`Task Execute(CombatState combat)` — future story callbacks depend on `broker`.

### Files to touch in `PlaceholderMainMenu`

Look at where `_restView`, `_inputLock`, and other components are initialized. The combat dev
buttons don't need any new fields on the class — they close over `_state` directly in the
lambda. Check that `_state` is in scope at the point you add the buttons.

---

## File Placement

| What | Where |
|------|-------|
| `AttackType`, `AttackDelivery`, `BlockType`, `MoveConversionMode`, `TokenColor`, `EnemyAbility` | `scripts/core/types/` (one file each, auto-included by existing glob) |
| `EnemyTokenDefinition`, `SummonBehavior`, `EnemyTokenInstance` | `scripts/combat/EnemyToken.cs` |
| `AttackContribution`, `BlockContribution`, `DamageAssignment`, `ICombatAttackModifier`, `ICombatPhaseCallback` | `scripts/combat/CombatContributions.cs` |
| `CombatGroup`, `CombatResult` | `scripts/combat/CombatGroup.cs` |
| `CombatState` | `scripts/combat/CombatState.cs` |
| `CombatResolver` | `scripts/combat/CombatResolver.cs` |
| `UIBroker` | `scripts/broker/UIBroker.cs` |
| Tests | `tests/unit/CombatResolverTest.cs` |

---

## Architecture Compliance Checklist

- [ ] All `scripts/combat/` and `scripts/broker/` files: no `: Node` inheritance, no `Godot` using
- [ ] `tests/maguswarrior.Tests.csproj` has `scripts/combat/**` and `scripts/broker/**` globs
- [ ] `GameState.SetPhase` fires `PhaseChanged` (with no-op guard for same phase)
- [ ] `GameState.RestoreSnapshot` fires `PhaseChanged` when phase changes (same pattern as `DayNightChanged`)
- [ ] `StagingAreaView` subscribes to `PhaseChanged` for phase label only
- [ ] `HandView` subscribes to `PhaseChanged` → `RefreshHand()` (NOT `ResourcesChanged`)
- [ ] `CardCompact.IsCardPlayable` checks `PhaseGate.IsLegal` for primary + powered + alternate types
- [ ] `CombatResolver.SetPhase` is `public` (testability)
- [ ] `EnemyTokenInstance` constructed inline in tests (no file I/O, no YAML)
- [ ] `ICombatPhaseCallback.Execute(CombatState, UIBroker)` — both params preserved
- [ ] Dev trigger uses `_state.SetPhase` directly (not CombatResolver.ResolveCombat — that's 3-2)
- [ ] No per-card ID checks in `CombatResolver` (no `if (card.Id == ...)`)
- [ ] `AttackType` is new — does NOT replace or alias `AttackElement`
- [ ] Log tags: `[Combat]` in `CombatResolver`, `[UI]` in views

---

## Definition of Done

- [ ] `CombatResolverTest.cs` written RED-first before `CombatResolver.cs` created (Task 7)
- [ ] ≥8 `CombatResolverTest.cs` tests pass GREEN
- [ ] ≥3 new `GameStateTest.cs` tests for `PhaseChanged` pass GREEN
- [ ] `dotnet build maguswarrior.csproj` — 0 errors, 0 warnings
- [ ] `dotnet test tests/maguswarrior.Tests.csproj` — **≥201 green**
- [ ] Manual WSLg verification:
  - [ ] "Combat: Start" button sets HUD phase label to "Ranged Attack"
  - [ ] Move cards visually grey in CombatRanged phase; Attack Ranged cards bright
  - [ ] "Combat: Leave" button restores "Movement" label and card opacity
  - [ ] Undo gate + undo button disabled state still work (regression)
- [x] Opus 4.8 code review passed (2026-06-20 — 1 decision resolved, 2 patches applied, 1 false positive, 6 deferred)

---

## Review Findings

_Opus 4.8 adversarial review (Blind Hunter + Edge Case Hunter + Acceptance Auditor), 2026-06-20._

### Decision Needed

- [x] [Review][Resolved] AC6 named `ResolveStartOfCombat_TransitionsToCombatRangedAfterPhase0` but the skeleton stopped at `CombatStart`. RESOLUTION (John, 2026-06-20): advance Phase 0 → Phase 1 inside `ResolveStartOfCombat` (enter `CombatRanged`, resolve nothing there — ranged resolution stays 3-2). Reworked `SetsPhaseToStart` to verify pass-through via a CombatStart callback; added the AC6 transition test. AC6 now literally satisfied; no spec amendment needed. [scripts/combat/CombatResolver.cs, tests/unit/CombatResolverTest.cs]

### Patch

- [x] [Review][Dismissed] ~~Remove unused `TokenColor.White`~~ — FALSE POSITIVE. White is a real Mage Knight token color (city defenders + ruins encounters). Spec/LLD listing only 5 colors was the error; spec Task 1 + TokenColor.cs corrected to keep White with an explanatory comment. [scripts/core/types/TokenColor.cs]
- [x] [Review][Patch] Wrapped dev "Combat: Start/Leave" buttons in `#if DEBUG` — they mutate real `CurrentPhase` and would ship in release builds; codebase convention guards all dev UI this way [scripts/ui/screens/PlaceholderMainMenu.cs]
- [x] [Review][Patch] Corrected completion-note rationale: the `Attacks: IReadOnlyList<EnemyAttack>` shape is a deliberate deviation from spec AND both LLDs (which use scalar `Attack`/`AttackType`) [story Dev Agent Record]

### Deferred (pre-existing or out-of-scope)

- [x] [Review][Defer] No event unsubscription in StagingAreaView/HandView — pre-existing pattern across all views; GameState outlives the Controls. Systemic lifecycle pass, not this story.
- [x] [Review][Defer] `RestoreSnapshot` fires `PhaseChanged` but `CombatState` sub-state is not in the snapshot — undo across a combat-phase boundary would desync phase from combat state. Combat undo isn't enabled until 3-6 (force-quit restore); address there.
- [x] [Review][Defer] `CardCompact.IsCardPlayable` ignores `card.LegalPhases` (PhaseValidator honors it at play time) — latent; no card sets `legal_phases` today. Revisit when a LegalPhases card is introduced. [scripts/ui/components/CardCompact.cs]
- [x] [Review][Defer] Stub semantics to revisit when `ResolveCombat` is wired in 3-2: `AllEnemiesDefeated` true pre-start, `HeroWon` meaningless in current stub, `ResolveStartOfCombat` AddRange-without-Clear on re-entry, `FirePhaseCallbacks` snapshots list (same-phase re-enqueue skipped). [scripts/combat/CombatResolver.cs, CombatState.cs]
- [x] [Review][Defer] `HasResistanceTo` branching logic (incl. ColdFire derivation) has no test — exercised when block phase 3-3 lands. [scripts/combat/EnemyToken.cs]
- [x] [Review][Defer] `EnemyTokenInstance.WoundCount`/`IsDestroyed` unwired to defeat/`ActiveEnemies` removal — wired in 3-3/3-4. [scripts/combat/EnemyToken.cs]

---

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

- Tasks 0-3 were partially complete from prior session (globs, type enums, EnemyToken.cs, CombatContributions.cs, CombatResolverTest.cs RED tests).
- EnemyTokenDefinition uses `Attacks: IReadOnlyList<EnemyAttack>` (list of attacks, each with value+type). NOTE: this is a deliberate deviation from BOTH the story spec AND the combat-flow/enemy-token LLDs, which all use scalar `Attack`/`AttackType` fields. The list generalizes for enemies with multiple attacks; the RED test drove it and it's internally consistent (code + tests + spec Task helper all updated to match). Flagged in code review — accepted as a forward-looking shape, not a correctness issue.
- HeroKnockedOut removed from CombatResult mid-session after rulebook review confirmed KO is a mid-combat event with no post-combat footprint.
- EnemyToken.cs needed `using System.Linq` for `IReadOnlyList<T>.Contains` extension.
- StagingAreaView needed `using MagusWarrior.Core.Types` for GamePhase switch expression.
- 202 tests green (190 baseline + 9 CombatResolverTest + 3 GameStateTest PhaseChanged). Code review added the Phase 0→1 transition test.
- Manual WSLg verification passed: "Combat: Start" → HUD shows "Ranged Attack" ✓ (confirmed by John 2026-06-20).

### File List

| File | Action |
|------|--------|
| `tests/maguswarrior.Tests.csproj` | Modified — add `scripts/combat/**` and `scripts/broker/**` globs |
| `tests/unit/CombatResolverTest.cs` | Created — 8 TDD tests |
| `tests/unit/GameStateTest.cs` | Modified — 3 new PhaseChanged tests |
| `scripts/core/types/AttackType.cs` | Created |
| `scripts/core/types/AttackDelivery.cs` | Created |
| `scripts/core/types/BlockType.cs` | Created |
| `scripts/core/types/MoveConversionMode.cs` | Created |
| `scripts/core/types/TokenColor.cs` | Created |
| `scripts/core/types/EnemyAbility.cs` | Created |
| `scripts/core/GameState.cs` | Modified — `PhaseChanged` event; `SetPhase` no-op guard + event fire; `RestoreSnapshot` fires `PhaseChanged` |
| `scripts/combat/EnemyToken.cs` | Created — `EnemyTokenDefinition`, `SummonBehavior`, `EnemyTokenInstance` |
| `scripts/combat/CombatContributions.cs` | Created — `AttackContribution`, `BlockContribution`, `DamageAssignment`, `ICombatAttackModifier`, `ICombatPhaseCallback` |
| `scripts/combat/CombatGroup.cs` | Created — `CombatGroup`, `CombatResult` |
| `scripts/combat/CombatState.cs` | Created |
| `scripts/combat/CombatResolver.cs` | Created |
| `scripts/broker/UIBroker.cs` | Created |
| `scripts/ui/components/StagingAreaView.cs` | Modified — `_phaseLabel`; subscribe to `PhaseChanged`; `RefreshPhase()` |
| `scripts/ui/components/HandView.cs` | Modified — subscribe to `state.PhaseChanged` → `RefreshHand()` |
| `scripts/ui/components/CardCompact.cs` | Modified — `IsCardPlayable` real check |
| `scripts/ui/screens/PlaceholderMainMenu.cs` | Modified — "Combat: Start" and "Combat: Leave" buttons |
