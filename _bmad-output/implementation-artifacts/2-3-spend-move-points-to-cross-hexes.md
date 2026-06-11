# Story 2-3 — Spend Move Points to Cross Hexes

**Epic:** 2 — Hex Map + Movement
**Story ID:** 2-3
**Status:** done
**Created:** 2026-06-10
**Dependencies:** 2-2 (HexMapView tap handler, TerrainCosts, GameState.IsDay, InputLock wiring — all complete and reviewed)
**Reviewer model:** Use Opus 4.8 for code review (per project feedback convention)

---

## User Story

As a player, I spend Move points to cross hexes with terrain costs applying correctly, and I can undo any move until I reveal a new tile.

---

## Context

### What stories 2-0 through 2-2 built (do not recreate any of this)

- **`InputLock`** (`scripts/ui/InputLock.cs`) — shared re-entrancy guard; already injected into `HexMapView`
- **`HexCoord`** (`scripts/hex/`) — axial `readonly record struct`, `Neighbors()` yields 6 adjacent coords, `Distance()`, `+` operator; value equality (`==`) works correctly
- **`HexGrid`** — `GetState()`, `GetNeighbors()`, `AllHexes()`, `Contains()`
- **`HexState(TerrainType)`** — grid membership IS the revealed predicate
- **`WorldMap`** — `PlaceTile`, `Grid`, `HeroPosition`, `SetHeroPosition(coord)` fires `HeroMoved`
- **`TerrainCosts.GetCost(TerrainType, bool isDay)`** — returns `int?` (null = impassable); 18 unit tests
- **`GameState.IsDay`**, **`AddMovePoints`**, **`ResourcesChanged`**, **`DayNightChanged`** — all wired
- **`HexMapView`** — `Initialize(WorldMap, GameState, InputLock)`; `_UnhandledInput` (primary touch, `SetInputAsHandled`); `_previewedHex` field; `RefreshPreview` subscribed to `ResourcesChanged` + `DayNightChanged`; cost preview label with affordability color
- **`PlaceholderMainMenu.BuildStartingMap()`** — 7-hex tile (Plains ×3, Forest, Hills, Swamp, Wasteland)
- **143 tests passing** at story 2-2 close

### Epic 1 Retro Action Item 3 — addressed in this story

`GameState.MovePointsThisTurn` has no reset path. Flagged at story 1a-1, deferred to "story 2-2 or start of 2-3." This story adds `GameState.ResetMovePoints()` — `TurnManager` (Epic 7) will call it at turn start. See: `deferred-work.md` and `epic-1-retro-2026-06-06.md` (Action Item 3).

### What story 2-3 adds

1. **`GameState.SpendMovePoints(int n)`** — decrements `MovePointsThisTurn`, fires `ResourcesChanged`
2. **`GameState.ResetMovePoints()`** — sets `MovePointsThisTurn` to 0, fires `ResourcesChanged` (Retro Action Item 3)
3. **`WorldMap.CommitHeroMove(HexCoord coord, int costPaid)`** — undo-tracked movement; records `(previousPosition, costPaid)` before calling `SetHeroPosition`
4. **`WorldMap.UndoLastMove()`** — returns `(HexCoord previous, int costRefund)?`; null if nothing to undo. `HexMapView` calls `AddMovePoints(costRefund)` after. Fires `HeroMoved`.
5. **`WorldMap.CanUndoMove`** — `bool` property; true when move path is non-empty
6. **`WorldMap.ClearMovePath()`** — called by story 2-4 (tile exploration = undo gate). Added here as a stub so 2-4 has a clean API to call.
7. **`HexMapView`** — three-way tap dispatch: tap hero's current hex → undo; tap valid adjacent → commit; tap anything else → preview. No two-step confirmation: single tap commits immediately (undo replaces the need for confirmation).
8. **Tests** — 5 new `GameStateTest` + ~10 new `WorldMapTest`

### Interaction model rationale

No confirmation tap is needed because undo is free until tile exploration (the only undo gate in this game — revealed new information is final). Single tap on a valid adjacent hex commits immediately; the player undoes by tapping their own current position. This matches the fluency of the board game (pick up figure, place on destination; put it back if you change your mind) while being discoverable on a touch screen.

