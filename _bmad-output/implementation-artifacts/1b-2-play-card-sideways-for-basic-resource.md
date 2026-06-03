# Story 1b.2: Play Card Sideways for Basic Resource

Status: done

## Story

As a player,
I want to play any card sideways for a basic resource,
so that I always have a fallback play regardless of my hand contents.

## Acceptance Criteria

> **⚠ Superseded shapes (design change — see Dev Agent Record + Review Findings):** ACs **2, 3, 5, 6, 12** and the "Key code pattern" snippet below were written against scalar `AttackPointsThisTurn`/`BlockPointsThisPhase` fields and single-arg effect constructors. The shipped implementation instead uses typed pools — `AttackPool` (`Dictionary<(EffectType Distance, AttackElement Element), int>`) and `BlockPool` (`Dictionary<AttackElement, int>`) — plus a new `AttackElement` enum and multi-arg effect constructors (`AttackEffect(int, EffectType, AttackElement)`, `BlockEffect(int, AttackElement)`). This was John's approved call (elements and mana color are distinct concepts). Read those ACs as describing *intent* (track attack/block/influence resource and keep the LOCKSTEP snapshot in sync), not the literal field shape. `InfluencePointsThisTurn` stays scalar. The tests (ACs 10–12) were updated to the pool shape and all 36 pass.

1. `scripts/cards/SidewaysRule.cs` created — pure C#, namespace `MagusWarrior.Cards`, no Godot dependency:
   - `public static (EffectType EffectType, int Amount)? GetEffect(GamePhase phase)` — returns the sideways tuple for legal phases, `null` otherwise:
     - `GamePhase.Movement` → `(EffectType.Move, 1)`
     - `GamePhase.Interaction` → `(EffectType.Influence, 1)`
     - `GamePhase.CombatBlock` → `(EffectType.Block, 1)`
     - `GamePhase.CombatMelee` → `(EffectType.AttackMelee, 1)`
     - All other phases (CombatRanged, CombatStart, CombatAssignDamage, Rest, EndOfTurn, Any) → `null`
   - **1b-2 simplification:** The full MK rule lets the player choose any of Move 1 / Block 1 / Attack 1 / Influence 1 regardless of phase — the phase convention just selects the obvious best. A handful of powered card combos (e.g. a card that gives elemental block per influence point, or ranged attack per move point) make a "wrong-phase" resource genuinely valuable. `SidewaysRule.GetEffect` uses the phase-deterministic mapping as a correct default for all cards in the current test hand. Choice UI is deferred — see Dev Notes.
   - Required usings: `using MagusWarrior.Core.Types;`

2. `scripts/cards/effects/combat/AttackEffect.cs` created — pure C#, namespace `MagusWarrior.Cards.Effects.Combat`:
   - `public class AttackEffect : IEffect`
   - `public AttackEffect(int points)` — stores `_points`
   - `Execute`: calls `state.AddAttackPoints(_points)`, returns `Task.FromResult(EffectResult.Ok())`
   - Required usings: `using System.Threading.Tasks; using MagusWarrior.Core;`

3. `scripts/cards/effects/combat/BlockEffect.cs` created — pure C#, namespace `MagusWarrior.Cards.Effects.Combat`:
   - `public class BlockEffect : IEffect`
   - `public BlockEffect(int points)` — stores `_points`
   - `Execute`: calls `state.AddBlockPoints(_points)`, returns `Task.FromResult(EffectResult.Ok())`
   - Required usings: `using System.Threading.Tasks; using MagusWarrior.Core;`

4. `scripts/cards/effects/influence/InfluenceEffect.cs` created — pure C#, namespace `MagusWarrior.Cards.Effects.Influence`:
   - `public class InfluenceEffect : IEffect`
   - `public InfluenceEffect(int points)` — stores `_points`
   - `Execute`: calls `state.AddInfluencePoints(_points)`, returns `Task.FromResult(EffectResult.Ok())`
   - Required usings: `using System.Threading.Tasks; using MagusWarrior.Core;`

5. `scripts/core/GameState.cs` updated — add three new resource tracking properties and their mutators:
   - `public int AttackPointsThisTurn { get; private set; }`
   - `public int BlockPointsThisPhase { get; private set; }`
   - `public int InfluencePointsThisTurn { get; private set; }`
   - `public void AddAttackPoints(int n) { AttackPointsThisTurn += n; }`
   - `public void AddBlockPoints(int n) { BlockPointsThisPhase += n; }`
   - `public void AddInfluencePoints(int n) { InfluencePointsThisTurn += n; }`
   - Update the LOCKSTEP comment to include the three new fields
   - No other changes to GameState.cs

