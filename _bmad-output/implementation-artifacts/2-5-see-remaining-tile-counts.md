# Story 2-5 — See Remaining Tile Counts

**Epic:** 2 — Hex Map + Movement
**Story ID:** 2-5
**Status:** done
**Created:** 2026-06-19
**Dependencies:** 2-4 (WorldMap.TileRevealed event, MapTile.TileType, the reveal flow — all complete and reviewed)
**Reviewer model:** Use Opus 4.8 for code review (per project feedback convention)

---

## User Story

As a player, I see remaining tile counts (countryside and core) on a HUD overlay so I know how deep into the map I am, and the counts update the moment I reveal a tile.

---

## Context

### What stories 2-0 through 2-4 built (do not recreate any of this)

- **`MapTile`** — `TileId`, `TileType` (`{ Starting, Countryside, Core }`), `IsRevealed`, `Origin`, `WorldHexes()`, `Reveal()`
- **`WorldMap`** — `PlaceTile`, `RevealTile(MapTile)` (canonical reveal path; idempotent — re-reveal is a no-op), `FindTileForCoord`, `TileRevealed` event (`Action<MapTile>`), `HeroMoved`, `Grid`, `PlacedTiles`, `HeroPosition`
- **`HexMapView`** — renders terrain + fog hexes; four-branch tap dispatch; `OnTileRevealed` re-colors fog→terrain
- **`PlaceholderMainMenu`** — `BuildStartingMap()` places a revealed Starting tile + one unrevealed Countryside tile (`countryside-1`); wires `HexMapView`, `HandView`, `RestView`, `ImprovisationView`, debug inspector
- **`GameState`** — `Fame`/`AddFame` (added in 2-4, not called on reveal), `ResourcesChanged`, etc.
- **168 tests passing** at story 2-4 close

### The gap this story fills

There is **no tile-deck / stock / count model anywhere in the codebase yet.** `data/tiles.yaml` is a full tile catalog (11 countryside, 4 city, 4 non-city core) but **nothing loads it** — `BuildStartingMap()` hardcodes its two tiles. So this story does **not** load `tiles.yaml`, shuffle, or draw real tiles. That full tile-deck system is the **deferred tile-drawing story** flagged in `deferred-work.md` (code review of 2-4).

This story introduces the **minimal source of truth for "tiles remaining to reveal"** and a HUD overlay that displays it. The model is a plain counter that decrements when a tile is revealed.

### Scope decision — minimal count model, not a real deck

**In scope:**
- A pure-C# `TileStock` model holding `CountrysideRemaining` and `CoreRemaining` counts + a `Changed` event
- Decrementing the matching counter when `WorldMap.TileRevealed` fires, keyed on the revealed tile's `TileType`
- A `TileCountView` HUD overlay that displays both counts and refreshes on `TileStock.Changed`
- Wiring in `PlaceholderMainMenu`

**Explicitly OUT of scope (deferred to the tile-drawing story / Epic 7):**
- Loading `data/tiles.yaml`, a `TileLoader`, shuffling, drawing real tile definitions
- Placement-legality rules (coastline, adjacency ≥ 2 — `hex-movement-lld §5.4`)
- City tile types (`CityWhite/Blue/Green/Red`) — `TileType` enum stays `{ Starting, Countryside, Core }`; City handling is Epic 7
- Persisting `TileStock` in the save / `GameStateSnapshot` (same deferral as `WorldMap.HeroPosition` — not yet snapshotted)
- i18n for the overlay label (Epic 2 UI scaffolding uses hardcoded strings — `HexMapView` "Move: N" / "Explore: 2" — stay consistent; i18n is a later UI pass)

### Count semantics — "tiles remaining to reveal"

The display means **tiles not yet revealed**, from the player's perspective ("how deep into the map am I"). The pre-placed `countryside-1` tile from story 2-4 is on the table **face-down (unrevealed)**, so it counts as one of the remaining countryside tiles. Revealing it decrements countryside `8 → 7`. This makes the reveal produce a visible counter decrement (good feedback, matches epic "Tile count updates on reveal").

### First Reconnaissance scenario counts

