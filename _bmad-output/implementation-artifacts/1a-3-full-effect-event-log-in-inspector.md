# Story 1a.3: Full Effect Event Log in Inspector

Status: done

## Story

As a dev,
I want to see the full effect event log in an on-screen inspector panel,
so that every fired effect is visible and undoable without opening the Godot editor.

## Acceptance Criteria

1. `scripts/ui/screens/PlaceholderMainMenu.cs` updated — replaces the ephemeral `new GameState()` calls with a shared field `private readonly GameState _state = new();`; `_saveManager.Save(_state)` uses `_state`; `GameDebug.FireTestEffect(_state, "march", GamePhase.Movement)` uses `_state`. This establishes the single `GameState` instance pattern before UI is layered on top.

2. `scenes/debug/EffectEventLogPanel.tscn` created — root node is a `Panel` named `EffectEventLogPanel` with its `script` property pointing to `res://scripts/ui/debug/EffectEventLogPanel.cs`. Panel is initially hidden (`visible = false`). Internal layout:
   - `VBoxContainer` (anchored to fill panel, named `Layout`)
     - `Label` named `TitleLabel` (text: `"Effect Event Log"`)
     - `ScrollContainer` named `Scroll` (size-flags: expand-fill)
       - `VBoxContainer` named `EntriesContainer` (size-flags: expand-fill horizontal)
     - `HBoxContainer` named `ActionBar`
       - `Button` named `UndoLastButton` (text: `"Undo Last"`)
       - `Button` named `CloseButton` (text: `"Close"`)

3. `scripts/ui/debug/EffectEventLogPanel.cs` created — `namespace MagusWarrior.UI; public partial class EffectEventLogPanel : Panel`. Public interface:
   - `public void Initialize(GameState state)` — stores the state reference in `private GameState? _state`.
   - `public void RefreshDisplay()` — clears all children of `EntriesContainer` via `QueueFree()`, then repopulates with one `Label` per entry in `_state.EventLog.Events`, formatted: `$"[{i}]: {e.SourceCardId} {e.EffectType} phase={e.Phase} powered={e.Powered}"`. If `_state` is null or no events, displays a single `Label` with `"(no events)"`.
   - `UndoLastButton.Pressed` signal connected in `_Ready()` — handler calls `GameDebug.UndoLastEvent(_state!)` then `RefreshDisplay()`.
   - `CloseButton.Pressed` signal connected in `_Ready()` — handler sets `Visible = false`.
   - Uses `Log.Debug("[UI]", ...)` for all debug output. No `GD.Print` calls.

4. `scenes/screens/PlaceholderMainMenu.tscn` updated — adds `EffectEventLogPanel.tscn` as an instanced child scene (instance key `EffectEventLogPanel`); adds a `Button` named `DebugToggleArea` at position `(0, 0)`, size `(80, 80)`, with `flat = true` so it has no visual chrome. This is the 5-tap trigger target in the top-left corner.

5. `scripts/ui/screens/PlaceholderMainMenu.cs` updated — adds the 5-tap toggle wiring:
   - `private EffectEventLogPanel _effectInspector = null!;` and `private Button _debugToggleArea = null!;` fetched in `_Ready()` via `GetNode<>`.
   - `private int _debugTapCount = 0;`
   - `_effectInspector.Initialize(_state)` called before `FireTestEffect`.
   - `_debugToggleArea.Pressed` signal connected in `_Ready()` to a handler that increments `_debugTapCount`; at 5, it toggles `_effectInspector.Visible`, calls `_effectInspector.RefreshDisplay()` if becoming visible, and resets `_debugTapCount = 0`.

6. `dotnet test tests/maguswarrior.Tests.csproj` passes — all 19 existing tests green. No new compile includes are needed (`EffectEventLogPanel.cs` inherits Godot `Panel` and cannot be compiled in the test project).

## Tasks / Subtasks

- [x] Task 1: Update PlaceholderMainMenu.cs — shared GameState field (AC: 1)
  - [x] Add `private readonly GameState _state = new();` field
  - [x] Change `GameDebug.FireTestEffect(new GameState(), ...)` → `GameDebug.FireTestEffect(_state, ...)`
  - [x] Change `_saveManager.Save(new GameState())` → `_saveManager.Save(_state)`

