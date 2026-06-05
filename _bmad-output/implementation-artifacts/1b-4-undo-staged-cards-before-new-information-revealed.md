# Story 1b.4: Undo Staged Cards Before New Information Is Revealed

Status: done

## Story

As a player,
I want to undo staged cards before committing,
so that I can change my mind and try a different combination without penalty.

## Acceptance Criteria

1. `scripts/deck/StagingManager.cs` updated — add `public StagedEntry? Unstage()`:
   - Removes and returns the **last** `StagedEntry` (LIFO — most recently staged)
   - Returns `null` if the staging list is empty (no-op, no event fired)
   - Fires `StagingChanged` when a card is successfully removed
   - No Godot dependency — pure C#

2. `scripts/deck/DeckManager.cs` updated — add `public void ReturnCard(CardDefinition card)`:
   - Appends `card` to the end of the hand
   - Fires `HandChanged`
   - No Godot dependency — pure C#

3. `scripts/ui/components/StagingAreaView.cs` updated:
   - Add `[Signal] public delegate void UndoRequestedEventHandler()`
   - Add `private Button _undoButton = null!;` field
   - In `_Ready()`: after `_commitButton`, add `_undoButton = new Button(); _undoButton.Text = "Undo"; _undoButton.AddThemeFontSizeOverride("font_size", 32); _undoButton.Disabled = true; _undoButton.Pressed += () => { EmitSignal(SignalName.UndoRequested); Log.Debug("[UI]", "Undo tapped"); }; container.AddChild(_undoButton);`
   - In `Refresh()`: add `_undoButton.Disabled = _stagingManager.StagedCards.Count == 0;`
   - No new usings required (`Log` and `StagingManager` already imported)

4. `scripts/ui/components/HandView.cs` updated:
   - In `Initialize`: add `_stagingAreaView.UndoRequested += OnUndoRequested;` (alongside the existing `CommitRequested` wire)
   - Add method `private void OnUndoRequested()`:
     - `if (_stagingManager.StagedCards.Count == 0) return;`
     - `var entry = _stagingManager.Unstage();`
     - `if (entry is null) return;`
     - `_deck.ReturnCard(entry.Card);`
     - `Log.Debug("[UI]", $"Undo staged: {entry.Card.Id} returned to hand");`
   - No new usings required

5. `tests/unit/StagingManagerTest.cs` updated — add 4 xUnit tests:
   - `Unstage_RemovesLastStagedCard` — stage march then stamina → Unstage → `StagedCards.Count == 1` and `StagedCards[0].Card.Id == "march"`
   - `Unstage_ReturnsMostRecentEntry` — stage march → Unstage → returned entry is not null and `entry.Card.Id == "march"`
   - `Unstage_FiresStagingChanged` — subscribe counter, Stage march, reset counter, Unstage → counter == 1
   - `Unstage_OnEmptyList_ReturnsNullAndNoEvent` — subscribe counter on empty manager, Unstage → returns null, counter == 0

6. `tests/unit/DeckManagerTest.cs` updated — add 2 xUnit tests:
   - `ReturnCard_AppendsToHand` — SetHand([march]) → ReturnCard(stamina) → `Hand.Count == 2` and `Hand[1].Id == "stamina"`
   - `ReturnCard_FiresHandChanged` — subscribe counter, ReturnCard → counter == 1

7. `dotnet test tests/maguswarrior.Tests.csproj` passes — all 42 existing tests + 6 new tests = **48 green**. Zero warnings.

## Tasks / Subtasks

- [x] Task 1: Add `StagingManager.Unstage()` (AC: 1, 5)
  - [x] Add `public StagedEntry? Unstage()` to `scripts/deck/StagingManager.cs` — LIFO remove + `StagingChanged` fire; return null without firing if list is empty
  - [x] Add 4 tests in `tests/unit/StagingManagerTest.cs` (Unstage_RemovesLastStagedCard, Unstage_ReturnsMostRecentEntry, Unstage_FiresStagingChanged, Unstage_OnEmptyList_ReturnsNullAndNoEvent)
  - [x] Run `dotnet test` — confirm 4 new tests green (46 total)