6. `scripts/core/GameEventLog.cs` updated — `GameStateSnapshot` record extended with three new fields (positional record — all three constructors and test uses must be updated):
   - `public record GameStateSnapshot(GamePhase CurrentPhase, int MovePointsThisTurn, int AttackPointsThisTurn, int BlockPointsThisPhase, int InfluencePointsThisTurn);`
   - `TakeSnapshot()` in `GameState.cs` updated: `new(CurrentPhase, MovePointsThisTurn, AttackPointsThisTurn, BlockPointsThisPhase, InfluencePointsThisTurn)`
   - `RestoreSnapshot()` in `GameState.cs` updated to restore the three new fields

7. `scripts/deck/DeckManager.cs` updated — add `PlayCard`:
   - `public Result<CardDefinition> PlayCard(string cardId)`:
     - Calls `Hand.ToList()`, finds first card matching `cardId` by `FindIndex`
     - If not found: `return Result<CardDefinition>.Fail($"Card '{cardId}' not in hand")`
     - Removes the found card at its index, calls `SetHand(updatedList)`, returns `Result<CardDefinition>.Ok(played)`
   - Add required usings: `using System.Linq; using MagusWarrior.Core;`

8. `scripts/ui/components/HandView.cs` updated:
   - Add field: `private EffectScheduler _scheduler = null!;`
   - `Initialize` signature extended: `public void Initialize(DeckManager deck, GameState state, EffectScheduler scheduler)`
     - Stores `_scheduler = scheduler`
     - Connects `_expandedPanel.PlaySidewaysRequested += OnPlaySidewaysRequested` (in addition to existing setup)
   - Add method `private async void OnPlaySidewaysRequested(string cardId)`:
     - Calls `SidewaysRule.GetEffect(_state.CurrentPhase)`
     - If `null`: `Log.Debug("[UI]", $"PlaySideways: no sideways effect in {_state.CurrentPhase}"); return;`
     - Destructures the tuple: `var (effectType, amount) = sideways.Value;`
     - Creates the appropriate `IEffect` instance via switch expression (see Dev Notes)
     - Builds `EffectContext(cardId, effectType, _state.CurrentPhase, false)`
     - `_scheduler.Enqueue(effect, 0, ctx)`
     - `await _scheduler.ResolveAll(_state)`
     - `var result = _deckManager.PlayCard(cardId)` — remove card from hand
     - `if (!result.IsSuccess) Log.Warn("[UI]", $"PlaySideways: PlayCard failed: {result.Error}");`
     - `Log.Debug("[UI]", $"PlaySideways: {cardId} → {effectType} {amount} applied")`
   - Add required usings: `using System; using MagusWarrior.Cards.Effects; using MagusWarrior.Cards.Effects.Combat; using MagusWarrior.Cards.Effects.Influence; using MagusWarrior.Cards.Effects.Movement;`

9. `scripts/ui/screens/PlaceholderMainMenu.cs` updated:
   - Add field: `private EffectScheduler _effectScheduler = null!;`
   - In `_Ready()`: add `_effectScheduler = new EffectScheduler();` after `_deckManager = new DeckManager();`
   - Update `handView.Initialize(...)` call: `handView.Initialize(_deckManager, _state, _effectScheduler)`
   - Add using: `using MagusWarrior.Cards.Effects;`

10. `tests/unit/SidewaysRuleTest.cs` created — 5 xUnit tests:
    - `GetEffect_Movement_ReturnsMove1`
    - `GetEffect_Interaction_ReturnsInfluence1`
    - `GetEffect_CombatBlock_ReturnsBlock1`
    - `GetEffect_CombatMelee_ReturnsAttackMelee1`
    - `GetEffect_CombatRanged_ReturnsNull`
    - Namespace: `MagusWarrior.Tests`; usings: `using MagusWarrior.Cards; using MagusWarrior.Core.Types; using Xunit;`

11. `tests/unit/DeckManagerTest.cs` updated — add 4 tests for `PlayCard`:
    - `PlayCard_RemovesCardFromHand` — after `PlayCard("march")`, Hand does not contain march
    - `PlayCard_FiresHandChanged` — `HandChanged` fires exactly once during `PlayCard`
    - `PlayCard_ReturnsPlayedCard` — `result.IsSuccess` is true and `result.Value!.Id == "march"`
    - `PlayCard_FailsWhenCardNotInHand` — `result.IsSuccess` is false for unknown cardId
    - Add usings: `using MagusWarrior.Core;`

