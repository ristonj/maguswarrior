# Story 1a.2: Undo Last Event

Status: done

## Story

As a dev,
I want to undo the last effect event from the inspector,
so that the event log architecture is proven before hand UI is built on top of it.

## Acceptance Criteria

1. `scripts/core/GameEventLog.cs` exists — contains `record GameStateSnapshot(GamePhase CurrentPhase, int MovePointsThisTurn)`, `record EffectFiredEvent(string SourceCardId, EffectType EffectType, GamePhase Phase, bool Powered, GameStateSnapshot StateBefore)`, and `class GameEventLog` with `IReadOnlyList<EffectFiredEvent> Events { get; }`, `void Append(EffectFiredEvent e)`, and `EffectFiredEvent? PopLast()` (removes and returns the last entry, or returns null if empty). Pure C#, no Godot dependency. Namespace: `MagusWarrior.Core`. Explicit usings: `System`, `System.Collections.Generic`, `MagusWarrior.Core.Types`.

2. `GameState` updated: adds `GameEventLog EventLog { get; } = new()`, `GameStateSnapshot TakeSnapshot()` (returns `new GameStateSnapshot(CurrentPhase, MovePointsThisTurn)`), and `void RestoreSnapshot(GameStateSnapshot snapshot)` (restores `CurrentPhase` and `MovePointsThisTurn` from the snapshot). These methods have no Godot dependency and no `Log.Debug` calls.

3. `EffectScheduler.ResolveAll` updated: before each effect call, invoke `state.TakeSnapshot()` and store the result; after the effect completes, append a new `EffectFiredEvent` to `state.EventLog` using the stored snapshot, `pending.Ctx.SourceCardId`, `pending.Ctx.ChosenType`, `pending.Ctx.Phase`, and `pending.Ctx.Powered`. Triggered re-enqueued effects each get their own snapshot when they dequeue.

4. `scripts/core/GameDebug.cs` gains two `[Conditional("DEBUG")]` methods:
   - `public static void UndoLastEvent(GameState state)` — calls `state.EventLog.PopLast()`; if null, logs `"UndoLastEvent: nothing to undo"` via `Log.Debug("[Effect]", ...)` and returns; otherwise calls `state.RestoreSnapshot(ev.StateBefore)` and logs `$"UndoLastEvent: undid {ev.SourceCardId}/{ev.EffectType} in {ev.Phase}"`.
   - `public static void InspectEventLog(GameState state)` — if log is empty, logs `"EventLog: (empty)"`; otherwise logs each entry as `$"EventLog[{i}]: {e.SourceCardId} {e.EffectType} phase={e.Phase} powered={e.Powered}"`.
   Both methods use `Log.Debug("[Effect]", ...)`.