**Undo gate:** tile exploration (story 2-4) calls `WorldMap.ClearMovePath()` — after that, the moves from this turn are final. `WorldMap.ClearMovePath()` is stubbed in this story so 2-4 has a clean API.

### Deferred items from story 2-2 still open (do not fix in this story)

- `HeroMoved` subscription never unsubscribed — deferred codebase-wide
- `PlaceTile` silent overwrite on hex collision — deferred to story 2-4
- `MapTile.Reveal()` back-propagation — deferred to story 2-4
- `HexState.IsRevealed` redundant flag — pending decision
- `IsDay` in per-effect undo snapshot can roll back a day/night flip — deferred to Epic 7
- `ToLocal` camera assumption — deferred, no Camera2D yet

---

## Terrain Cost Table (canonical source: `docs/hex-movement-lld.md §2`)

| TerrainType | Day | Night | Impassable? |
|-------------|:---:|:-----:|:-----------:|
| Plains      | 2   | 2     | |
| Hills       | 3   | 3     | |
| Forest      | 3   | 5     | |
| Desert      | 5   | 3     | |
| Swamp       | 5   | 5     | |
| Wasteland   | 4   | 4     | |
| Mountain    | —   | —     | ✓ |
| Lake        | —   | —     | ✓ |
| CitySpace   | 2   | 2     | |

Hero pays the cost of the **destination** hex. [hex-movement-lld.md §4.4]

---

## Acceptance Criteria

**AC1 — Tap valid adjacent hex commits immediately**
When the player taps a hex that is adjacent to `WorldMap.HeroPosition`, has `TerrainCosts.GetCost != null`, and `MovePointsThisTurn >= cost`: `MovePointsThisTurn` decrements by the terrain cost, `WorldMap.HeroPosition` updates to that hex, the preview label hides, and `_previewedHex` clears. Single tap — no confirmation required.

**AC2 — Tap non-adjacent hex previews only**
Tapping a hex not in `HeroPosition.Neighbors()` shows the cost preview (2-2 behavior) and does not commit. Repeated taps on a non-adjacent hex keep updating the preview but never move the hero.

**AC3 — Tap impassable hex previews "Impassable", never commits**
Tapping Mountain or Lake shows "Impassable" (grey). Re-tapping or tapping as a "second tap" never moves the hero.

**AC4 — Tap unaffordable adjacent hex previews red, never commits**
Tapping an adjacent hex when `MovePointsThisTurn < cost` shows the red affordability label. No move occurs.

**AC5 — Tap hero's current hex undoes last move**
When `WorldMap.CanUndoMove` is true, tapping the hex occupied by the hero (i.e., `coord == _map.HeroPosition`) calls `WorldMap.UndoLastMove()`, refunds the returned cost to `MovePointsThisTurn` via `AddMovePoints`, and the hero marker returns to the previous position. Preview hides and `_previewedHex` clears.

**AC6 — Tap hero's current hex when nothing to undo is a no-op**
When `WorldMap.CanUndoMove` is false, tapping the hero's current hex does nothing (no preview, no error, no crash).

**AC7 — Hero marker updates on commit and undo**
`WorldMap.CommitHeroMove` and `WorldMap.UndoLastMove` both call `SetHeroPosition` internally, which fires `HeroMoved`. `HexMapView` already subscribes to update the marker — no additional wiring needed.

**AC8 — Multi-hop undo**
After two consecutive moves (A→B→C), tapping C undoes to B (`CanUndoMove` still true); tapping B undoes to A; tapping A when `CanUndoMove` is false is a no-op.

**AC9 — StagingAreaView running total reflects spent and refunded points**
Both `SpendMovePoints` and `AddMovePoints` (refund) fire `ResourcesChanged`. `StagingAreaView` auto-refreshes — no code change.

**AC10 — `ResetMovePoints` resets to zero and fires ResourcesChanged (Retro Action Item 3)**
`GameState.ResetMovePoints()` sets `MovePointsThisTurn` to 0 and fires `ResourcesChanged`. TurnManager (Epic 7) calls this at turn start.

