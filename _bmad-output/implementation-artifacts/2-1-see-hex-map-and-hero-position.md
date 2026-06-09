# Story 2.1: See Hex Map and Hero Position

Status: done

## Story

As a player,
I want to see the hex map with my hero's position marked,
so that I can orient myself and plan movement.

## Background

This story introduces the hex data layer (`HexCoord`, `HexGrid`, `MapTile`, `WorldMap`) that every subsequent Epic 2 and Epic 3 movement story builds on. Story 2-1 is **visual only** — no tapping, no movement, no cost preview.

**Tiles are included now** (not deferred to story 2-4). The Mage Knight board is composed of large physical board pieces ("tiles"), each containing exactly 7 hexes. Building `WorldMap` without this abstraction would require a refactor in story 2-4; including it here makes 2-4 a clean implementation-only story.

**Epic 1 Retro Action Item 3** (`MovePointsThisTurn` reset) is explicitly **not addressed here**. Stories 2-1 and 2-2 are visual-only; the reset path is required only before story 2-3.

---

## Acceptance Criteria

### AC1 — `HexCoord` struct (pure C#, `scripts/hex/`)

- `HexCoord(int q, int r)` is a value type (record struct or struct with value-equality)
- `static readonly HexCoord[] Directions` — the 6 flat-top axial direction vectors: `(+1,0)`, `(-1,0)`, `(0,+1)`, `(0,-1)`, `(+1,-1)`, `(-1,+1)`
- `IEnumerable<HexCoord> Neighbors()` — returns 6 coords via `this + direction` for each direction
- `static HexCoord operator+(HexCoord a, HexCoord b)` — component-wise addition
- `int Distance(HexCoord other)` — cube-coordinate distance: convert both to cube (`x=q`, `z=r`, `y=-q-r`), then `(|dx|+|dy|+|dz|)/2`
- Correct `GetHashCode` + `Equals` — usable as `Dictionary<HexCoord, …>` key
- No `using Godot;`

### AC2 — `HexGrid` class (pure C#, `scripts/hex/`)

- `Add(HexCoord coord, HexState state)` — inserts or overwrites
- `bool Contains(HexCoord coord)`
- `HexState? GetState(HexCoord coord)` — returns `null` for absent coords, never throws
- `IEnumerable<HexCoord> GetNeighbors(HexCoord coord)` — returns only neighbor coords that exist in the grid
- `IEnumerable<(HexCoord Coord, HexState State)> AllHexes()` — iterate all entries
- No Godot dependency

### AC3 — Enums in `scripts/core/types/`

- `TerrainType.cs`: `Plains`, `Hills`, `Forest`, `Desert`, `Swamp`, `Wasteland`, `Mountain`, `Lake`, `CitySpace`
- `TileType.cs`: `Starting`, `Countryside`, `Core`

### AC4 — `HexState` record (pure C#, `scripts/hex/`)

```csharp
public record HexState(TerrainType Terrain, bool IsRevealed);
```

### AC5 — `MapTile` class (pure C#, `scripts/map/`)

**Every Mage Knight tile contains exactly 7 hexes** (one center + its 6 neighbors — the minimal hexagonal cluster). This is a hard game rule, enforced in the constructor.

- Constructor: `MapTile(string tileId, TileType tileType, bool isRevealed, HexCoord origin, IEnumerable<(HexCoord RelativeCoord, TerrainType Terrain)> hexes)`
  - Throws `ArgumentException` if `hexes.Count() != 7`: `"Mage Knight tiles contain exactly 7 hexes, got {n}"`
- `string TileId { get; }`, `TileType TileType { get; }`, `bool IsRevealed { get; private set; }`, `HexCoord Origin { get; }`
- `void Reveal()` — sets `IsRevealed = true`
- `IEnumerable<(HexCoord WorldCoord, TerrainType Terrain)> WorldHexes()` — adds `Origin` to each `RelativeCoord` using `HexCoord +`
- No Godot dependency

### AC6 — `WorldMap` class (pure C#, `scripts/map/`)

