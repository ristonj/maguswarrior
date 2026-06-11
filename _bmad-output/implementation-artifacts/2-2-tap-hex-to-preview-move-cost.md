# Story 2-2 — Tap Hex to Preview Move Cost

**Epic:** 2 — Hex Map + Movement
**Story ID:** 2-2
**Status:** done  <!-- dev + 143 automated tests + Opus 4.8 multi-layer code review complete 2026-06-10; on-device tap/label verification batched to the epic-2 manual test pass (see unchecked DoD items) -->

**Created:** 2026-06-10
**Dependencies:** 2-1 (HexMapView, HexGrid, WorldMap, HexState complete)
**Reviewer model:** Use Opus 4.8 for code review (per project feedback convention)

---

## User Story

As a player, I can tap a hex to preview its Move cost so I know if I can afford it.

---

## Context

### What story 2-1 built (don't recreate any of this)

- **`HexCoord`** (`scripts/hex/`) — axial coord record struct, `+` operator, `Neighbors()`, `Distance()`, static `Directions[]`
- **`HexGrid`** (`scripts/hex/`) — dictionary-backed, `Add/Contains/GetState()/GetNeighbors()/AllHexes()`
- **`HexState`** (`scripts/hex/`) — `record HexState(TerrainType Terrain)` — grid membership IS the revealed predicate; no `IsRevealed` field
- **`MapTile`** (`scripts/map/`) — 7-hex enforced, `Reveal()`, `WorldHexes()`
- **`WorldMap`** (`scripts/map/`) — `PlaceTile`, `HexGrid Grid`, `HeroPosition`, `SetHeroPosition`, `event Action<HexCoord>? HeroMoved`
- **`HexMapView`** (`scripts/ui/components/`) — Node2D, `Initialize(WorldMap map)`, flat-top Polygon2D per hex, white hero marker, subscribes `HeroMoved`
- `PlaceholderMainMenu` builds the starting 7-hex map and positions HexMapView at `(540, 600)`
- **121 tests passing** at story 2-1 close

### What story 2-2 adds

1. **`TerrainCosts`** — pure C# static class; canonical terrain cost lookup (Day and Night) from `docs/hex-movement-lld.md §2`
2. **`GameState.IsDay`** — Day/Night flag; defaults `true`; added to snapshot per LOCKSTEP rule
3. **`HexMapView`** — updated `Initialize` signature to accept `GameState` and `InputLock`; tap input handler; cost preview label
4. **`PlaceholderMainMenu`** — pass `_state` and `_inputLock` to the updated `Initialize` call

No movement happens in this story. Hero position is unchanged. Only a visual preview is shown.

### Deferred items from story 2-1 still open (do not fix in this story unless stated)

- `HeroMoved` subscription never unsubscribed — deferred codebase-wide
- `PlaceTile` silently overwrites on hex collision — deferred to story 2-4
- `MapTile.Reveal()` doesn't back-propagate to `WorldMap.Grid` — deferred to story 2-4
- `HexState.IsRevealed` redundant flag — deferred decision

---

## Terrain Cost Table

Canonical source: `docs/hex-movement-lld.md §2`. Implement exactly this table — do NOT invent costs.

| TerrainType | Day Cost | Night Cost | Notes |
|-------------|:--------:|:----------:|-------|
| Plains      | 2        | 2          | |
| Hills       | 3        | 3          | |
| Forest      | 3        | 5          | Night harder |
| Desert      | 5        | 3          | Day harder (opposite of Forest) |
| Swamp       | 5        | 5          | |
| Wasteland   | 4        | 4          | |
| Mountain    | null     | null       | Impassable |
| Lake        | null     | null       | Impassable |
| CitySpace   | 2        | 2          | Always 2 per LLD |

Return type for impassable terrain: `int?` — null means impassable, never use a magic number sentinel.

---

## Acceptance Criteria

**AC1 — Terrain cost table accuracy (unit-tested)**
`TerrainCosts.GetCost(TerrainType t, bool isDay)` returns the correct value for every terrain type in both Day and Night. Specifically:
- Forest: Day=3, Night=5
- Desert: Day=5, Night=3
- Mountain/Lake: null (both Day and Night)
- All other Day/Night pairs match the table above