12. `tests/unit/EffectSystemTest.cs` updated — add 4 tests:
    - `AttackEffect_AddsAttackPoints` — `new AttackEffect(3).Execute(state, ctx)` → `state.AttackPointsThisTurn == 3`
    - `BlockEffect_AddsBlockPoints` — `new BlockEffect(2).Execute(state, ctx)` → `state.BlockPointsThisPhase == 2`
    - `InfluenceEffect_AddsInfluencePoints` — `new InfluenceEffect(1).Execute(state, ctx)` → `state.InfluencePointsThisTurn == 1`
    - `GameStateSnapshot_CapturesNewFields` — `TakeSnapshot()` after `AddAttackPoints(5)` → `snapshot.AttackPointsThisTurn == 5`
    - Add usings as needed for new namespaces

13. `tests/maguswarrior.Tests.csproj` updated — add compile entries for the four new pure C# files:
    ```xml
    <Compile Include="../scripts/cards/SidewaysRule.cs" />
    <Compile Include="../scripts/cards/effects/combat/AttackEffect.cs" />
    <Compile Include="../scripts/cards/effects/combat/BlockEffect.cs" />
    <Compile Include="../scripts/cards/effects/influence/InfluenceEffect.cs" />
    ```

14. `dotnet test tests/maguswarrior.Tests.csproj` passes — all 23 existing tests + 13 new tests = 36 green. `HandView.cs`, `CardExpanded.cs`, `PlaceholderMainMenu.cs` are **NOT** added to the test project (they have Godot dependencies).

## Tasks / Subtasks

- [x] Task 1: Create SidewaysRule (AC: 1, 13, 10)
  - [x] Create `scripts/cards/SidewaysRule.cs` — pure C#, phase→(EffectType, int)? mapping
  - [x] Add `<Compile Include="../scripts/cards/SidewaysRule.cs" />` to test project
  - [x] Create `tests/unit/SidewaysRuleTest.cs` with 5 tests
  - [x] Run `dotnet test` — confirm 5 new tests green

- [x] Task 2: Create AttackEffect, BlockEffect, InfluenceEffect (AC: 2, 3, 4, 13, 12)
  - [x] Create `scripts/cards/effects/combat/AttackEffect.cs`
  - [x] Create `scripts/cards/effects/combat/BlockEffect.cs`
  - [x] Create `scripts/cards/effects/influence/InfluenceEffect.cs`
  - [x] Add compile entries for all three to test project csproj
  - [x] Add 4 tests to `EffectSystemTest.cs` (AttackEffect, BlockEffect, InfluenceEffect, snapshot)
  - [x] Run `dotnet test` — confirm 4 more green

- [x] Task 3: Extend GameState and GameStateSnapshot (AC: 5, 6)
  - [x] Add `AttackPool`, `BlockPool`, `InfluencePointsThisTurn` to `GameState.cs` (pool model — see Dev Agent Record)
  - [x] Add `AddAttackPoints`, `AddBlockPoints`, `AddInfluencePoints` mutators
  - [x] Update `GameStateSnapshot` record in `GameEventLog.cs` with pool fields
  - [x] Update `TakeSnapshot()` and `RestoreSnapshot()` in `GameState.cs` (LOCKSTEP rule)
  - [x] Update LOCKSTEP comment in `GameState.cs`
  - [x] Run `dotnet test` — confirm all still green

- [x] Task 4: Add DeckManager.PlayCard (AC: 7, 11)
  - [x] Add `PlayCard(string cardId)` method to `DeckManager.cs`
  - [x] Add 4 PlayCard tests to `DeckManagerTest.cs`
  - [x] Run `dotnet test` — confirm 4 more green

- [x] Task 5: Wire HandView + PlaceholderMainMenu (AC: 8, 9)
  - [x] Update `HandView.cs` — add `_scheduler` field, extend `Initialize`, add `OnPlaySidewaysRequested`
  - [x] Update `PlaceholderMainMenu.cs` — create `_effectScheduler`, pass to `handView.Initialize`

- [x] Task 6: Verify all tests and no regressions (AC: 14)
  - [x] Run `dotnet test tests/maguswarrior.Tests.csproj` — 36/36 green, 0 warnings, Godot build clean

## Dev Notes