Per the epic (Epic 2 scope) and UX spec, the V-shape layout for First Reconnaissance is **8 countryside + 3 core tiles**. `TileStock` is initialized with these. They are hardcoded named constants in `PlaceholderMainMenu` for now; real per-scenario configuration lands in Epic 7 (scenario setup). The `Starting` tile is **not** part of the deck and does not count.

---

## Acceptance Criteria

**AC1 — Overlay shows initial counts on launch**
On game launch, a HUD overlay is visible showing `Countryside: 8` and `Core: 3` (the First Reconnaissance scenario counts). The overlay is always on screen.

**AC2 — Revealing a countryside tile decrements countryside, live**
Revealing `countryside-1` (the 2-4 reveal flow: move adjacent, tap the fog hex, spend 2) decrements the overlay's countryside count from `8` to `7` immediately, with no re-tap or polling. The update is driven by `TileStock.Changed`.

**AC3 — A countryside reveal does not change the core count**
After revealing a countryside tile, `Core` still reads `3`. Only the matching counter decrements.

**AC4 — Core reveal decrements core only**
`TileStock.RecordReveal(TileType.Core)` decrements `CoreRemaining` by 1 and leaves `CountrysideRemaining` unchanged. (No core tile is placed in the current map, so this is verified by unit test, not on-screen.)

**AC5 — Starting tile reveal is a no-op**
`TileStock.RecordReveal(TileType.Starting)` does not change either counter and does not fire `Changed` (the Starting tile is not part of the deck). Note: in practice the Starting tile is placed already-revealed via `PlaceTile`, so it never fires `TileRevealed` anyway — this AC guards the model directly.

