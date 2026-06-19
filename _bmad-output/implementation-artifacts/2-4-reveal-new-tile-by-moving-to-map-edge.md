# Story 2-4 — Reveal New Tile by Moving to Map Edge

**Epic:** 2 — Hex Map + Movement
**Story ID:** 2-4
**Status:** done
**Created:** 2026-06-19
**Dependencies:** 2-3 (WorldMap.ClearMovePath stub, HexMapView tap dispatch, GameState.SpendMovePoints — all complete and reviewed)
**Reviewer model:** Use Opus 4.8 for code review (per project feedback convention)

---

## User Story

As a player, I reveal a new tile by moving to the map edge and paying 2 Move points, and I can continue moving onto the newly revealed tile.

---

## Context

### What stories 2-0 through 2-3 built (do not recreate any of this)

- **`InputLock`** (`scripts/ui/InputLock.cs`) — shared re-entrancy guard; already injected into `HexMapView`
- **`HexCoord`** — axial `readonly record struct`, `Neighbors()`, value equality
- **`HexGrid`** — `GetState()`, `GetNeighbors()`, `AllHexes()`, `Contains()`, `Add()`
- **`HexState(TerrainType)`** — grid membership IS the revealed predicate; no `IsRevealed` flag (deferred item from 2-1 is already resolved: `HexState` only has `Terrain`)
- **`MapTile`** — `TileId`, `TileType`, `IsRevealed`, `Origin`, `WorldHexes()`, `Reveal()`
- **`WorldMap`** — `PlaceTile`, `Grid`, `PlacedTiles`, `HeroPosition`, `SetHeroPosition`, `CommitHeroMove`, `UndoLastMove`, `CanUndoMove`, `ClearMovePath`, `HeroMoved`
- **`TerrainCosts.GetCost(TerrainType, bool isDay)`** — returns `int?` (null = impassable)
- **`GameState`** — `IsDay`, `MovePointsThisTurn`, `AddMovePoints`, `SpendMovePoints`, `ResetMovePoints`, `ResourcesChanged`, `DayNightChanged`
- **`HexMapView`** — `Initialize(WorldMap, GameState, InputLock)`; `_UnhandledInput`; three-way tap dispatch (undo/commit/preview); `RefreshPreview`; `UpdatePreviewLabel`; `_previewedHex`; `_previewLabel`; iterates `_map.Grid.AllHexes()` in Initialize
- **`PlaceholderMainMenu.BuildStartingMap()`** — 7-hex starting tile (Plains ×3, Forest, Hills, Swamp, Wasteland), all revealed
- **158 tests passing** at story 2-3 close

### Deferred items from prior stories that THIS story must address

These were explicitly logged in `deferred-work.md` as owned by story 2-4:

1. **`MapTile.Reveal()` back-propagation** — `PlaceTile` checks `tile.IsRevealed` at placement time and populates `_grid` only if revealed. `MapTile.Reveal()` flips the per-tile flag but `WorldMap` has no hook. The fix is `WorldMap.RevealTile(MapTile)` (see Technical Design). **Never call `MapTile.Reveal()` directly from game code — always use `WorldMap.RevealTile()`.**

2. **`ClearMovePath` fires no event → preview label persists after undo gate trips** — The deferred note says: "handle the UI reset in story 2-4's integration." Fix: subscribe `HexMapView` to the new `WorldMap.TileRevealed` event and clear `_previewedHex`/hide `_previewLabel` there.

3. **`PlaceTile` silent overwrite on hex collision** — Still deferred for full validation, but the second tile added in this story must NOT overlap the starting tile's hexes. Enforce this by careful placement only (no code guard required in this story).

### What story 2-4 adds

1. **`GameState.Fame`** — `int` property, starts at 0
2. **`GameState.AddFame(int n)`** — increments `Fame`, fires `ResourcesChanged`
3. **`WorldMap.TileRevealed`** — `Action<MapTile>?` event fired by `RevealTile`
4. **`WorldMap.RevealTile(MapTile tile)`** — the proper fix for back-propagation; calls `tile.Reveal()`, populates `_grid` with the tile's 7 hexes, calls `ClearMovePath()` (undo gate), fires `TileRevealed`
5. **`WorldMap.FindTileForCoord(HexCoord coord)`** — searches `_tiles` for the first unrevealed tile whose `WorldHexes()` contains `coord`; returns null if none found or if the coord is already revealed
6. **`HexMapView`** — extended to render unrevealed tile hexes in fog color; subscribe to `TileRevealed` to re-color on reveal + clear preview; extend tap dispatch to handle taps on unrevealed hexes
7. **`PlaceholderMainMenu.BuildStartingMap()`** — add one unrevealed Countryside tile adjacent to the starting tile
8. **Tests** — 2 new `GameStateTest` + 7 new `WorldMapTest` → **167 tests green**

### +1 Fame per tile revealed — scenario-specific, NOT implemented here