**AC2 — Tap a revealed hex shows cost preview**
When the player taps any hex that exists in `WorldMap.Grid`, a label appears near that hex showing `"Move: N"` where N is the terrain cost for the current `GameState.IsDay`.

**AC3 — Impassable terrain shows "Impassable"**
Tapping a Mountain or Lake hex shows `"Impassable"` instead of a number. (Starting map has no Mountain/Lake, but the code path must exist and be covered by unit tests on `TerrainCosts`.)

**AC4 — Affordability color**
The preview label text color reflects whether the player can afford the move:
- `MovePointsThisTurn >= cost` → green (`#4CAF50`, `Color(0.298f, 0.686f, 0.314f)`)
- `MovePointsThisTurn < cost` → red (`#F44336`, `Color(0.957f, 0.263f, 0.212f)`)
- Impassable → grey (`Color(0.6f, 0.6f, 0.6f)`)

**AC5 — Preview replaces on second tap**
Tapping a second hex replaces the first preview. No accumulation. No ghost labels.

**AC6 — Day/Night flag drives cost**
`GameState.IsDay` defaults to `true`. When set to `false`, the cost preview reflects night costs (visible for Forest and Desert on the starting map — starting map has both).

**AC7 — InputLock injected and acquired on tap**
`HexMapView.Initialize` accepts an `InputLock`. The `_Input` handler acquires the lock before processing and releases in `finally`. A tap while locked is dropped silently (logged at `[Input]`).

**AC8 — Hero position unchanged**
`WorldMap.HeroPosition` is identical before and after any tap. No movement in this story.

**AC9 — GameState.IsDay in snapshot**
`TakeSnapshot()` includes `IsDay`; `RestoreSnapshot()` restores it. The LOCKSTEP comment on `GameState` must be updated with `IsDay`.

---

## Technical Design

### New: `scripts/hex/TerrainCosts.cs`

Pure C# static class. No Godot dependency. Add to `tests/maguswarrior.Tests.csproj` compile includes if `scripts/hex/**/*.cs` glob is not already present (it is — check line 36 of the csproj before adding).

```csharp
namespace MagusWarrior.Hex;

public static class TerrainCosts {
    // Returns null for impassable terrain (Mountain, Lake).
    public static int? GetCost(Core.Types.TerrainType terrain, bool isDay) => terrain switch {
        Core.Types.TerrainType.Plains     => 2,
        Core.Types.TerrainType.Hills      => 3,
        Core.Types.TerrainType.Forest     => isDay ? 3 : 5,
        Core.Types.TerrainType.Desert     => isDay ? 5 : 3,
        Core.Types.TerrainType.Swamp      => 5,
        Core.Types.TerrainType.Wasteland  => 4,
        Core.Types.TerrainType.Mountain   => null,
        Core.Types.TerrainType.Lake       => null,
        Core.Types.TerrainType.CitySpace  => 2,
        _                                 => throw new ArgumentOutOfRangeException(nameof(terrain), terrain, "Unknown terrain type"),
    };
}
```

Note the `_` arm throws — if a new `TerrainType` is added without updating `TerrainCosts`, the failure is loud and immediate, not a silent wrong cost.

### Changed: `scripts/core/GameEventLog.cs` — `GameStateSnapshot`

Add `IsDay` as the last positional parameter (append only — do not reorder existing fields):

```csharp
public record GameStateSnapshot(
    GamePhase CurrentPhase,
    int MovePointsThisTurn,
    int InfluencePointsThisTurn,
    IReadOnlyDictionary<(EffectType Distance, AttackElement Element), int> AttackPool,
    IReadOnlyDictionary<AttackElement, int> BlockPool,
    bool IsDay);   // ← add this
```

### Changed: `scripts/core/GameState.cs`

Add `IsDay` field, `SetIsDay` mutator, and update snapshot methods:

```csharp
public bool IsDay { get; private set; } = true;

public void SetIsDay(bool isDay) { IsDay = isDay; }
```

