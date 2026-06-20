# Story 3-0b — Unify UndoController

**Epic:** 3 — Combat System (infrastructure prerequisite)
**Story ID:** 3-0b
**Status:** done
**Created:** 2026-06-19
**Dependencies:** 3-0 (TripUndoGate, EventLog, RecallFromDiscard — all done)
**Reviewer model:** Use Opus 4.8 for code review (per project feedback convention)
**Must complete before:** 3-1 (combat introduces triggered effects that need group undo)

---

## User Story

As a player, I can undo any combination of card plays and hero moves in the correct order,
including multi-effect plays, without the board position and move points getting out of
sync, and with full unit-test coverage for the undo logic.

---

## Context

### Why this story exists (three review defects from 3-0 code review)

**Defect 1 — Multi-effect play partial rollback (HIGH, latent)**
`EffectScheduler.ResolveAll` appends one `EffectFiredEvent` per effect to `EventLog`, including
triggered children. A card play with N triggered effects produces N log entries. The 3-0 undo
handler called `EventLog.PopLast()` once — reversing only the last effect, leaving prior effects
applied. No effect emits triggered children today, but Epic 3 combat (attack/wound chains) will;
this must land before 3-1. Decision: snapshot state ONCE before the whole play and restore it
as a group.

**Defect 2 — Card-undo + hero-move desync (MEDIUM, reachable)**
Scenario: Play Move card (+3 pts) → Walk to (1,0) (−2 pts, hero relocates) → tap Undo.
Before this fix: move points rewind AND card returns to hand BUT hero stays at (1,0) — free move.
Root cause: hero moves are tracked in `WorldMap._movePath`; card plays are tracked in `EventLog`.
Two independent stacks. Decision: unify onto a single ordered `UndoController` stack; both
card plays and hero moves push to the same stack and pop in LIFO order.

**Defect 3 — Zero automated coverage for undo logic (AUDITOR)**
`OnUndoRequested` lives in `HandView` (Godot `Control`) — untestable from the pure-C# test
project. Decision: extract undo logic into a pure-C# `UndoController` seam, red-first TDD.

All three were resolved as a single design in the 3-0 review findings (stored in
`_bmad-output/implementation-artifacts/3-0-trip-undo-gate-and-remove-commit-button.md`,
section "Review Findings → Decisions needed").

### What prior stories built — do NOT recreate

- **`GameEventLog`** / **`EffectFiredEvent`** / **`GameStateSnapshot`** (`scripts/core/GameEventLog.cs`)
  — debug inspector source, still used by `EffectEventLogPanel`. `EventLog.Clear()` in `TripUndoGate`
  still clears it. **`UndoController` replaces `EventLog` as the undo source, not as the event log.**
- **`GameState.TripUndoGate()`** — clears `EventLog`, writes `LastGateSnapshot`, fires `UndoGateCrossed`.
  This story adds `UndoController.Clear()` to `TripUndoGate`. No other changes to `TripUndoGate`.
- **`DeckManager.RecallFromDiscard(string cardId)`** — added in 3-0. Used by `UndoController` to
  recall the cost card when undoing an Improvisation play.
- **`HandView.OnPlayRequested`** — resolves immediately (3-0). This story changes only what happens
  AFTER `ResolveAll` returns: push a `CardPlayGroup` to `UndoController`.
- **`ImprovisationView.OnResourceSelected`** — resolves immediately (3-0). Same: after `ResolveAll`,
  push `CardPlayGroup` with `costCardId`.
- **`WorldMap._movePath` / `CanUndoMove` / `UndoLastMove` / `ClearMovePath`** — ALL REMOVED in this
  story. `UndoController` owns the move path. `HexMapView.Branch 1` (hero-hex tap) no longer calls
  `_map.UndoLastMove()` — it calls `_state.UndoController.PopHeroMove()`.
- **`PlaceholderMainMenu` wiring** — `_state.UndoGateCrossed += _worldMap.ClearMovePath` is removed
  because `WorldMap.ClearMovePath` no longer exists and `UndoController.Clear()` now handles the gate.
- **184 tests green** as of story 3-0 close.

### Architecture of the `UndoController` (read before implementing)

**Single ordered stack of `UndoEntry`:** each entry is either a `CardPlayGroup` (one card play,
however many triggered effects it produced) or a `HeroMoveEntry` (one committed hero walk step).
LIFO: the Undo button pops whatever is on top; hero-hex tap pops only if top is a `HeroMoveEntry`.

**`stateBefore` for a card play is captured ONCE before `_scheduler.Enqueue` is called.** All
effects produced by that play (direct + triggered) restore to that single snapshot. This fixes
the multi-effect partial-rollback defect.

**For hero moves:** `HexMapView.Branch 2` (move commit) captures `prevPos = _map.HeroPosition`
BEFORE calling `CommitHeroMove`, then pushes `UndoController.PushHeroMove(prevPos, cost.Value)`.
`CommitHeroMove` no longer tracks `costPaid` internally. On undo, `SetHeroPosition(previous)` is
called directly (fires `HeroMoved` event → `HexMapView` marker updates automatically).

**`GameState` holds `UndoController` as a property.** `TripUndoGate` calls `UndoController.Clear()`.
No dependency injection needed — tests construct `UndoController` stand-alone.

---

## Acceptance Criteria

**AC1 — `UndoController` unit tests (TDD, red-first)**
`tests/unit/UndoControllerTest.cs` written RED before implementation. Minimum tests:
1. `CanUndo_FalseInitially`
2. `PushCardPlay_ThenExecuteUndo_RestoresStateAndReturnsCard`
3. `PushCardPlay_WithCostCard_ExecuteUndo_RecallsCostCard`
4. `PushCardPlay_CostCardNotInDiscard_Aborts_NothingMutated`
5. `PushCardPlay_Multiple_UndoesInLifoOrder`
6. `PushHeroMove_ThenPopHeroMove_ReturnsEntryAndLeavesThanStackEmpty`
7. `PushHeroMove_ThenExecuteUndo_RestoresPositionAndRefundsPoints`
8. `Clear_EmptiesStack`
9. `CanUndoHeroMove_TrueOnlyWhenTopIsHeroMoveEntry`
10. `PushCardPlay_PushHeroMove_ExecuteUndo_PopsHeroMoveFirst` (LIFO mixed)
11. `PushHeroMove_PushCardPlay_ExecuteUndo_PopsCardPlayFirst` (LIFO mixed)