- Constructor: `WorldMap(HexCoord initialHeroPosition)`
- `HexGrid Grid { get; }` — maintained in sync; contains all hexes from all **revealed** placed tiles
- `IReadOnlyList<MapTile> PlacedTiles { get; }` — all tiles added via `PlaceTile`
- `HexCoord HeroPosition { get; private set; }`
- `void PlaceTile(MapTile tile)` — adds tile to `PlacedTiles`; if `tile.IsRevealed`, adds all its `WorldHexes()` to `Grid` as revealed `HexState`
- `void SetHeroPosition(HexCoord coord)` — updates `HeroPosition`, fires `event Action<HexCoord>? HeroMoved`
- No Godot dependency

### AC7 — `HexMapView` component (Godot `Node2D`, `scripts/ui/components/`)

- `Initialize(WorldMap map)` — wires view; builds initial visuals from `map.Grid.AllHexes()`
- Each hex renders as a flat-top hexagon `Polygon2D` child, colored by terrain type (see Dev Notes)
- Hero marker: small inner hexagon `Polygon2D` (white, `ZIndex = 1`) at hero's hex; repositioned when `HeroMoved` fires
- No signal handlers, no InputLock wiring (no user input this story)

### AC8 — Map visible on screen

- `PlaceholderMainMenu._Ready()` calls `BuildStartingMap()`, creates `HexMapView`, wires them, adds view as child
- All 7 colored hexes visible on screen alongside the existing hand view
- Hero marker visible on starting hex (0,0)

### AC9 — Unit tests (`tests/unit/`)

**`HexCoordTest.cs`:**
- `Neighbors_ReturnsExactlySix`
- `Neighbors_AllDistinct`
- `Distance_ToNeighbor_IsOne`
- `Distance_TwoSteps_IsTwo` — e.g. (0,0) to (2,0)
- `Distance_Diagonal_IsTwo` — e.g. (0,0) to (1,1)
- `Addition_SumsComponents`
- `EqualityByValue`

**`HexGridTest.cs`:**
- `Contains_AfterAdd_ReturnsTrue`
- `Contains_NotAdded_ReturnsFalse`
- `GetState_AfterAdd_ReturnsState`
- `GetState_Absent_ReturnsNull`
- `GetNeighbors_ReturnsOnlyGridMembers`

**`MapTileTest.cs`:**
- `WorldHexes_AddsOriginToRelative` — relative (1,0) + origin (3,3) = world (4,3)
- `WorldHexes_CountIsSeven`
- `Reveal_SetsIsRevealedTrue`
- `Constructor_WrongHexCount_ThrowsArgumentException` — pass 6 hexes, expect `ArgumentException`

**`WorldMapTest.cs`:**
- `InitialHeroPosition_MatchesConstructor`
- `PlaceTile_RevealedTile_AddsHexesToGrid`
- `PlaceTile_RevealedTile_AddsExactlySevenHexes`
- `PlaceTile_UnrevealedTile_DoesNotAddToGrid`
- `SetHeroPosition_UpdatesProperty`
- `SetHeroPosition_FiresHeroMoved`
- `HeroMoved_PassesNewCoord`

### AC10 — All tests pass

`dotnet test tests/maguswarrior.Tests.csproj` — 0 failures, no regressions in the existing 98 tests.

---

## Dev Notes

### What This Story Is NOT

- No movement spending (`MovePointsThisTurn` unchanged — required before 2-3)
- No tap interaction (story 2-2)
- No tile reveal mechanic (story 2-4; data model is ready)
- No fog-of-war rendering distinction — starting tile is fully revealed
- No LOCKSTEP changes — `WorldMap`/`HeroPosition` do NOT go in `GameStateSnapshot`

### File Placement

| File | Location | Godot dep? | In tests.csproj? |
|------|----------|------------|-----------------|
| `TerrainType.cs` | `scripts/core/types/` | No | Yes (existing glob) |
| `TileType.cs` | `scripts/core/types/` | No | Yes (existing glob) |
| `HexCoord.cs` | `scripts/hex/` | No | Yes (add glob) |
| `HexState.cs` | `scripts/hex/` | No | Yes (add glob) |
| `HexGrid.cs` | `scripts/hex/` | No | Yes (add glob) |
| `MapTile.cs` | `scripts/map/` | No | Yes (add glob) |
| `WorldMap.cs` | `scripts/map/` | No | Yes (add glob) |
| `HexMapView.cs` | `scripts/ui/components/` | **Yes** (Node2D) | No |
| `HexCoordTest.cs` | `tests/unit/` | No | — |
| `HexGridTest.cs` | `tests/unit/` | No | — |
| `MapTileTest.cs` | `tests/unit/` | No | — |
| `WorldMapTest.cs` | `tests/unit/` | No | — |