The epic scope note mentions "+1 Fame per tile revealed (First Reconnaissance rule)." This is **not a base game rule** — it applies only in scenarios that grant fame for exploration. Do NOT call `AddFame` on reveal in this story. `GameState.Fame` and `AddFame` should still be added (they unblock future stories), but the reveal path does not call `AddFame`. When scenario rules are implemented (Epic 7 or a dedicated scenario-config story), the fame-on-reveal hook can be wired there.

### Undo gate — why RevealTile calls ClearMovePath internally

Tile revelation is the only undo gate for movement in this game. Once new information is revealed, prior moves are final — the player cannot un-see the tile. Therefore `RevealTile` calls `ClearMovePath()` internally before firing `TileRevealed`. This is the correct encapsulation: the caller (HexMapView) does not need to remember to call both; the semantic "reveal = undo gate" is enforced by the model.

### Map edge detection and second tile setup

`HexMapView` detects a "map edge" opportunity by checking whether a tapped coord resolves to an unrevealed tile (via `WorldMap.FindTileForCoord`). The hero does NOT need to be AT the edge — they need to tap an unrevealed tile hex that is ADJACENT to their current position.

`BuildStartingMap()` must add a second tile so there is something to reveal. The second tile is a Countryside tile centered at origin `(3, 0)`. Its world hexes are at:
- `(3,0)` Plains — not adjacent to starting tile
- `(4,0)` Hills — not adjacent to starting tile
- `(2,0)` Forest — **adjacent to starting tile's `(1,0)` Plains**
- `(3,1)` Wasteland — not adjacent to starting tile
- `(3,-1)` Desert — not adjacent to starting tile
- `(4,-1)` Plains — not adjacent to starting tile
- `(2,1)` Mountains — adjacent to starting tile's `(1,0)` and `(-1,1)` via neighbors

Only `(2,0)` is adjacent to the starting tile (`(1,0)` neighbor). This gives the player a discoverable "move to (1,0), then tap (2,0) to explore" flow. None of these world coords overlap with the starting tile's hexes (starting tile uses `(-1,1)`, `(-1,0)`, `(0,-1)`, `(0,0)`, `(0,1)`, `(1,-1)`, `(1,0)` — no overlap with `(2,0)`, `(2,1)`, `(3,-1)`, `(3,0)`, `(3,1)`, `(4,-1)`, `(4,0)`).

---

## Acceptance Criteria

**AC1 — Unrevealed tile hexes are visible as fog**
On game launch, the 7 hexes of the unrevealed Countryside tile render in a distinct dark fog color (not the terrain color, not blank). The fog hexes are visually distinguishable from terrain but indicate "there is land here."

**AC2 — Tapping an adjacent unrevealed hex with sufficient points reveals the tile**
When the hero is at `(1,0)` and taps `(2,0)` (an unrevealed Forest hex adjacent to the hero) with ≥ 2 `MovePointsThisTurn`: the 7 fog hexes re-color to their proper terrain colors, `MovePointsThisTurn` decreases by 2, the preview label hides, and the undo stack is cleared (`CanUndoMove` is false). (No Fame is awarded — fame-on-reveal is scenario-specific; see AC10.)

**AC3 — Tapping adjacent unrevealed hex with insufficient points shows red "Explore: 2" preview**
When hero is at `(1,0)` with 0 or 1 `MovePointsThisTurn` and taps `(2,0)`: the preview label shows "Explore: 2" in red. No reveal occurs, no Move points spent.

**AC4 — Tapping non-adjacent unrevealed hex shows affordability-colored "Explore: 2" preview**
When hero is at `(0,0)` and taps `(2,0)` (not adjacent, even though unrevealed): shows "Explore: 2" in green (if ≥ 2 points) or red (if < 2). No reveal occurs.

**AC5 — Live re-render of explore preview (affordability update on point change)**
When an "Explore: 2" preview is showing in red, gaining ≥ 2 Move points (staging a card) flips it to green without re-tapping. Same `RefreshPreview` subscription already handles movement previews.

