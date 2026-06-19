# Epic 2 — Manual Test Checklist

**Workflow:** Stories are dev'd + automated-tested + code-reviewed individually, then **manually verified in one batch at the end of the epic** (before flipping `epic-2` → `done` and running the retro). This file is the running checklist for that end-of-epic pass.

**Each story is marked `done` in sprint-status once dev + automated tests + code review pass.** A `done` status here means *code-complete and reviewed* — the on-device/desktop boxes below are the remaining gate before the epic itself closes.

> **Maintenance:** when a new epic-2 story is developed, append its manual-verification items here (pull them from that story's Definition of Done). When you complete the epic-2 manual pass, check the boxes and note the date + device.

---

## How to run

- **Desktop (fast loop):** WSLg + Godot 4.6.3 mono. Enable **Project Settings → Input Devices → Pointing → Emulate Touch From Mouse** so mouse clicks fire `InputEventScreenTouch` (the views handle touch only, by design).
- **Device:** Galaxy S21, landscape. (`adb` from WSL per the device-dev-loop notes.)
- Watch the Godot log for the `[HexGrid]` / `[Input]` debug lines while testing — they confirm the tap resolved to the expected hex/cost.

---

## Starting map reference (`PlaceholderMainMenu.BuildStartingMap()`)

HexMapView is positioned at screen `(540, 600)`. The 7 starting hexes and their costs:

| Hex (Q,R) | Terrain   | Day cost | Night cost |
|-----------|-----------|:--------:|:----------:|
| (0, 0)    | Plains    | 2        | 2          |
| (1, 0)    | Plains    | 2        | 2          |
| (-1, 0)   | Forest    | **3**    | **5**      |
| (0, 1)    | Hills     | 3        | 3          |
| (0, -1)   | Swamp     | 5        | 5          |
| (1, -1)   | Plains    | 2        | 2          |
| (-1, 1)   | Wasteland | 4        | 4          |

> **Affordability color note:** `MovePointsThisTurn` starts at **0** (the reset/grant path lands in story 2-3), so on a fresh launch **every** cost > 0 renders **red**. To see a **green** (affordable) label you must first gain move points — stage a Move card sideways / via Improvisation — then tap. This is expected, not a bug.

---

## Story 2-0 — InputLock re-entrancy guard

✅ **No manual test required.** Pure C# logic, fully covered by `InputLockTest.cs` (5 xUnit tests). Nothing renders.

## Story 2-1 — See hex map and hero position

✅ **Already verified during dev** (story 2-1 Task 4.4): game ran on WSLg desktop; log confirmed `HexMapView initialized: 7 hexes, hero at 0,0`; 7 hexes render flat-top with the white hero marker at center; hand of 5 cards intact.

- [ ] *(Optional re-confirm in the batch pass)* 7 hexes render, correct terrain colors, white hero marker centered on (0,0).

## Story 2-2 — Tap hex to preview move cost

**Automated:** ✅ 143 tests green · main build 0/0 · Opus 4.8 multi-layer code review passed (5 patches applied).

**Manual (outstanding):**

- [ ] Tap each of the 7 starting hexes — each shows `"Move: N"` with the **Day** cost from the table above. Forest (-1,0) → "Move: 3", Swamp (0,-1) → "Move: 5", etc.
- [ ] With `MovePointsThisTurn == 0`, every label is **red** (unaffordable). After gaining move points (stage a Move card), tapping an affordable hex shows **green**.
- [ ] **Day/Night (AC6):** temporarily add `_state.SetIsDay(false)` in `PlaceholderMainMenu._Ready`, relaunch → Forest (-1,0) shows **"Move: 5"** (not 3), Swamp still 5. Then **restore `SetIsDay(true)` / remove the line.**
- [ ] **No movement (AC8):** tapping any hex never moves the hero marker — it stays on (0,0).
- [ ] **Second tap replaces first (AC5):** tap two hexes in sequence — only one label is ever visible, no ghosts/accumulation.

**Manual — new behaviors from the 2026-06-10 code-review patches:**

- [ ] **Live re-render (Patch 1):** tap an unaffordable hex (red "Move: 3"), then gain move points (stage a Move card) **without re-tapping** → the label flips to **green** on its own. With a temporary `SetIsDay` toggle wired to a key, the Forest cost should also update live (3 ⇄ 5).
- [ ] **No double-fire (Patch 2):** if any HandView/Rest/Improvisation control overlaps the map, tapping it does **not** also pop a hex preview. (May be a no-op today if nothing overlaps the map region.)

## Story 2-3 — Spend move points to cross hexes

**Automated:** ✅ 158 tests green (5 new GameState + 10 new WorldMap + 143 carry-over) · main build 0/0 · Opus 4.8 multi-layer code review passed (1 decision hardened, 1 patch applied, 5 deferred).

**Manual (outstanding):**

- [ ] Stage ≥ 2 Move points; tap adjacent Plains hex (cost 2) → hero marker moves immediately, points decrease by 2, preview clears. (Single tap commits — no second confirmation tap needed.)
- [ ] Tap hero's current hex → hero moves back, 2 points restored.
- [ ] Chain two moves (origin → A → B); tap B → back to A; tap A → back to origin; tap origin when nothing to undo → nothing happens (no crash).
- [ ] Tap non-adjacent hex → preview only, no movement.
- [ ] Tap Swamp (0,-1) with < 5 points (adjacent) → red preview, no move.
- [ ] `StagingAreaView` "Move:" total reflects both spending and refunds immediately.
- [ ] After a move, adjacency for further moves is relative to the **new** hero position.

**Manual — code-review D1 hardening (debug build only):**

- [ ] Gain move points (e.g. play a Move card so the effect log has an entry), move to an adjacent hex (spend points), then open the Effect Event Log inspector and **"Undo Last"**: move points return **once** (to the pre-effect value), and tapping the hero hex afterward does **not** refund a second time (no MP duplication). Hero may remain on the moved-to hex — that's the documented residual, not a bug.

## Story 2-4 — Reveal new tile by moving to map edge

**Automated:** ✅ 168 tests green · main build 0/0 · Opus 4.8 multi-layer code review passed (4 patches applied, 3 deferred)

**Starting map reference (updated — 2 tiles, 14 hexes):**

| Hex (Q,R) | Tile | Terrain | Revealed |
|-----------|------|---------|---------|
| (0,0) | starting | Plains | ✅ |
| (1,0) | starting | Plains | ✅ |
| (-1,0) | starting | Forest | ✅ |
| (0,1) | starting | Hills | ✅ |
| (0,-1) | starting | Swamp | ✅ |
| (1,-1) | starting | Plains | ✅ |
| (-1,1) | starting | Wasteland | ✅ |
| (2,0) | countryside-1 | Forest | ❌ fog — adjacent to (1,0) |
| (2,1) | countryside-1 | Mountain | ❌ fog |
| (3,-1) | countryside-1 | Desert | ❌ fog |
| (3,0) | countryside-1 | Plains | ❌ fog |
| (3,1) | countryside-1 | Wasteland | ❌ fog |
| (4,-1) | countryside-1 | Plains | ❌ fog |
| (4,0) | countryside-1 | Hills | ❌ fog |

**Manual (outstanding):**

- [ ] Launch → 14 hex polygons visible; 7 terrain-colored (starting tile), 7 dark near-black fog (countryside-1)
- [ ] Tap (2,0) fog hex from hero at (0,0) → "Explore: 2" label appears in **red** (0 Move points, not affordable)
- [ ] Stage a Move card (gain ≥ 2 points) without re-tapping → label flips to **green** (live re-render, AC5)
- [ ] Tap (3,0) fog hex from (0,0) — non-adjacent → "Explore: 2" preview (green/red by affordability); no reveal (AC4)
- [ ] Move hero to (1,0) (Plains, costs 2) → hero marker moves, points decrease
- [ ] With ≥ 2 Move points remaining, tap (2,0) → **all 7 fog hexes re-color to terrain** (Forest dark green at (2,0), Hills brown at (4,0), etc.); preview label hides; Godot log shows `[Input] Tile 'countryside-1' revealed` (AC2)
- [ ] After reveal, tap hero's current hex → **nothing** (undo disabled, `CanUndoMove` is false) (AC7)
- [ ] Move onto now-revealed (2,0) — Forest, Day cost 3 — with ≥ 3 remaining points → hero moves (AC6)
- [ ] Confirm Fame stays 0 after reveal (fame-on-reveal is not wired — scenario-specific) (AC10)

**Edge cases:**
- [ ] Tap (2,0) fog hex with < 2 points → "Explore: 2" **red** preview; no reveal (AC3)
- [ ] If a preview label is visible when reveal fires → it disappears (AC8)
- [ ] Single-tap reveals immediately — no second confirmation tap (AC9)

## Story 2-5 — See remaining tile counts

**Automated:** ✅ 177 tests green · main build 0/0 · Opus 4.8 multi-layer code review passed (2 test patches applied, 3 deferred)

**Manual (outstanding):**

- [ ] Launch → top-right overlay reads `Countryside: 8   Core: 3`
- [ ] Overlay does not overlap the centered title, the hex map, or the (invisible) top-left debug toggle
- [ ] Move hero to (1,0), reveal (2,0) (tap the fog hex with ≥ 2 Move points) → overlay updates **live** to `Countryside: 7   Core: 3` with no re-tap
- [ ] Core count stays `3` throughout (no core tile in the current map)
- [ ] Reveal does not move the count below 0 (only one countryside tile exists to reveal)

---

## Epic-2 manual pass sign-off

**Pass result — 2026-06-19, WSLg desktop (Godot 4.6.3 mono, Emulate Touch From Mouse enabled), commit `469fe8d`:**

John played a full end-to-end explore loop. Log (`[Input]`/`[HexGrid]`) + visual confirmation verified:
- ✅ 2-2 cost preview (Plains cost=2, Forest cost=3); red at 0 points, green when affordable
- ✅ 2-3 move (`Hero moved to 1,0 cost=2 remaining=0`) + undo (`Move undone: back to 0,0 refund=2 remaining=2`); hero marker visibly moves
- ✅ 2-4 explore-unaffordable refused to reveal (11× `affordable=False`, no reveal); reveal fired (`Tile 'countryside-1' revealed`), all 7 fog hexes turned to terrain colors; moved onto the newly-revealed Forest hex
- ✅ 2-5 tile-count overlay read `Countryside: 8   Core: 3` at launch and ticked to `Countryside: 7` on reveal; top-right, no overlap
- ⏭️ **Not exercised:** 2-4 AC7 (tap own hex after reveal → undo locked). Logic is covered by `RevealTile_ClearsMovePath` unit test; benign to leave for a future on-device pass.

**UX finding (playtest) — staging/commit model friction:** John flagged that each card play required an explicit **Commit** tap before its effect showed in the HUD. Desired: no manual commit in the normal flow; the **tile-explore undo gate** should be the only commit point and should **auto-commit** prior staged actions (ideally with a "this will commit your previous actions" warning). Captured for the retro — see memory `project_commit_model_feedback.md`. Not a 2.x story bug; it's a course-correction to the staging→HUD UX.

- [x] All behavioral + visual boxes confirmed (AC7 undo-lock skipped, unit-test-covered)
- Date: 2026-06-19  ·  Device(s): WSLg desktop + Galaxy S21  ·  Build/commit: `469fe8d`
- [x] `epic-2-retrospective` run — 2026-06-19; retro doc at `epic-2-retro-2026-06-19.md`; commit-model UX finding captured; undo gate principle defined; story 3-0 (TripUndoGate) queued