### Namespace Conventions

| File | Namespace |
|------|-----------|
| `TerrainType`, `TileType` | `MagusWarrior.Core.Types` |
| `HexCoord`, `HexGrid`, `HexState` | `MagusWarrior.Hex` |
| `MapTile`, `WorldMap` | `MagusWarrior.Map` |
| `HexMapView` | `MagusWarrior.UI` |

### Coordinate System (mandatory — redblobgames axial)

Architecture mandates **axial (q, r) coordinates**. Flat-top hex orientation matches the physical Mage Knight board.

**The 7-hex tile structure in axial coordinates:**
Every Mage Knight tile is a center hex surrounded by its 6 immediate neighbors — the smallest hexagonal ring. Using the tile's center as origin (0,0):
```
Relative positions:  (0,0), (+1,0), (−1,0), (0,+1), (0,−1), (+1,−1), (−1,+1)
```
All 7 relative coords are exactly the origin plus the 6 `HexCoord.Directions`. This is enforced by the 7-hex count guard.

**Flat-top axial neighbors (6 directions):**
```
(+1, 0)  (−1, 0)  (0, +1)  (0, −1)  (+1, −1)  (−1, +1)
```

**Cube distance:**
```csharp
public int Distance(HexCoord other) {
    int dx = Q - other.Q;
    int dz = R - other.R;
    int dy = -dx - dz;  // cube constraint: x + y + z = 0
    return (Math.Abs(dx) + Math.Abs(dy) + Math.Abs(dz)) / 2;
}
```

**`+` operator** (enables `Neighbors()` and `MapTile.WorldHexes()`):
```csharp
public static HexCoord operator +(HexCoord a, HexCoord b) =>
    new HexCoord(a.Q + b.Q, a.R + b.R);
```

### Rendering (HexMapView)

> Use `context7` to verify Godot 4.6.3 C# `Polygon2D` API before writing. Epic 1 Retro Action Item 3.

**Pixel conversion — flat-top, in `HexMapView` only (never in `HexGrid`):**
```csharp
private const float HexSize = 80f;

private static Vector2 HexToPixel(HexCoord coord) {
    float x = HexSize * (3f / 2f * coord.Q);
    float y = HexSize * (MathF.Sqrt(3f) / 2f * coord.Q + MathF.Sqrt(3f) * coord.R);
    return new Vector2(x, y);
}
```

**Flat-top hex corners (Polygon2D.Polygon):**
```csharp
private static Vector2[] HexCorners(float size) {
    var v = new Vector2[6];
    for (int i = 0; i < 6; i++) {
        float a = MathF.PI / 3f * i;
        v[i] = new Vector2(size * MathF.Cos(a), size * MathF.Sin(a));
    }
    return v;
}
```

**Per-hex Polygon2D:**
```csharp
var corners = HexCorners(HexSize);
foreach (var (coord, state) in _map.Grid.AllHexes()) {
    var poly = new Polygon2D();
    poly.Polygon = corners;
    poly.Color = TerrainColor(state.Terrain);
    poly.Position = HexToPixel(coord);
    AddChild(poly);
}
```

**Terrain colors (placeholder):**

| Terrain | Color |
|---------|-------|
| Plains | `#4CAF50` |
| Hills | `#A0835A` |
| Forest | `#1B5E20` |
| Desert | `#F9A825` |
| Swamp | `#6D7A3C` |
| Wasteland | `#757575` |
| Mountain | `#37474F` |
| Lake | `#1565C0` |
| CitySpace | `#FFD600` |

**Hero marker:**
```csharp
_heroMarker = new Polygon2D();
_heroMarker.Polygon = HexCorners(HexSize * 0.3f);
_heroMarker.Color = new Color(1f, 1f, 1f);
_heroMarker.ZIndex = 1;
_heroMarker.Name = "HeroMarker";
AddChild(_heroMarker);
_heroMarker.Position = HexToPixel(_map.HeroPosition);
_map.HeroMoved += coord => _heroMarker.Position = HexToPixel(coord);
```