- [x] Task 2: Add `DeckManager.ReturnCard(CardDefinition)` (AC: 2, 6)
  - [x] Add `public void ReturnCard(CardDefinition card)` to `scripts/deck/DeckManager.cs` — append card, fire `HandChanged`
  - [x] Add 2 tests in `tests/unit/DeckManagerTest.cs` (ReturnCard_AppendsToHand, ReturnCard_FiresHandChanged)
  - [x] Run `dotnet test` — confirm 2 new tests green (48 total)

- [x] Task 3: Add Undo button to StagingAreaView (AC: 3)
  - [x] Add `UndoRequestedEventHandler` signal
  - [x] Add `_undoButton` field
  - [x] Create and wire Undo button in `_Ready()` with font_size=32
  - [x] Update `Refresh()` to set `_undoButton.Disabled` correctly

- [x] Task 4: Wire Undo in HandView (AC: 4)
  - [x] Connect `_stagingAreaView.UndoRequested += OnUndoRequested` in `Initialize`
  - [x] Add `OnUndoRequested` method — guard empty, call `Unstage()`, call `ReturnCard`, log

- [x] Task 5: Verify all tests and no regressions (AC: 7)
  - [x] Run `dotnet test tests/maguswarrior.Tests.csproj` — 48/48 green, 0 warnings, Godot build clean

## Dev Notes

### What This Story Does and Does NOT Do

**Does:**
- `StagingManager.Unstage()` — LIFO un-stage: removes the most recently staged card and returns it to the caller; fires `StagingChanged`
- `DeckManager.ReturnCard()` — appends a returned card to the end of the hand; fires `HandChanged`
- "Undo" button in `StagingAreaView` — enabled when staged cards exist, disabled when empty
- Tapping Undo → HandView removes the last staged card and returns it to the hand (card reappears in hand view)
- Running totals update in real time via the existing `StagingChanged → Refresh()` path
- Commit button enabled/disabled state continues to work correctly (existing `Refresh()` logic)

**Does NOT:**
- Undo after commit — once Commit fires and `ResolveAll` runs, the effect is final (architecture: "pre-commit only")
- Undo sideways plays — sideways play is immediate (no staging), so there is nothing to un-stage
- Individual card selection undo — Undo always removes the most recently staged card (LIFO), not a user-chosen card
- Full event-sourced undo across rounds — architecture explicitly defers this ("a second game inside the first")
- Re-entrancy guard on `OnUndoRequested` — unlike `OnCommitRequested`, `OnUndoRequested` is synchronous; no guard needed

### Architecture: Pre-Commit Undo Only

The GDD and architecture both define this explicitly: "Undo is free until new information is revealed." Once a card effect commits and `ResolveAll` runs (card draw, tile reveal, die roll, enemy draw would gate it in the future), it is final. This story implements the pre-commit portion only.

`StagingManager` lives outside `GameState` — it is pre-commit staging state, not committed game state. Unstaging a card is simply reversing the `Stage()` call: remove from `_staged`, add back to `DeckManager.Hand`. No `GameState` snapshot, `RestoreSnapshot`, or `GameEventLog` is touched by this story.

The architecture note at line 470 says: "the player-facing undo UI lands in story 1b-4 on top of this mechanism" — but the mechanism it refers to (`GameEventLog`, `RestoreSnapshot`) is for committed-effect undo, which is out of scope for 1b-4. This story's "undo" is purely unstaging from `StagingManager` + returning to `DeckManager.Hand`.

### `StagingManager.Unstage()` — Implementation

```csharp
public StagedEntry? Unstage() {
    if (_staged.Count == 0) return null;
    var entry = _staged[_staged.Count - 1];
    _staged.RemoveAt(_staged.Count - 1);
    StagingChanged?.Invoke();
    return entry;
}
```