**AC11 — `ClearMovePath` disables undo**
After `WorldMap.ClearMovePath()`, `CanUndoMove` is false and `UndoLastMove()` returns null. (Stub for story 2-4's undo gate.)

**AC12 — InputLock guards both commit and undo**
Both paths run inside the existing `_UnhandledInput` try/finally lock (established in 2-2). No additional lock wiring.

---

## Technical Design

### Changed: `scripts/core/GameState.cs`

Add two methods below `AddMovePoints`:

```csharp
public void SpendMovePoints(int n) {
    MovePointsThisTurn -= n;
    ResourcesChanged?.Invoke();
}

// Called by TurnManager at turn start (Epic 7). Retro Action Item 3.
public void ResetMovePoints() {
    MovePointsThisTurn = 0;
    ResourcesChanged?.Invoke();
}
```

No snapshot changes — `MovePointsThisTurn` is already in `GameStateSnapshot`.

### Changed: `scripts/map/WorldMap.cs`

Add move path fields and three new methods. Keep `SetHeroPosition(HexCoord)` exactly as-is — existing tests and callers are unaffected.

**New fields:**

```csharp
private readonly List<(HexCoord Previous, int CostPaid)> _movePath = new();
```

**New members:**

```csharp
public bool CanUndoMove => _movePath.Count > 0;

// Undo-tracked movement. HexMapView calls this instead of SetHeroPosition.
public void CommitHeroMove(HexCoord coord, int costPaid) {
    _movePath.Add((HeroPosition, costPaid));
    SetHeroPosition(coord);
}

// Returns (previousPosition, costRefund) and fires HeroMoved; null if nothing to undo.
public (HexCoord Previous, int CostRefund)? UndoLastMove() {
    if (_movePath.Count == 0) return null;
    var (previous, cost) = _movePath[^1];
    _movePath.RemoveAt(_movePath.Count - 1);
    SetHeroPosition(previous);
    return (previous, cost);
}

// Called by tile exploration (story 2-4) — locks in all moves this turn.
public void ClearMovePath() => _movePath.Clear();
```

`SetHeroPosition` remains as-is (no-cost, no undo tracking). `CommitHeroMove` calls through to `SetHeroPosition`, so `HeroMoved` fires for both — `HexMapView` needs no additional wiring for the marker.

### Changed: `scripts/ui/components/HexMapView.cs`

Replace `HandleHexTap` with a three-way dispatch. Remove `TryCommitMove` (not needed — the logic is now inline). All other `HexMapView` code is unchanged.

```csharp
private void HandleHexTap(Vector2 localPos) {
    var coord = PixelToHex(localPos);

    // Tap hero's current position → undo last move
    if (coord == _map.HeroPosition) {
        if (!_map.CanUndoMove) return;
        var result = _map.UndoLastMove();
        if (result is { } r) {
            _state.AddMovePoints(r.CostRefund);
            _previewedHex = null;
            _previewLabel.Visible = false;
            Log.Debug("[Input]", $"Move undone: back to {r.Previous.Q},{r.Previous.R} refund={r.CostRefund} remaining={_state.MovePointsThisTurn}");
        }
        return;
    }

    var hexState = _map.Grid.GetState(coord);
    if (hexState == null) return;  // off-grid

    int? cost = TerrainCosts.GetCost(hexState.Terrain, _state.IsDay);
    bool isAdjacent = false;
    foreach (var n in _map.HeroPosition.Neighbors())
        if (n == coord) { isAdjacent = true; break; }

    if (isAdjacent && cost != null && _state.MovePointsThisTurn >= cost.Value) {
        // Valid move: commit immediately — no confirmation needed, undo is free
        _previewedHex = null;
        _previewLabel.Visible = false;
        _state.SpendMovePoints(cost.Value);
        _map.CommitHeroMove(coord, cost.Value);
        Log.Debug("[Input]", $"Hero moved to {coord.Q},{coord.R} cost={cost} remaining={_state.MovePointsThisTurn}");
    } else {
        // Not a valid move: preview only
        _previewedHex = coord;
        UpdatePreviewLabel(coord, cost);
        Log.Debug("[Input]", $"Hex tapped: {coord.Q},{coord.R} terrain={hexState.Terrain} cost={cost?.ToString() ?? "impassable"}");
    }
}
```

**Why clear `_previewedHex` before `SpendMovePoints` and `CommitHeroMove`?** `SpendMovePoints` fires `ResourcesChanged` → `RefreshPreview`. If `_previewedHex` is still set, `RefreshPreview` would re-show a preview for the hex the hero just left (stale). Clearing first makes `RefreshPreview` a no-op for those events.

**Why check adjacency with a foreach?** `HexCoord.Neighbors()` yields via an iterator. LINQ `.Any()` works correctly (record struct has value equality), but the explicit loop avoids hidden LINQ allocations on the main-thread input path. Either works; use whichever reads better.

### No changes to `PlaceholderMainMenu.cs`

---

## Implementation Tasks

Execute in TDD order — write failing tests first, confirm they fail, then implement.

**Task 1 — `GameState.SpendMovePoints` + `ResetMovePoints` + 5 tests** ✅

1. [x] Add 5 tests to `tests/unit/GameStateTest.cs` (all red — see Tests section below)
2. [x] Add `SpendMovePoints` and `ResetMovePoints` to `scripts/core/GameState.cs`
3. [x] Run `dotnet test` — **148 total** (143 + 5), zero failures

**Task 2 — `WorldMap` move path + 10 tests** ✅

1. [x] Add 10 tests to `tests/unit/WorldMapTest.cs` (all red — see Tests section below)
2. [x] Add `_movePath` field, `CanUndoMove`, `CommitHeroMove`, `UndoLastMove`, `ClearMovePath` to `scripts/map/WorldMap.cs`
3. [x] Run `dotnet test` — **158 total** (148 + 10), zero failures

**Task 3 — `HexMapView` three-way tap dispatch** ✅

1. [x] Replace `HandleHexTap` in `scripts/ui/components/HexMapView.cs` with the three-way version above
2. [x] Run `dotnet test` — still **158 green** (HexMapView is Godot-dependent, no new unit tests)
3. [ ] Manually verify on desktop (WSLg, Emulate Touch from Mouse enabled):
   - Stage Move cards to gain ≥ 2 points; tap adjacent Plains → hero moves, points decrease by 2
   - Tap hero's current hex → hero moves back, 2 points restored
   - Tap hero's hex again (nothing to undo) → nothing happens
   - Chain two moves (A→B→C): undo goes C→B→A as expected
   - Tap non-adjacent hex → preview only, no move
   - Tap Swamp (cost 5) with < 5 points → red preview on adjacent Swamp, no move

### Review Findings (Opus 4.8 multi-layer — 2026-06-10)

Blind Hunter + Edge Case Hunter + Acceptance Auditor. All 12 ACs verified MET; all 15 tests present and faithful. Triage: 1 decision, 1 patch, 5 deferred, 4 dismissed.

- [x] [Review][Decision → Patched] Cross-mechanism undo desync → move-point double-refund via debug "Undo Last Event" — `RestoreSnapshot` rolls back `MovePointsThisTurn` but `WorldMap._movePath`/`HeroPosition` are not in `GameStateSnapshot`, so after a card-effect undo the spent points are refunded AND the move stays undoable; tapping the hero hex then refunds the cost a second time (concrete: MP 5 → spend 2 → MP 3 → card-undo → MP 5 → tap-hero-undo → MP 7). Spec Dev Notes deliberately deferred WorldMap/snapshot coordination to Epic 7/TurnManager, but did not flag the double-refund. **Resolution (John chose to harden now):** `EffectEventLogPanel` now takes `WorldMap` and calls `_map.ClearMovePath()` after `GameDebug.UndoLastEvent`, so a card-effect undo also drops the movement undo stack — closing the double-refund. Coordination lives in the UI layer (debug panel), keeping `GameState`/`GameDebug` decoupled from `WorldMap`. Residual (still Epic 7): hero position is not part of the snapshot, so the hero stays on the moved-to hex — a benign desync, no resource duplication. Debug-build only (`[Conditional("DEBUG")]`). `EffectEventLogPanel.cs:OnUndoLastPressed` + `PlaceholderMainMenu.cs:44`.
- [x] [Review][Patch] Undo-branch event ordering causes preview flicker — in `HandleHexTap`, the undo branch called `_state.AddMovePoints(r.CostRefund)` (fires `ResourcesChanged → RefreshPreview` while `_previewedHex` was still set) BEFORE clearing `_previewedHex`/hiding the label. **Fixed:** reordered to clear `_previewedHex`/hide the label first, mirroring the commit branch. `scripts/ui/components/HexMapView.cs` undo branch.
- [x] [Review][Defer] `_movePath` unbounded + `ResetMovePoints`/`ClearMovePath` have no caller → cross-turn move-point leak once turns exist [`WorldMap.cs:9,48`, `GameState.cs:56`] — deferred, already acknowledged in Dev Notes; turn-boundary wiring is Epic 7 / story 2-4.
- [x] [Review][Defer] `ClearMovePath` fires no event, so a showing preview label persists when 2-4 wires the undo gate [`WorldMap.cs:48`] — deferred, surfaces only when story 2-4 calls `ClearMovePath`; handle the UI reset there.
- [x] [Review][Defer] Hero-hex tap reserved for undo means the hero's own tile can't be cost-previewed and a no-op undo tap gives no feedback [`HexMapView.cs` undo branch] — deferred, by-design per AC5/AC6; minor UX, revisit in polish (epic-8/UX).
- [x] [Review][Defer] No automated coverage for `PixelToHex`/`HexRound` [`HexMapView.cs`] — deferred, already logged from the 2-2 review (2-2-attributable code).
- [x] [Review][Defer] AC7 (marker moves) and AC9 (StagingAreaView running total) not exercisable in unit tests — deferred, tracked in `epic-2-manual-test-checklist.md` (end-of-epic manual pass).

**Dismissed (4):** `SpendMovePoints` has no negative/underflow guard (documented design decision — caller validates, consistent with `AddMovePoints`); `CommitHeroMove` does no adjacency/affordability validation (by-design model/view separation, same as `SetHeroPosition`); `UndoLastMove` tuple field `Previous` naming (no functional issue); `SetIsDay` early-returns while `Reset/Spend` always fire (documented + tested intent — `ResetMovePoints_WhenAlreadyZero_StillFiresResourcesChanged`).

---

## Tests to Write

### Additions to `tests/unit/GameStateTest.cs` — 5 tests

```csharp
[Fact]
public void SpendMovePoints_DecrementsCorrectly() {
    var state = EmptyState();
    state.AddMovePoints(5);
    state.SpendMovePoints(3);
    Assert.Equal(2, state.MovePointsThisTurn);
}

[Fact]
public void SpendMovePoints_FiresResourcesChanged() {
    var state = EmptyState();
    state.AddMovePoints(3);
    bool fired = false;
    state.ResourcesChanged += () => fired = true;
    state.SpendMovePoints(2);
    Assert.True(fired);
}

[Fact]
public void ResetMovePoints_SetsToZero() {
    var state = EmptyState();
    state.AddMovePoints(7);
    state.ResetMovePoints();
    Assert.Equal(0, state.MovePointsThisTurn);
}

[Fact]
public void ResetMovePoints_FiresResourcesChanged() {
    var state = EmptyState();
    state.AddMovePoints(3);
    bool fired = false;
    state.ResourcesChanged += () => fired = true;
    state.ResetMovePoints();
    Assert.True(fired);
}

[Fact]
public void ResetMovePoints_WhenAlreadyZero_StillFiresResourcesChanged() {
    var state = EmptyState();
    bool fired = false;
    state.ResourcesChanged += () => fired = true;
    state.ResetMovePoints();
    Assert.True(fired);
}
```

### Additions to `tests/unit/WorldMapTest.cs` — 10 tests

```csharp
[Fact]
public void CanUndoMove_FalseInitially() {
    var map = new WorldMap(new HexCoord(0, 0));
    Assert.False(map.CanUndoMove);
}

[Fact]
public void CommitHeroMove_UpdatesHeroPosition() {
    var map = new WorldMap(new HexCoord(0, 0));
    map.CommitHeroMove(new HexCoord(1, 0), costPaid: 2);
    Assert.Equal(new HexCoord(1, 0), map.HeroPosition);
}

[Fact]
public void CommitHeroMove_FiresHeroMoved() {
    var map = new WorldMap(new HexCoord(0, 0));
    HexCoord received = default;
    map.HeroMoved += c => received = c;
    map.CommitHeroMove(new HexCoord(1, 0), costPaid: 2);
    Assert.Equal(new HexCoord(1, 0), received);
}

[Fact]
public void CommitHeroMove_EnablesUndo() {
    var map = new WorldMap(new HexCoord(0, 0));
    map.CommitHeroMove(new HexCoord(1, 0), costPaid: 2);
    Assert.True(map.CanUndoMove);
}

[Fact]
public void UndoLastMove_RestoresPreviousPosition() {
    var map = new WorldMap(new HexCoord(0, 0));
    map.CommitHeroMove(new HexCoord(1, 0), costPaid: 2);
    map.UndoLastMove();
    Assert.Equal(new HexCoord(0, 0), map.HeroPosition);
}

[Fact]
public void UndoLastMove_ReturnsCorrectCostRefund() {
    var map = new WorldMap(new HexCoord(0, 0));
    map.CommitHeroMove(new HexCoord(1, 0), costPaid: 3);
    var result = map.UndoLastMove();
    Assert.NotNull(result);
    Assert.Equal(3, result!.Value.CostRefund);
}

[Fact]
public void UndoLastMove_FiresHeroMoved() {
    var map = new WorldMap(new HexCoord(0, 0));
    map.CommitHeroMove(new HexCoord(1, 0), costPaid: 2);
    HexCoord received = default;
    map.HeroMoved += c => received = c;
    map.UndoLastMove();
    Assert.Equal(new HexCoord(0, 0), received);
}

[Fact]
public void UndoLastMove_WhenNothingToUndo_ReturnsNull() {
    var map = new WorldMap(new HexCoord(0, 0));
    Assert.Null(map.UndoLastMove());
}

[Fact]
public void UndoLastMove_MultiHop_UndoesInReverseOrder() {
    var map = new WorldMap(new HexCoord(0, 0));
    map.CommitHeroMove(new HexCoord(1, 0), costPaid: 2);
    map.CommitHeroMove(new HexCoord(1, 1), costPaid: 3);
    map.UndoLastMove();
    Assert.Equal(new HexCoord(1, 0), map.HeroPosition);
    map.UndoLastMove();
    Assert.Equal(new HexCoord(0, 0), map.HeroPosition);
    Assert.False(map.CanUndoMove);
}

[Fact]
public void ClearMovePath_DisablesUndo() {
    var map = new WorldMap(new HexCoord(0, 0));
    map.CommitHeroMove(new HexCoord(1, 0), costPaid: 2);
    map.ClearMovePath();
    Assert.False(map.CanUndoMove);
    Assert.Null(map.UndoLastMove());
}
```

**Total new tests: 15** (5 GameState + 10 WorldMap) → **158 tests green expected** at story close.

---

## Dev Notes

### Single-tap commit with undo — no confirmation needed

Pre-commit undo (free until new information is revealed — `docs/game-architecture.md §Undo Scope`) makes a confirmation tap redundant. In the board game, you pick up the figure and set it on the destination; if you change your mind before drawing a tile you put it back. Tapping your current hex is the digital equivalent of "put it back." The affordability color from 2-2 gives cost-awareness before committing; undo gives recovery after.

### `SetHeroPosition` vs `CommitHeroMove`

`SetHeroPosition` (existing) is for non-undo-tracked position changes: initial placement, tests, future teleport effects. `CommitHeroMove` (new) is for player-initiated movement that the undo stack should track. Never use `SetHeroPosition` for player movement in game logic — use `CommitHeroMove`.

### Undo gate is story 2-4's job

`ClearMovePath()` is stubbed here so story 2-4 has a clean API. Story 2-4 calls it when a tile is revealed (the only undo gate for movement in this game). Until then, the full move history accumulates across the session (there's no TurnManager resetting it yet — same situation as `MovePointsThisTurn` before `ResetMovePoints` existed).

### `SpendMovePoints` does not guard against overspend

The caller validates `MovePointsThisTurn >= cost` before calling. Consistent with `AddMovePoints` (which also has no guard). Overspending is a programmer error, not expected game-logic failure.

### `ResetMovePoints` fires `ResourcesChanged` unconditionally

Even when already at 0 — consistent with `AddMovePoints`. Listeners get notified regardless so no edge-case stale state.

### `_movePath` and the LOCKSTEP comment

`WorldMap` is not part of `GameStateSnapshot` — hero position is not currently snapshotted. This is intentional: the movement undo mechanism is separate from the card effect undo mechanism. `TurnManager` (Epic 7) will coordinate both. The LOCKSTEP comment in `GameState.cs` does not need updating.

### Starting map for manual verification

Hero starts at (0,0). All 6 other hexes are adjacent. To test non-adjacent: move to (1,0), then the hex at (−1,0) is no longer adjacent — tap it should show preview only.

| Hex (Q,R)  | Terrain   | Day cost | Adjacent to (0,0) |
|------------|-----------|:--------:|:-----------------:|
| (1, 0)     | Plains    | 2        | yes |
| (-1, 0)    | Forest    | 3        | yes |
| (0, 1)     | Hills     | 3        | yes |
| (0, -1)    | Swamp     | 5        | yes |
| (1, -1)    | Plains    | 2        | yes |
| (-1, 1)    | Wasteland | 4        | yes |

### Project-context.md rules that apply here

- **Pure C# game logic** — `SpendMovePoints`, `ResetMovePoints`, `CommitHeroMove`, `UndoLastMove`, `ClearMovePath` are all in pure C# classes. `HandleHexTap` changes are in `HexMapView` (already in `scripts/ui/`).
- **No async void** — all new methods are synchronous.
- **InputLock** — both commit and undo run inside the existing `_UnhandledInput` lock scope from 2-2.
- **Log with `[Input]` tag** — used for both commit and undo log lines.
- **Events are data only** — no new event types; reusing `ResourcesChanged` and `HeroMoved`.
- **Result\<T\>** — `UndoLastMove` returns nullable tuple (not `Result<T>`) because null-means-nothing-to-undo is idiomatic C# for an optional return, not a game-logic error.

---

## Definition of Done

- [ ] `dotnet test` passes — 143 existing + 15 new = **158 tests green**, zero failures
- [ ] Stage ≥ 2 Move points; tap adjacent Plains hex → hero marker moves, points decrease by 2
- [ ] Tap hero's current hex → hero returns to previous position, points restored
- [ ] Chain two moves then undo twice → back to start
- [ ] Tap current hex when nothing to undo → nothing happens (no crash)
- [ ] Non-adjacent hex tap → preview only, hero stays put
- [ ] `StagingAreaView` "Move:" total reflects both spending and refunds immediately
- [ ] Code review by Opus 4.8 before marking story done in sprint-status.yaml

---

## Dev Agent Record

### Agent Model Used

Claude Sonnet 4.6

### Debug Log References

### Completion Notes List

- TDD followed strictly: tests written red before each implementation phase. 143 → 148 → 158 total tests.
- `SpendMovePoints` / `ResetMovePoints` added inline with `AddMovePoints` in `GameState.cs` — same fire-unconditionally pattern.
- `WorldMap._movePath` is `List<(HexCoord Previous, int CostPaid)>`. `CommitHeroMove` pushes before `SetHeroPosition`; `UndoLastMove` pops with `[^1]` / `RemoveAt(_movePath.Count - 1)`. `SetHeroPosition` unchanged — existing 7 tests unaffected.
- `HexMapView.HandleHexTap` replaced: three-way dispatch (undo / commit / preview). `_previewedHex` cleared before `SpendMovePoints` to prevent stale-preview race via `ResourcesChanged → RefreshPreview`.
- Manual test pending at end of epic.

### File List

**Modified Files**
- `scripts/core/GameState.cs` — `SpendMovePoints()` and `ResetMovePoints()` added
- `scripts/map/WorldMap.cs` — `_movePath`, `CanUndoMove`, `CommitHeroMove`, `UndoLastMove`, `ClearMovePath` added
- `scripts/ui/components/HexMapView.cs` — `HandleHexTap` replaced with three-way dispatch; undo-branch reordered (review patch P1)
- `scripts/ui/debug/EffectEventLogPanel.cs` — `Initialize` now takes `WorldMap`; `OnUndoLastPressed` calls `ClearMovePath` to close the cross-mechanism double-refund (review D1)
- `scripts/ui/screens/PlaceholderMainMenu.cs` — passes `_worldMap` into `EffectEventLogPanel.Initialize` (review D1)
- `tests/unit/GameStateTest.cs` — 5 new tests
- `tests/unit/WorldMapTest.cs` — 10 new tests
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — story 2-3 status: ready-for-dev → review
- `_bmad-output/implementation-artifacts/2-3-spend-move-points-to-cross-hexes.md` — tasks checked, completion notes, status → review