- [x] Task 2: Create EffectEventLogPanel.tscn (AC: 2)
  - [x] Create directory `scenes/debug/`
  - [x] Create `scenes/debug/EffectEventLogPanel.tscn` with the node layout from AC 2
  - [x] Set root Panel `visible = false`
  - [x] Wire script path to `res://scripts/ui/debug/EffectEventLogPanel.cs`

- [x] Task 3: Create EffectEventLogPanel.cs (AC: 3)
  - [x] Create directory `scripts/ui/debug/`
  - [x] Create `scripts/ui/debug/EffectEventLogPanel.cs` — `partial class EffectEventLogPanel : Panel`
  - [x] Implement `Initialize(GameState state)`, `RefreshDisplay()`
  - [x] Connect `UndoLastButton.Pressed` → call `GameDebug.UndoLastEvent(_state!)` then `RefreshDisplay()`
  - [x] Connect `CloseButton.Pressed` → `Visible = false`
  - [x] Use `Log.Debug("[UI]", ...)` for any debug output

- [x] Task 4: Update PlaceholderMainMenu.tscn + PlaceholderMainMenu.cs (AC: 4, 5)
  - [x] Add instanced `EffectEventLogPanel.tscn` child to `PlaceholderMainMenu.tscn`
  - [x] Add `Button DebugToggleArea` (0,0 position, 80×80, flat) to `PlaceholderMainMenu.tscn`
  - [x] In `PlaceholderMainMenu.cs`: fetch both nodes via `GetNode<>` in `_Ready()`
  - [x] Call `_effectInspector.Initialize(_state)` before `FireTestEffect`
  - [x] Connect `_debugToggleArea.Pressed` to 5-tap counter handler

- [x] Task 5: Verify no regressions (AC: 6)
  - [x] Run `dotnet test tests/maguswarrior.Tests.csproj` — confirm 19/19 green

### Review Findings

_From `/gds-code-review` on 2026-05-30 (Blind Hunter + Edge Case Hunter + Acceptance Auditor)._

#### Decision Needed

- [x] [Review][Decision] **RESOLVED** — `[Conditional("DEBUG")]` strips Undo + FireTestEffect in release builds, but the panel did not. **Decision (John, 2026-05-30): Option 1 — gate the debug inspector behind `#if DEBUG`.** No debug surface should be active in a release build; debug builds are produced per-story so the developer can test on device when desired (this was never a "release must always contain debug tools" requirement). A dormant, unreachable debug panel in the release scene is also acceptable. **Crucially: undo is a player feature, not a debug feature** — players undo until an "undo gate" (new information revealed). The undo *mechanism* (`GameEventLog`, `RestoreSnapshot`, scheduler log append) is already non-conditional and ships in release; player-facing undo UI is story 1b-4. Only the `GameDebug.UndoLastEvent` wrapper (used by this dev inspector) stays `[Conditional("DEBUG")]`. **Applied:** inspector wiring in `PlaceholderMainMenu.cs` now `#if DEBUG`; architecture Debug Tools section updated. [scripts/ui/screens/PlaceholderMainMenu.cs:11-46, _bmad-output/game-architecture.md §Debug Tools]

#### Deferred (real but out of scope)

- [x] [Review][Defer] `RefreshDisplay` uses `QueueFree()` + immediate `AddChild()` — old labels are queued for end-of-frame deletion while new ones are added synchronously, so the container transiently holds both within the frame [scripts/ui/debug/EffectEventLogPanel.cs:20-32] — deferred. Spec (AC3) explicitly prescribed `QueueFree()`, and Godot frees the old nodes before the next redraw, so no visible duplication occurs for this debug tool. Robust pattern (`RemoveChild` then `QueueFree`, or `Free()`) should be adopted if this panel ever becomes player-facing or refreshes rapidly. Rapid Undo-mashing (`OnUndoLastPressed` → `RefreshDisplay`) compounds the same transient overlap.
- [x] [Review][Defer] `FireTestEffect` is `async void` and not awaited; if any effect ever truly `await`s, the panel can show a stale/empty log with no re-refresh path [scripts/ui/screens/PlaceholderMainMenu.cs:27, scripts/cards/effects/EffectScheduler.cs:20] — deferred, by-design. `async void` on `FireTestEffect` is the documented exception (`[Conditional("DEBUG")]` cannot return Task); `MoveEffect.Execute` is synchronous today (`Task.FromResult`), so the event is appended before `_Ready()` returns. Same class of latent issue tracked in 1a-2; revisit when effects perform real awaits.
- [x] [Review][Defer] New Godot behavior (refresh formatting, undo wiring, 5-tap toggle) has zero automated coverage — AC6 is met (19/19 green) but explicitly acknowledges `EffectEventLogPanel.cs` cannot be compiled into the test project, so all verification of the new UI paths rests on manual/device testing [scripts/ui/debug/EffectEventLogPanel.cs] — deferred, inherent to the Godot/test-project boundary. Manual verification on device recommended before considering Epic 1a fully closed.