### Starting Layout

7-hex starting tile with varied terrain. The terrain values here are illustrative — a later story will use actual starting tile art data. Hero starts at (0,0) — the portal space.

```csharp
private static WorldMap BuildStartingMap() {
    var tile = new MapTile(
        tileId: "starting",
        tileType: TileType.Starting,
        isRevealed: true,
        origin: new HexCoord(0, 0),
        hexes: new[] {
            (new HexCoord( 0,  0), TerrainType.Plains),    // portal
            (new HexCoord( 1,  0), TerrainType.Plains),
            (new HexCoord(-1,  0), TerrainType.Forest),
            (new HexCoord( 0,  1), TerrainType.Hills),
            (new HexCoord( 0, -1), TerrainType.Swamp),
            (new HexCoord( 1, -1), TerrainType.Plains),
            (new HexCoord(-1,  1), TerrainType.Wasteland),
        }
    );
    var map = new WorldMap(new HexCoord(0, 0));
    map.PlaceTile(tile);
    return map;
}
```

**PlaceholderMainMenu wiring** (after `_inputLock = new InputLock();`):
```csharp
_worldMap = BuildStartingMap();

var hexMapView = new HexMapView();
hexMapView.Name = "HexMapView";
hexMapView.Position = new Vector2(540f, 600f);
AddChild(hexMapView);
hexMapView.Initialize(_worldMap);
```

Add field: `private WorldMap _worldMap = null!;`

### tests.csproj Update

```xml
<!-- scripts/hex: fully pure C# -->
<Compile Include="../scripts/hex/**/*.cs" />
<!-- scripts/map: fully pure C# -->
<Compile Include="../scripts/map/**/*.cs" />
```

`HexMapView.cs` is not added — Godot dependency.

### WorldMap is NOT in GameState

`WorldMap` is created in `PlaceholderMainMenu` as a standalone instance, same as `StagingManager`. NOT part of `GameState` or `GameStateSnapshot`. Do not touch `TakeSnapshot()` / `RestoreSnapshot()`.

### LOCKSTEP

`WorldMap` and `HeroPosition` are not in `GameStateSnapshot`. Do NOT add them. The LOCKSTEP comment in `GameState.cs` documents the current snapshot fields — do not modify it this story.

### InputLock

No async void signal handlers in `HexMapView` this story. No InputLock needed. Story 2-2 adds tap handlers and will inject InputLock at that point.

### Error Handling

`HexGrid.GetState()` returns `null` for absent coords — never throws.
`WorldMap.SetHeroPosition()` does not validate that the coord exists in the grid — movement validation is story 2-3.
`MapTile` constructor **does** throw `ArgumentException` for wrong hex count — startup failure, not player action.

### Project Context Rules (from project-context.md)

- **Pure C# boundary**: `HexCoord`, `HexGrid`, `HexState`, `MapTile`, `WorldMap` — zero `using Godot`. Only `HexMapView.cs` may use Godot types.
- **Logging**: `Log.Debug("[HexGrid]", …)` for hex-related events. `[HexGrid]` is a required system tag.
- **No singleton**: `WorldMap` is constructed in `PlaceholderMainMenu` and injected. No static instance.
- **TDD discipline**: Write failing tests first, confirm red, then implement.
- **Startup-failure rule**: `MapTile` constructor throws on invalid data (wrong hex count), per the project pattern for bad startup data.

---

## Tasks / Subtasks

- [x] Task 1 — Write failing tests and update tests.csproj (RED) (AC: 9, 10)
  - [x] 1.1 — Add `<Compile Include="../scripts/hex/**/*.cs" />` and `<Compile Include="../scripts/map/**/*.cs" />` to `tests/maguswarrior.Tests.csproj`
  - [x] 1.2 — Create `tests/unit/HexCoordTest.cs` with all 7 tests from AC9
  - [x] 1.3 — Create `tests/unit/HexGridTest.cs` with all 5 tests from AC9
  - [x] 1.4 — Create `tests/unit/MapTileTest.cs` with all 4 tests from AC9 (including wrong-count exception test)
  - [x] 1.5 — Create `tests/unit/WorldMapTest.cs` with all 7 tests from AC9
  - [x] 1.6 — Run `dotnet build tests/maguswarrior.Tests.csproj` — confirmed 14 compilation errors (red phase)