**AC6 — Counts floor at zero**
`RecordReveal` never drives a counter below 0. Calling `RecordReveal(TileType.Countryside)` when `CountrysideRemaining == 0` leaves it at 0 (and still fires `Changed`, consistent with the project's fire-unconditionally convention for deck-type reveals).

**AC7 — `Changed` fires on every countryside/core reveal**
`RecordReveal(TileType.Countryside)` and `RecordReveal(TileType.Core)` fire `TileStock.Changed` so the overlay refreshes. (Fires even at floor, per AC6.)

**AC8 — Overlay does not overlap the hex map or the debug toggle**
The overlay is anchored to the top-right corner. It does not cover the centered title, the hex map (positioned at screen `(540, 600)`), or the debug toggle area (invisible 80×80 button at top-left).

**AC9 — `TileStock` is pure C# game logic**
`TileStock` lives in `scripts/map/`, inherits nothing from Godot, references no Godot API (no `Log`), and is fully unit-tested. Only `TileCountView` (`scripts/ui/components/`) touches Godot.

---

## Technical Design

### New: `scripts/map/TileStock.cs`

Pure C# counter model. Source of truth for "tiles remaining to reveal." No Godot dependency (compiled into the test project via the `scripts/map/**/*.cs` glob).

```csharp
using System;
using MagusWarrior.Core.Types;

namespace MagusWarrior.Map;

// Tracks how many tiles remain to be revealed, by back type. This is the minimal
// "deck depth" model — it does NOT hold real tile definitions (that is the future
// tile-drawing story that will load data/tiles.yaml). Counts decrement when a tile
// is revealed; the HUD overlay observes Changed.
public class TileStock {
    public int CountrysideRemaining { get; private set; }
    public int CoreRemaining { get; private set; }

    public event Action? Changed;

    public TileStock(int countrysideRemaining, int coreRemaining) {
        CountrysideRemaining = countrysideRemaining;
        CoreRemaining = coreRemaining;
    }

    // Decrement the counter matching the revealed tile's back type and notify observers.
    // Starting tiles are not part of the deck — no-op, no event. Counts floor at 0.
    public void RecordReveal(TileType type) {
        switch (type) {
            case TileType.Countryside:
                CountrysideRemaining = Math.Max(0, CountrysideRemaining - 1);
                Changed?.Invoke();
                break;
            case TileType.Core:
                CoreRemaining = Math.Max(0, CoreRemaining - 1);
                Changed?.Invoke();
                break;
            // TileType.Starting (and any future non-deck type): no-op, no event.
        }
    }
}
```

**Why fire `Changed` even at floor?** Consistent with the project convention defended in story 2-3 (`ResetMovePoints` fires `ResourcesChanged` even when already 0). Listeners refresh unconditionally; no stale-state edge case.

**Why `Math.Max(0, …)`?** Defensive floor — with the 2-4 idempotency guard a tile can't be revealed twice, but the model shouldn't go negative if a future caller over-decrements.

### New: `scripts/ui/components/TileCountView.cs`

HUD overlay. Observes `TileStock.Changed` and renders both counts. Anchored top-right.

```csharp
using Godot;
using MagusWarrior.Map;

namespace MagusWarrior.UI;

public partial class TileCountView : Control {
    private TileStock _stock = null!;
    private Label _label = null!;

    public void Initialize(TileStock stock) {
        _stock = stock;

        SetAnchorsAndOffsetsPreset(LayoutPreset.TopRight);

        _label = new Label();
        _label.Name = "TileCountLabel";
        _label.AddThemeFontSizeOverride("font_size", 28);
        _label.HorizontalAlignment = HorizontalAlignment.Right;
        _label.SetAnchorsAndOffsetsPreset(LayoutPreset.TopRight);
        _label.OffsetLeft = -360f;
        _label.OffsetTop = 20f;
        _label.OffsetRight = -20f;
        AddChild(_label);

        _stock.Changed += RefreshLabel;
        RefreshLabel();
        Log.Debug("[UI]", $"TileCountView initialized: countryside={_stock.CountrysideRemaining} core={_stock.CoreRemaining}");
    }

    private void RefreshLabel() {
        _label.Text = $"Countryside: {_stock.CountrysideRemaining}   Core: {_stock.CoreRemaining}";
    }
}
```

**Note on the unsubscribe deferral:** like every other view in the project (`HexMapView`, `HandView`, `RestView`, `StagingAreaView`), `TileCountView` subscribes to a model event without an `_ExitTree` unsubscribe. This is the established codebase-wide pattern (single-instance, app-lifetime views) and is tracked as a cross-cutting deferral — do **not** single this view out for a teardown fix.

### Changed: `scripts/ui/screens/PlaceholderMainMenu.cs`

Add the scenario constants, create the `TileStock`, wire it to `WorldMap.TileRevealed`, and add the overlay.

1. Add fields near the other members:

```csharp
private TileStock _tileStock = null!;
private TileCountView _tileCountView = null!;
```

2. Add named constants (First Reconnaissance scenario deck sizes; real scenario config is Epic 7):

```csharp
private const int FirstReconCountrysideTiles = 8;
private const int FirstReconCoreTiles = 3;
```

3. In `_Ready()`, after `_worldMap = BuildStartingMap();`, create and wire the stock:

```csharp
_tileStock = new TileStock(FirstReconCountrysideTiles, FirstReconCoreTiles);
_worldMap.TileRevealed += tile => _tileStock.RecordReveal(tile.TileType);
```

4. After the `HexMapView` is added/initialized, add the overlay:

```csharp
_tileCountView = new TileCountView();
_tileCountView.Name = "TileCountView";
AddChild(_tileCountView);
_tileCountView.Initialize(_tileStock);
```

**Wiring rationale:** `TileStock` is wired as an independent observer of `WorldMap.TileRevealed` rather than being owned by `WorldMap`. This keeps `WorldMap` unchanged (no new dependency) and keeps the two concerns distinct: `WorldMap` = placed tiles + grid; `TileStock` = deck depth. The reveal chain is `WorldMap.RevealTile` → `TileRevealed` → `TileStock.RecordReveal` → `TileStock.Changed` → `TileCountView.RefreshLabel`.

### No changes to `WorldMap`, `MapTile`, `GameState`, or `HexMapView`

The reveal event and `TileType` already exist. This story only observes them.

---

## Implementation Tasks

Execute in TDD order — write failing tests first, confirm they fail, then implement.

**Task 1 — `TileStock` model + 7 tests** ✅

1. [x] Add 7 tests to a new `tests/unit/TileStockTest.cs`
2. [x] Create `scripts/map/TileStock.cs`
3. [x] Run `dotnet test` — **175 total** (168 + 7), zero failures

**Task 2 — `TileCountView` overlay** ✅

1. [x] Create `scripts/ui/components/TileCountView.cs`
2. [x] Run `dotnet test` — still **175 green** (Godot-dependent, no new unit tests)

**Task 3 — Wire `PlaceholderMainMenu`** ✅

1. [x] Add `_tileStock`/`_tileCountView` fields, the two scenario constants, the `TileStock` creation + `TileRevealed` wiring, and the overlay
2. [x] Run `dotnet test` — still **175 green**; `dotnet build maguswarrior.csproj` succeeds 0/0 (caught a missing `using MagusWarrior.Core;` for `Log` — fixed)
3. [ ] Manually verify on desktop (WSLg) — tracked in `epic-2-manual-test-checklist.md`

**Task 4 — Add manual test entries to `epic-2-manual-test-checklist.md`** ✅

1. [x] Story 2-5 section added to the checklist

### Review Findings (Opus 4.8 multi-layer — 2026-06-19)

Blind Hunter + Edge Case Hunter + Acceptance Auditor. All 9 ACs verified MET or unverifiable-in-unit-test (AC1/AC2-render/AC8 = manual). No correctness bug in `RecordReveal`; no project-context violations. Triage: 0 decisions, 2 patches, 3 deferred, ~5 dismissed.

**Patches (test coverage gaps — unchecked):**

- [x] [Review][Patch] No test asserts the `Core` branch fires `Changed`. **Fixed:** added `RecordReveal_Core_FiresChanged`. [`tests/unit/TileStockTest.cs`]
- [x] [Review][Patch] No test locks the fire-on-floor convention (AC6+AC7). **Fixed:** added `RecordReveal_AtFloor_StillFiresChanged` (`TileStock(0,3)` → reveal → asserts `Changed` fired AND count still 0). [`tests/unit/TileStockTest.cs`]

**Deferred (logged in `deferred-work.md`):**

- [x] [Review][Defer] `TileStock` is not persisted (save writes only `current_phase`) — reload mid-exploration resets the count to 8/3. The map itself isn't persisted either, so the count stays self-consistent with the (reset) map, but exploration progress is silently lost. Save/snapshot of map + deck state is the Epic 7 / save story (spec Dev Notes already defer this). [`scripts/save/SaveData.cs`, `scripts/map/TileStock.cs`]
- [x] [Review][Defer] Counts are a hardcoded scenario placeholder decoupled from actual map inventory — after revealing `countryside-1` the HUD reads `Countryside: 7` though zero further tiles exist to reveal, and `Math.Max(0, …)` would silently mask an over-reveal if the constant ever undershoots the real tile count. Inherent until the real tile deck exists; resolved by the deferred tile-drawing story (real deck) + Epic 7 scenario config. The `8/3` constants are intentional to satisfy the epic's "8 countryside + 3 core" display requirement. [`scripts/ui/screens/PlaceholderMainMenu.cs`, `scripts/map/TileStock.cs`]
- [x] [Review][Defer] Event subscriptions never unsubscribed — `TileCountView._stock.Changed += RefreshLabel` and `PlaceholderMainMenu`'s `_worldMap.TileRevealed += tile => …` lambda have no `_ExitTree`/`-=` teardown. Established codebase-wide pattern (`HexMapView.HeroMoved`, `HandView`, `RestView`, `StagingAreaView` all do the same); single-instance app-lifetime nodes today. Solve as a cross-cutting teardown concern when scene lifecycle is introduced — do not single these out. [`scripts/ui/components/TileCountView.cs`, `scripts/ui/screens/PlaceholderMainMenu.cs`]

**Dismissed (5):** fire-on-floor "redundant event" (spec-sanctioned AC6/AC7 convention, not a bug); initial-count "off-by-one" (the face-down tile counting as remaining is the user-approved "tiles remaining to reveal" semantic); label double-`TopRight`-preset clip risk (render concern already covered by the manual checklist "overlay does not overlap" item); `using MagusWarrior.Core;` differs from spec snippet (the documented build-fix — diff is more correct than the snippet); `TileCountView` no `_ExitTree` raised separately by the Auditor (folded into the teardown defer).

---

## Tests to Write

### New file `tests/unit/TileStockTest.cs` — 7 tests

```csharp
using MagusWarrior.Core.Types;
using MagusWarrior.Map;
using Xunit;

namespace MagusWarrior.Tests;

public class TileStockTest {
    [Fact]
    public void Constructor_SetsInitialCounts() {
        var stock = new TileStock(8, 3);
        Assert.Equal(8, stock.CountrysideRemaining);
        Assert.Equal(3, stock.CoreRemaining);
    }

    [Fact]
    public void RecordReveal_Countryside_DecrementsCountrysideOnly() {
        var stock = new TileStock(8, 3);
        stock.RecordReveal(TileType.Countryside);
        Assert.Equal(7, stock.CountrysideRemaining);
        Assert.Equal(3, stock.CoreRemaining);
    }

    [Fact]
    public void RecordReveal_Core_DecrementsCoreOnly() {
        var stock = new TileStock(8, 3);
        stock.RecordReveal(TileType.Core);
        Assert.Equal(8, stock.CountrysideRemaining);
        Assert.Equal(2, stock.CoreRemaining);
    }

    [Fact]
    public void RecordReveal_Starting_DoesNotDecrement() {
        var stock = new TileStock(8, 3);
        stock.RecordReveal(TileType.Starting);
        Assert.Equal(8, stock.CountrysideRemaining);
        Assert.Equal(3, stock.CoreRemaining);
    }

    [Fact]
    public void RecordReveal_Countryside_FiresChanged() {
        var stock = new TileStock(8, 3);
        bool fired = false;
        stock.Changed += () => fired = true;
        stock.RecordReveal(TileType.Countryside);
        Assert.True(fired);
    }

    [Fact]
    public void RecordReveal_Starting_DoesNotFireChanged() {
        var stock = new TileStock(8, 3);
        bool fired = false;
        stock.Changed += () => fired = true;
        stock.RecordReveal(TileType.Starting);
        Assert.False(fired);
    }

    [Fact]
    public void RecordReveal_Countryside_FloorsAtZero() {
        var stock = new TileStock(1, 3);
        stock.RecordReveal(TileType.Countryside);
        stock.RecordReveal(TileType.Countryside);  // already at 0
        Assert.Equal(0, stock.CountrysideRemaining);
    }
}
```

**Total new tests: 7** → **175 tests green expected** at story close.

---

## Manual Test Additions for `epic-2-manual-test-checklist.md`

Add before the sign-off block:

```markdown
## Story 2-5 — See remaining tile counts

**Automated:** ✅ 177 tests green · main build 0/0 · Opus 4.8 multi-layer code review passed (2 test patches applied, 3 deferred)

**Manual (outstanding):**

- [ ] Launch → top-right overlay reads `Countryside: 8   Core: 3`
- [ ] Overlay does not overlap the centered title, the hex map, or the (invisible) top-left debug toggle
- [ ] Move hero to (1,0), reveal (2,0) (tap the fog hex with ≥ 2 Move points) → overlay updates **live** to `Countryside: 7   Core: 3` with no re-tap
- [ ] Core count stays `3` throughout (no core tile in the current map)
- [ ] Reveal does not move the count below 0 (only one countryside tile exists to reveal)
```

---

## Dev Notes

### `TileStock` is the minimal model — not the real deck

This story deliberately does NOT load `data/tiles.yaml` or implement a draw/shuffle deck. `TileStock` is two integers + an event. When the tile-drawing story lands (deferred from the 2-4 review), it will own the real tile definitions and `TileStock` either becomes the count facet of that system or is replaced. Keep it small.

### Count semantics: face-down tiles count as "remaining"

The `countryside-1` tile placed in 2-4 is on the table but unrevealed, so it counts toward `CountrysideRemaining`. Revealing it decrements `8 → 7`. This is the player-facing "how many tiles left to discover" reading, and it makes the reveal produce a visible counter change.

### `TileStock` is not yet in the save / snapshot

Like `WorldMap.HeroPosition`, `TileStock` is not part of `GameStateSnapshot` and is not persisted. Per-effect undo does not touch it, and a save/load won't restore it yet. This is the same deferral pattern as hero position — the turn/save story (Epic 7 / save story) wires deck state into persistence. Do not add it to `GameStateSnapshot` in this story.

### Wiring independence

`TileStock` observes `WorldMap.TileRevealed`; it is not owned by `WorldMap`. Two distinct concerns: placed tiles + grid (`WorldMap`) vs. deck depth (`TileStock`). The lambda `tile => _tileStock.RecordReveal(tile.TileType)` is the only coupling, living in `PlaceholderMainMenu` where all wiring lives.

### Overlay positioning

Top-right corner via `LayoutPreset.TopRight`. The debug toggle is an invisible 80×80 button at top-left (`OffsetRight = 80, OffsetBottom = 80`); the title is centered (full-rect, centered alignment); the hex map is at `(540, 600)`. Top-right is clear of all three. If the label collides with anything on-device, adjust the `OffsetLeft`/`OffsetTop` in `TileCountView` — geometry only, no logic change.

### Project-context.md rules that apply here

- **Pure C# game logic** — `TileStock` is in `scripts/map/`, no Godot inheritance, no `Log` (so it compiles into the test project). `TileCountView` is the only Godot-touching addition, in `scripts/ui/`.
- **No async void** — both new methods are synchronous.
- **Events are data only** — `TileStock.Changed` is a parameterless `Action` (the view re-reads counts); no logic on an event type.
- **Log with `[UI]` tag** — used in `TileCountView.Initialize`.
- **No `Result<T>` needed** — `RecordReveal` has no expected-failure path (out-of-range is floored, not an error).
- **InputLock** — not applicable; `TileCountView` handles no input (display only).

---

## Definition of Done

- [x] `dotnet test` passes — 168 existing + 9 new = **177 tests green**, zero failures (7 story tests + 2 review-patch coverage tests)
- [ ] Launch on desktop: top-right overlay reads `Countryside: 8   Core: 3`
- [ ] Reveal the countryside tile → overlay updates live to `Countryside: 7   Core: 3`
- [ ] Core count unchanged by a countryside reveal
- [ ] Overlay does not overlap title / hex map / debug toggle
- [ ] Manual test entries added to `epic-2-manual-test-checklist.md`
- [ ] Code review by Opus 4.8 before marking story done in sprint-status.yaml

---

## Dev Agent Record

### Agent Model Used

Claude Sonnet 4.6

### Completion Notes List

- TDD followed: 7 `TileStock` tests written red before implementation. 168 → 175 total; +2 review-patch coverage tests → **177 total**, all green.
- `TileStock` is a pure-C# two-counter model in `scripts/map/` (no Godot, no `Log` — compiles into the test project). `RecordReveal` decrements the matching counter (floored at 0) and fires `Changed`; `Starting` is a no-op with no event.
- `TileCountView` (`scripts/ui/components/`) is a display-only `Control` anchored top-right; observes `TileStock.Changed` and re-renders. Handles no input.
- Wired in `PlaceholderMainMenu`: `TileStock(8, 3)` (First Recon constants), `_worldMap.TileRevealed += tile => _tileStock.RecordReveal(tile.TileType)`, overlay added after `HexMapView`.
- **Build verification caught a real error the test project can't see:** `TileCountView` used `Log` without `using MagusWarrior.Core;`. The tests passed (Godot UI files aren't in the test csproj), but `dotnet build maguswarrior.csproj` failed CS0103 → added the using → build now 0/0. Lesson reinforced: run the Godot build for any new `scripts/ui/` file, not just `dotnet test`.
- Reveal chain verified by design: `WorldMap.RevealTile` → `TileRevealed` → `TileStock.RecordReveal` → `TileStock.Changed` → `TileCountView.RefreshLabel`. The 2-4 idempotency guard means a re-reveal won't double-decrement.
- Manual desktop verification pending at end of epic (tracked in `epic-2-manual-test-checklist.md`).

### File List

**Created:**
- `scripts/map/TileStock.cs` — pure C# count model
- `scripts/ui/components/TileCountView.cs` — HUD overlay
- `tests/unit/TileStockTest.cs` — 7 tests

**Modified:**
- `scripts/ui/screens/PlaceholderMainMenu.cs` — `TileStock` + overlay wiring, scenario constants
- `_bmad-output/implementation-artifacts/epic-2-manual-test-checklist.md` — story 2-5 section
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — story 2-5: ready-for-dev → in-progress → review
- `_bmad-output/implementation-artifacts/2-5-see-remaining-tile-counts.md` — tasks checked, completion notes, status → review