Update the LOCKSTEP comment to mention `IsDay`:
```
// LOCKSTEP: ... CurrentPhase, MovePointsThisTurn, InfluencePointsThisTurn,
// AttackPool, BlockPool, IsDay.
```

Update `TakeSnapshot()`:
```csharp
public GameStateSnapshot TakeSnapshot() => new(
    CurrentPhase,
    MovePointsThisTurn,
    InfluencePointsThisTurn,
    new Dictionary<(EffectType, AttackElement), int>(_attackPool),
    new Dictionary<AttackElement, int>(_blockPool),
    IsDay);
```

Update `RestoreSnapshot()`:
```csharp
public void RestoreSnapshot(GameStateSnapshot snapshot) {
    CurrentPhase = snapshot.CurrentPhase;
    MovePointsThisTurn = snapshot.MovePointsThisTurn;
    InfluencePointsThisTurn = snapshot.InfluencePointsThisTurn;
    _attackPool = new Dictionary<(EffectType, AttackElement), int>(snapshot.AttackPool);
    _blockPool = new Dictionary<AttackElement, int>(snapshot.BlockPool);
    IsDay = snapshot.IsDay;
}
```

### Changed: `scripts/ui/components/HexMapView.cs`

**New Initialize signature** (breaks the 2-1 call site in PlaceholderMainMenu — fix that simultaneously):

```csharp
public void Initialize(WorldMap map, GameState state, InputLock inputLock)
```

**New fields:**

```csharp
private GameState _state = null!;
private InputLock _lock = null!;
private Label _previewLabel = null!;
private HexCoord? _previewedHex;
```

**In `Initialize`**: store `state` and `inputLock`; create the preview label as a child:

```csharp
_state = state;
_lock = inputLock;

_previewLabel = new Label();
_previewLabel.Name = "MoveCostPreview";
_previewLabel.AddThemeFontSizeOverride("font_size", 28);
_previewLabel.ZIndex = 2;
_previewLabel.Visible = false;
AddChild(_previewLabel);
```

**Pixel-to-hex inverse** (add as private static method — pair with existing `HexToPixel`):

```csharp
private static HexCoord? PixelToHex(Vector2 localPos) {
    float q = 2f * localPos.X / (3f * HexSize);
    float r = localPos.Y / (HexSize * MathF.Sqrt(3f)) - q / 2f;
    // Cube rounding
    float s = -q - r;
    int rq = (int)MathF.Round(q);
    int rr = (int)MathF.Round(r);
    int rs = (int)MathF.Round(s);
    float dq = MathF.Abs(rq - q);
    float dr = MathF.Abs(rr - r);
    float ds = MathF.Abs(rs - s);
    if (dq > dr && dq > ds)
        return new HexCoord(-rr - rs, rr);
    if (dr > ds)
        return new HexCoord(rq, -rq - rs);
    return new HexCoord(rq, rr);
}
```

**`_Input` override for tap detection:**

```csharp
public override void _Input(InputEvent @event) {
    if (@event is not InputEventScreenTouch { Pressed: false } touch) return;
    if (!_lock.TryAcquire()) {
        Log.Debug("[Input]", "HexMapView tap dropped: input locked");
        return;
    }
    try {
        HandleHexTap(ToLocal(touch.Position));
    } finally {
        _lock.Release();
    }
}

private void HandleHexTap(Vector2 localPos) {
    var coord = PixelToHex(localPos);
    if (coord == null) return;
    var state = _map.Grid.GetState(coord.Value);
    if (state == null) return;  // tapped outside the grid

    _previewedHex = coord;
    int? cost = TerrainCosts.GetCost(state.Terrain, _state.IsDay);
    UpdatePreviewLabel(coord.Value, cost);
    Log.Debug("[Input]", $"Hex tapped: {coord.Value.Q},{coord.Value.R} terrain={state.Terrain} cost={cost?.ToString() ?? "impassable"}");
}

private void UpdatePreviewLabel(HexCoord coord, int? cost) {
    if (cost == null) {
        _previewLabel.Text = "Impassable";
        _previewLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
    } else {
        _previewLabel.Text = $"Move: {cost}";
        bool canAfford = _state.MovePointsThisTurn >= cost.Value;
        _previewLabel.AddThemeColorOverride("font_color",
            canAfford ? new Color(0.298f, 0.686f, 0.314f)
                      : new Color(0.957f, 0.263f, 0.212f));
    }
    var hexCenter = HexToPixel(coord);
    _previewLabel.Position = hexCenter + new Vector2(-30f, -HexSize - 10f);
    _previewLabel.Visible = true;
}
```