Key constraint: `StagingChanged` is NOT fired when the list is empty (return null path). Only fire when a card is actually removed. This prevents spurious `Refresh()` calls in `StagingAreaView`.

### `DeckManager.ReturnCard()` — Implementation

```csharp
public void ReturnCard(CardDefinition card) {
    var list = Hand.ToList();
    list.Add(card);
    SetHand(list);
}
```

`SetHand` already fires `HandChanged`, so no separate event call is needed. Card is appended to the end — the visual order of the existing hand is preserved, and the returned card appears on the right.

### `OnUndoRequested()` — Synchronous (No `async void`)

Unlike `OnCommitRequested`, `OnUndoRequested` is synchronous:
- `Unstage()` is synchronous (removes from list)
- `ReturnCard()` is synchronous (adds to list, fires `HandChanged` → `RefreshHand`)

No `async/await` needed. No re-entrancy guard needed. The whole operation completes before Godot processes another input event.

### `_committing` Guard Interaction

The existing `_committing` re-entrancy guard on `OnCommitRequested` does NOT need to block `OnUndoRequested`. A tap on Undo during a commit would:
1. `OnCommitRequested` runs synchronously today (Task.FromResult path)
2. A second signal cannot interleave in the same frame with the current synchronous implementation

This is consistent with the 1b-3 architecture: the guard is there for when `ResolveAll` becomes genuinely awaitable (UIBroker). At that point, `OnUndoRequested` during an in-flight commit would be a UX problem (empty staging list), but the Undo button will already be disabled by `Refresh()` which fires at commit start via the `Clear()` path. No additional guard needed.

### Test Patterns — New StagingManagerTest.cs Tests

```csharp
[Fact]
public void Unstage_RemovesLastStagedCard() {
    var mgr = new StagingManager();
    mgr.Stage(MakeCard("march",   EffectType.Move, move: 2),      EffectType.Move);
    mgr.Stage(MakeCard("stamina", EffectType.Move, move: 2),      EffectType.Move);
    mgr.Unstage();
    Assert.Single(mgr.StagedCards);
    Assert.Equal("march", mgr.StagedCards[0].Card.Id);
}

[Fact]
public void Unstage_ReturnsMostRecentEntry() {
    var mgr = new StagingManager();
    mgr.Stage(MakeCard("march", EffectType.Move, move: 2), EffectType.Move);
    var entry = mgr.Unstage();
    Assert.NotNull(entry);
    Assert.Equal("march", entry!.Card.Id);
}

[Fact]
public void Unstage_FiresStagingChanged() {
    var mgr = new StagingManager();
    mgr.Stage(MakeCard("march", EffectType.Move, move: 2), EffectType.Move);
    int count = 0;
    mgr.StagingChanged += () => count++;
    mgr.Unstage();
    Assert.Equal(1, count);
}

[Fact]
public void Unstage_OnEmptyList_ReturnsNullAndNoEvent() {
    var mgr = new StagingManager();
    int count = 0;
    mgr.StagingChanged += () => count++;
    var entry = mgr.Unstage();
    Assert.Null(entry);
    Assert.Equal(0, count);
}
```

### Test Patterns — New DeckManagerTest.cs Tests

```csharp
[Fact]
public void ReturnCard_AppendsToHand() {
    var dm = new DeckManager();
    dm.SetHand(new[] { new CardDefinition { Id = "march", Name = "March" } });
    dm.ReturnCard(new CardDefinition { Id = "stamina", Name = "Stamina" });
    Assert.Equal(2, dm.Hand.Count);
    Assert.Equal("stamina", dm.Hand[1].Id);
}

[Fact]
public void ReturnCard_FiresHandChanged() {
    var dm = new DeckManager();
    int fired = 0;
    dm.HandChanged += () => fired++;
    dm.ReturnCard(new CardDefinition { Id = "march", Name = "March" });
    Assert.Equal(1, fired);
}
```