- [x] Task 2 — Implement pure C# data layer (GREEN) (AC: 1, 2, 3, 4, 5, 6)
  - [x] 2.1 — Create `scripts/core/types/TerrainType.cs` and `scripts/core/types/TileType.cs`
  - [x] 2.2 — Create `scripts/hex/HexCoord.cs` — record struct, q/r, Directions, `+` operator, Neighbors, Distance, GetHashCode/Equals
  - [x] 2.3 — Create `scripts/hex/HexState.cs` — `record HexState(TerrainType Terrain, bool IsRevealed)`
  - [x] 2.4 — Create `scripts/hex/HexGrid.cs` — dictionary-backed, Add/Contains/GetState/GetNeighbors/AllHexes
  - [x] 2.5 — Create `scripts/map/MapTile.cs` — 7-hex guard, Reveal(), WorldHexes()
  - [x] 2.6 — Create `scripts/map/WorldMap.cs` — PlaceTile, Grid, HeroPosition, SetHeroPosition, HeroMoved
  - [x] 2.7 — Run `dotnet test` — 121 passed (98 existing + 23 new), 0 failures

- [x] Task 3 — Implement HexMapView (Godot layer) (AC: 7)
  - [x] 3.1 — Verified `Polygon2D.Polygon` (Vector2[]), `Color`, `Position`, `ZIndex` via context7
  - [x] 3.2 — Create `scripts/ui/components/HexMapView.cs` — flat-top Polygon2D per hex, terrain colors, white hero marker
  - [x] 3.3 — `dotnet build maguswarrior.csproj` — 0 errors, 0 warnings

- [x] Task 4 — Wire into PlaceholderMainMenu and verify (AC: 8, 10)
  - [x] 4.1 — Added `private WorldMap _worldMap = null!;` and `BuildStartingMap()` to `PlaceholderMainMenu`
  - [x] 4.2 — In `_Ready()`: WorldMap created, HexMapView created and wired before HandView
  - [x] 4.3 — `dotnet test` — 121 passed, 0 failures
  - [x] 4.4 — Game ran on desktop (WSLg); log confirms "HexMapView initialized: 7 hexes, hero at 0,0"; hand 5 cards intact

---

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