### Changed: `scripts/ui/screens/PlaceholderMainMenu.cs`

Update the `hexMapView.Initialize` call to pass `_state` and `_inputLock`:

```csharp
hexMapView.Initialize(_worldMap, _state, _inputLock);
```

No other changes to PlaceholderMainMenu.

---

## Implementation Tasks

Execute in this order — TDD: write the failing test first, confirm it fails, then implement.

**Task 1 — TerrainCosts + tests**
1. Create `tests/unit/TerrainCostsTest.cs` with all 18 cases below (all red)
2. Create `scripts/hex/TerrainCosts.cs` with the switch expression above
3. Run `dotnet test` — all 18 new tests green, 121 existing still green

**Task 2 — GameState.IsDay + snapshot**
1. Add test to `tests/unit/GameStateTest.cs`: `IsDay_DefaultsToTrue`, `SetIsDay_False_UpdatesFlag`, `TakeSnapshot_IncludesIsDay`, `RestoreSnapshot_RestoresIsDay`
2. Update `GameStateSnapshot` in `GameEventLog.cs` (add `bool IsDay`)
3. Update `GameState.cs` (`IsDay` property, `SetIsDay`, both snapshot methods, LOCKSTEP comment)
4. Run `dotnet test` — all new tests green, all existing still green

**Task 3 — HexMapView + PlaceholderMainMenu**
1. Update `HexMapView.Initialize` signature to `(WorldMap map, GameState state, InputLock inputLock)`
2. Add `_state`, `_lock`, `_previewLabel` fields
3. Add `PixelToHex`, `HandleHexTap`, `UpdatePreviewLabel`, `_Input` override
4. Update `PlaceholderMainMenu._Ready` to call `hexMapView.Initialize(_worldMap, _state, _inputLock)`
5. Run `dotnet test` — all tests still green (HexMapView is Godot, no new unit tests for it)
6. Manually verify on desktop (Godot editor play) or device: tap hexes, see cost labels

### Review Findings (Opus 4.8 adversarial review, 2026-06-10)

- [ ] [Review][Decision] Preview goes stale when game state changes while it is visible — `SetIsDay` fires no event and `HexMapView` only resolves cost at tap time. Reachable now: tap a hex showing red "Move: 3" (insufficient points), then stage cards to gain move points → label stays red (stale affordability). Also: a future day/night flip leaves Forest/Desert cost stale. The spec's Technical Design originally listed a `private HexCoord? _previewedHex` field (dropped in the shipped code) implying a re-render intent. Fork: (a) hide the preview on `ResourcesChanged`, or (b) cache the last-tapped hex and re-render on `ResourcesChanged` (+ wire `SetIsDay` to notify). [HexMapView.cs:78, GameState.cs:47]
- [ ] [Review][Patch] `_Input` should be `_UnhandledInput` — `HexMapView` is a `Node2D`; `_Input` fires for every touch even when a GUI `Control` (e.g. a HandView card button overlapping the map) already consumed it, and the handler never calls `SetInputAsHandled`. A tap on an overlapping card both clicks the card and previews a hex. [HexMapView.cs:54]
- [ ] [Review][Patch] Dead null guard — `PixelToHex` is typed `HexCoord?` but `HexRound` always returns a value, so `if (coord == null) return;` is unreachable. Make `PixelToHex` return non-nullable `HexCoord` and remove the guard; off-grid taps are already filtered by the `GetState() == null` check. [HexMapView.cs:96, HexMapView.cs:69]
- [x] [Review][Defer] No round-trip test for `PixelToHex`/`HexToPixel` — the inverse math is confirmed correct by the acceptance audit, but it has zero automated coverage. Blocked: the math is private to a Godot `Node2D` and not reachable from the pure-C# test project. Would require extracting the hex transforms into a pure-C# helper (e.g. `MagusWarrior.Hex`). [HexMapView.cs:96] — deferred, arch-limited
- [x] [Review][Defer] Preview label has no viewport-bounds clamp; the fixed `(-30, -HexSize-10)` offset mis-centers the "Impassable" text and can render off-screen for top/edge hexes on larger maps. [HexMapView.cs:90] — deferred, polish
- [x] [Review][Defer] `HeroMoved` lambda subscription and the per-hex child nodes are created in `Initialize` with no `_ExitTree` cleanup and no re-entry guard; a second `Initialize` call would duplicate children and stack handlers. Pre-existing from story 2-1; `Initialize` is called exactly once today. [HexMapView.cs:50] — deferred, pre-existing
- [x] [Review][Defer] `ToLocal(touch.Position)` maps screen→node correctly only while there is no `Camera2D`; once panning/zoom is added the tap will resolve to the wrong hex. No camera exists today. [HexMapView.cs:61] — deferred, forward-looking