**AC6 — Hero can move onto revealed hexes after tile reveal**
After revealing the Countryside tile, moving to `(2,0)` is valid (it's now in the grid, terrain Forest, cost 3 Day/5 Night). AC1 of story 2-3 (tap adjacent committed hex) applies to `(2,0)` once revealed.

**AC7 — Undo is locked after tile reveal**
After revealing the Countryside tile, tapping the hero's current hex does nothing (undo disabled). `WorldMap.CanUndoMove` is false.

**AC8 — Preview label clears on tile reveal**
If the player had a preview label visible (movement OR explore) when the tile is revealed, it disappears. No stale label persists after reveal.

**AC9 — Single-tap reveal with no confirmation required**
Tapping an adjacent affordable unrevealed hex reveals immediately — same philosophy as single-tap movement commit.

**AC10 — `GameState.Fame` and `AddFame` exist but are NOT called on reveal**
`GameState.Fame` and `AddFame(int n)` are implemented and tested in this story to unblock future work, but the explore path does NOT call `AddFame`. Fame-on-reveal is scenario-specific (e.g. First Reconnaissance) and will be wired when scenario rules land.

---

## Technical Design

### Changed: `scripts/core/GameState.cs`

Add after `ResetMovePoints`:

```csharp
public int Fame { get; private set; } = 0;

public void AddFame(int n) {
    Fame += n;
    ResourcesChanged?.Invoke();
}
```

No snapshot changes — `Fame` is not snapshotted (same pattern as `HeroPosition`; the effect-log undo mechanism doesn't cover fame increments yet).

### Changed: `scripts/map/WorldMap.cs`

Add after `HeroMoved` event:

```csharp
public event Action<MapTile>? TileRevealed;
```

Add after `ClearMovePath`:

```csharp
// The canonical way to reveal a tile. Calling MapTile.Reveal() directly leaves
// the grid stale — those hexes never appear. Always use this method.
public void RevealTile(MapTile tile) {
    tile.Reveal();
    foreach (var (worldCoord, terrain) in tile.WorldHexes())
        _grid.Add(worldCoord, new HexState(terrain));
    ClearMovePath();
    TileRevealed?.Invoke(tile);
}

// Returns the first unrevealed tile whose world hexes contain coord, or null.
// Used by HexMapView to identify which tile a tapped fog hex belongs to.
public MapTile? FindTileForCoord(HexCoord coord) {
    foreach (var tile in _tiles) {
        if (tile.IsRevealed) continue;
        foreach (var (worldCoord, _) in tile.WorldHexes())
            if (worldCoord == coord) return tile;
    }
    return null;
}
```

**Why `ClearMovePath` inside `RevealTile`?** Revealing a tile is the only undo gate for movement. This keeps the invariant enforced at the model level — the view cannot forget to trip the undo gate.

**Why NOT add a collision guard to `PlaceTile` here?** The second tile's hexes are confirmed non-overlapping. Adding a guard now would be speculative hardening. Deferred-work note stays open.

### Changed: `scripts/ui/components/HexMapView.cs`

#### Field additions

```csharp
private const int RevealCost = 2;
private readonly Dictionary<HexCoord, Polygon2D> _hexPolygons = new();
private bool _previewIsExplore;
```

The `_previewIsExplore` bool disambiguates the two preview types in `RefreshPreview`: when `true`, the preview is "Explore: 2" for an unrevealed hex; when `false`, it's a movement cost preview.

#### `Initialize` — iterate `PlacedTiles` instead of `Grid.AllHexes`

Replace the current loop that iterates `_map.Grid.AllHexes()` with one that iterates `_map.PlacedTiles` to include unrevealed tiles:

```csharp
var corners = HexCorners(HexSize);
int hexCount = 0;
foreach (var tile in _map.PlacedTiles) {
    foreach (var (worldCoord, terrain) in tile.WorldHexes()) {
        var poly = new Polygon2D();
        poly.Polygon = corners;
        poly.Color = tile.IsRevealed ? TerrainColor(terrain) : FogColor();
        poly.Position = HexToPixel(worldCoord);
        AddChild(poly);
        _hexPolygons[worldCoord] = poly;
        hexCount++;
    }
}
```

Add the event subscription after the hero marker setup:

```csharp
_map.TileRevealed += OnTileRevealed;
```

#### `OnTileRevealed` handler (new method)

```csharp
private void OnTileRevealed(MapTile tile) {
    foreach (var (worldCoord, terrain) in tile.WorldHexes()) {
        if (_hexPolygons.TryGetValue(worldCoord, out var poly))
            poly.Color = TerrainColor(terrain);
    }
    _previewedHex = null;
    _previewIsExplore = false;
    _previewLabel.Visible = false;
    Log.Debug("[HexGrid]", $"Tile '{tile.TileId}' revealed — {_hexPolygons.Count} total hexes");
}
```

#### `HandleHexTap` — new fourth dispatch branch

Insert a new branch between the "off-grid" early return and the existing adjacent/preview logic. The full updated `HandleHexTap`:

```csharp
private void HandleHexTap(Vector2 localPos) {
    var coord = PixelToHex(localPos);

    // Branch 1: hero's current hex → undo last move
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

    // Branch 2: revealed hex → movement commit or preview
    var hexState = _map.Grid.GetState(coord);
    if (hexState != null) {
        int? cost = TerrainCosts.GetCost(hexState.Terrain, _state.IsDay);
        bool isAdjacent = false;
        foreach (var n in _map.HeroPosition.Neighbors())
            if (n == coord) { isAdjacent = true; break; }

        if (isAdjacent && cost != null && _state.MovePointsThisTurn >= cost.Value) {
            _previewedHex = null;
            _previewIsExplore = false;
            _previewLabel.Visible = false;
            _state.SpendMovePoints(cost.Value);
            _map.CommitHeroMove(coord, cost.Value);
            Log.Debug("[Input]", $"Hero moved to {coord.Q},{coord.R} cost={cost} remaining={_state.MovePointsThisTurn}");
        } else {
            _previewedHex = coord;
            _previewIsExplore = false;
            UpdatePreviewLabel(coord, cost);
            Log.Debug("[Input]", $"Hex tapped: {coord.Q},{coord.R} terrain={hexState.Terrain} cost={cost?.ToString() ?? "impassable"}");
        }
        return;
    }

    // Branch 3: unrevealed tile hex → explore commit or preview
    var unrevealedTile = _map.FindTileForCoord(coord);
    if (unrevealedTile != null) {
        bool isAdjacent = false;
        foreach (var n in _map.HeroPosition.Neighbors())
            if (n == coord) { isAdjacent = true; break; }

        if (isAdjacent && _state.MovePointsThisTurn >= RevealCost) {
            _previewedHex = null;
            _previewIsExplore = false;
            _previewLabel.Visible = false;
            _state.SpendMovePoints(RevealCost);
            // NOTE: do NOT call AddFame here — fame-on-reveal is scenario-specific
            _map.RevealTile(unrevealedTile);
            Log.Debug("[Input]", $"Tile '{unrevealedTile.TileId}' revealed from {coord.Q},{coord.R} remaining={_state.MovePointsThisTurn}");
        } else {
            _previewedHex = coord;
            _previewIsExplore = true;
            UpdateExploreLabelVisuals(coord);
            Log.Debug("[Input]", $"Unrevealed hex tapped: {coord.Q},{coord.R} adjacent={isAdjacent} affordable={_state.MovePointsThisTurn >= RevealCost}");
        }
        return;
    }

    // Branch 4: complete miss (off-grid and not an unrevealed tile) — no-op
}
```

**Why clear `_previewedHex` before `SpendMovePoints` and `AddFame`?** Both fire `ResourcesChanged → RefreshPreview`. If `_previewedHex` is still set, `RefreshPreview` would try to re-render a preview for the hex that was just revealed — which is now in the grid as a terrain hex, not an unrevealed hex. Clearing first makes `RefreshPreview` a no-op for those events (same pattern as movement commit in 2-3).


#### `RefreshPreview` — handle explore preview type

Update `RefreshPreview` to branch on `_previewIsExplore`:

```csharp
private void RefreshPreview() {
    if (_previewedHex is not { } coord) return;
    if (_previewIsExplore) {
        UpdateExploreLabelVisuals(coord);
    } else {
        var hexState = _map.Grid.GetState(coord);
        if (hexState == null) { _previewedHex = null; _previewLabel.Visible = false; return; }
        UpdatePreviewLabel(coord, TerrainCosts.GetCost(hexState.Terrain, _state.IsDay));
    }
}
```

#### `UpdateExploreLabelVisuals` (new private method)

```csharp
private void UpdateExploreLabelVisuals(HexCoord coord) {
    _previewLabel.Text = "Explore: 2";
    bool canAfford = _state.MovePointsThisTurn >= RevealCost;
    _previewLabel.AddThemeColorOverride("font_color",
        canAfford
            ? new Color(0.298f, 0.686f, 0.314f)   // #4CAF50 green
            : new Color(0.957f, 0.263f, 0.212f));  // #F44336 red
    _previewLabel.Position = HexToPixel(coord) + new Vector2(-30f, -HexSize - 10f);
    _previewLabel.Visible = true;
}
```

#### `FogColor` (new private static method)

```csharp
private static Color FogColor() => new Color(0.1f, 0.1f, 0.1f);
```

Dark near-black. Distinct from all terrain colors and from Mountain (0.216, 0.278, 0.310) and Lake (0.086, 0.396, 0.753).

### Changed: `scripts/ui/screens/PlaceholderMainMenu.cs`

Add a second unrevealed Countryside tile to `BuildStartingMap()`. The tile must be constructed AFTER the starting `MapTile` and placed so its hexes don't overlap:

```csharp
private static WorldMap BuildStartingMap() {
    var startingTile = new MapTile(
        tileId: "starting",
        tileType: TileType.Starting,
        isRevealed: true,
        origin: new HexCoord(0, 0),
        hexes: new[] {
            (new HexCoord( 0,  0), TerrainType.Plains),
            (new HexCoord( 1,  0), TerrainType.Plains),
            (new HexCoord(-1,  0), TerrainType.Forest),
            (new HexCoord( 0,  1), TerrainType.Hills),
            (new HexCoord( 0, -1), TerrainType.Swamp),
            (new HexCoord( 1, -1), TerrainType.Plains),
            (new HexCoord(-1,  1), TerrainType.Wasteland),
        }
    );

    var countryside1 = new MapTile(
        tileId: "countryside-1",
        tileType: TileType.Countryside,
        isRevealed: false,
        origin: new HexCoord(3, 0),
        hexes: new[] {
            (new HexCoord( 0,  0), TerrainType.Plains),     // world (3,0)
            (new HexCoord( 1,  0), TerrainType.Hills),      // world (4,0)
            (new HexCoord(-1,  0), TerrainType.Forest),     // world (2,0) — adjacent to starting (1,0)
            (new HexCoord( 0,  1), TerrainType.Wasteland),  // world (3,1)
            (new HexCoord( 0, -1), TerrainType.Desert),     // world (3,-1)
            (new HexCoord( 1, -1), TerrainType.Plains),     // world (4,-1)
            (new HexCoord(-1,  1), TerrainType.Mountain),   // world (2,1)
        }
    );

    var map = new WorldMap(new HexCoord(0, 0));
    map.PlaceTile(startingTile);
    map.PlaceTile(countryside1);
    return map;
}
```

**Hex world coordinate verification (no overlaps with starting tile):**

| Relative | World | Starting tile? |
|----------|-------|---------------|
| (0,0)    | (3,0) | No |
| (1,0)    | (4,0) | No |
| (-1,0)   | (2,0) | No — adjacent to (1,0) but not same |
| (0,1)    | (3,1) | No |
| (0,-1)   | (3,-1)| No |
| (1,-1)   | (4,-1)| No |
| (-1,1)   | (2,1) | No |

Starting tile hexes: (−1,1), (−1,0), (0,−1), (0,0), (0,1), (1,−1), (1,0). Zero overlap.

**Adjacency check:** `(2,0)` is at axial distance 1 from `(1,0)` — they differ by `(1,0)` which is a valid hex direction. Confirmed adjacent.

---

## Implementation Tasks

Execute in TDD order — write failing tests first, confirm they fail, then implement.

**Task 1 — `GameState.Fame` + `AddFame` + 2 tests** ✅

1. [x] Add 2 tests to `tests/unit/GameStateTest.cs`
2. [x] Add `Fame` property and `AddFame` method to `scripts/core/GameState.cs`
3. [x] Run `dotnet test` — **160 total** (158 + 2), zero failures

**Task 2 — `WorldMap.RevealTile`, `FindTileForCoord`, `TileRevealed` + 7 tests** ✅

1. [x] Add 7 tests to `tests/unit/WorldMapTest.cs`
2. [x] Add `TileRevealed` event, `RevealTile(MapTile)`, `FindTileForCoord(HexCoord)` to `scripts/map/WorldMap.cs`
3. [x] Run `dotnet test` — **167 total** (160 + 7), zero failures

**Task 3 — `HexMapView` tile rendering and explore dispatch** ✅

1. [x] Add `_hexPolygons`, `_previewIsExplore`, `RevealCost` fields
2. [x] Update `Initialize` to iterate `_map.PlacedTiles`, populate `_hexPolygons`, subscribe to `TileRevealed`
3. [x] Add `OnTileRevealed` handler
4. [x] Replace `HandleHexTap` with the 4-branch version
5. [x] Update `RefreshPreview` to branch on `_previewIsExplore`
6. [x] Add `UpdateExploreLabelVisuals` and `FogColor` methods
7. [x] Run `dotnet test` — still **167 green** (HexMapView is Godot-dependent, no new unit tests)
8. [ ] Manually verify on desktop (WSLg) — tracked in `epic-2-manual-test-checklist.md`

**Task 4 — `PlaceholderMainMenu.BuildStartingMap` — add unrevealed tile** ✅

1. [x] Update `BuildStartingMap()` to add unrevealed `countryside1` tile
2. [x] Run `dotnet test` — still **167 green**

**Task 5 — Add manual test entries to `epic-2-manual-test-checklist.md`** ✅

1. [x] Story 2-4 section added to `epic-2-manual-test-checklist.md`
2. [x] Resolved deferred items marked in `deferred-work.md`

### Review Findings (Opus 4.8 multi-layer — 2026-06-19)

Blind Hunter + Edge Case Hunter + Acceptance Auditor. All 10 ACs verified MET (AC1–AC5 UI-render unverifiable in unit tests; tracked in manual checklist). 167 tests green at review; 168 after the idempotency-guard regression test. Triage: 0 decisions, 4 patches (all applied), 3 deferred, ~12 dismissed.

**Patches (unchecked):**

- [x] [Review][Patch] AC2 spec text still says "Fame increases by 1" — contradicts AC10 + your scenario-specific decision; code correctly omits AddFame. **Fixed:** AC2 line updated to remove the Fame clause and point to AC10. [`2-4-...md` AC2]
- [x] [Review][Patch] `UpdateExploreLabelVisuals` hardcodes `"Explore: 2"` while the gate uses `RevealCost`. **Fixed:** now `$"Explore: {RevealCost}"`. [`HexMapView.cs` UpdateExploreLabelVisuals]
- [x] [Review][Patch] `RevealTile` has no idempotency guard. **Fixed:** added `if (tile.IsRevealed) return;` at the top; locked in by `RevealTile_OnAlreadyRevealedTile_DoesNotRefire` (168 tests green). [`WorldMap.cs` RevealTile]
- [x] [Review][Patch] `RefreshPreview` explore branch has no self-heal guard. **Fixed:** explore branch now clears the preview if `FindTileForCoord(coord) == null`, mirroring the movement branch. [`HexMapView.cs` RefreshPreview]

**Deferred (logged in `deferred-work.md`):**

- [x] [Review][Defer] Tile-overlap silent-overwrite cluster — `RevealTile`/`HexGrid.Add`/`_hexPolygons` all last-write-wins on a shared world coord, and Branch 2 (revealed) shadows Branch 3 (explore) for any coord in both grid and an unrevealed tile. The two current tiles are verified non-overlapping; this extends the already-deferred `PlaceTile` collision item to the new `RevealTile` path. [`WorldMap.cs` RevealTile, `HexGrid.cs:8`, `HexMapView.cs` Initialize]
- [x] [Review][Defer] Polygons are created only in `Initialize` — a tile placed at runtime (the natural next story: drawing exploration tiles from a deck) lands in the grid and `FindTileForCoord` but has no polygon, so its hexes are invisible-but-tappable. The tile-drawing story must add a "create polygon on PlaceTile/RevealTile" path. [`HexMapView.cs` Initialize/OnTileRevealed]
- [x] [Review][Defer] `PixelToHex`/`HexRound` always resolve to the nearest hex, so a tap in the fog gutter or just off-map can still commit/preview the nearest hex — no "tap landed inside a rendered polygon" test. Pre-existing from 2-2 (already logged); re-surfaced now that fog hexes widen the tappable area. [`HexMapView.cs` PixelToHex]

---

## Tests to Write

### Additions to `tests/unit/GameStateTest.cs` — 2 tests

```csharp
[Fact]
public void AddFame_IncreasesFame() {
    var state = EmptyState();
    state.AddFame(3);
    Assert.Equal(3, state.Fame);
}

[Fact]
public void AddFame_FiresResourcesChanged() {
    var state = EmptyState();
    bool fired = false;
    state.ResourcesChanged += () => fired = true;
    state.AddFame(1);
    Assert.True(fired);
}
```

### Additions to `tests/unit/WorldMapTest.cs` — 7 tests

```csharp
[Fact]
public void RevealTile_PopulatesGridFromUnrevealedTile() {
    var map = new WorldMap(new HexCoord(0, 0));
    var tile = UnrevealedTile(new HexCoord(5, 0));
    map.PlaceTile(tile);
    Assert.False(map.Grid.Contains(new HexCoord(5, 0)));  // not in grid before reveal
    map.RevealTile(tile);
    Assert.True(map.Grid.Contains(new HexCoord(5, 0)));   // in grid after reveal
}

[Fact]
public void RevealTile_SetsIsRevealedTrue() {
    var map = new WorldMap(new HexCoord(0, 0));
    var tile = UnrevealedTile(new HexCoord(5, 0));
    map.PlaceTile(tile);
    map.RevealTile(tile);
    Assert.True(tile.IsRevealed);
}

[Fact]
public void RevealTile_FiresTileRevealedEvent() {
    var map = new WorldMap(new HexCoord(0, 0));
    var tile = UnrevealedTile(new HexCoord(5, 0));
    map.PlaceTile(tile);
    MapTile? received = null;
    map.TileRevealed += t => received = t;
    map.RevealTile(tile);
    Assert.Same(tile, received);
}

[Fact]
public void RevealTile_ClearsMovePath() {
    var map = new WorldMap(new HexCoord(0, 0));
    map.PlaceTile(RevealedTile(new HexCoord(0, 0)));
    map.CommitHeroMove(new HexCoord(1, 0), costPaid: 2);
    Assert.True(map.CanUndoMove);
    var tile = UnrevealedTile(new HexCoord(5, 0));
    map.PlaceTile(tile);
    map.RevealTile(tile);
    Assert.False(map.CanUndoMove);
}

[Fact]
public void FindTileForCoord_ReturnsOwningUnrevealedTile() {
    var map = new WorldMap(new HexCoord(0, 0));
    var tile = UnrevealedTile(new HexCoord(5, 0));
    map.PlaceTile(tile);
    // UnrevealedTile origin (5,0) has relative (0,0) → world (5,0)
    var result = map.FindTileForCoord(new HexCoord(5, 0));
    Assert.Same(tile, result);
}

[Fact]
public void FindTileForCoord_ReturnsNullForRevealedTile() {
    var map = new WorldMap(new HexCoord(0, 0));
    var tile = RevealedTile(new HexCoord(0, 0));
    map.PlaceTile(tile);
    // Revealed tile hexes are in the grid; FindTileForCoord skips revealed tiles
    var result = map.FindTileForCoord(new HexCoord(0, 0));
    Assert.Null(result);
}

[Fact]
public void FindTileForCoord_ReturnsNullForMiss() {
    var map = new WorldMap(new HexCoord(0, 0));
    var result = map.FindTileForCoord(new HexCoord(99, 99));
    Assert.Null(result);
}
```

Note: `RevealedTile` and `UnrevealedTile` helpers already exist in `WorldMapTest.cs`. The `RevealTile_ClearsMovePath` test adds a revealed tile so `CommitHeroMove` works (it doesn't check grid membership, but the test is more realistic with a placed tile).

**Total new tests: 9** (2 GameState + 7 WorldMap) → **167 tests green expected** at story close.

---

## Manual Test Additions for `epic-2-manual-test-checklist.md`

Add the following section before the sign-off block:

```markdown
## Story 2-4 — Reveal new tile by moving to map edge

**Automated:** ✅ 168 tests green · main build 0/0 · Opus 4.8 multi-layer code review passed (4 patches applied, 3 deferred)

**Manual (outstanding):**

Starting position reference:
- Starting tile: 7 hexes rendered in terrain color (unchanged from 2-1)
- Countryside tile: 7 hexes rendered in dark fog color at world coords (2,0), (2,1), (3,-1), (3,0), (3,1), (4,-1), (4,0)
- Total polygons on screen: 14

Exploration flow:
- [ ] Launch → 14 hex polygons visible; 7 terrain-colored (starting tile), 7 dark fog (countryside)
- [ ] Tap (2,0) fog hex from hero at (0,0) → "Explore: 2" label appears in **red** (0 move points)
- [ ] Stage a Move card (gain ≥ 2 points) without re-tapping → label flips to **green** (live re-render)
- [ ] Move hero to (1,0) (Plains, costs 2) → hero marker moves
- [ ] With ≥ 2 Move points remaining, tap (2,0) → **fog hexes become terrain-colored** (Forest dark green at (2,0), Hills brown at (4,0), etc.); preview label disappears; log shows `[Input] Tile 'countryside-1' revealed`
- [ ] After reveal, tap hero's current hex → **nothing** (undo disabled, `CanUndoMove` is false)
- [ ] Move onto now-revealed (2,0) — Forest, Day cost 3 — with ≥ 3 remaining points → hero moves

Edge cases:
- [ ] Tap a fog hex from (0,0) when < 2 points → "Explore: 2" **red** preview; no reveal
- [ ] Tap fog hex NOT adjacent to hero (e.g. (3,0) from (0,0)) → "Explore: 2" preview (green or red); no reveal
- [ ] With preview showing when reveal fires → preview clears (no stale label)
```

---

## Dev Notes

### `HexMapView.Initialize` change — PlacedTiles vs Grid.AllHexes

The existing `Initialize` iterates `_map.Grid.AllHexes()` which only has revealed hexes. This story changes it to iterate `_map.PlacedTiles` (all placed tiles, revealed or not) so fog hexes render at startup. The `_hexPolygons` dictionary replaces the anonymous `poly` variable — we keep a reference so `OnTileRevealed` can re-color without re-creating Polygon2D nodes.

This is a targeted change. The hex count log line should update to reflect all rendered hexes: `Log.Debug("[HexGrid]", $"HexMapView initialized: {_hexPolygons.Count} hexes ({hexRevealedCount} revealed, {hexFogCount} fog), hero at ...")` — but keeping it simple is fine too.

### Why no `HexGrid.Add` collision guard in this story

The deferred note says to add a guard in story 2-4, but the countryside tile hexes are confirmed non-overlapping. Adding a guard now would be speculative. The deferred-work note stays open for when tile placement becomes dynamic (tile deck, real placement rules).

### `MapTile.Reveal()` is now effectively internal

After this story, all game code paths that reveal a tile should go through `WorldMap.RevealTile()`. `MapTile.Reveal()` remains public (C# doesn't have assembly-internal friends), but `project-context.md` should be updated to document the convention. Consider adding a note in the Dev Agent Record.

### `StagingAreaView` and Fame

`AddFame` fires `ResourcesChanged`. `StagingAreaView` subscribes to `ResourcesChanged`. Whether Fame appears in `StagingAreaView` depends on whether `StagingAreaView.Refresh` reads `_state.Fame`. It currently does not (no Fame label). This is correct — Fame display is a UX story (Epic 8 or future). The `ResourcesChanged` fire is for correctness, not for display.

### Fog color choice

`new Color(0.1f, 0.1f, 0.1f)` is a near-black that reads as "unknown terrain" without being invisible. It's distinct from all current terrain colors, Mountain (`0.37, 0.47, 0.51`), and Lake (`0.09, 0.40, 0.75`). May be revisited in Epic 9 (Art).

### `_previewIsExplore` and the null-clear pattern

When the explore preview is showing and the tile gets revealed (via the `OnTileRevealed` handler), `_previewIsExplore` is reset to `false` along with clearing `_previewedHex`. This prevents `RefreshPreview` from trying to re-render an explore preview for a hex that is now revealed and in the grid.

### Project-context.md rules that apply here

- **Pure C# game logic** — `Fame`, `AddFame`, `RevealTile`, `FindTileForCoord` are in pure C# classes. `HexMapView`/`OnTileRevealed` changes are in `scripts/ui/`.
- **No async void** — all new methods are synchronous.
- **InputLock** — explore commit runs inside the existing `_UnhandledInput` lock scope.
- **Log with tags** — `[Input]` for the tap dispatch lines; `[HexGrid]` for the `OnTileRevealed` log.
- **Events are data only** — `TileRevealed` passes the `MapTile` reference; no logic on the event itself.
- **Never call `MapTile.Reveal()` directly** — always use `WorldMap.RevealTile()` so the grid is populated and the undo gate fires.

---

## Definition of Done

- [x] `dotnet test` passes — 158 existing + 10 new = **168 tests green**, zero failures (9 story tests + 1 review-patch regression test)
- [ ] Launch game on desktop: 14 polygons render (7 terrain + 7 dark fog)
- [ ] Tap fog hex adjacent to hero with ≥ 2 points → terrain colors appear; preview clears; log confirms reveal
- [ ] Tap hero's hex after reveal → nothing (undo disabled)
- [ ] Move onto newly revealed hex → hero moves, points spent at terrain cost
- [ ] `Fame` stays 0 after reveal (fame-on-reveal is scenario-specific, not wired here)
- [ ] Manual test entries added to `epic-2-manual-test-checklist.md`
- [ ] `deferred-work.md` entries for `MapTile.Reveal` back-propagation and `ClearMovePath` preview bug marked resolved
- [ ] Code review by Opus 4.8 before marking story done in sprint-status.yaml

---

## Dev Agent Record

### Agent Model Used

Claude Sonnet 4.6

### Completion Notes List

- TDD followed strictly: tests written red before each implementation phase. 158 → 160 → 167 total tests.
- `GameState.Fame` + `AddFame(int n)` added inline with existing point methods; fires `ResourcesChanged` unconditionally (same pattern as all other Add* methods). NOT called on tile reveal — fame-on-reveal is scenario-specific.
- `WorldMap.TileRevealed` event + `RevealTile(MapTile)` + `FindTileForCoord(HexCoord)` added. `RevealTile` calls `ClearMovePath()` internally (undo gate) then fires `TileRevealed`. This is the canonical path — `MapTile.Reveal()` direct calls are now a code smell.
- `HexMapView` rewritten to iterate `PlacedTiles` instead of `Grid.AllHexes()` in `Initialize`, building `_hexPolygons` dict for per-hex re-coloring. Four-branch `HandleHexTap` (undo / revealed / unrevealed / miss). `OnTileRevealed` re-colors fog polygons and clears preview (resolves the deferred `ClearMovePath`-fires-no-event bug).
- `BuildStartingMap()` now places an unrevealed Countryside tile at origin (3,0); world hex (2,0) is adjacent to starting (1,0). 14 polygons render on startup (7 terrain + 7 fog). Zero hex coord overlaps with starting tile confirmed.
- Two deferred-work items from 2-1 and 2-3 marked resolved: `MapTile.Reveal()` back-propagation and `ClearMovePath` preview persistence.
- Manual verification pending at end of epic (tracked in `epic-2-manual-test-checklist.md`).

### File List

**Modified files:**
- `scripts/core/GameState.cs` — `Fame` property + `AddFame(int n)`
- `scripts/map/WorldMap.cs` — `TileRevealed` event + `RevealTile(MapTile)` + `FindTileForCoord(HexCoord)`
- `scripts/ui/components/HexMapView.cs` — `_hexPolygons`, `_previewIsExplore`, `RevealCost`; updated `Initialize`; `OnTileRevealed`; updated `HandleHexTap`; updated `RefreshPreview`; new `UpdateExploreLabelVisuals` + `FogColor`
- `scripts/ui/screens/PlaceholderMainMenu.cs` — `BuildStartingMap()` adds unrevealed `countryside1` tile
- `tests/unit/GameStateTest.cs` — 2 new tests (`AddFame_IncreasesFame`, `AddFame_FiresResourcesChanged`)
- `tests/unit/WorldMapTest.cs` — 8 new tests (`RevealTile_*` ×4, `FindTileForCoord_*` ×3, `RevealTile_OnAlreadyRevealedTile_DoesNotRefire` from review patch 3)
- `_bmad-output/implementation-artifacts/epic-2-manual-test-checklist.md` — story 2-4 section added
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — story 2-4: ready-for-dev → in-progress → review
- `_bmad-output/implementation-artifacts/deferred-work.md` — two deferred items resolved
- `_bmad-output/implementation-artifacts/2-4-reveal-new-tile-by-moving-to-map-edge.md` — tasks checked, completion notes, status → review