- TDD red phase: 14 compilation errors (MagusWarrior.Hex and .Map namespaces didn't exist)
- Missing `using MagusWarrior.Core;` in HexMapView caught by first build attempt
- Missing `using System.Linq;` in HexMapView caught by first build attempt
- xUnit2013 warning on `Assert.Equal(0, ...)` — fixed to `Assert.Empty()`
- Visual verify: Godot log `[DEBUG] [HexGrid] HexMapView initialized: 7 hexes, hero at 0,0`

### Completion Notes List

- Created full hex data layer: HexCoord (axial, flat-top), HexGrid (dictionary + queries), HexState (record), TerrainType (9 values), TileType (3 values)
- MapTile enforces 7-hex invariant at construction — ArgumentException on violation
- WorldMap: PlaceTile populates HexGrid for revealed tiles only; SetHeroPosition fires HeroMoved event
- HexMapView: one Polygon2D child per hex colored by terrain; white 30%-size inner hex for hero marker; subscribes to HeroMoved for live repositioning
- PlaceholderMainMenu: BuildStartingMap() creates 7-hex starting tile with varied terrain; HexMapView added as child at screen center above hand view
- 23 new tests across 4 files; total 121 tests, all green
- WorldMap is standalone instance (NOT in GameState/GameStateSnapshot — LOCKSTEP unchanged)

### File List

- **NEW** `scripts/core/types/TerrainType.cs`
- **NEW** `scripts/core/types/TileType.cs`
- **NEW** `scripts/hex/HexCoord.cs`
- **NEW** `scripts/hex/HexState.cs`
- **NEW** `scripts/hex/HexGrid.cs`
- **NEW** `scripts/map/MapTile.cs`
- **NEW** `scripts/map/WorldMap.cs`
- **NEW** `scripts/ui/components/HexMapView.cs`
- **NEW** `tests/unit/HexCoordTest.cs`
- **NEW** `tests/unit/HexGridTest.cs`
- **NEW** `tests/unit/MapTileTest.cs`
- **NEW** `tests/unit/WorldMapTest.cs`
- **MODIFIED** `scripts/ui/screens/PlaceholderMainMenu.cs`
- **MODIFIED** `tests/maguswarrior.Tests.csproj`

### Change Log

- 2026-06-08: Story 2-1 implemented — hex data layer + HexMapView rendering (claude-sonnet-4-6)
- 2026-06-08: Code review (Opus 4.8) — 3 adversarial layers; 1 decision, 1 patch, 3 deferred, 7 dismissed

## Review Findings

Reviewed 2026-06-08 by gds-code-review (Opus 4.8) — Blind Hunter, Edge Case Hunter, Acceptance Auditor.
Acceptance Auditor verdict: **PASS** — all 10 ACs satisfied, 121/121 tests green.

### Decisions Needed

- [x] [Review][Decision] `HexState.IsRevealed` is dead/always-true redundant state [scripts/hex/HexState.cs:5, scripts/map/WorldMap.cs:23-25] — **RESOLVED (John, 2026-06-08): Option (a) — drop the field now. Grid-membership is the revealed predicate; story 2-4 re-adds per-hex reveal when fog-of-war needs it.** Converted to patch P2.

### Patches

- [x] [Review][Patch] `AllHexes()` enumerated a second time purely for the log-line count [scripts/ui/components/HexMapView.cs:37] — **APPLIED**: capture `hexCount` in the build loop; removed now-dead `using System.Linq;`.
- [x] [Review][Patch] Drop dead `HexState.IsRevealed` field [scripts/hex/HexState.cs:5] — **APPLIED**: `record HexState(TerrainType Terrain)`; removed `IsRevealed: true` arg in `WorldMap.PlaceTile` and `HexGridTest`. Build clean, 121/121 green.

### Deferred

- [x] [Review][Defer] `HeroMoved` subscription never unsubscribed — freed-node risk on teardown [scripts/ui/components/HexMapView.cs:37] — deferred, cross-cutting codebase pattern (HandView/RestView/StagingAreaView all subscribe with `+=`, zero `_ExitTree` in scripts/). Cannot manifest this story (view lives for app session). Solve codebase-wide when screen teardown is introduced.
- [x] [Review][Defer] `PlaceTile` silently overwrites on hex collision; no §5.4 placement-legality enforcement [scripts/map/WorldMap.cs:21-26] — deferred, trap for story 2-4. `HexGrid.Add` is last-writer-wins; overlapping tiles clobber silently. Spec explicitly defers placement/movement validation. Story 2-4 must add a collision guard.
- [x] [Review][Defer] `MapTile.Reveal()` does not back-propagate to `WorldMap.Grid` [scripts/map/MapTile.cs:31, scripts/map/WorldMap.cs:21-26] — deferred, trap for story 2-4. Revealing a tile after unrevealed placement leaves the grid missing its 7 hexes. Story 2-4 needs a `WorldMap.RevealTile(...)` that re-runs grid population; calling `MapTile.Reveal()` directly will be a silent no-op against the grid.

### Dismissed (7)

- Blind Hunter: flat-top vs pointy-top rendering mismatch — **false positive**, adjudicated. `HexToPixel` (`x=3/2·q`) and `HexCorners` (`angle=π/3·i`) are both correct redblobgames flat-top; neighbors tessellate at `size·√3 ≈ 138.56px`. Confirmed by Edge Case Hunter's numerical check and the on-device render.
- Blind Hunter: Distance diagonal disagrees with Directions — self-cleared, no bug (cube formula correct).
- Blind Hunter: Neighbors yields fixed 6 — self-cleared, no bug.
- Blind Hunter: `Initialize` NREs if never called — pattern-only, currently safe (called immediately after AddChild).
- Edge Case Hunter: 7-hex guard vs. physical wedge starting tile — intentional, documented design decision.
- Edge Case Hunter: `(0,0)` default-vs-portal ambiguity — speculative; standard value-type behavior, no sentinel collision today.
- Acceptance Auditor: PASS, no violations (informational notes folded into patch/defer above).