### Review Findings — Multi-layer adversarial pass (2026-06-10)

Blind Hunter + Edge Case Hunter + Acceptance Auditor (all Opus 4.8). **Acceptance Auditor confirmed all 9 ACs SATISFIED** and all project-context rules compliant (logging tags, InputLock acquire/release in `finally`, pure-C# boundary, snapshot LOCKSTEP, no `async void`). This pass reconfirms the three open items from the single-reviewer pass above (all still un-applied in shipped code) and adds two new patches and one design note.

**Patches (all applied 2026-06-10, 143 tests green, main build 0/0):**
- [x] [Review][Patch] Preview goes stale when game state changes while visible. **RESOLUTION (John, option 2):** added `_previewedHex` cache, `RefreshPreview()` handler, and subscriptions to `GameState.ResourcesChanged` + new `GameState.DayNightChanged` event (fired from `SetIsDay`, now no-op-guarded). [HexMapView.cs, GameState.cs]
- [x] [Review][Patch] `_Input` → `_UnhandledInput` + `GetViewport().SetInputAsHandled()` so a tap an overlapping GUI `Control` already consumed no longer double-fires a hex preview. Dev Note below corrected. [HexMapView.cs]
- [x] [Review][Patch] Dead null guard removed — `PixelToHex` now returns non-nullable `HexCoord`; off-grid taps filtered by `GetState() == null`. [HexMapView.cs]
- [x] [Review][Patch] Multi-touch filtered — `if (touch.Index != 0) return;` added so only the primary touch previews. [HexMapView.cs]
- [x] [Review][Patch] `RestoreSnapshot_RestoresIsDay` strengthened — snapshots `IsDay == false` and restores over `true`, so a no-op restore can no longer pass. [tests/unit/GameStateTest.cs]

**Deferred (new this pass):**
- [x] [Review][Defer] `IsDay` now participates in the per-effect undo snapshot (AC9-required). Once day/night round transitions are wired (Epic 7), an undo of an unrelated effect could roll back a day/night flip — day/night is a round-level global, not effect-local state. Revisit during turn/round-structure work. [GameState.cs RestoreSnapshot] — deferred, forward-looking
- [x] [Review][Defer] Drag/swipe that lifts over a hex registers as a tap (no press-position capture or movement threshold). Harmless today (no pan/camera gesture); revisit when panning lands in 2-3+. [HexMapView.cs:55] — deferred, forward-looking

(Reconfirmed-but-already-deferred from the pass above, not re-logged: no automated coverage for `PixelToHex`/`HexRound` math; `ToLocal` camera-blind; preview label off-screen clamp.)

---

## Tests to Write

### `tests/unit/TerrainCostsTest.cs` — 18 tests

```csharp
using MagusWarrior.Core.Types;
using MagusWarrior.Hex;
using Xunit;

namespace MagusWarrior.Tests;

public class TerrainCostsTest {
    [Fact] public void GetCost_Plains_Day_Returns2() =>
        Assert.Equal(2, TerrainCosts.GetCost(TerrainType.Plains, isDay: true));
    [Fact] public void GetCost_Plains_Night_Returns2() =>
        Assert.Equal(2, TerrainCosts.GetCost(TerrainType.Plains, isDay: false));
    [Fact] public void GetCost_Hills_Day_Returns3() =>
        Assert.Equal(3, TerrainCosts.GetCost(TerrainType.Hills, isDay: true));
    [Fact] public void GetCost_Hills_Night_Returns3() =>
        Assert.Equal(3, TerrainCosts.GetCost(TerrainType.Hills, isDay: false));
    [Fact] public void GetCost_Forest_Day_Returns3() =>
        Assert.Equal(3, TerrainCosts.GetCost(TerrainType.Forest, isDay: true));
    [Fact] public void GetCost_Forest_Night_Returns5() =>
        Assert.Equal(5, TerrainCosts.GetCost(TerrainType.Forest, isDay: false));
    [Fact] public void GetCost_Desert_Day_Returns5() =>
        Assert.Equal(5, TerrainCosts.GetCost(TerrainType.Desert, isDay: true));
    [Fact] public void GetCost_Desert_Night_Returns3() =>
        Assert.Equal(3, TerrainCosts.GetCost(TerrainType.Desert, isDay: false));
    [Fact] public void GetCost_Swamp_Day_Returns5() =>
        Assert.Equal(5, TerrainCosts.GetCost(TerrainType.Swamp, isDay: true));
    [Fact] public void GetCost_Swamp_Night_Returns5() =>
        Assert.Equal(5, TerrainCosts.GetCost(TerrainType.Swamp, isDay: false));
    [Fact] public void GetCost_Wasteland_Day_Returns4() =>
        Assert.Equal(4, TerrainCosts.GetCost(TerrainType.Wasteland, isDay: true));
    [Fact] public void GetCost_Wasteland_Night_Returns4() =>
        Assert.Equal(4, TerrainCosts.GetCost(TerrainType.Wasteland, isDay: false));
    [Fact] public void GetCost_Mountain_Day_ReturnsNull() =>
        Assert.Null(TerrainCosts.GetCost(TerrainType.Mountain, isDay: true));
    [Fact] public void GetCost_Mountain_Night_ReturnsNull() =>
        Assert.Null(TerrainCosts.GetCost(TerrainType.Mountain, isDay: false));
    [Fact] public void GetCost_Lake_Day_ReturnsNull() =>
        Assert.Null(TerrainCosts.GetCost(TerrainType.Lake, isDay: true));
    [Fact] public void GetCost_Lake_Night_ReturnsNull() =>
        Assert.Null(TerrainCosts.GetCost(TerrainType.Lake, isDay: false));
    [Fact] public void GetCost_CitySpace_Day_Returns2() =>
        Assert.Equal(2, TerrainCosts.GetCost(TerrainType.CitySpace, isDay: true));
    [Fact] public void GetCost_CitySpace_Night_Returns2() =>
        Assert.Equal(2, TerrainCosts.GetCost(TerrainType.CitySpace, isDay: false));
}
```

### Additions to `tests/unit/GameStateTest.cs` — 4 tests

```csharp
[Fact]
public void IsDay_DefaultsToTrue() {
    var state = new GameState(Array.Empty<CardDefinition>());
    Assert.True(state.IsDay);
}

[Fact]
public void SetIsDay_False_UpdatesFlag() {
    var state = new GameState(Array.Empty<CardDefinition>());
    state.SetIsDay(false);
    Assert.False(state.IsDay);
}

[Fact]
public void TakeSnapshot_IncludesIsDay() {
    var state = new GameState(Array.Empty<CardDefinition>());
    state.SetIsDay(false);
    var snap = state.TakeSnapshot();
    Assert.False(snap.IsDay);
}

[Fact]
public void RestoreSnapshot_RestoresIsDay() {
    var state = new GameState(Array.Empty<CardDefinition>());
    var snap = state.TakeSnapshot();  // IsDay = true
    state.SetIsDay(false);
    state.RestoreSnapshot(snap);
    Assert.True(state.IsDay);
}
```

**Total new tests: 22** (18 TerrainCosts + 4 GameState)

---

## Dev Notes

### `_Input` vs `_GuiInput` on Node2D

`_GuiInput` only fires for `Control` nodes. `HexMapView` is a `Node2D`. **Use `_UnhandledInput(InputEvent)`** — it fires for `Node2D` and receives `InputEventScreenTouch`, but (unlike `_Input`) skips events a GUI `Control` already consumed, so a tap on an overlapping card button does not also preview a hex. The handler calls `GetViewport().SetInputAsHandled()` after processing. *(Corrected by the 2026-06-10 code review — an earlier draft of this note recommended `_Input`, which double-fires under overlapping Controls.)*

On desktop (Godot editor play), touch events don't fire from mouse clicks by default. To test on desktop without a touchscreen, either:
1. Enable **Emulate Touch from Mouse** in Project Settings → Input Devices → Pointing
2. Or test on Galaxy S21 directly

Enable "Emulate Touch from Mouse" for the desktop dev loop.

### Pixel-to-hex math must match `HexToPixel`

The `PixelToHex` function was derived directly from inverting the `HexToPixel` formula in `HexMapView.cs`. If `HexToPixel` is ever changed, `PixelToHex` must be updated in lockstep. Verify they agree: for any coord `c`, `PixelToHex(HexToPixel(c)) == c`.

### Label position offset

The preview label is positioned at `HexToPixel(coord) + Vector2(-30, -HexSize - 10)` — above the hex center. `HexSize = 80` so the label appears ~90px above the center. This places it clear of the hex for typical hex sizes. The `_previewLabel` is sized by content; no explicit width needed. If label clips the screen edge, a future story can add bounds clamping.

### Why `InputLock` here for a read-only tap

The tap preview doesn't mutate game state, but the project-context.md explicitly specifies "Story 2-2 adds tap handlers and will inject InputLock at that point." This establishes the pattern for story 2-3 (which WILL mutate state by spending move points). Story 2-3 will find `InputLock` already wired and will keep the same acquire/release structure.

### Starting map for manual verification

`PlaceholderMainMenu.BuildStartingMap()` creates:
- `(0, 0)` Plains → Day: 2, Night: 2
- `(1, 0)` Plains → Day: 2, Night: 2
- `(-1, 0)` Forest → Day: **3**, Night: **5**
- `(0, 1)` Hills → Day: 3, Night: 3
- `(0, -1)` Swamp → Day: 5, Night: 5
- `(1, -1)` Plains → Day: 2, Night: 2
- `(-1, 1)` Wasteland → Day: 4, Night: 4

The Forest at `(-1, 0)` is the visual proof that Day/Night variant costs work: tap it during Day → "Move: 3"; after `_state.SetIsDay(false)` → "Move: 5". Desert is not on the starting map so the Desert cost change is covered by unit tests only.

### Snapshot LOCKSTEP — compile error will catch drift

The `GameStateSnapshot` is a positional record. Adding `bool IsDay` as the last parameter means all 4 existing `new GameStateSnapshot(...)` call sites (1 in `TakeSnapshot`, 1 in `EffectFiredEvent` test snapshots) will produce CS1501 compile errors if not updated. This is by design — the LOCKSTEP comment says every field must be added to both snapshot and restore. The compile error enforces it.

Check `tests/unit/GameStateTest.cs` for any hand-constructed `GameStateSnapshot` instances and update them.

### `HexState` has no `IsRevealed` field

From story 2-1 review: `IsRevealed` was dropped from `HexState`. A hex's presence in `WorldMap.Grid` is the revealed predicate. The `GetState()` null-check in `HandleHexTap` serves as the revealed guard: if `GetState` returns null, the tap is outside the grid (unrevealed / off-map), and the preview is not shown.

### Do not add `InputEventMouse` handling

This is an Android-first project. Mouse events in the editor are handled via the "Emulate Touch from Mouse" project setting — not by adding `InputEventMouseButton` branches to `_Input`. Keeping `_Input` as touch-only keeps the code clean and ensures desktop testing mirrors mobile behavior.

### No `IsRevealed` guard needed in `HandleHexTap`

The starting map in 2-2 has only one tile, placed face-up. All hexes in `WorldMap.Grid` are revealed. `GetState()` returning null is sufficient to guard off-grid taps. A per-hex `IsRevealed` check is not needed in this story.

---

## Definition of Done

- [x] `dotnet test` passes — 121 existing + 22 new = **143 tests green**, zero failures
- [ ] Tap each of the 7 starting hexes on the desktop or device; each shows "Move: N" with correct cost and affordability color
- [ ] `_state.SetIsDay(false)` in `PlaceholderMainMenu._Ready` (temporarily, for testing): Forest at (-1,0) shows "Move: 5" not 3 — then restore `SetIsDay(true)` (or remove the call)
- [ ] No hero movement occurs when tapping any hex
- [ ] Code review by Opus 4.8 before marking story done in sprint-status.yaml

---

## Dev Agent Record

### Implementation Notes

- **Task 1 — TerrainCosts:** Created `scripts/hex/TerrainCosts.cs` — pure static class, switch expression with `_` throw arm. All 18 test cases green on first pass.
- **Task 2 — GameState.IsDay:** Added `IsDay` property (default `true`) + `SetIsDay()` mutator. Updated `GameStateSnapshot` positional record (last position to avoid reordering existing args). Updated both `TakeSnapshot` and `RestoreSnapshot`. Updated LOCKSTEP comment. 4 new tests green.
- **Task 3 — HexMapView + PlaceholderMainMenu:** Replaced `Initialize(WorldMap)` with `Initialize(WorldMap, GameState, InputLock)`. Added `PixelToHex`/`HexRound` inverse math (derived from existing `HexToPixel` formula). Added `_Input` tap handler with InputLock acquire/release. Added `_previewLabel` Label child node (shown on tap, repositioned per hex, affordability color). Updated `PlaceholderMainMenu` call site. Main Godot project builds clean (0 warnings, 0 errors).
- **Final count:** 143 tests passing (121 pre-existing + 22 new).

### Implementation Plan

Followed exact task order from story: (1) TerrainCosts pure C# + 18 tests, (2) GameState.IsDay + snapshot + 4 tests, (3) HexMapView tap handler + PlaceholderMainMenu call site update. TDD red-green discipline maintained throughout.

### Completion Notes

All three tasks complete. 143 tests green, both `dotnet build` and `dotnet test` pass clean. On-device/desktop visual verification of tap → cost label is pending human confirmation per the Definition of Done checklist.

---

## File List

### New Files
- `scripts/hex/TerrainCosts.cs`
- `tests/unit/TerrainCostsTest.cs`

### Modified Files
- `scripts/core/GameEventLog.cs` — `GameStateSnapshot` gained `bool IsDay` positional parameter
- `scripts/core/GameState.cs` — `IsDay` property, `SetIsDay()`, snapshot methods updated, LOCKSTEP comment updated
- `scripts/ui/components/HexMapView.cs` — new `Initialize` signature, `_Input` tap handler, `PixelToHex`, `HexRound`, `HandleHexTap`, `UpdatePreviewLabel`, `_previewLabel` field
- `scripts/ui/screens/PlaceholderMainMenu.cs` — `hexMapView.Initialize` call passes `_state` and `_inputLock`
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — story 2-2 status updated
- `_bmad-output/implementation-artifacts/2-2-tap-hex-to-preview-move-cost.md` — this file (status, task checkboxes, dev record)

---

## Change Log

- **2026-06-10:** Implemented story 2-2. Added `TerrainCosts` pure C# terrain cost lookup (Day/Night, from hex-movement-lld.md §2). Added `GameState.IsDay` flag with snapshot support. Updated `HexMapView` to accept `GameState` + `InputLock`, handle touch tap input, and show move-cost preview label with affordability color. 143 tests passing.