### Scope — what this story does and does NOT do

**Does:**
- `SidewaysRule` maps phase → `(EffectType, int)?` (the system-level sideways table from the effect LLD)
- Three new IEffect implementations: `AttackEffect`, `BlockEffect`, `InfluenceEffect`
- Three new fields on `GameState` + matching `GameStateSnapshot` expansion (LOCKSTEP rule)
- `DeckManager.PlayCard` — removes one card from Hand and fires `HandChanged`
- `HandView.OnPlaySidewaysRequested` — resolves effect immediately, removes card from hand
- Sideways is always immediate (no staging) — it's a fixed 1-resource fallback with no choices

**Does NOT:**
- Normal "Play" button wiring — still logs only (1b-3 introduces staging + running totals)
- "Power" button wiring — always greyed; ManaPool not until Epic 6
- Staging area / running totals — Story 1b-3
- Phase indicator HUD — Story 1b-7 (improvisation) context; Epic 8 for full HUD
- Wound card untappable treatment — Story 1b-5
- Rest turn — Story 1b-6
- Improvisation card — Story 1b-7
- Undo of staged cards — Story 1b-4 (undo of already-resolved sideways works via event log from Epic 1a, but there's no UI button for it yet)

### Key code pattern — OnPlaySidewaysRequested

```csharp
private async void OnPlaySidewaysRequested(string cardId) {
    var sideways = SidewaysRule.GetEffect(_state.CurrentPhase);
    if (sideways is null) {
        Log.Debug("[UI]", $"PlaySideways: no sideways effect in {_state.CurrentPhase}");
        return;
    }
    var (effectType, amount) = sideways.Value;
    IEffect effect = effectType switch {
        EffectType.Move        => new MoveEffect(amount),
        EffectType.AttackMelee => new AttackEffect(amount),
        EffectType.Block       => new BlockEffect(amount),
        EffectType.Influence   => new InfluenceEffect(amount),
        _                      => throw new InvalidOperationException(
                                      $"SidewaysRule returned unexpected EffectType: {effectType}")
    };
    var ctx = new EffectContext(cardId, effectType, _state.CurrentPhase, false);
    _scheduler.Enqueue(effect, 0, ctx);
    await _scheduler.ResolveAll(_state);
    var result = _deckManager.PlayCard(cardId);
    if (!result.IsSuccess)
        Log.Warn("[UI]", $"PlaySideways: PlayCard failed for '{cardId}': {result.Error}");
    Log.Debug("[UI]", $"PlaySideways: {cardId} → {effectType} {amount} applied");
}
```

The `_ => throw` arm is intentional — if `SidewaysRule.GetEffect` ever returns a type not covered here, it's a bug in `SidewaysRule`, not an invalid player action. Loud failure is correct.

### async void — accepted exception

`OnPlaySidewaysRequested` is `async void` because Godot signal handlers cannot return `Task`. This is the same accepted exception as `GameDebug.FireTestEffect` (noted in deferred work from 1a-3). Safe because all current effects use `Task.FromResult` (synchronous). Revisit when `UIBroker` introduces real awaitable interactions.

Do NOT write `async void` anywhere else in the codebase. This pattern is constrained to Godot signal handlers that must fire-and-forget.

### LOCKSTEP snapshot rule — CRITICAL

The comment in `GameState.cs` reads:
> LOCKSTEP: every mutable field added to GameState MUST also be added to GameStateSnapshot and restored here, or undo silently produces a half-rollback.

`GameStateSnapshot` is a positional record in `GameEventLog.cs`. Adding fields to the record is a compile-time change — the compiler will flag every construction site that doesn't provide all positional arguments. After updating the record, search for `new GameStateSnapshot(` and `new(` to find all construction sites and update them.

Current construction sites before this story:
- `GameState.TakeSnapshot()` — `scripts/core/GameState.cs`
- `EffectScheduler.ResolveAll` — `scripts/cards/effects/EffectScheduler.cs` (passes snapshot as event data, no direct construction)

The `EffectScheduler` passes `snapshot` to `EffectFiredEvent` — no changes needed there since `snapshot` is obtained by calling `TakeSnapshot()`.

### DeckManager.PlayCard — removes by first occurrence

If the hand theoretically contains two copies of a card with the same ID, `PlayCard` removes only the first occurrence. This matches natural deck behavior (you played one card, not both).

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using MagusWarrior.Cards;
using MagusWarrior.Core;

namespace MagusWarrior.Deck;

public class DeckManager {
    public IReadOnlyList<CardDefinition> Hand { get; private set; } = Array.Empty<CardDefinition>();
    public event Action? HandChanged;

    public void SetHand(IReadOnlyList<CardDefinition> hand) {
        Hand = hand;
        HandChanged?.Invoke();
    }

    public Result<CardDefinition> PlayCard(string cardId) {
        var list = Hand.ToList();
        var idx = list.FindIndex(c => c.Id == cardId);
        if (idx < 0)
            return Result<CardDefinition>.Fail($"Card '{cardId}' not in hand");
        var played = list[idx];
        list.RemoveAt(idx);
        SetHand(list);
        return Result<CardDefinition>.Ok(played);
    }
}
```

### File placement

| File | Location |
|------|----------|
| SidewaysRule.cs | `scripts/cards/` |
| AttackEffect.cs | `scripts/cards/effects/combat/` |
| BlockEffect.cs | `scripts/cards/effects/combat/` |
| InfluenceEffect.cs | `scripts/cards/effects/influence/` |
| SidewaysRuleTest.cs | `tests/unit/` |

Matches effect subdirectory map from `project-context.md`: `movement/`, `combat/`, `mana/`, `healing/`, `influence/`, `special/`.

### Namespace conventions

| Folder | Namespace |
|--------|-----------|
| `scripts/cards/` | `MagusWarrior.Cards` |
| `scripts/cards/effects/combat/` | `MagusWarrior.Cards.Effects.Combat` |
| `scripts/cards/effects/influence/` | `MagusWarrior.Cards.Effects.Influence` |

### Effect LLD sideways rules (authoritative)

From `docs/effect-lld.md` §Universal Rules → Sideways Play:

| Phase | Sideways Effect |
|-------|----------------|
| Movement | Move 1 |
| Interaction | Influence 1 |
| Combat — Block | Block 1 |
| Combat — Melee | Attack 1 (physical melee — never elemental) |
| Combat — Ranged | **No useful sideways option** — returns null |

> Sideways cannot provide Ranged Attack, Siege Attack, or elemental Attack/Block.

This is why `SidewaysRule.GetEffect` never returns `EffectType.AttackRanged`, `EffectType.AttackSiege`, or any elemental variant. The single `EffectType.AttackMelee` with no element field covers melee-phase sideways correctly.

### HandView.Initialize signature change

The method signature changes from:
```csharp
public void Initialize(DeckManager deck, GameState state)
```
to:
```csharp
public void Initialize(DeckManager deck, GameState state, EffectScheduler scheduler)
```

There is exactly **one caller**: `PlaceholderMainMenu._Ready()`. Update it to pass `_effectScheduler`.

The `EffectScheduler` type lives in namespace `MagusWarrior.Cards.Effects`. `PlaceholderMainMenu.cs` must add `using MagusWarrior.Cards.Effects;`.

### HandView usings to add (ImplicitUsings=disable)

```csharp
using System;                                      // InvalidOperationException
using MagusWarrior.Cards.Effects;                  // EffectScheduler, IEffect, EffectContext
using MagusWarrior.Cards.Effects.Combat;           // AttackEffect, BlockEffect
using MagusWarrior.Cards.Effects.Influence;        // InfluenceEffect
using MagusWarrior.Cards.Effects.Movement;         // MoveEffect
```

The existing `using MagusWarrior.Core.Types;` already covers `EffectType` and `GamePhase`.

### CardExpanded already closes on Sideways tap

In `CardExpanded._Ready()`, the Pressed handler already calls `Close()` after emitting `PlaySidewaysRequested`:

```csharp
_playSidewaysButton.Pressed += () => {
    EmitSignal(SignalName.PlaySidewaysRequested, _cardId);
    Close();
    Log.Debug("[UI]", $"Sideways tapped: {_cardId}");
};
```

Since Godot signals are synchronous, `OnPlaySidewaysRequested` runs to completion (all effects are `Task.FromResult`) before `Close()` is called. The sequence is:
1. Signal emits → `OnPlaySidewaysRequested` runs → effect resolves → `PlayCard` removes card → `HandChanged` → `RefreshHand` rebuilds container
2. `Close()` hides `_expandedPanel`

No changes needed to `CardExpanded.cs` for this story.

### Test patterns — DeckManagerTest.cs additions

```csharp
[Fact]
public void PlayCard_RemovesCardFromHand() {
    var deck = new DeckManager();
    var march = new CardDefinition { Id = "march", Name = "March", Type = CardType.BasicAction };
    deck.SetHand(new[] { march });
    deck.PlayCard("march");
    Assert.Empty(deck.Hand);
}

[Fact]
public void PlayCard_FiresHandChanged() {
    var deck = new DeckManager();
    var march = new CardDefinition { Id = "march", Name = "March", Type = CardType.BasicAction };
    deck.SetHand(new[] { march });
    int callCount = 0;
    deck.HandChanged += () => callCount++;
    callCount = 0; // reset after SetHand
    deck.PlayCard("march");
    Assert.Equal(1, callCount);
}

[Fact]
public void PlayCard_ReturnsPlayedCard() {
    var deck = new DeckManager();
    var march = new CardDefinition { Id = "march", Name = "March", Type = CardType.BasicAction };
    deck.SetHand(new[] { march });
    var result = deck.PlayCard("march");
    Assert.True(result.IsSuccess);
    Assert.Equal("march", result.Value!.Id);
}

[Fact]
public void PlayCard_FailsWhenCardNotInHand() {
    var deck = new DeckManager();
    deck.SetHand(Array.Empty<CardDefinition>());
    var result = deck.PlayCard("march");
    Assert.False(result.IsSuccess);
}
```

### SidewaysRuleTest.cs pattern

```csharp
[Fact]
public void GetEffect_Movement_ReturnsMove1() {
    var result = SidewaysRule.GetEffect(GamePhase.Movement);
    Assert.NotNull(result);
    Assert.Equal(EffectType.Move, result!.Value.EffectType);
    Assert.Equal(1, result.Value.Amount);
}

[Fact]
public void GetEffect_CombatRanged_ReturnsNull() {
    var result = SidewaysRule.GetEffect(GamePhase.CombatRanged);
    Assert.Null(result);
}
```

### Deferred — sideways choice for cross-phase card combos

**Full MK rule:** Playing a card sideways always yields the player's choice of Move 1, Block 1, Attack 1, or Influence 1. The phase-to-resource table in `SidewaysRule` reflects the obviously correct choice for each phase — not a hard constraint. Two concrete cases where another resource is the right pick:

- A powered card that gives N fire or ice block per N influence → player wants Influence 1 sideways during the block phase instead of the default Block 1.
- A powered card that gives N ranged attack per N move point accumulated → player wants Move 1 sideways during the ranged phase (where `GetEffect` currently returns `null`).

**What needs to change when this is implemented:**
1. `SidewaysRule.GetEffect` becomes `SidewaysRule.GetOptions(GamePhase)` → returns the conventionally-best option AND a flag indicating whether other options are worth offering (or just always returns all four)
2. A `UIBroker.ChooseOne` prompt is shown when the player has a card in play that makes a non-default resource valuable — or always offer a choice via the `CardExpanded` panel
3. `OnPlaySidewaysRequested` awaits the choice before constructing the `IEffect`

This is non-trivial because it requires either (a) the UI to always present a 4-option choice (adds friction for the 95% case), or (b) the game logic to know which cards in play make alternate resources valuable. Defer until powered card combos are being implemented.

### Known issues to carry forward (not blocking this story)

These are from the deferred-work log, noted here for awareness:

- `HandView` never unsubscribes `HandChanged` — deferred until scene lifecycle becomes complex
- `QueueFree`'d `CardCompact` nodes ghost-tap risk — not triggered since `PlayCard` + `RefreshHand` happens before next input frame
- `SetHand` fires before `Initialize` subscribes — ordering dependency; `PlayCard` calls `SetHand` which fires to existing subscribers (safe)

### Project Context Rules

**Godot boundary:** `SidewaysRule.cs`, `AttackEffect.cs`, `BlockEffect.cs`, `InfluenceEffect.cs` are all pure C# — no Godot dependency. Add all four to `tests/maguswarrior.Tests.csproj`.

**No service locator:** `EffectScheduler` is constructed in `PlaceholderMainMenu` and injected into `HandView.Initialize` — not accessed via static or singleton.

**Result<T>:** `DeckManager.PlayCard` returns `Result<CardDefinition>`. Callers must check `IsSuccess`. A failed `PlayCard` in `OnPlaySidewaysRequested` is logged as Warn but not thrown (game continues).

**Logging:** Use `Log.Debug("[UI]", ...)` for all UI-layer output. Use `Log.Warn("[UI]", ...)` for the `PlayCard` failure path. Never use `GD.Print`.

**No async void except Godot signal handlers:** `OnPlaySidewaysRequested` is `async void` — the only accepted exception.

**ImplicitUsings=disable:** All `.cs` files must have all using statements explicit. The new `AttackEffect.cs`, `BlockEffect.cs`, `InfluenceEffect.cs` files each need `using System.Threading.Tasks; using MagusWarrior.Core;`. `SidewaysRule.cs` needs `using MagusWarrior.Core.Types;`.

### References

- Effect LLD sideways rules: `docs/effect-lld.md` §Universal Rules → Sideways Play
- Architecture Screen Contract (Hand Display / Card Play Flow): `_bmad-output/game-architecture.md` §Screen Contracts
- Architecture System Location Map: `_bmad-output/game-architecture.md` §System Location Map
- PhaseGate: `scripts/cards/effects/PhaseGate.cs` — `EffectType` members confirm correct type names
- GameState LOCKSTEP comment: `scripts/core/GameState.cs:28` — mandates updating snapshot with every new mutable field
- GameStateSnapshot: `scripts/core/GameEventLog.cs:7` — positional record, update required
- MoveEffect pattern to replicate: `scripts/cards/effects/movement/MoveEffect.cs`
- Previous story (1b-1) file placement table: `_bmad-output/implementation-artifacts/1b-1-tap-card-to-see-options.md` §Dev Notes
- async void accepted exception context: `_bmad-output/implementation-artifacts/deferred-work.md` — deferred from 1a-3 review
- Epics: `_bmad-output/epics.md` §Epic 1b — scope, stories, deliverable

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

- All 6 tasks completed. 36/36 xUnit tests pass (23 existing + 13 new). Godot build: 0 errors, 0 warnings.
- **Design change from story spec:** `AttackPointsThisTurn` / `BlockPointsThisPhase` scalar fields replaced by typed pools `Dictionary<(EffectType Distance, AttackElement Element), int>` for attack and `Dictionary<AttackElement, int>` for block, after John identified that elements (Physical/Fire/Ice/ColdFire) and mana color are distinct concepts. `AttackElement` enum added to `scripts/core/types/`. `InfluencePointsThisTurn` stays scalar. Snapshot copies both dictionaries for correct undo behavior.
- `SidewaysRule.cs` created — static phase→`(EffectType, int)?` lookup. Returns `null` for CombatRanged and all non-play phases.
- `AttackEffect(int, EffectType, AttackElement)` created — adds to `AttackPool`. Sideways always passes `EffectType.AttackMelee, AttackElement.Physical`.
- `BlockEffect(int, AttackElement)` created — adds to `BlockPool`. Sideways always passes `AttackElement.Physical`.
- `InfluenceEffect(int)` created — adds to `InfluencePointsThisTurn`.
- `DeckManager.PlayCard(string)` added — removes first occurrence by index, fires `HandChanged`, returns `Result<CardDefinition>`.
- `HandView.Initialize` extended with `EffectScheduler` parameter. `OnPlaySidewaysRequested` wired: resolves effect via scheduler, removes card from hand via `PlayCard`. `async void` accepted pattern for Godot signal handler.
- `PlaceholderMainMenu` creates `_effectScheduler` and passes it to `handView.Initialize`.
- Tasks 2 and 3 executed in swapped order (3 before 2) — `GameState` pool methods must exist before `AttackEffect`/`BlockEffect` can compile.

### File List

- `scripts/cards/SidewaysRule.cs` (new)
- `scripts/core/types/AttackElement.cs` (new)
- `scripts/cards/effects/combat/AttackEffect.cs` (new)
- `scripts/cards/effects/combat/BlockEffect.cs` (new)
- `scripts/cards/effects/influence/InfluenceEffect.cs` (new)
- `scripts/core/GameState.cs` (modified — pool model, InfluencePointsThisTurn, LOCKSTEP update)
- `scripts/core/GameEventLog.cs` (modified — GameStateSnapshot with pool fields)
- `scripts/deck/DeckManager.cs` (modified — PlayCard added)
- `scripts/ui/components/HandView.cs` (modified — EffectScheduler injected, OnPlaySidewaysRequested)
- `scripts/ui/screens/PlaceholderMainMenu.cs` (modified — _effectScheduler field and injection)
- `tests/unit/SidewaysRuleTest.cs` (new)
- `tests/unit/DeckManagerTest.cs` (modified — 4 PlayCard tests added)
- `tests/unit/EffectSystemTest.cs` (modified — 4 effect/snapshot tests added)
- `tests/maguswarrior.Tests.csproj` (modified — 5 new compile entries)

## Review Findings

Code review 2026-06-02 (Opus 4.8) — Blind Hunter + Edge Case Hunter + Acceptance Auditor + rulebook cross-check (`MKUE_Rulebook_BOOKLET.pdf`, walkthrough). 36/36 tests verified green. 1 decision, 2 patch, 5 defer, 5 dismissed.

- [x] [Review][Patch] Sideways resolution ordering / atomicity & async-void re-entrancy — **RESOLVED (Option 1, reorder).** `OnPlaySidewaysRequested` now calls `_deck.PlayCard(cardId)` first and `return`s on `!IsSuccess`, applying the effect only if the card actually leaves the hand. This pre-empts the double-bank and orphaned-resource windows that open once `ResolveAll` becomes genuinely awaitable. Full atomic rollback (card returns to hand if a future awaitable effect fails) is a down-payment, not the finish — it's deferred to the UIBroker story since `Hand` is not yet part of the `GameState` snapshot. Build clean, 36/36 tests green. [scripts/ui/components/HandView.cs:92-118]
- [x] [Review][Patch] Reconcile stale AC text with the approved pool/AttackElement design [_bmad-output/implementation-artifacts/1b-2-play-card-sideways-for-basic-resource.md] — **DONE.** Added a "Superseded shapes" banner under Acceptance Criteria flagging ACs 2, 3, 5, 6, 12 and the key-code snippet as describing intent, not the literal scalar field shape; code ships the `Dictionary` pools + `AttackElement` per the Dev Agent Record.
- [x] [Review][Patch] Expose `AttackPool`/`BlockPool` as truly read-only [scripts/core/GameState.cs:15-22] — **DONE.** Getters now return `new ReadOnlyDictionary<…>(_backing)`, so the live dictionary can't be cast back to `Dictionary` and mutated; all writes go through `AddAttackPoints`/`AddBlockPoints`. Re-wrapped per access because `RestoreSnapshot` reassigns the backing fields.
- [x] [Review][Defer] Wound cards can be played sideways [scripts/ui/components/HandView.cs:92] — rulebook p4: "Wound cards cannot be played in any way." No `CardType` guard in `OnPlaySidewaysRequested`; only the test-hand filter currently hides it. Owned by Story 1b-5 — deferred, rules-confirmed.
- [x] [Review][Defer] Record value-equality landmine [scripts/core/GameEventLog.cs:7] — `GameStateSnapshot`/`EffectFiredEvent` are records with `IReadOnlyDictionary` members, so auto-generated `Equals`/`GetHashCode` use reference equality. Any future `Distinct`/`HashSet`/dedup over snapshots or events in the undo system will silently misbehave. No current consumer compares them — deferred.
- [x] [Review][Defer] Dead Sideways affordance in non-play phases [scripts/ui/components/HandView.cs:94] — `CardExpanded` enables the Sideways button unconditionally, but `GetEffect` returns `null` for `CombatRanged`/`CombatStart`/`Rest`/etc., so the tap silently no-ops. Disable the button when `GetEffect` is null. Not reachable until phase transitions exist (`CurrentPhase` is always `Movement` today). Rulebook p9 confirms no sideways during Ranged/Siege — `null` is correct — deferred, pre-phase-HUD.
- [x] [Review][Defer] Minor robustness on pools [scripts/core/GameState.cs:32-39] — the `AttackPool` key accepts any `EffectType` as its "distance" axis (a dedicated distance enum would tighten the invariant); `Add*` mutators don't guard `amount > 0` (a 0-amount play would leave a `(key, 0)` entry). Not reachable (sideways is always 1) — deferred, low priority.
- [x] [Review][Defer] `PriorityQueue` tie-break instability [scripts/cards/effects/EffectScheduler.cs] — same-priority effects have no FIFO guarantee; pre-existing scheduler property, unreachable with single-effect sideways — deferred, pre-existing.

**Dismissed (5):** `FindIndex` first-occurrence removal (by design, documented in Dev Notes); `SetHand` list-aliasing (local list discarded, not triggered); `EffectContext` melee-only distance (correct per rules); `ColdFire`/Fire+Ice aggregation (Epic 3 combat scope); subagent claim that ranged-phase sideways should yield Attack 1 (incorrect — rulebook p9: "Cards cannot be played sideways to contribute to Ranged or Siege Attacks").