**AC2 — Multi-effect play undoes as a group**
Play any card (direct effect only today) → tap Undo → state rolls back to the snapshot
captured before `_scheduler.Enqueue`. A future card with triggered effects will also roll
back completely in one Undo tap because `stateBefore` was captured before the play.

**AC3 — Hero-move undo routes through `UndoController` (replaces `WorldMap.UndoLastMove`)**
Walk to (1,0) → tap (1,0) → `UndoController.PopHeroMove()` → hero back at (0,0), +cost refund.
`WorldMap.CanUndoMove` and `WorldMap.UndoLastMove` are removed; `HexMapView.Branch 1` checks
`_state.UndoController.CanUndoHeroMove`.

**AC4 — Card-undo + hero-move stay in sync (fixes the free-move bug)**
Scenario A: Play Move card (+3 pts) → Walk to (1,0) (−2 pts) → Undo button → hero back at
(0,0), +2 pts refunded → Undo button again → card returns to hand, −3 pts removed.
Scenario B: Walk to (1,0) → tap (1,0) → hero back → Undo button → card returned.

**AC5 — Undo blocked after gate (regression)**
Play card → Walk → Tile reveal (`TripUndoGate`) → Undo button → no-op (stack cleared by gate).
Hero hex tap also no-op (`CanUndoHeroMove` false).

**AC6 — `WorldMap._movePath` tracking fully removed**
`WorldMap.cs` has no `_movePath` field, no `CanUndoMove`, no `UndoLastMove`, no `ClearMovePath`.
`CommitHeroMove(HexCoord coord)` takes one parameter (no `costPaid`). `RevealTile` no longer
calls `ClearMovePath`.

**AC7 — WorldMap tests updated cleanly**
9 `WorldMapTest.cs` tests that exercised `CanUndoMove`/`UndoLastMove`/`ClearMovePath` are
removed. 2 tests that called `CommitHeroMove(..., costPaid: N)` are updated to
`CommitHeroMove(...)` (remove `costPaid:` arg). Total WorldMap tests drop by 9.

**AC8 — All surviving tests pass**
`dotnet test tests/maguswarrior.Tests.csproj` — 184 baseline − 9 removed + ≥11 new = **≥186 green**.

---

## Implementation Tasks

### Task 0 — Add `UndoController.cs` to test project compile includes

**File:** `tests/maguswarrior.Tests.csproj`