### Test Hand Reminder

`PlaceholderMainMenu` builds the test hand as `_state.Cards.Where(c => c.Type != CardType.Wound).Take(4)`. From `cards.yaml`, that yields **[march, stamina, threaten, promise]** in document order. In `GamePhase.Movement`:

| Card | EffectType | Play (stage) enabled? |
|------|-----------|----------------------|
| march | `Move` | ✅ |
| stamina | `Move` | ✅ |
| threaten | `Influence` | ❌ |
| promise | `Influence` | ❌ |

Manual verification flow: tap march → Play → hand shows [stamina, threaten, promise], staging shows Move: 2; tap Undo → hand returns to [stamina, threaten, promise, march] (march appended at end), staging shows Move: 0, Undo button disables.

### UX: Undo Button Position

The Undo button is added to the `HBoxContainer` in `StagingAreaView` after the Commit button: `[Move: N] [Attack: N] [Block: N] [Influence: N] [Commit] [Undo]`. Both buttons share font_size=32 for consistent tap target size on Android (Galaxy S21 screen).

### No New GameState Fields — LOCKSTEP Unchanged

`StagingManager` lives outside `GameState`. `DeckManager.Hand` is not yet part of the `GameState` snapshot (the carry-forward from 1b-2 review: "Full atomic sideways play — carry to the UIBroker story"). No changes to `GameStateSnapshot`, `TakeSnapshot()`, `RestoreSnapshot()`, or the LOCKSTEP comment. This story does not touch `GameState` at all.

### Namespace Conventions

| Folder | Namespace |
|--------|-----------|
| `scripts/deck/` | `MagusWarrior.Deck` |
| `scripts/ui/components/` | `MagusWarrior.UI` |
| `tests/unit/` | `MagusWarrior.Tests` |

### File Placement

| File | Action | Type |
|------|--------|------|
| `scripts/deck/StagingManager.cs` | modified (add `Unstage`) | Pure C# |
| `scripts/deck/DeckManager.cs` | modified (add `ReturnCard`) | Pure C# |
| `scripts/ui/components/StagingAreaView.cs` | modified (add Undo button) | Godot Control |
| `scripts/ui/components/HandView.cs` | modified (wire `UndoRequested`) | Godot Control |
| `tests/unit/StagingManagerTest.cs` | modified (4 new tests) | xUnit test |
| `tests/unit/DeckManagerTest.cs` | modified (2 new tests) | xUnit test |

No changes to `tests/maguswarrior.Tests.csproj` — `StagingManager.cs` and `DeckManager.cs` are already in the compile list. `StagingAreaView.cs` and `HandView.cs` are NOT added (Godot dependencies).

### Project Context Rules

**Pure C# boundary:** `StagingManager.Unstage()` and `DeckManager.ReturnCard()` are pure C# — no Godot dependency. Both files are already in `tests/maguswarrior.Tests.csproj`. `StagingAreaView.cs` and `HandView.cs` are Godot nodes — do NOT add to test project.

**No service locator:** `StagingManager` is already injected via `HandView.Initialize`. `DeckManager` is already injected. No new construction or injection paths needed.

**`async void` rule:** `OnUndoRequested` is `void` (not async). No effects are applied during undo — it is a pure in-memory reversal of staging. The `async void` exception applies only to Godot signal handlers that await effects. Do NOT make `OnUndoRequested` async.

**Result<T>:** `StagingManager.Unstage()` returns `StagedEntry?` (nullable), not `Result<T>`. The null return for an empty list is sufficient; the caller guards with `if (entry is null) return;`. No exception thrown.

**Logging:** `Log.Debug("[UI]", ...)` for the undo event in `HandView.OnUndoRequested`. No logging inside `StagingManager.Unstage()` or `DeckManager.ReturnCard()` — those are pure C# and do not import `Log` (which has Godot dependency).

**ImplicitUsings=disable:** All using statements must be explicit. No new usings are needed in any file touched by this story — all required types are already imported.