#### Dismissed (noise / false positive)

- Blind Hunter "saving mutated shared `_state` changes persisted content" — verified false: `SaveData` only serializes `schema_version` and `current_phase` (`SaveData.cs:6-9`); `FireTestEffect` mutates `MovePointsThisTurn`, never `CurrentPhase`, so the saved bytes are identical to the prior `Save(new GameState())`.
- `DebugToggleArea` invisible button stealing input in the top-left 80×80 corner (+ `z_index = 10` beyond spec) — by-design: this IS the 5-tap target; `z_index` is required so it sits above the full-rect placeholder `Label`. AC4 permits it; this is a placeholder/debug screen.
- 5-tap counter has no time window (cumulative, never resets except at 5) — spec-compliant; AC5 specifies a plain 5-tap, not a windowed gesture.
- Closing via `CloseButton` leaves stale content / refresh only fires on open — pull-based design per Dev Notes; the only re-show path (`OnDebugToggleAreaPressed`) calls `RefreshDisplay()`.
- `OnUndoLastPressed` on empty log gives no on-screen feedback — handled at data layer (`PopLast()` returns null, logs "nothing to undo"); acceptable for a debug tool.
- `async void` pre-`try` exception unobserved — entire body is inside the `try`; narrow and speculative.
- Node-path / `_Ready` ordering risks (Blind Hunter B4/B5) — self-resolved by the reviewer: all `GetNode<>` paths match the `.tscn` exactly, and Godot runs the child's `_Ready` before the parent's `Initialize`, and the child's `_Ready` does not touch `_state`.
- CloseButton lambda vs named handler; tap-count reset ordering; extra font sizes on labels/buttons — cosmetic deviations with identical behavior; no constraint violated.

## Dev Notes

### Scope — what this story does and does not do

This story adds the Godot UI shell that surfaces the `GameEventLog` built in story 1a-2. No new pure C# logic is required. The existing `GameDebug.UndoLastEvent` and `GameDebug.InspectEventLog` are already complete. The work here is:
- Fix the ephemeral `GameState` pattern in `PlaceholderMainMenu` so the inspector reads from a real shared state.
- Build the Godot panel scene and script that calls `RefreshDisplay()` on demand.
- Wire the 5-tap toggle.

### `scripts/ui/` is the only place for Godot-inheriting classes

`EffectEventLogPanel.cs` inherits `Panel` (a Godot type). Per project-context.md §The Most Important Rule, `: Panel`, `: Control`, and `partial class` are only permitted inside `scripts/ui/`. `scripts/ui/debug/` is a subdirectory of `scripts/ui/` — this is correct placement.

### Debug inspector is `#if DEBUG`-gated; undo is a release feature (post-review decision)

Per John's code-review decision (2026-05-30): no debug surface ships active in a release build. The inspector wiring in `PlaceholderMainMenu.cs` (`_effectInspector`/`_debugToggleArea` fields, the `GetNode`/`Initialize`/signal-connect block, and `OnDebugToggleAreaPressed`) is wrapped in `#if DEBUG`. The `EffectEventLogPanel` and `DebugToggleArea` nodes still exist in `PlaceholderMainMenu.tscn` in release, but with no wiring they are unreachable — an acceptable dormant state.