The test project uses per-file `<Compile>` includes for `scripts/core/` (comment says "per-file
because Log.cs and GameDebug.cs have direct Godot deps"). Add the new file:

```xml
<Compile Include="../scripts/core/UndoController.cs" />
```

Add it immediately after the `GameState.cs` include (line 26). Do this BEFORE writing any tests
so the test project compiles as you add the new file.

### Task 1 — Create `UndoController.cs` (TDD — start with failing tests)

**Test file (write RED first):** `tests/unit/UndoControllerTest.cs`

```csharp
using System.Collections.Generic;
using MagusWarrior.Cards;
using MagusWarrior.Core;
using MagusWarrior.Core.Types;
using MagusWarrior.Deck;
using MagusWarrior.Hex;
using MagusWarrior.Map;
using MagusWarrior.Core.Types;
using Xunit;

namespace MagusWarrior.Tests;

public class UndoControllerTest {
    // Helpers
    private static GameState EmptyState() => new(new List<CardDefinition>());
    private static GameStateSnapshot Snap(GameState s) => s.TakeSnapshot();
    private static WorldMap EmptyMap() => new(new HexCoord(0, 0));
    private static DeckManager EmptyDeck() => new();

    [Fact]
    public void CanUndo_FalseInitially() {
        var uc = new UndoController();
        Assert.False(uc.CanUndo);
    }

    [Fact]
    public void PushCardPlay_ThenExecuteUndo_RestoresStateAndReturnsCard() {
        var state = EmptyState();
        var deck = new DeckManager();
        var card = state.Cards.First(c => c.Id == "march");
        deck.SetHand(new[] { card });
        var snap = Snap(state);
        state.AddMovePoints(3);

        var uc = new UndoController();
        uc.PushCardPlay("march", null, snap);
        deck.PlayCard("march");  // remove from hand (simulates what HandView does)

        var result = uc.ExecuteUndo(state, deck, EmptyMap());

        Assert.True(result.IsSuccess);
        Assert.Equal(0, state.MovePointsThisTurn);   // snapshot restored
        Assert.Single(deck.Hand);                    // card back in hand
        Assert.False(uc.CanUndo);
    }

    [Fact]
    public void PushCardPlay_WithCostCard_ExecuteUndo_RecallsCostCard() {
        var state = EmptyState();
        var deck = new DeckManager();
        var march = state.Cards.First(c => c.Id == "march");
        deck.SetHand(new[] { march });
        deck.DiscardCard("march");                   // march is now in discard
        var snap = Snap(state);

        var uc = new UndoController();
        uc.PushCardPlay("improvisation", "march", snap);

        var result = uc.ExecuteUndo(state, deck, EmptyMap());

        Assert.True(result.IsSuccess);
        Assert.Single(deck.Hand);      // march recalled to hand
        Assert.Empty(deck.DiscardPile);
    }

    [Fact]
    public void PushCardPlay_CostCardNotInDiscard_Aborts_NothingMutated() {
        var state = EmptyState();
        state.AddMovePoints(5);
        var snap = Snap(state);
        var deck = EmptyDeck();  // discard pile is empty

        var uc = new UndoController();
        uc.PushCardPlay("improvisation", "march", snap);

        var result = uc.ExecuteUndo(state, deck, EmptyMap());

        Assert.False(result.IsSuccess);
        Assert.Equal(5, state.MovePointsThisTurn);   // NOT rolled back
        Assert.True(uc.CanUndo);                     // entry put back on stack
    }

    [Fact]
    public void PushCardPlay_Multiple_UndoesInLifoOrder() {
        var state = EmptyState();
        var deck = EmptyDeck();
        var snap1 = Snap(state);
        state.AddMovePoints(1);
        var snap2 = Snap(state);
        state.AddMovePoints(1);

        var uc = new UndoController();
        uc.PushCardPlay("march", null, snap1);
        uc.PushCardPlay("swiftness", null, snap2);

        // First undo pops swiftness (restores to snap2 = 1 point)
        uc.ExecuteUndo(state, deck, EmptyMap());
        Assert.Equal(1, state.MovePointsThisTurn);

        // Second undo pops march (restores to snap1 = 0 points)
        uc.ExecuteUndo(state, deck, EmptyMap());
        Assert.Equal(0, state.MovePointsThisTurn);

        Assert.False(uc.CanUndo);
    }

    [Fact]
    public void PushHeroMove_ThenPopHeroMove_ReturnsEntryAndStackEmpty() {
        var uc = new UndoController();
        uc.PushHeroMove(new HexCoord(0, 0), costRefund: 2);

        var entry = uc.PopHeroMove();

        Assert.NotNull(entry);
        Assert.Equal(new HexCoord(0, 0), entry!.Previous);
        Assert.Equal(2, entry.CostRefund);
        Assert.False(uc.CanUndo);
    }

    [Fact]
    public void PushHeroMove_ThenExecuteUndo_RestoresPositionAndRefundsPoints() {
        var state = EmptyState();
        state.AddMovePoints(5);
        state.SpendMovePoints(2);   // spent 2 to walk
        var map = EmptyMap();
        map.SetHeroPosition(new HexCoord(1, 0));   // hero walked here

        var uc = new UndoController();
        uc.PushHeroMove(new HexCoord(0, 0), costRefund: 2);

        uc.ExecuteUndo(state, EmptyDeck(), map);

        Assert.Equal(new HexCoord(0, 0), map.HeroPosition);
        Assert.Equal(3, state.MovePointsThisTurn);  // 5 - 2 + 2 = 3
    }

    [Fact]
    public void Clear_EmptiesStack() {
        var uc = new UndoController();
        var snap = EmptyState().TakeSnapshot();
        uc.PushCardPlay("march", null, snap);
        uc.PushHeroMove(new HexCoord(0, 0), costRefund: 1);

        uc.Clear();

        Assert.False(uc.CanUndo);
        Assert.False(uc.CanUndoHeroMove);
    }

    [Fact]
    public void CanUndoHeroMove_TrueOnlyWhenTopIsHeroMoveEntry() {
        var uc = new UndoController();
        Assert.False(uc.CanUndoHeroMove);

        var snap = EmptyState().TakeSnapshot();
        uc.PushCardPlay("march", null, snap);
        Assert.False(uc.CanUndoHeroMove);   // top is CardPlayGroup

        uc.PushHeroMove(new HexCoord(0, 0), 1);
        Assert.True(uc.CanUndoHeroMove);    // top is HeroMoveEntry
    }

    [Fact]
    public void PopHeroMove_WhenTopIsCardPlay_ReturnsNull() {
        var uc = new UndoController();
        uc.PushCardPlay("march", null, EmptyState().TakeSnapshot());

        Assert.Null(uc.PopHeroMove());
        Assert.True(uc.CanUndo);   // stack not modified
    }

    [Fact]
    public void PushCardPlay_PushHeroMove_ExecuteUndo_PopsHeroMoveFirst() {
        var state = EmptyState();
        state.AddMovePoints(3);
        var snap = Snap(state);
        var map = EmptyMap();
        map.SetHeroPosition(new HexCoord(1, 0));

        var uc = new UndoController();
        uc.PushCardPlay("march", null, snap);          // bottom
        uc.PushHeroMove(new HexCoord(0, 0), 2);       // top

        uc.ExecuteUndo(state, EmptyDeck(), map);

        // Hero move undone; card play still on stack
        Assert.Equal(new HexCoord(0, 0), map.HeroPosition);
        Assert.True(uc.CanUndo);
    }

    [Fact]
    public void PushHeroMove_PushCardPlay_ExecuteUndo_PopsCardPlayFirst() {
        var state = EmptyState();
        var snap = Snap(state);
        state.AddMovePoints(3);
        var map = EmptyMap();
        map.SetHeroPosition(new HexCoord(1, 0));

        var uc = new UndoController();
        uc.PushHeroMove(new HexCoord(0, 0), 2);       // bottom
        uc.PushCardPlay("march", null, snap);          // top

        uc.ExecuteUndo(state, EmptyDeck(), map);

        // Card play undone (state restored to snap = 0 points); hero move still on stack
        Assert.Equal(0, state.MovePointsThisTurn);
        Assert.True(uc.CanUndo);
    }
}
```

Confirm RED (compile error: `UndoController` does not exist). Then implement:

**New file:** `scripts/core/UndoController.cs`

```csharp
using System.Collections.Generic;
using System.Linq;
using MagusWarrior.Deck;
using MagusWarrior.Hex;
using MagusWarrior.Map;

namespace MagusWarrior.Core;

public abstract record UndoEntry;

public record CardPlayGroup(
    string SourceCardId,
    string? CostCardId,
    GameStateSnapshot StateBefore) : UndoEntry;

public record HeroMoveEntry(
    HexCoord Previous,
    int CostRefund) : UndoEntry;

public class UndoController {
    private readonly Stack<UndoEntry> _stack = new();

    public bool CanUndo => _stack.Count > 0;
    public bool CanUndoHeroMove => _stack.Count > 0 && _stack.Peek() is HeroMoveEntry;

    public void PushCardPlay(string sourceCardId, string? costCardId, GameStateSnapshot stateBefore) =>
        _stack.Push(new CardPlayGroup(sourceCardId, costCardId, stateBefore));

    public void PushHeroMove(HexCoord previous, int costRefund) =>
        _stack.Push(new HeroMoveEntry(previous, costRefund));

    // Returns the HeroMoveEntry if it is on top of the stack and pops it; null otherwise (stack unchanged).
    public HeroMoveEntry? PopHeroMove() {
        if (_stack.Count == 0 || _stack.Peek() is not HeroMoveEntry) return null;
        return (HeroMoveEntry)_stack.Pop();
    }

    // Pops and executes whatever is on top. Requires all three deps; hero-move undo only uses state+map.
    public Result<string> ExecuteUndo(GameState state, DeckManager deck, WorldMap map) {
        if (_stack.Count == 0) return Result<string>.Fail("Nothing to undo");
        var entry = _stack.Pop();
        return entry switch {
            CardPlayGroup g => ExecuteCardPlayUndo(g, state, deck),
            HeroMoveEntry m => ExecuteHeroMoveUndo(m, state, map),
            _ => Result<string>.Fail($"Unknown undo entry type: {entry.GetType().Name}")
        };
    }

    public void Clear() => _stack.Clear();

    private Result<string> ExecuteCardPlayUndo(CardPlayGroup group, GameState state, DeckManager deck) {
        // Attempt cost-card recall first — it is the only step that can fail.
        // If it fails, abort before mutating anything (prevent partial rollback).
        if (group.CostCardId != null) {
            var recall = deck.RecallFromDiscard(group.CostCardId);
            if (!recall.IsSuccess) {
                _stack.Push(group); // put back — nothing mutated
                return Result<string>.Fail(
                    $"Undo aborted: cost card '{group.CostCardId}' not in discard — {recall.Error}");
            }
        }
        var card = state.Cards.FirstOrDefault(c => c.Id == group.SourceCardId);
        if (card != null) deck.ReturnCard(card);
        state.RestoreSnapshot(group.StateBefore);
        return Result<string>.Ok($"Undo play: {group.SourceCardId}");
    }

    private static Result<string> ExecuteHeroMoveUndo(HeroMoveEntry move, GameState state, WorldMap map) {
        map.SetHeroPosition(move.Previous);   // fires HeroMoved → HexMapView marker updates
        state.AddMovePoints(move.CostRefund);
        return Result<string>.Ok($"Undo hero move: refund={move.CostRefund}");
    }
}
```

Run GREEN: `dotnet test tests/maguswarrior.Tests.csproj`. All 11+ new tests pass.

### Task 2 — Add `UndoController` to `GameState` and wire into `TripUndoGate`

**File:** `scripts/core/GameState.cs`

Add property (after `GameEventLog EventLog`):
```csharp
public UndoController UndoController { get; } = new();
```

In `TripUndoGate()`, add `UndoController.Clear()`:
```csharp
public void TripUndoGate() {
    LastGateSnapshot = TakeSnapshot();
    EventLog.Clear();
    UndoController.Clear();       // ← add this line
    UndoGateCrossed?.Invoke();
#if GODOT
    Log.Debug("[Core]", "Undo gate crossed — snapshot written, event log cleared");
#endif
}
```

Run tests — should be green (no existing test constructs `TripUndoGate` in a way that would conflict with a new no-arg `UndoController()`).

### Task 3 — Remove `WorldMap._movePath` tracking

**File:** `scripts/map/WorldMap.cs`

**Remove these members entirely:**
- `private readonly List<(HexCoord Previous, int CostPaid)> _movePath = new();`
- `public bool CanUndoMove => _movePath.Count > 0;`
- `public (HexCoord Previous, int CostRefund)? UndoLastMove()` (entire method)
- `public void ClearMovePath()` (entire method)

**Simplify `CommitHeroMove` — remove `costPaid` parameter:**
```csharp
// Before:
public void CommitHeroMove(HexCoord coord, int costPaid) {
    _movePath.Add((HeroPosition, costPaid));
    SetHeroPosition(coord);
}

// After:
public void CommitHeroMove(HexCoord coord) {
    SetHeroPosition(coord);
}
```

**Remove `ClearMovePath()` call from `RevealTile`:**
```csharp
// Before:
public void RevealTile(MapTile tile) {
    if (tile.IsRevealed) return;
    tile.Reveal();
    foreach (var (worldCoord, terrain) in tile.WorldHexes())
        _grid.Add(worldCoord, new HexState(terrain));
    ClearMovePath();     // ← REMOVE this line
    TileRevealed?.Invoke(tile);
}

// After:
public void RevealTile(MapTile tile) {
    if (tile.IsRevealed) return;
    tile.Reveal();
    foreach (var (worldCoord, terrain) in tile.WorldHexes())
        _grid.Add(worldCoord, new HexState(terrain));
    TileRevealed?.Invoke(tile);
}
```

**Build check:** `dotnet build maguswarrior.csproj` will now fail on callers of the removed members.
Proceed to fix callers in subsequent tasks.

### Task 4 — Update `WorldMapTest.cs`

**File:** `tests/unit/WorldMapTest.cs`

**Delete these 9 test methods entirely** (they test removed `WorldMap` features):
- `CanUndoMove_FalseInitially`
- `CommitHeroMove_EnablesUndo`
- `UndoLastMove_RestoresPreviousPosition`
- `UndoLastMove_ReturnsCorrectCostRefund`
- `UndoLastMove_FiresHeroMoved`
- `UndoLastMove_WhenNothingToUndo_ReturnsNull`
- `UndoLastMove_MultiHop_UndoesInReverseOrder`
- `ClearMovePath_DisablesUndo`
- `RevealTile_ClearsMovePath`

**Update these 2 tests** — remove `costPaid:` named argument:
```csharp
// CommitHeroMove_UpdatesHeroPosition: change
map.CommitHeroMove(new HexCoord(1, 0), costPaid: 2);  // →
map.CommitHeroMove(new HexCoord(1, 0));

// CommitHeroMove_FiresHeroMoved: change
map.CommitHeroMove(new HexCoord(1, 0), costPaid: 2);  // →
map.CommitHeroMove(new HexCoord(1, 0));
```

Run tests after Task 3+4: all WorldMap tests pass, UndoController tests pass.

### Task 5 — Update `HexMapView` to use `UndoController`

**File:** `scripts/ui/components/HexMapView.cs`

**Branch 1 — hero-hex tap (undo move):**

```csharp
// Before:
if (coord == _map.HeroPosition) {
    if (!_map.CanUndoMove) return;
    var result = _map.UndoLastMove();
    if (result is { } r) {
        _previewedHex = null;
        _previewIsExplore = false;
        _previewLabel.Visible = false;
        _state.AddMovePoints(r.CostRefund);
        Log.Debug("[Input]", $"Move undone: back to {r.Previous.Q},{r.Previous.R} refund={r.CostRefund} remaining={_state.MovePointsThisTurn}");
    }
    return;
}

// After:
if (coord == _map.HeroPosition) {
    if (!_state.UndoController.CanUndoHeroMove) return;
    var move = _state.UndoController.PopHeroMove();
    if (move is { } m) {
        _previewedHex = null;
        _previewIsExplore = false;
        _previewLabel.Visible = false;
        // SetHeroPosition fires HeroMoved → marker updates automatically
        _map.SetHeroPosition(m.Previous);
        _state.AddMovePoints(m.CostRefund);
        Log.Debug("[Input]", $"Move undone: back to {m.Previous.Q},{m.Previous.R} refund={m.CostRefund} remaining={_state.MovePointsThisTurn}");
    }
    return;
}
```

**Branch 2 — revealed hex / move commit:**

```csharp
// Before:
if (isAdjacent && cost != null && _state.MovePointsThisTurn >= cost.Value) {
    _previewedHex = null;
    _previewIsExplore = false;
    _previewLabel.Visible = false;
    _state.SpendMovePoints(cost.Value);
    _map.CommitHeroMove(coord, cost.Value);
    Log.Debug("[Input]", $"Hero moved to {coord.Q},{coord.R} cost={cost} remaining={_state.MovePointsThisTurn}");
}

// After:
if (isAdjacent && cost != null && _state.MovePointsThisTurn >= cost.Value) {
    _previewedHex = null;
    _previewIsExplore = false;
    _previewLabel.Visible = false;
    var prevPos = _map.HeroPosition;   // capture BEFORE CommitHeroMove changes it
    _state.SpendMovePoints(cost.Value);
    _map.CommitHeroMove(coord);        // no costPaid arg
    _state.UndoController.PushHeroMove(prevPos, cost.Value);
    Log.Debug("[Input]", $"Hero moved to {coord.Q},{coord.R} cost={cost} remaining={_state.MovePointsThisTurn}");
}
```

No changes needed to Branch 3 (tile reveal) — `TripUndoGate()` already calls
`UndoController.Clear()` (Task 2), so moves are cleared automatically before reveal.
`HexMapView` does NOT need to call `UndoController.Clear()` directly.

### Task 6 — Update `HandView` to push to `UndoController`

**File:** `scripts/ui/components/HandView.cs`

**Add `WorldMap` dependency:**
```csharp
using MagusWarrior.Map;   // add at top

private WorldMap _map = null!;  // add field
```

**Update `Initialize` signature:**
```csharp
public void Initialize(DeckManager deck, GameState state, EffectScheduler scheduler, InputLock inputLock, WorldMap map) {
    ...
    _map = map;
    ...
}
```

**`OnPlayRequested` — capture `stateBefore` and push after `ResolveAll`:**
```csharp
private async void OnPlayRequested(string cardId) {
    if (!_lock.TryAcquire()) return;
    try {
        var card = _deck.Hand.FirstOrDefault(c => c.Id == cardId);
        if (card is null) { ... return; }
        if (card.Type == CardType.Wound) { ... return; }
        if (_state.CurrentPhase == GamePhase.Rest && ...) { ... return; }
        if (cardId == "improvisation" && _improvView != null) {
            _improvView.Activate(card);
            return;
        }
        if (card.Unpowered is null) { ... return; }
        var result = _deck.PlayCard(cardId);
        if (!result.IsSuccess) { ... return; }
        var effect = BuildEffect(card, card.Unpowered.EffectType);
        if (effect is null) { ... return; }
        var stateBefore = _state.TakeSnapshot();   // ← capture BEFORE enqueue
        var ctx = new EffectContext(card.Id, card.Unpowered.EffectType, _state.CurrentPhase, false);
        _scheduler.Enqueue(effect, 0, ctx);
        await _scheduler.ResolveAll(_state);
        _state.UndoController.PushCardPlay(card.Id, null, stateBefore);   // ← push AFTER resolve
        Log.Debug("[UI]", $"Play resolved: {cardId} → {card.Unpowered.EffectType}");
    } finally {
        _lock.Release();
    }
}
```

**`OnPlaySidewaysRequested` — same pattern (no cost card for sideways):**
```csharp
// After await _scheduler.ResolveAll(_state);
_state.UndoController.PushCardPlay(cardId, null, stateBefore);
// (stateBefore captured just before _scheduler.Enqueue, same as above)
```

**`OnUndoRequested` — replace body with `UndoController.ExecuteUndo`:**
```csharp
private void OnUndoRequested() {
    if (_lock.IsLocked) return;
    var result = _state.UndoController.ExecuteUndo(_state, _deck, _map);
    if (result.IsSuccess)
        Log.Debug("[UI]", $"Undo: {result.Value}");
    else
        Log.Debug("[UI]", $"OnUndoRequested: {result.Error}");
}
```

**Remove all `EventLog` references from `HandView`** — the new `OnUndoRequested` does not read `_state.EventLog` at all.

### Task 7 — Update `ImprovisationView` to push to `UndoController`

**File:** `scripts/ui/components/ImprovisationView.cs`

In `OnResourceSelected`, capture `stateBefore` and `sourceCardId` BEFORE the resolve call,
then push AFTER `ResolveAll`:

```csharp
private async void OnResourceSelected(EffectType effectType, int amount) {
    if (!_lock.TryAcquire()) return;
    try {
        if (_improvCard is null) return;
        var costCardId = _discardedCard?.Id;
        var stateBefore = _state.TakeSnapshot();        // ← capture BEFORE enqueue
        var sourceCardId = _improvCard.Id;              // ← capture before _improvCard = null
        var effect = new ImprovisationEffect(effectType, amount);
        var ctx = new EffectContext(_improvCard.Id, effectType, _state.CurrentPhase, false, costCardId);
        _scheduler.Enqueue(effect, 0, ctx);
        await _scheduler.ResolveAll(_state);
        _state.UndoController.PushCardPlay(sourceCardId, costCardId, stateBefore);  // ← push AFTER
        Log.Debug("[UI]", $"Improvisation resolved: {effectType} {amount} in {_state.CurrentPhase}");
        _improvCard = null;
        _discardedCard = null;
        Visible = false;
    } finally {
        _lock.Release();
    }
}
```

Note: `sourceCardId` must be captured BEFORE `_improvCard = null` on line `_improvCard = null`.

### Task 8 — Update `PlaceholderMainMenu` wiring

**File:** `scripts/ui/screens/PlaceholderMainMenu.cs`

**1. Remove `UndoGateCrossed += ClearMovePath` subscription** (WorldMap no longer has `ClearMovePath`):
```csharp
// Remove this line:
_state.UndoGateCrossed += _worldMap.ClearMovePath;
```
`UndoController.Clear()` is called directly from `TripUndoGate` (Task 2) — no event subscription needed.

**2. Update `handView.Initialize` to pass `_worldMap`:**
```csharp
// Before:
handView.Initialize(_deckManager, _state, _effectScheduler, _inputLock);

// After:
handView.Initialize(_deckManager, _state, _effectScheduler, _inputLock, _worldMap);
```

No other changes to `PlaceholderMainMenu`.

### Task 9 — Run full test suite and fix fallout

```bash
dotnet test tests/maguswarrior.Tests.csproj
```

**Expected test count: 184 baseline − 9 removed WorldMap tests + ≥11 new UndoController tests = ≥186 green**

**Expected fallout to investigate:**
- Any test that constructs `HandView` or `ImprovisationView` directly: update `Initialize` calls
  (unlikely — those are Godot classes not in the test project, but check).
- `UndoGateTest.cs`: `TripUndoGate_ClearsEventLog` — verify it still passes (we added
  `UndoController.Clear()` inside `TripUndoGate`, which should be additive).
- `WorldMapTest.cs`: ensure deleted tests are gone and updated tests compile cleanly.

---

## Dev Notes

### Architecture rules (non-negotiable)

From `docs/project-context.md`:

- `UndoController` is in `scripts/core/` — no `Node` inheritance, no Godot dependency.
  It depends on `MagusWarrior.Deck` and `MagusWarrior.Map`, which are also pure-C#.
  C# has no circular-reference issue within one assembly.
- `HexMapView` is a Godot `Node2D` (in `scripts/ui/`). All Godot-specific logic stays there.
  `UndoController` has no Godot knowledge.
- `async void` signal handlers: `HandView.OnPlayRequested`, `OnPlaySidewaysRequested`, and
  `ImprovisationView.OnResourceSelected` are all `async void` because they are Godot signal handlers.
  `InputLock` prevents re-entry. This is correct and expected.
- `OnUndoRequested` is synchronous — it checks `_lock.IsLocked` but does NOT acquire.
  (Same deferred D1/D2 class from prior reviews — only a real race once UIBroker introduces
  genuinely awaitable effects. Do not change the locking behavior of this handler.)

### `stateBefore` must be captured before `_scheduler.Enqueue`

This is the critical correctness requirement for AC2. `TakeSnapshot()` must run BEFORE
`_scheduler.Enqueue(effect, 0, ctx)`. If it's captured after, it captures post-effect state,
and undo would roll back to a state that already has the effect applied — a no-op rollback.

### `sourceCardId` must be captured before `_improvCard = null` in `ImprovisationView`

In `OnResourceSelected`, after `ResolveAll`, the code sets `_improvCard = null`. The `PushCardPlay`
call uses `sourceCardId` which was captured from `_improvCard.Id` at the start of the method.
If you push AFTER setting `_improvCard = null` without the captured variable, `NullReferenceException`.

### `prevPos = _map.HeroPosition` must be captured before `CommitHeroMove`

`CommitHeroMove` calls `SetHeroPosition(coord)` which immediately updates `HeroPosition`. If you
capture `_map.HeroPosition` after `CommitHeroMove`, you get `coord` (the destination), not the
starting position — the undo would teleport the hero to the destination instead of the origin.

### `WorldMap._movePath` is gone — do not recreate it

The previous `_movePath` tracking in `WorldMap` is fully replaced by `UndoController._stack`.
`WorldMap.CommitHeroMove` no longer takes a `costPaid` parameter; the caller (`HexMapView.Branch 2`)
passes the cost directly to `UndoController.PushHeroMove`.

### `EventLog` is NOT replaced — it still accumulates for the debug inspector

`GameState.EventLog` continues to have `EffectFiredEvent`s appended by `EffectScheduler.ResolveAll`
for display in `EffectEventLogPanel`. Do NOT remove `EventLog` or stop appending to it.
The only change is that `HandView.OnUndoRequested` no longer reads from `EventLog` — it uses
`UndoController` instead.

### Test project `.csproj` requires explicit include for new `scripts/core/` files

**The test project uses per-file `<Compile>` for `scripts/core/`** (see comment in
`tests/maguswarrior.Tests.csproj` line 21). New files in `scripts/core/` are NOT auto-included
by a glob. You MUST add:
```xml
<Compile Include="../scripts/core/UndoController.cs" />
```
to `tests/maguswarrior.Tests.csproj` or the test project won't compile `UndoController`.

### Branch 3 tile-reveal and `TripUndoGate` interaction

In `HexMapView.Branch 3`, `_state.TripUndoGate()` is called BEFORE `_map.RevealTile(...)`.
`TripUndoGate` now calls `UndoController.Clear()` directly (Task 2). The old
`_state.UndoGateCrossed += _worldMap.ClearMovePath` subscription in `PlaceholderMainMenu` is
REMOVED (Task 8) because `WorldMap.ClearMovePath` no longer exists.
`WorldMap.RevealTile` no longer calls `ClearMovePath()` (Task 3).
The undo stack is therefore cleared exactly once at gate crossing, via `TripUndoGate` → `UndoController.Clear()`.

### File placement

| What | Where |
|------|-------|
| `UndoController`, `UndoEntry`, `CardPlayGroup`, `HeroMoveEntry` | `scripts/core/UndoController.cs` |
| Tests | `tests/unit/UndoControllerTest.cs` |

### Project Context Rules

- **Pure C# game logic. Scene tree is render-only.** `UndoController` must NOT inherit from `Node`.
- **Every player decision uses async/await** — the undo handler `OnUndoRequested` is synchronous
  (no `await`); this is correct because undo is a synchronous state restoration.
- **`Result<T>` for expected failures** — `ExecuteUndo` returns `Result<string>`. Never throw
  for "nothing to undo" or cost-card-recall failure.
- **`Log.Debug` with system tags** — use `[UI]` in view handlers, `[Core]` (if any) in `UndoController`.
  In practice `UndoController` has no log calls — callers log after inspecting the result.
- **TDD is required** — write failing tests (Task 1) before implementing `UndoController.cs`.

### References

- Root cause + decisions: [3-0 Review Findings `_bmad-output/implementation-artifacts/3-0-trip-undo-gate-and-remove-commit-button.md` §Review Findings]
- Project conventions: [`docs/project-context.md`]
- `WorldMap` current state: [`scripts/map/WorldMap.cs`]
- `HandView` current state: [`scripts/ui/components/HandView.cs`]
- `ImprovisationView` current state: [`scripts/ui/components/ImprovisationView.cs`]
- `HexMapView` current state: [`scripts/ui/components/HexMapView.cs`]
- `PlaceholderMainMenu` current state: [`scripts/ui/screens/PlaceholderMainMenu.cs`]
- `GameState` current state (includes `TripUndoGate`, `UndoGateCrossed`, `LastGateSnapshot`): [`scripts/core/GameState.cs`]
- `GameEventLog` current state: [`scripts/core/GameEventLog.cs`]
- Test project compile includes: [`tests/maguswarrior.Tests.csproj`]

---

## Files Changed

| File | Change |
|------|--------|
| `tests/maguswarrior.Tests.csproj` | Add `<Compile Include="../scripts/core/UndoController.cs" />` |
| `tests/unit/UndoControllerTest.cs` | **NEW** — ≥11 unit tests, TDD red-first |
| `tests/unit/WorldMapTest.cs` | Remove 9 tests; update 2 `CommitHeroMove` calls (remove `costPaid:` arg) |
| `scripts/core/UndoController.cs` | **NEW** — `UndoEntry`, `CardPlayGroup`, `HeroMoveEntry`, `UndoController` |
| `scripts/core/GameState.cs` | Add `UndoController` property; call `UndoController.Clear()` in `TripUndoGate` |
| `scripts/map/WorldMap.cs` | Remove `_movePath`, `CanUndoMove`, `UndoLastMove`, `ClearMovePath`; simplify `CommitHeroMove`; remove `ClearMovePath()` from `RevealTile` |
| `scripts/ui/components/HexMapView.cs` | Branch 1: `UndoController.CanUndoHeroMove`/`PopHeroMove`; Branch 2: capture `prevPos`, simplify `CommitHeroMove(coord)`, push `UndoController.PushHeroMove` |
| `scripts/ui/components/HandView.cs` | Add `WorldMap` field + `Initialize` param; `OnPlayRequested` + `OnPlaySidewaysRequested` capture `stateBefore` + push; `OnUndoRequested` delegates to `UndoController.ExecuteUndo` |
| `scripts/ui/components/ImprovisationView.cs` | `OnResourceSelected` captures `stateBefore` + `sourceCardId` before resolve; pushes `UndoController.PushCardPlay` after |
| `scripts/ui/screens/PlaceholderMainMenu.cs` | Remove `UndoGateCrossed += ClearMovePath`; update `handView.Initialize` to pass `_worldMap` |
| `_bmad-output/implementation-artifacts/sprint-status.yaml` | Mark 3-0b `ready-for-dev` (done by create-story) |

---

## Architecture Compliance Checklist

- [x] `UndoController`, `CardPlayGroup`, `HeroMoveEntry` are pure C# — no Godot inheritance
- [x] `UndoController` is in `scripts/core/` — uses `MagusWarrior.Deck` and `MagusWarrior.Map`
      (intra-assembly deps, no circular project references)
- [x] `scripts/core/UndoController.cs` added to `tests/maguswarrior.Tests.csproj` compile includes
- [x] `stateBefore = _state.TakeSnapshot()` called BEFORE `_scheduler.Enqueue(...)` in all callers
- [x] `sourceCardId` captured from `_improvCard.Id` BEFORE `_improvCard = null` in `ImprovisationView`
- [x] `prevPos = _map.HeroPosition` captured BEFORE `CommitHeroMove(coord)` in `HexMapView`
- [x] `EventLog` still accumulates events (debug inspector unchanged)
- [x] `WorldMap` has no `_movePath`, `CanUndoMove`, `UndoLastMove`, `ClearMovePath`
- [x] `CommitHeroMove(HexCoord coord)` — single parameter, no `costPaid`
- [x] `PlaceholderMainMenu` has no `UndoGateCrossed += _worldMap.ClearMovePath`
- [x] `OnUndoRequested` does not read `_state.EventLog` — only calls `UndoController.ExecuteUndo`
- [x] `TripUndoGate` calls `UndoController.Clear()` (in addition to `EventLog.Clear()`)
- [x] `Log.Debug` uses `[UI]` tag in view handlers; no direct `GD.Print` calls
- [x] 9 WorldMap move-path tests removed; 2 `CommitHeroMove` tests updated
- [x] `StagingManager` still NOT deleted (spec retained it for possible combat reuse)

---

## Definition of Done

- [x] `UndoControllerTest.cs` written RED-first before any implementation (Task 0+1)
- [x] ≥11 new `UndoController` unit tests pass GREEN (12 written)
- [x] 9 `WorldMapTest.cs` tests removed; 2 updated; remaining WorldMap tests pass
- [x] `dotnet build maguswarrior.csproj` — 0 errors, 0 warnings
- [x] `dotnet test tests/maguswarrior.Tests.csproj` — 190 green (184 − 9 + 12 + 3 review-fix)
- [x] Opus 4.8 code review passed (ultrareview: 1 false positive, 2 real findings fixed)
- [x] Manual verification (WSLg desktop) — all passed 2026-06-19:
  - [x] AC3: Walk → tap hero hex → hero back, points refunded (hero-move undo via `UndoController`)
  - [x] AC4: Play Move card → Walk → Undo button → walk reversed → Undo again → card returned
  - [x] AC4b: Walk → tap hero hex (undo walk) → Undo button → card returned
  - [x] AC5: Play card → Walk → Reveal tile → Undo button → no-op; hero hex tap → no-op
  - [x] Improvisation undo still works (Improvisation → pick resource → Undo → both cards back in hand)
  - [x] StagingAreaView values update correctly after undo
  - Note: one intermittent undo-move no-op observed then self-resolved; logged for watch (suspect input-lock race), not blocking.

---

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

- Test path issue: two tests used `CardLoader.LoadAll("data/cards.yaml")` with wrong CWD. Fixed by using inline `CardDefinition` objects (no file I/O) — same pattern as `DeckManagerTest.cs`.
- Arithmetic bug in `PushHeroMove_ThenExecuteUndo_RestoresPositionAndRefundsPoints`: expected 3, should be 5 (3 remaining + 2 refunded = 5). Fixed.
- `EffectEventLogPanel.OnUndoLastPressed` had an unexpected `_map?.ClearMovePath()` call (debug tool). Removed.
- `PopHeroMove_WhenTopIsCardPlay_ReturnsNull` — added extra test to cover gap in AC spec (12 tests vs ≥11).

### Completion Notes List

- All 9 tasks complete. Build clean, 190 tests green (187 + 3 review-fix tests).
- `EffectEventLogPanel.cs` also needed `ClearMovePath` removal (not in original task list but required for build).
- Test file uses fully inline `CardDefinition` objects; no `cards.yaml` dependency in `UndoControllerTest.cs`.

#### Ultrareview findings addressed (Opus cloud review)

- **merged_bug_001 (FALSE POSITIVE for working tree):** Reviewer reported `UndoController.cs`/`UndoControllerTest.cs` missing. Cause: cloud bundle captured tracked files only; both new files are present locally but untracked. Action item — **`git add` the two new files before committing** or the branch won't build. Local build/tests are genuinely green.
- **bug_013 (REAL, normal):** `GameState.RestoreSnapshot` mutated observed fields without firing `ResourcesChanged`/`DayNightChanged`. `StagingAreaView` refreshes only on `ResourcesChanged`, so card-play/Improvisation undo left the HUD stale. Fixed: `RestoreSnapshot` now fires `ResourcesChanged` always and `DayNightChanged` when `IsDay` flips. Added 3 tests.
- **merged_bug_003 (REAL, nit):** Debug `EffectEventLogPanel.OnUndoLastPressed` popped `EventLog` + `RestoreSnapshot` without touching `UndoController`, recreating the double-refund the old `ClearMovePath()` defended against — now transposed onto the hero-move stack. Fixed: added `_state.UndoController.Clear()` after `UndoLastEvent`. Also removed dead `_map` field/param/`using` and updated the `PlaceholderMainMenu` call site to `Initialize(_state)`.

### File List

| File | Action |
|------|--------|
| `tests/maguswarrior.Tests.csproj` | Modified — added `UndoController.cs` compile include |
| `tests/unit/UndoControllerTest.cs` | Created — 12 unit tests |
| `tests/unit/WorldMapTest.cs` | Modified — 9 tests removed, 2 updated |
| `scripts/core/UndoController.cs` | Created — `UndoEntry`, `CardPlayGroup`, `HeroMoveEntry`, `UndoController` |
| `scripts/core/GameState.cs` | Modified — `UndoController` property; `UndoController.Clear()` in `TripUndoGate`; `RestoreSnapshot` fires `ResourcesChanged`/`DayNightChanged` (bug_013) |
| `tests/unit/GameStateTest.cs` | Modified — 3 tests for `RestoreSnapshot` event firing (bug_013) |
| `scripts/map/WorldMap.cs` | Modified — removed `_movePath`, `CanUndoMove`, `UndoLastMove`, `ClearMovePath`; simplified `CommitHeroMove` |
| `scripts/ui/components/HexMapView.cs` | Modified — Branch 1 + Branch 2 updated |
| `scripts/ui/components/HandView.cs` | Modified — `WorldMap` added; `stateBefore` capture; `OnUndoRequested` rewritten |
| `scripts/ui/components/ImprovisationView.cs` | Modified — `stateBefore` + `sourceCardId` capture; `PushCardPlay` after resolve |
| `scripts/ui/screens/PlaceholderMainMenu.cs` | Modified — removed `UndoGateCrossed += ClearMovePath`; updated `handView.Initialize` |
| `scripts/ui/debug/EffectEventLogPanel.cs` | Modified — removed stale `_map?.ClearMovePath()`; added `UndoController.Clear()` to debug undo (merged_bug_003); removed dead `_map` field/param/`using` |