**Events are data-only:** `StagingChanged` is `event Action?` (no data). The `UndoRequested` signal in `StagingAreaView` is also no-data (no args). Consumers call `Unstage()` + `ReturnCard()` themselves.

**LOCKSTEP:** No new fields added to `GameState`. The LOCKSTEP comment at `scripts/core/GameState.cs:48` does not need updating.

### Carry-Forward Notes from 1b-3 (Unchanged)

- `async void` in Godot signal handlers: accepted exception for `OnCommitRequested` and `OnPlaySidewaysRequested`. `OnUndoRequested` is `void`, not async — no exception needed.
- `ImplicitUsings=disable`: all using statements must be explicit in every `.cs` file.
- No `GD.Print` — use `Log.Debug/Warn/Error` with system tag `[UI]`.
- The `HandChanged` and `StagingChanged` subscription leak deferred items (from 1b-1/1b-3 reviews) are not re-opened here — `UndoRequested` follows the same pattern as `CommitRequested` and shares its lifecycle.

### References

- Epics: `_bmad-output/epics.md` §Epic 1b — "Undo via event log (extends Epic 1a — not rebuilt)"; story description "As a player, I can undo staged cards before new information is revealed"
- Architecture Undo System (pre-commit scope): `_bmad-output/game-architecture.md:321-325`
- Architecture Screen Contract (Hand Display — CardUnstaged signal): `_bmad-output/game-architecture.md:814-820`
- Architecture Undo player feature note: `_bmad-output/game-architecture.md:470`
- Project Context Rules: `docs/project-context.md` (pure C# boundary, async/await, Result<T>, logging, file placement)
- Previous story (1b-3) Dev Notes — atomicity pattern, LOCKSTEP rule, async void accepted exception, BuildEffect: `_bmad-output/implementation-artifacts/1b-3-stage-multiple-cards-and-see-running-totals.md`
- Deferred work log: `_bmad-output/implementation-artifacts/deferred-work.md`
- StagingManager (current): `scripts/deck/StagingManager.cs`
- DeckManager (current): `scripts/deck/DeckManager.cs`
- StagingAreaView (current): `scripts/ui/components/StagingAreaView.cs`
- HandView.OnCommitRequested (re-entrancy guard model, async void exception): `scripts/ui/components/HandView.cs:135-154`
- HandView.Initialize (signal wiring model): `scripts/ui/components/HandView.cs:76-87`

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

- All 5 tasks completed. 48/48 xUnit tests pass (42 existing + 6 new). Godot build: 0 errors, 0 warnings.
- `StagingManager.Unstage()` added — LIFO pop from `_staged`, fires `StagingChanged` only on non-empty; returns `null` without event on empty list.
- `DeckManager.ReturnCard(CardDefinition)` added — appends card to end of hand via `SetHand`, which fires `HandChanged`.
- `StagingAreaView.cs` updated — `UndoRequestedEventHandler` signal added; `_undoButton` field + Undo button created in `_Ready()` (font_size=32, initially disabled); `Refresh()` now sets `_undoButton.Disabled` in sync with `_commitButton.Disabled`.
- `HandView.cs` updated — `_stagingAreaView.UndoRequested += OnUndoRequested` wired in `Initialize`; `OnUndoRequested` is synchronous `void` (no async needed — pure in-memory reversal); guards empty staging list; calls `Unstage()` + `ReturnCard()` + `Log.Debug("[UI]", ...)`.
- No new usings required in any file. No changes to `.csproj` (both modified pure-C# files were already in compile list). No `GameState` fields touched; LOCKSTEP unchanged.

### File List

- `scripts/deck/StagingManager.cs` (modified — added `Unstage`)
- `scripts/deck/DeckManager.cs` (modified — added `ReturnCard`)
- `scripts/ui/components/StagingAreaView.cs` (modified — Undo button + signal)
- `scripts/ui/components/HandView.cs` (modified — `UndoRequested` wire + `OnUndoRequested`)
- `tests/unit/StagingManagerTest.cs` (modified — 4 new tests)
- `tests/unit/DeckManagerTest.cs` (modified — 2 new tests)

## Review Findings

Code review 2026-06-04 (Opus 4.8) — Blind Hunter + Edge Case Hunter + Acceptance Auditor. All 7 ACs functionally satisfied; Acceptance Auditor verified 48/48 tests green and 0 warnings. 1 patch, 2 defer, 9 dismissed. No decision-needed items.

- [x] [Review][Patch] `OnUndoRequested` not covered by the `_committing` re-entrancy guard; spec's stated mitigation fires after the await, not at commit start [scripts/ui/components/HandView.cs OnUndoRequested] — **RESOLVED.** Added `if (_committing) return;` as the first line of `OnUndoRequested`. `OnCommitRequested` snapshots `StagedCards.ToList()`, `await ResolveAll`, then `Clear()`. Previously `OnUndoRequested` never checked `_committing`, so once `ResolveAll` genuinely awaits (UIBroker), an Undo tap inside the await window would unstage + return a card to hand while that same card was still in the commit snapshot and about to be applied → effect committed AND card back in hand. The Dev Notes mitigation ("Undo button disabled by `Refresh()` via the `Clear()` path") was incorrect: `Clear()` runs post-await, so the button stayed enabled for the whole in-flight window. Now symmetric to the 1b-3 commit re-entrancy patch — window closed before async lands. Build clean, 48/48 tests green.

- [x] [Review][Defer] `DeckManager.ReturnCard` has no Id-uniqueness / membership guard [scripts/deck/DeckManager.cs ReturnCard] — deferred, latent. `ReturnCard` does `list.Add(card)` with no check that a card of the same `Id` is already in `Hand`. The hand's "Id appears at most once" invariant — relied on by `OnCardTapped`/`OnPlayRequested`/`PlayCard` via `FirstOrDefault`/`FindIndex(c => c.Id == ...)` — holds today only by the LIFO Stage/Unstage convention, not by `ReturnCard` itself. Unreachable through the current undo path; one careless future caller from a silent wrong-card bind. Ties to broader hand-invariant hardening.

- [x] [Review][Defer] Integrated `OnUndoRequested` flow (Unstage + ReturnCard) has no automated coverage [scripts/ui/components/HandView.cs OnUndoRequested] — deferred, pre-existing structural limit. The combined handler lives in `HandView` (Godot `Control`), which cannot be compiled into the pure-C# test project. The two halves are unit-tested in isolation (`Unstage_*`, `ReturnCard_*`) but their composition in the signal handler is verifiable only on-device. Same Godot/test-project boundary already logged for `StagingAreaView`/`HandView` in prior stories.

**Dismissed (9):** "Undo returns wrong-cost card variant" (Blind — false positive: `CardDefinition` is immutable card data and the sideways path never stages, so there is no lost orientation/mode); order-to-end on undo (spec-sanctioned, Dev Notes explicitly append to hand end); redundant empty double-guard / TOCTOU (intentional defense-in-depth; Godot is single-threaded — Edge Case Hunter validated the triple-guard); `StagedEntry.Card` null risk (non-nullable by record construction; `OnPlayRequested` guards `card`/`Unpowered` null before staging); non-atomic two-step mutation rollback (`ReturnCard` has no throwing path for a non-null card); `StagedCards` vs `_staged` snapshot fear (confirmed live read-only view from 1b-3); `_undoButton.Disabled` init state (mirrors commit button; `Initialize` calls `Refresh()` immediately after subscribing); mixed-type LIFO test gap (LIFO already proven by `Unstage_RemovesLastStagedCard`; `Unstage` returns the whole entry so `EffectType` preservation is structural); `UndoRequested` never unsubscribed (Edge Case Hunter self-dismissed — view-to-child subscription torn down with the subtree, not the manager-outlives-view leak class already in `deferred-work.md`).