Undo is a **player** feature, not debug. The undo mechanism (`GameEventLog`, `GameState.RestoreSnapshot`, the scheduler's log append) is non-conditional and ships in release; player-facing undo (until the "undo gate") is story 1b-4. Only `GameDebug.UndoLastEvent` — the wrapper this dev inspector calls — is `[Conditional("DEBUG")]`. Do not make `GameEventLog`/`RestoreSnapshot` conditional.

### `async void FireTestEffect` cannot be awaited

`GameDebug.FireTestEffect` is `async void` (required because it's `[Conditional("DEBUG")]` — Conditional methods cannot return Task). The inspector won't auto-refresh after the effect fires during `_Ready()`. This is intentional: the panel is a **pull-based** dev tool. Toggle it open to see the current log state. Do not add polling, timers, or deferred callbacks to work around this — it is out of scope for this story.

### `GameDebug.UndoLastEvent` is `[Conditional("DEBUG")]`

Call it directly from the button handler. In release builds, the call is stripped by the compiler. No `#if DEBUG` guard is needed in the panel script.

### Clearing `EntriesContainer` children in Godot 4

```csharp
foreach (Node child in _entriesContainer.GetChildren())
    child.QueueFree();
```

`QueueFree()` defers deletion to the end of the frame, which is fine here. Do not call `Free()` directly.

### Node path conventions (Godot 4 C#)

Use `GetNode<T>("NodeName")` for direct children and `GetNode<T>("Parent/Child")` for deeper paths. Unique names (`%NodeName`) require the node to have "Unique Name in Owner" checked in the editor. Since the tscn is written by hand for this story, use the simple path form.

```csharp
// In PlaceholderMainMenu._Ready():
_effectInspector = GetNode<EffectEventLogPanel>("EffectEventLogPanel");
_debugToggleArea  = GetNode<Button>("DebugToggleArea");
```

### 5-tap toggle pattern

```csharp
private int _debugTapCount = 0;

private void OnDebugToggleAreaPressed() {
    _debugTapCount++;
    if (_debugTapCount < 5) return;
    _debugTapCount = 0;
    _effectInspector.Visible = !_effectInspector.Visible;
    if (_effectInspector.Visible)
        _effectInspector.RefreshDisplay();
}
```

### PlaceholderMainMenu shared-state pattern

Current (broken — inspector can't share the state):
```csharp
GameDebug.FireTestEffect(new GameState(), "march", GamePhase.Movement);
// ...
_saveManager.Save(new GameState());
```

After this story (single shared instance):
```csharp
private readonly GameState _state = new();

public override void _Ready() {
    // ...
    _effectInspector = GetNode<EffectEventLogPanel>("EffectEventLogPanel");
    _debugToggleArea  = GetNode<Button>("DebugToggleArea");
    _debugToggleArea.Pressed += OnDebugToggleAreaPressed;
    UndoLastButton is wired inside EffectEventLogPanel._Ready()
    _effectInspector.Initialize(_state);
    // Load save as before
    GameDebug.FireTestEffect(_state, "march", GamePhase.Movement);
}

public override void _Notification(int what) {
    if (what == NotificationApplicationPaused)
        _saveManager.Save(_state);
}
```

### EffectEventLogPanel — explicit usings required (`ImplicitUsings=disable`)

```csharp
using Godot;
using MagusWarrior.Core;
```

No other usings are needed. `EffectFiredEvent` and `GameEventLog` are in `MagusWarrior.Core` (already available via `GameState`). `Log` is in `MagusWarrior.Core`.

### RefreshDisplay implementation sketch

```csharp
public void RefreshDisplay() {
    var container = GetNode<VBoxContainer>("Layout/Scroll/EntriesContainer");
    foreach (Node child in container.GetChildren())
        child.QueueFree();

    if (_state == null || _state.EventLog.Events.Count == 0) {
        container.AddChild(new Label { Text = "(no events)" });
        return;
    }

    for (int i = 0; i < _state.EventLog.Events.Count; i++) {
        var e = _state.EventLog.Events[i];
        container.AddChild(new Label {
            Text = $"[{i}]: {e.SourceCardId} {e.EffectType} phase={e.Phase} powered={e.Powered}"
        });
    }
}
```

### Namespace conventions

| Folder | Namespace |
|--------|-----------|
| `scripts/ui/` | `MagusWarrior.UI` |
| `scripts/ui/debug/` | `MagusWarrior.UI` (no sub-namespace for small debug tools) |
| `scripts/core/` | `MagusWarrior.Core` |

### Project Context Rules

**Godot dependency boundary:**
- `scripts/ui/debug/EffectEventLogPanel.cs` — inherits `Panel` (Godot); `Log.Debug` allowed; `partial class` required
- Never compile `EffectEventLogPanel.cs` into the test project — it has a Godot dependency
- All pure C# game logic (GameState, GameEventLog, GameDebug) was completed in 1a-2; this story only builds the Godot wrapper

**File placement:**
- Panel scene → `scenes/debug/EffectEventLogPanel.tscn`
- Panel script → `scripts/ui/debug/EffectEventLogPanel.cs`
- No new files in `scripts/core/`, `scripts/cards/`, or anywhere outside `scripts/ui/` and `scenes/debug/`

**Logging:**
- Use `Log.Debug("[UI]", ...)` in `EffectEventLogPanel.cs` — `[UI]` is the required system tag
- No `GD.Print` anywhere

**Dependency injection:**
- `EffectEventLogPanel` receives `GameState` via `Initialize(GameState state)` — not via static access
- `GameState` instance owned by `PlaceholderMainMenu`; passed down via `Initialize`

**async void:**
- `FireTestEffect` remains `async void` — do not change it; do not await it; do not add deferred refresh
- The panel is pull-based (toggle to refresh); this is the correct pattern for a dev tool at this stage

### References

- Architecture Debug Tools section: `_bmad-output/game-architecture.md` §Debug Tools (5-tap toggle, state inspector overlay)
- Architecture Screen Contracts: `_bmad-output/game-architecture.md` §Screen Contracts (no contract for debug panel — it is a dev tool, not a player screen)
- Epic 1a story and UI Verification: `_bmad-output/epics.md` §Epic 1a (deliverable: "Effect inspector showing every effect fired, source, type, value. Event log with full history. Undo pops cleanly from inspector.")
- GameEventLog and GameDebug: `scripts/core/GameEventLog.cs`, `scripts/core/GameDebug.cs` (pure C# infrastructure, complete)
- Previous story patterns: `_bmad-output/implementation-artifacts/1a-2-undo-last-event.md` §Dev Notes (Godot boundary, no Log in EffectScheduler, namespace table)
- Node/scene boundary rule: `docs/project-context.md` §The Most Important Rule

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

- All 5 tasks completed. 19/19 xUnit tests pass (unchanged — no new pure C# logic).
- `PlaceholderMainMenu.cs` now holds `private readonly GameState _state = new()` shared across `FireTestEffect`, `_saveManager.Save`, and `_effectInspector.Initialize`. Eliminates the ephemeral state pattern.
- `scenes/debug/EffectEventLogPanel.tscn` created: full-screen Panel (starts hidden), VBoxContainer layout, ScrollContainer with EntriesContainer VBoxContainer, HBoxContainer with "Undo Last" and "Close" buttons.
- `scripts/ui/debug/EffectEventLogPanel.cs` created: `partial class EffectEventLogPanel : Panel`, namespace `MagusWarrior.UI`. `Initialize(GameState)` stores state reference. `RefreshDisplay()` clears and rebuilds EntriesContainer labels. Undo button calls `GameDebug.UndoLastEvent` + refresh. Close button hides panel. All debug output via `Log.Debug("[UI]", ...)`.
- `PlaceholderMainMenu.tscn` updated: `EffectEventLogPanel.tscn` instanced as child; `DebugToggleArea` Button (80×80, flat, top-left) added. `load_steps` bumped from 2 to 3.
- `PlaceholderMainMenu.cs` updated: 5-tap toggle wired via `OnDebugToggleAreaPressed()`; toggles panel visibility and calls `RefreshDisplay()` on show.
- `EffectEventLogPanel.cs` is NOT compiled into the test project (Godot dependency).
- **Code-review follow-up (2026-05-30):** Per John's decision, the debug inspector wiring in `PlaceholderMainMenu.cs` is now `#if DEBUG`-gated so no debug surface is active in release builds. Undo confirmed as a release player feature (mechanism is non-conditional; player UI is story 1b-4); only the `GameDebug.UndoLastEvent` wrapper stays `[Conditional("DEBUG")]`. Architecture Debug Tools section updated accordingly. 19/19 tests still green.

### File List

- `scripts/ui/screens/PlaceholderMainMenu.cs` (modified — shared _state field, inspector wiring, 5-tap toggle)
- `scripts/ui/debug/EffectEventLogPanel.cs` (new)
- `scenes/debug/EffectEventLogPanel.tscn` (new)
- `scenes/screens/PlaceholderMainMenu.tscn` (modified — instanced inspector panel + DebugToggleArea)
- `_bmad-output/game-architecture.md` (modified — Debug Tools section: DEBUG-only gating + undo-is-release clarification, from code-review decision)