5. `tests/unit/EffectSystemTest.cs` gains 4 new xUnit tests (all pure C# — no Godot):
   - `EventLog_HasOneEntry_AfterEffectFires` — create `GameState(new List<CardDefinition>())`, build a scheduler, enqueue `MoveEffect(2)` with a context, call `ResolveAll`, assert `state.EventLog.Events.Count == 1`.
   - `EventLog_Entry_CapturesContext_And_Snapshot` — same setup; assert `Events[0].SourceCardId == "march"`, `Events[0].EffectType == EffectType.Move`, `Events[0].StateBefore.MovePointsThisTurn == 0` (before effect ran).
   - `EventLog_Undo_RestoresState_AndRemovesEntry` — fire effect, verify `MovePointsThisTurn == 2`; then call `var ev = state.EventLog.PopLast()` + `state.RestoreSnapshot(ev!.StateBefore)`; assert `MovePointsThisTurn == 0` and `state.EventLog.Events.Count == 0`.
   - `EventLog_UndoOnEmpty_IsNoOp` — create empty `GameEventLog`, call `PopLast()`, assert result is null and no exception thrown.

6. `tests/maguswarrior.Tests.csproj` updated: adds `<Compile Include="../scripts/core/GameEventLog.cs" />` to the existing `<ItemGroup>` of pure C# compile links.

7. `dotnet test tests/maguswarrior.Tests.csproj` passes — all 19 tests green (15 existing + 4 new). No Godot runtime needed.

## Tasks / Subtasks

- [x] Task 1: Create GameEventLog (AC: 1)
  - [x] Create `scripts/core/GameEventLog.cs` — namespace `MagusWarrior.Core`
  - [x] Define `record GameStateSnapshot(GamePhase CurrentPhase, int MovePointsThisTurn)` in same file
  - [x] Define `record EffectFiredEvent(string SourceCardId, EffectType EffectType, GamePhase Phase, bool Powered, GameStateSnapshot StateBefore)` in same file
  - [x] Define `class GameEventLog` with `List<EffectFiredEvent> _events = new()`, property `IReadOnlyList<EffectFiredEvent> Events => _events`, method `void Append(EffectFiredEvent e) => _events.Add(e)`, method `EffectFiredEvent? PopLast()` that removes and returns `_events[^1]` or returns null if empty

- [x] Task 2: Update GameState (AC: 2)
  - [x] Add `public GameEventLog EventLog { get; } = new()` to `GameState`
  - [x] Add `public GameStateSnapshot TakeSnapshot() => new(CurrentPhase, MovePointsThisTurn)` to `GameState`
  - [x] Add `public void RestoreSnapshot(GameStateSnapshot snapshot)` — sets `CurrentPhase = snapshot.CurrentPhase` and `MovePointsThisTurn = snapshot.MovePointsThisTurn` (requires `CurrentPhase` to be assignable from within the class — it has `private set`, so this works from inside `GameState`)
  - [x] Add `using MagusWarrior.Core;` is already the file's namespace — no new usings needed for these additions

- [x] Task 3: Update EffectScheduler (AC: 3)
  - [x] In `EffectScheduler.ResolveAll`, before `var result = await pending.Effect.Execute(state, pending.Ctx)`, add `var snapshot = state.TakeSnapshot()`
  - [x] After the `Execute` call (before re-enqueue loop), add `state.EventLog.Append(new EffectFiredEvent(pending.Ctx.SourceCardId, pending.Ctx.ChosenType, pending.Ctx.Phase, pending.Ctx.Powered, snapshot))`
  - [x] No new usings needed (GameState is already imported; EffectFiredEvent is in MagusWarrior.Core which is already used)
  - [x] Verify: GameEventLog and EffectFiredEvent are in `MagusWarrior.Core`, same as GameState — the existing `using MagusWarrior.Core;` in EffectScheduler covers this

- [x] Task 4: Update GameDebug (AC: 4)
  - [x] Add `[Conditional("DEBUG")] public static void UndoLastEvent(GameState state)` to `GameDebug.cs`
  - [x] Add `[Conditional("DEBUG")] public static void InspectEventLog(GameState state)` to `GameDebug.cs`
  - [x] No new usings needed (`GameState`, `Log` already imported; `MagusWarrior.Core` already in scope)

- [x] Task 5: Write tests and update test project (AC: 5, 6, 7)
  - [x] Add 4 tests to `tests/unit/EffectSystemTest.cs` — see AC 5 for exact test names and assertions
  - [x] Add `<Compile Include="../scripts/core/GameEventLog.cs" />` to `tests/maguswarrior.Tests.csproj`
  - [x] Run `dotnet test tests/maguswarrior.Tests.csproj` and confirm 19/19 green

### Review Findings

_From `/gds-code-review` on 2026-05-29 (Blind Hunter + Edge Case Hunter + Acceptance Auditor)._

#### Patches

- [x] [Review][Patch] `GameEventLog.cs` omits `using System;` that AC1 and Dev Notes explicitly prescribe — added for spec conformance [scripts/core/GameEventLog.cs:1]
- [x] [Review][Patch] Test `EventLog_Entry_CapturesContext_And_Snapshot` had a weak assertion — added `Assert.Equal(2, state.MovePointsThisTurn)` to contrast post-state (2) against snapshot pre-state (0), proving before-timing [tests/unit/EffectSystemTest.cs]

#### Deferred (real but out of scope)

- [x] [Review][Defer] Snapshot captures scalars only (`CurrentPhase`, `MovePointsThisTurn`) — undo silently fails to restore any other mutable state. This is the documented incremental contract (Dev Notes "GameStateSnapshot captures mutable scalars only"); the landmine is that the snapshot MUST be grown in lockstep as `GameState` gains mutable fields (reputation, fame, hand). [scripts/core/GameState.cs, scripts/core/GameEventLog.cs] — deferred, by-design
- [x] [Review][Defer] Undo grouping semantics for triggered-effect chains undecided — sequential LIFO undo composes correctly today (nested snapshots: event_B.before = event_A.after), but one `UndoLastEvent` reverses one effect, not a whole causal chain as a unit. Needs a transaction/grouping decision before any effect returns triggered effects. Not exercised today (MoveEffect returns empty `Triggered`). [scripts/cards/effects/EffectScheduler.cs] — deferred, no triggered effects yet
- [x] [Review][Defer] Event appended regardless of `EffectResult.Success`; no after-state or success flag recorded — a no-op/failed effect still consumes a log slot whose `StateBefore == StateAfter`. Revisit when effects can fail (today `MoveEffect` always returns `Ok()`). [scripts/cards/effects/EffectScheduler.cs] — deferred, no failing effects yet
- [x] [Review][Defer] `EffectScheduler` has no exception handling — a thrown `Execute` kills the queue with no rollback. Already tracked from 1a-1 review. [scripts/cards/effects/EffectScheduler.cs] — deferred, pre-existing
- [x] [Review][Defer] Triggered effects re-enqueued with their `Ctx` are not phase/type-validated — a stale or illegal triggered context is logged as-is. Ties to the same future triggered-effect work. [scripts/cards/effects/EffectScheduler.cs] — deferred, no triggered effects yet

#### Dismissed (noise / false positive)

- `Events` exposes the live backing list via `IReadOnlyList<>` — matches the established `GameState.Cards` pattern; single-threaded; `IReadOnlyList` is the documented contract.
- `EffectFiredEvent`/`GameStateSnapshot` record equality could surprise in a `HashSet`/`Dictionary` — speculative; events are only appended/popped, never compared.
- `SourceCardId` null/empty not validated — `EffectContext.SourceCardId` is a non-nullable record field by convention; a debug-only event log has no consequence from an empty id.
- `Powered` could mismatch if context mutates between enqueue and execute — `EffectContext` is an immutable record; mutation is impossible. False positive.
- No re-entrancy guard in `ResolveAll` — no re-entrant call path exists; speculative.

## Dev Notes

### This story proves the event log architecture — not player-facing undo

The architecture distinguishes two types of "undo":
- **This story:** Event log infrastructure — every effect that fires appends to a log; `PopLast()` + `RestoreSnapshot()` reverses the last entry. Developer/inspector tool only. Wrapped in `[Conditional("DEBUG")]` in GameDebug.
- **Story 1b-4 (future):** Player-facing undo — staging area, "free until new information revealed" per the GDD. That will build on the event log established here.

Do NOT implement a staging area, "commit vs pre-commit" distinction, or player-visible undo button in this story. The deliverable is: event appended when effect fires, `UndoLastEvent` debug method reverts it.

### `GameStateSnapshot` captures mutable scalars only

`GameState` currently has two mutable fields: `CurrentPhase` and `MovePointsThisTurn`. The `Cards` list is set at construction and never changes, so it is not included in the snapshot. As `GameState` gains new mutable fields in future stories (e.g., reputation, fame, hand), add them to `GameStateSnapshot` at that time. This is not scope-creep prevention — it's an explicit incremental contract.

### `CurrentPhase` has `private set` — that is fine for `RestoreSnapshot`

`RestoreSnapshot` lives inside `GameState` itself. Private setters are accessible within the declaring class, so `CurrentPhase = snapshot.CurrentPhase` compiles without changes to the access modifier. Do not change `private set` to `public set` or `internal set` just for this story.

### `EffectScheduler` must not call `Log.Debug`

`Log.cs` has a Godot dependency (`GD.Print`). `EffectScheduler.cs` is compiled into the test project (no Godot). Logging the event append must NOT happen inside `EffectScheduler.ResolveAll`. The only logging is in `GameDebug.UndoLastEvent` and `GameDebug.InspectEventLog` (which are NOT compiled into the test project).

### `GameEventLog.cs` is pure C# — compile it into the test project

Add `<Compile Include="../scripts/core/GameEventLog.cs" />` to `tests/maguswarrior.Tests.csproj`. The test project's existing Compile links cover `GameState.cs`, `EffectScheduler.cs`, etc. — add `GameEventLog.cs` alongside them.

### `EffectFiredEvent` is in `MagusWarrior.Core` — no new usings needed in EffectScheduler

`EffectScheduler.cs` already has `using MagusWarrior.Core;`. `GameEventLog`, `GameStateSnapshot`, and `EffectFiredEvent` are all in `MagusWarrior.Core`. The compiler resolves them without any additional using directive.

### Test setup pattern — established by 1a-1

All tests use `new GameState(new List<CardDefinition>())` to avoid YAML file I/O. Copy the existing test setup from `EffectSystemTest.cs`. The test context for the event log tests:

```csharp
var state = new GameState(new List<CardDefinition>());
var ctx = new EffectContext("march", EffectType.Move, GamePhase.Movement, Powered: false);
var scheduler = new EffectScheduler();
scheduler.Enqueue(new MoveEffect(2), priority: 0, ctx);
await scheduler.ResolveAll(state);
```

### `PopLast()` design — remove-and-return semantics

`PopLast()` both removes the entry from `_events` and returns it. This is intentional: calling undo twice undoes two separate effects. The caller is responsible for deciding whether to restore the snapshot. Tests must verify the entry is gone from `Events` after pop.

```csharp
public EffectFiredEvent? PopLast() {
    if (_events.Count == 0) return null;
    var last = _events[^1];
    _events.RemoveAt(_events.Count - 1);
    return last;
}
```

### Namespace conventions (established by 1a-1)

| Folder | Namespace |
|--------|-----------|
| `scripts/core/` | `MagusWarrior.Core` |
| `scripts/core/types/` | `MagusWarrior.Core.Types` |
| `scripts/cards/effects/` | `MagusWarrior.Cards.Effects` |

`ImplicitUsings=disable` — every `using` must be explicit in every file. `GameEventLog.cs` needs:
```csharp
using System;
using System.Collections.Generic;
using MagusWarrior.Core.Types;
```

(`System` is needed for `Array.Empty` or nullable inference; `System.Collections.Generic` for `List<T>` and `IReadOnlyList<T>`; `MagusWarrior.Core.Types` for `GamePhase` and `EffectType`.)

### Project Context Rules

**Godot dependency boundary (most common failure mode):**
- `scripts/core/GameEventLog.cs` — no Godot, no `Log.Debug`, no `GD.Print`
- `scripts/core/GameState.cs` additions — no Godot dependency; `TakeSnapshot` and `RestoreSnapshot` are pure math on fields
- `scripts/cards/effects/EffectScheduler.cs` — already pure C#; new event-append line must not call `Log.Debug`
- `scripts/core/GameDebug.cs` — already has Godot dependency; this is the ONLY file in this story where `Log.Debug` is called; NOT compiled into test project

**File placement:**
- `GameEventLog.cs` → `scripts/core/` (shared state infrastructure, not a card/effect)
- Never put it in `scripts/cards/effects/` — the event log is not an effect, it is state infrastructure

**Return `Result<T>` for expected failures; throw for startup failures:**
- `PopLast()` returns `null` for empty log — not a failure, not a Result, just an optional return
- No throwing anywhere in `GameEventLog`, `TakeSnapshot`, or `RestoreSnapshot`

**Dependency injection — no statics:**
- `GameEventLog` is owned by `GameState` as a field; `EffectScheduler` accesses it via `state.EventLog`
- No `GameEventLog.Instance` or static accessor

**Logging convention:**
- System tag for all effect-related logging: `[Effect]`
- `UndoLastEvent` logs before and after the undo; `InspectEventLog` logs one line per entry

**`[Conditional("DEBUG")]` on all GameDebug methods:**
- `UndoLastEvent` and `InspectEventLog` both get `[Conditional("DEBUG")]`
- This attribute strips the method body (and call site) in release builds at zero cost

### References

- Undo System: `_bmad-output/game-architecture.md` §Undo System (pre-commit only; snapshot-based)
- Epic scope: `_bmad-output/epics.md` §Epic 1a: Effect System Architecture (event log as foundation for Epics 2+)
- File placement: `docs/project-context.md` §File Placement
- Async / no-Godot rule: `docs/project-context.md` §The Most Important Rule
- Logging convention: `docs/project-context.md` §Logging
- Previous story patterns: `_bmad-output/implementation-artifacts/1a-1-define-and-fire-effect.md` §Dev Notes
- `GameState` two-constructor pattern: `_bmad-output/implementation-artifacts/1a-1-define-and-fire-effect.md` §GameState two-constructor pattern
- Namespace conventions: `_bmad-output/implementation-artifacts/1a-1-define-and-fire-effect.md` §Namespace conventions
- Test csproj Compile links: `tests/maguswarrior.Tests.csproj` (add GameEventLog.cs alongside existing entries)

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

- All 5 tasks completed. 19/19 xUnit tests pass (15 existing + 4 new).
- `GameEventLog.cs` placed in `scripts/core/` — pure C#, no Godot dependency, compiled into test project.
- `GameStateSnapshot` snapshots only mutable scalar fields (`CurrentPhase`, `MovePointsThisTurn`). `Cards` list excluded (immutable after construction).
- `EffectScheduler.ResolveAll` takes a snapshot before each `Execute` call and appends an `EffectFiredEvent` after. No logging inside the scheduler (respects Godot boundary).
- `GameDebug.UndoLastEvent` and `InspectEventLog` both carry `[Conditional("DEBUG")]`; all logging via `Log.Debug("[Effect]", ...)`.
- xUnit2013 analyzer warnings fixed: `Assert.Equal(1, count)` → `Assert.Single`, `Assert.Equal(0, count)` → `Assert.Empty`.
- `RestoreSnapshot` uses `CurrentPhase`'s `private set` from within `GameState` — no access modifier change required.

### File List

- `scripts/core/GameEventLog.cs` (new)
- `scripts/core/GameState.cs` (modified — added EventLog, TakeSnapshot, RestoreSnapshot)
- `scripts/cards/effects/EffectScheduler.cs` (modified — snapshot + event append in ResolveAll)
- `scripts/core/GameDebug.cs` (modified — added UndoLastEvent, InspectEventLog)
- `tests/unit/EffectSystemTest.cs` (modified — added 4 new tests)
- `tests/maguswarrior.Tests.csproj` (modified — added GameEventLog.cs Compile link)
