# Story 2.0: Input Lock Re-entrancy Guard

Status: done

## Story

As a developer,
I want a centralized `InputLock` that all UI handlers share,
so that rapid taps, async commits, and cross-view interactions cannot cause double-applies, stuck overlays, or inconsistent game state.

## Background

Epic 1b had five re-entrancy incidents across seven stories (see epic-1-retro-2026-06-06.md). Each was patched individually with a per-component `_committing` bool or null-check idiom. The root cause is structural: `async void` Godot signal handlers + synchronous C# events (`HandChanged`, `StagingChanged`) + state mutations = re-entrancy windows.

This story introduces `InputLock` as the single shared guard, retrofits existing handlers, closes a currently unguarded bug in `OnPlaySidewaysRequested`, and documents the pattern in `project-context.md` so future Epic 2+ stories follow it consistently.

**Retro action item:** Epic 1 Retro Action Item 1 — "Before hex map stories."

---

## Acceptance Criteria

### AC 1 — `scripts/ui/InputLock.cs` (new, pure C#)

Create `scripts/ui/InputLock.cs` — **no Godot dependency**, namespace `MagusWarrior.UI`.

```csharp
namespace MagusWarrior.UI;

public class InputLock {
    private bool _locked;

    public bool IsLocked => _locked;

    // Returns true if lock acquired (caller may proceed); false if already locked (caller drops action).
    public bool TryAcquire() {
        if (_locked) return false;
        _locked = true;
        return true;
    }

    public void Release() { _locked = false; }
}
```

No usings required. No Godot dependency — this file is added to the test project.

---

### AC 2 — `scripts/ui/components/HandView.cs` updated

**Remove** the `private bool _committing;` field.

**Add** `private InputLock _lock = null!;`

**Update `Initialize`** — add `InputLock inputLock` as last parameter; assign `_lock = inputLock;`:
```csharp
public void Initialize(DeckManager deck, GameState state, EffectScheduler scheduler, StagingManager staging, InputLock inputLock)
```

**Update `OnCommitRequested`** — replace `_committing` flag with InputLock:
```csharp
private async void OnCommitRequested() {
    if (!_lock.TryAcquire() || _stagingManager.StagedCards.Count == 0) return;
    try {
        foreach (var entry in _stagingManager.StagedCards.ToList()) {
            var effect = BuildEffect(entry);
            if (effect is null) {
                Log.Warn("[UI]", $"OnCommitRequested: unsupported effect type {entry.EffectType} for '{entry.Card.Id}' — skipping");
                continue;
            }
            var ctx = new EffectContext(entry.Card.Id, entry.EffectType, _state.CurrentPhase, false);
            _scheduler.Enqueue(effect, 0, ctx);
        }
        await _scheduler.ResolveAll(_state);
        _stagingManager.Clear();
        Log.Debug("[UI]", "Commit resolved");
    } finally {
        _lock.Release();
    }
}
```

**Update `OnUndoRequested`** — replace `if (_committing)` with `if (_lock.IsLocked)`:
```csharp
private void OnUndoRequested() {
    if (_lock.IsLocked) return;
    ...
}
```

**Update `OnPlaySidewaysRequested`** — add InputLock guard (currently unguarded — this is the concrete bug fix):
```csharp
private async void OnPlaySidewaysRequested(string cardId) {
    if (!_lock.TryAcquire()) return;
    try {
        var card = _deck.Hand.FirstOrDefault(c => c.Id == cardId);
        if (card is not null && card.Type == CardType.Wound) {
            Log.Warn("[UI]", $"OnPlaySidewaysRequested: '{cardId}' is a Wound — cannot be played sideways");
            return;
        }
        var sideways = SidewaysRule.GetEffect(_state.CurrentPhase);
        if (sideways is null) {
            Log.Debug("[UI]", $"PlaySideways: no sideways effect in {_state.CurrentPhase}");
            return;
        }
        var result = _deck.PlayCard(cardId);
        if (!result.IsSuccess) {
            Log.Warn("[UI]", $"PlaySideways: PlayCard failed for '{cardId}': {result.Error}");
            return;
        }
        var (effectType, amount) = sideways.Value;
        IEffect effect = effectType switch {
            EffectType.Move        => new MoveEffect(amount),
            EffectType.AttackMelee => new AttackEffect(amount, EffectType.AttackMelee, AttackElement.Physical),
            EffectType.Block       => new BlockEffect(amount, AttackElement.Physical),
            EffectType.Influence   => new InfluenceEffect(amount),
            _                      => throw new InvalidOperationException(
                                          $"SidewaysRule returned unexpected EffectType: {effectType}")
        };
        var ctx = new EffectContext(cardId, effectType, _state.CurrentPhase, false);
        _scheduler.Enqueue(effect, 0, ctx);
        await _scheduler.ResolveAll(_state);
        Log.Debug("[UI]", $"PlaySideways: {cardId} → {effectType} {amount} applied");
    } finally {
        _lock.Release();
    }
}
```

Note: The early returns (Wound guard, null sideways) inside the try block will still hit `_lock.Release()` via the finally — this is correct and intentional.

---

### AC 3 — `scripts/ui/components/ImprovisationView.cs` updated

**Add** `private InputLock _lock = null!;`

**Update `Initialize`** — add `InputLock inputLock` as last parameter; assign `_lock = inputLock;`:
```csharp
public void Initialize(DeckManager deck, GameState state, StagingManager staging, InputLock inputLock)
```

**`Activate` — no change** to the method signature or body. `Activate` is synchronous and all its guard checks are already in place (`anyOptionLegal`, `PlayCard`, non-Wound hand check). No lock needed here.

**Update `OnResourceSelected`** — add TryAcquire/Release guard (per-method, same pattern as `OnCommitRequested`):
```csharp
private void OnResourceSelected(EffectType effectType, int amount) {
    if (!_lock.TryAcquire()) return;
    try {
        if (_improvCard is null) return;
        Log.Debug("[UI]", $"Improvisation: staging {effectType} {amount} in {_state.CurrentPhase}");
        _stagingManager.Stage(_improvCard, effectType, costCard: _discardedCard, overrideAmount: amount);
        _improvCard = null;
        _discardedCard = null;
        Visible = false;
        Log.Debug("[UI]", "ImprovisationView closed");
    } finally {
        _lock.Release();
    }
}
```

The `_improvCard is null` check inside the try block is a defensive guard (should never be true; `Activate` always sets it before the overlay opens). The `TryAcquire/finally/Release` pattern replaces the prior ad-hoc null-check idempotency with the shared lock convention.

---

### AC 4 — `scripts/ui/components/RestView.cs` updated

**Add** `private InputLock _lock = null!;`

**Update `Initialize`** — add `InputLock inputLock` as last parameter; assign `_lock = inputLock;`:
```csharp
public void Initialize(DeckManager deck, GameState state, InputLock inputLock)
```

**Update `OnDeclareRestPressed`** — add guard (defense-in-depth; `_declareRestButton.Disabled` already prevents most re-entry but the lock closes the cross-handler window):
```csharp
private void OnDeclareRestPressed() {
    if (!_lock.TryAcquire()) return;
    try {
        // ... full existing body unchanged ...
    } finally {
        _lock.Release();
    }
}
```

**Update `OnEndRestPressed`** — add guard:
```csharp
private void OnEndRestPressed() {
    if (!_lock.TryAcquire()) return;
    try {
        _state.SetPhase(GamePhase.Movement);
        _nonWoundDiscarded = false;
        _inExhaustion = false;
        _restPanel.Visible = false;
        _endRestButton.Disabled = true;
        Log.Debug("[UI]", "Rest completed — phase returned to Movement");
        RefreshView();
    } finally {
        _lock.Release();
    }
}
```

`OnDiscardPressed` — leave unchanged. It is synchronous, and `BuildDiscardRows()` rebuilds with disabled state before the player can tap again. Re-entrancy here is blocked by Godot's single-threaded signal dispatch.

---

### AC 5 — `scripts/ui/screens/PlaceholderMainMenu.cs` updated

**Add field:**
```csharp
private InputLock _inputLock = null!;
```

**In `_Ready()`**, create the lock after `_stagingManager`:
```csharp
_inputLock = new InputLock();
```

**Update all three Initialize calls** to pass `_inputLock`:
```csharp
handView.Initialize(_deckManager, _state, _effectScheduler, _stagingManager, _inputLock);
_restView.Initialize(_deckManager, _state, _inputLock);
_improvView.Initialize(_deckManager, _state, _stagingManager, _inputLock);
```

Order of initialization lines is unchanged — only the extra `_inputLock` argument is added.

---

### AC 6 — `docs/project-context.md` updated

Add a new **Input Lock** section after the **Re-entrancy is structural** retro insight. Insert between the `## Why These Rules Exist` section and the `## LLDs Required Before Full Implementation` section:

```markdown
## Input Lock

`InputLock` (`scripts/ui/InputLock.cs`) is the single re-entrancy guard shared by all UI views. Every `async void` Godot signal handler that mutates game state — or any synchronous handler that must not interleave with another — must acquire the lock before proceeding and release it in a `finally` block.

```csharp
// CORRECT — centralized guard, guaranteed release
private async void OnCommitRequested() {
    if (!_lock.TryAcquire()) return;
    try {
        // ... mutate game state
    } finally {
        _lock.Release();
    }
}

// FORBIDDEN — per-component bool, not guaranteed on exception
private bool _committing;
private async void OnCommitRequested() {
    if (_committing) return;
    _committing = true;
    // ... if an exception occurs, _committing is never reset → UI is permanently locked
}
```

One `InputLock` instance is created in `PlaceholderMainMenu` and passed to every view through `Initialize`. While any view holds the lock, all other views reject user input — this closes cross-view races that per-component bools cannot.

**Why a shared instance, not per-component:**  
In Epic 1b, `HandView._committing` prevented HandView re-entry but could not block `ImprovisationView` from firing simultaneously. A shared lock gives the invariant: only one stateful UI operation runs at a time, across all views. When `ImprovisationView` is active and holds the lock, `HandView.OnCommitRequested` also returns immediately.

**Lock lifetime for multi-step flows (ImprovisationView):**  
When a multi-step overlay acquires the lock in `Activate()`, it does NOT release it in `Activate`. The lock is held for the duration of the overlay session and released only in the terminal handler (`OnResourceSelected`). This prevents the overlay from being activated twice and blocks unrelated handlers during the session.
```

---

### AC 7 — `tests/maguswarrior.Tests.csproj` updated

**Replace all per-file `<Compile Include>` entries** with the following glob-based ItemGroup. This resolves Epic 1 Retro Action Item 4 (maintenance burden of 13+ manual entries):

```xml
<!-- Pure C# files with no Godot dependency — safe to compile without Godot SDK -->
<ItemGroup>
  <!-- scripts/core: per-file because Log.cs and GameDebug.cs have direct Godot (GD.Print) deps -->
  <Compile Include="../scripts/core/GameEventLog.cs" />
  <Compile Include="../scripts/core/Result.cs" />
  <Compile Include="../scripts/core/GameConstants.cs" />
  <Compile Include="../scripts/core/GameState.cs" />
  <Compile Include="../scripts/core/SaveMigrator.cs" />
  <Compile Include="../scripts/core/types/**/*.cs" />
  <!-- scripts/cards and scripts/deck: fully pure C# subtrees — glob picks up all future files -->
  <Compile Include="../scripts/cards/**/*.cs" />
  <Compile Include="../scripts/deck/**/*.cs" />
  <!-- scripts/save: only SaveData is pure C#; SaveManager uses Godot FileAccess directly -->
  <Compile Include="../scripts/save/SaveData.cs" />
  <!-- scripts/ui: only InputLock is pure C# in this subtree -->
  <Compile Include="../scripts/ui/InputLock.cs" />
</ItemGroup>
```

`dotnet test` must still pass with 98/98 green after this change — verify the glob does not accidentally pull in any Godot-dependent files. If any `scripts/cards/**` or `scripts/deck/**` file is found to have an unguarded Godot dependency, add a `<Compile Remove>` entry for it rather than reverting to per-file includes.

---

### AC 8 — `tests/unit/InputLockTest.cs` (new, 5 xUnit tests)

Namespace: `MagusWarrior.Tests`. Usings: `MagusWarrior.UI`, `Xunit`.

```
InputLockTest.cs tests (5):
```

- `TryAcquire_WhenFree_ReturnsTrue` — new lock, `TryAcquire()` returns `true`
- `TryAcquire_WhenFree_SetsIsLocked` — after `TryAcquire()`, `IsLocked == true`
- `TryAcquire_WhenLocked_ReturnsFalse` — acquire once, second `TryAcquire()` returns `false`
- `Release_AfterAcquire_ClearsIsLocked` — acquire then release, `IsLocked == false`
- `TryAcquire_AfterRelease_CanAcquireAgain` — acquire, release, `TryAcquire()` returns `true` again

No `[Fact]` order dependency. Each test creates a fresh `InputLock()` instance.

---

### AC 9 — Pass-all gate

`dotnet test tests/maguswarrior.Tests.csproj` — **98/98 green** (93 existing + 5 new). Zero warnings.

`dotnet build maguswarrior.csproj` — 0 errors, 0 warnings.

---

## Tasks / Subtasks

- [x] Task 1: InputLock class + test (AC 1, 7, 8)
  - [x] Create `scripts/ui/InputLock.cs`
  - [x] Update `tests/maguswarrior.Tests.csproj` (AC 7 glob change)
  - [x] Create `tests/unit/InputLockTest.cs` — 5 tests
  - [x] Run `dotnet test` — confirm 5 new tests green (98 total)

- [x] Task 2: HandView retrofit (AC 2)
  - [x] Remove `_committing` field; add `_lock` field
  - [x] Update `Initialize` signature and body
  - [x] Update `OnCommitRequested` — TryAcquire/Release
  - [x] Update `OnUndoRequested` — IsLocked check
  - [x] Update `OnPlaySidewaysRequested` — add TryAcquire/Release guard
  - [x] `dotnet build maguswarrior.csproj` — 0 errors, 0 warnings

- [x] Task 3: ImprovisationView retrofit (AC 3)
  - [x] Add `_lock` field; update `Initialize`
  - [x] Update `OnResourceSelected` — TryAcquire/Release guard
  - [x] `dotnet build maguswarrior.csproj` — 0 errors, 0 warnings

- [x] Task 4: RestView retrofit (AC 4)
  - [x] Add `_lock` field; update `Initialize`
  - [x] Update `OnDeclareRestPressed` — TryAcquire/Release
  - [x] Update `OnEndRestPressed` — TryAcquire/Release
  - [x] `dotnet build maguswarrior.csproj` — 0 errors, 0 warnings

- [x] Task 5: PlaceholderMainMenu wiring (AC 5)
  - [x] Add `_inputLock` field; create `new InputLock()` in `_Ready`
  - [x] Pass `_inputLock` to all three Initialize calls
  - [x] `dotnet build maguswarrior.csproj` — 0 errors, 0 warnings

- [x] Task 6: project-context.md + final gate (AC 6, 9)
  - [x] Add InputLock section to `docs/project-context.md`
  - [x] Run `dotnet test tests/maguswarrior.Tests.csproj` — 98/98 green, 0 warnings
  - [x] Run `dotnet build maguswarrior.csproj` — 0 errors, 0 warnings

---

## Dev Notes

### Why `OnPlaySidewaysRequested` Was Unguarded

The per-component `_committing` bool was only checked in `OnCommitRequested` and `OnUndoRequested`. `OnPlaySidewaysRequested` is equally `async void` and equally awaits `_scheduler.ResolveAll` — a rapid double-tap could enqueue and apply the same card's sideways effect twice, granting duplicate Move/Attack/Block/Influence points. InputLock closes this gap.

### Lock Scope in ImprovisationView

`OnResourceSelected` uses the per-method pattern: `TryAcquire()` at entry, `Release()` in finally, exactly like `OnCommitRequested`. `Activate` does NOT acquire the lock — it is synchronous, all guards are already in place, and there is no real concurrent call risk in Godot's single-threaded signal queue.

The full-screen overlay (ZIndex=5) physically prevents the player from reaching HandView buttons while ImprovisationView is showing — the lock is defense-in-depth for the close path, not the primary guard for cross-view exclusion.

If ImprovisationView is ever extended with an async internal operation (e.g., UIBroker-backed choice), the per-method pattern is sufficient and no refactor is needed.

### What InputLock Does NOT Replace

- `_declareRestButton.Disabled = true` — still necessary; the InputLock is per-operation, not per-UI-state. Disabling the button prevents the user from even trying to declare rest twice.
- `card.Type == CardType.Wound` guard — still necessary; this is a game rules check, not re-entrancy.
- `_improvCard is null` null-check in `OnResourceSelected` — kept as a defensive guard. The InputLock prevents re-entry, the null check guards against the impossible case where `_improvCard` was never set.

### Note on Deferred csproj Glob (Action Item 4)

This story includes the glob change (AC 7). The glob covers `scripts/cards/**/*.cs` and `scripts/deck/**/*.cs` — both are pure C# subtrees per the file placement rule in project-context.md. If any future file in those subtrees adds a Godot dependency (rare, would be a rules violation), add a `<Compile Remove>` entry.

### Epic 2 Critical Path Reminder

`MovePointsThisTurn` needs a reset path before story 2-3 (spending move points). This is Epic 1 Retro Action Item 3. Stories 2-1 and 2-2 are visual-only and can proceed without it. Address it as part of story 2-2 or at the start of 2-3. **Do not implement this in the current story.**

### Carry-Forward From 1b-7

- `ImplicitUsings=disable` — all `using` statements must be explicit.
- No `GD.Print` — use `Log.Debug`/`Log.Warn` with `[UI]` tag.
- `async void` only in Godot signal handlers. All non-signal handlers remain synchronous.
- `public bool TryAcquire()` / `public void Release()` — do not add overloads or variants in this story.

### File Placement

| File | Action | Type |
|------|--------|------|
| `scripts/ui/InputLock.cs` | new | Pure C# |
| `scripts/ui/components/HandView.cs` | modified — `_lock` replaces `_committing`; `Initialize` sig change; `OnPlaySidewaysRequested` guarded | Godot Control |
| `scripts/ui/components/ImprovisationView.cs` | modified — `_lock` field; `Initialize` sig change; `Activate` acquires lock; `OnResourceSelected` releases lock in try/finally | Godot Control |
| `scripts/ui/components/RestView.cs` | modified — `_lock` field; `Initialize` sig change; `OnDeclareRestPressed`/`OnEndRestPressed` guarded | Godot Control |
| `scripts/ui/screens/PlaceholderMainMenu.cs` | modified — `_inputLock` field; create in `_Ready`; pass to all three Initialize calls | Godot CanvasLayer |
| `tests/unit/InputLockTest.cs` | new — 5 tests | xUnit |
| `tests/maguswarrior.Tests.csproj` | modified — replace per-file includes with globs + InputLock.cs | project |
| `docs/project-context.md` | modified — add InputLock pattern section | doc |

### Namespace Conventions

| Folder | Namespace |
|--------|-----------|
| `scripts/ui/` | `MagusWarrior.UI` |
| `tests/unit/` | `MagusWarrior.Tests` |

### Project Context Rules

**Pure C# boundary:** `InputLock.cs` has no Godot dependency — added to test csproj. `HandView.cs`, `ImprovisationView.cs`, `RestView.cs`, `PlaceholderMainMenu.cs` — Godot nodes, NOT in test project.

**Constructor injection / No service locator:** `InputLock` is passed via `Initialize` to all views. `PlaceholderMainMenu` owns the one instance. Views do not create their own lock.

**No static access:** `InputLock` is an instance, not static. This matches the architecture rule — only `Log` and `GameDebug` are static exceptions.

**async void:** Only in Godot signal handlers. `OnCommitRequested` and `OnPlaySidewaysRequested` remain `async void` (Godot constraint). All other handlers in this story are synchronous.

**Result<T>:** No new failure paths. InputLock methods return `bool` / `void` — no failure semantics needed.

**Logging tags:** `[UI]` for all new log calls in this story.

### References

- Epic 1 Retro Action Items: `_bmad-output/implementation-artifacts/epic-1-retro-2026-06-06.md:126-134`
- Epic 1 Retro Key Insight 1: `_bmad-output/implementation-artifacts/epic-1-retro-2026-06-06.md:90`
- HandView `_committing` field: `scripts/ui/components/HandView.cs:28`
- HandView `OnCommitRequested`: `scripts/ui/components/HandView.cs:188`
- HandView `OnUndoRequested`: `scripts/ui/components/HandView.cs:169`
- HandView `OnPlaySidewaysRequested` (unguarded): `scripts/ui/components/HandView.cs:211`
- ImprovisationView `Initialize`: `scripts/ui/components/ImprovisationView.cs:54`
- ImprovisationView `Activate`: `scripts/ui/components/ImprovisationView.cs:60`
- ImprovisationView `OnResourceSelected`: `scripts/ui/components/ImprovisationView.cs:158`
- RestView `Initialize`: `scripts/ui/components/RestView.cs:65`
- RestView `OnDeclareRestPressed`: `scripts/ui/components/RestView.cs:72`
- RestView `OnEndRestPressed`: `scripts/ui/components/RestView.cs:169`
- PlaceholderMainMenu Initialize calls: `scripts/ui/screens/PlaceholderMainMenu.cs:77-88`
- Architecture — no static except Log/GameDebug: `_bmad-output/game-architecture.md:359`
- Project context — DI rule: `docs/project-context.md:227-240`
- Test csproj: `tests/maguswarrior.Tests.csproj`

---

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

- All 6 tasks completed. 98/98 xUnit tests pass (93 existing + 5 InputLockTest). Godot build: 0 errors, 0 warnings.
- `InputLock.cs` created — pure C# `TryAcquire()/IsLocked/Release()` class in `scripts/ui/`, namespace `MagusWarrior.UI`. No Godot dependency — added to test csproj.
- `tests/maguswarrior.Tests.csproj` switched from 13 per-file `<Compile Include>` entries to glob-based approach: `scripts/cards/**/*.cs`, `scripts/deck/**/*.cs`, `scripts/core/types/**/*.cs` as globs; per-file entries kept only for `scripts/core/` files with Godot deps (`Log.cs`, `GameDebug.cs` excluded), `scripts/save/SaveData.cs`, and the new `scripts/ui/InputLock.cs`.
- `HandView.cs` — removed `_committing` bool; added `_lock InputLock` field; `Initialize` gains `InputLock inputLock` parameter; `OnCommitRequested` now uses `TryAcquire()/Release()`; `OnUndoRequested` checks `_lock.IsLocked`; `OnPlaySidewaysRequested` (previously unguarded) now wrapped in `TryAcquire/try/finally/Release`.
- `ImprovisationView.cs` — added `_lock InputLock` field; `Initialize` gains parameter; `OnResourceSelected` now uses `TryAcquire()/try/finally/Release()` replacing the prior null-check idempotency pattern.
- `RestView.cs` — added `_lock InputLock` field; `Initialize` gains parameter; `OnDeclareRestPressed` and `OnEndRestPressed` wrapped in `TryAcquire/try/finally/Release`.
- `PlaceholderMainMenu.cs` — added `_inputLock InputLock` field; created `new InputLock()` in `_Ready` after `_stagingManager`; passed to all three view `Initialize` calls.
- `docs/project-context.md` — added `## Input Lock` section with pattern documentation, CORRECT/FORBIDDEN code examples, shared-vs-per-component rationale, and "if you add a new handler" guidance.

### File List

- `scripts/ui/InputLock.cs` (new)
- `scripts/ui/components/HandView.cs` (modified — `_lock` replaces `_committing`; `Initialize` sig; `OnUndoRequested`; `OnCommitRequested`; `OnPlaySidewaysRequested` guarded)
- `scripts/ui/components/ImprovisationView.cs` (modified — `_lock` field; `Initialize` sig; `OnResourceSelected` TryAcquire/Release)
- `scripts/ui/components/RestView.cs` (modified — `_lock` field; `Initialize` sig; `OnDeclareRestPressed`; `OnEndRestPressed`)
- `scripts/ui/screens/PlaceholderMainMenu.cs` (modified — `_inputLock` field; `new InputLock()`; three Initialize call updates)
- `tests/unit/InputLockTest.cs` (new — 5 tests)
- `tests/maguswarrior.Tests.csproj` (modified — glob-based includes)
- `docs/project-context.md` (modified — Input Lock section added)

## Review Findings

Code review 2026-06-08 (Opus 4.8, develop-in-Sonnet / review-in-Opus split) — Blind Hunter + Edge Case Hunter + Acceptance Auditor. All 9 ACs satisfied (Acceptance Auditor: 98/98 green, build clean, 0 project-context violations). Triage: 1 patch, 5 defer, 7 dismissed. No decision-needed items.

### Patches

- [x] [Review][Patch] P1 (High): `OnCommitRequested` leaks the InputLock when staging is empty. `if (!_lock.TryAcquire() || _stagingManager.StagedCards.Count == 0) return;` evaluates `TryAcquire()` first (acquiring the lock), then the `|| count==0` branch returns BEFORE the `try`, so the `finally { _lock.Release(); }` never runs — the shared lock stays held and all three views deadlock until restart. The commit button is normally disabled at count==0 (`StagingAreaView.cs:77`), but the in-method count check is a deliberate defensive guard for exactly that case; with the old `_committing` flag the early return was harmless because no flag had been set yet. **APPLIED:** reordered to `if (_stagingManager.StagedCards.Count == 0 || !_lock.TryAcquire()) return;` so the lock is only acquired when there is real work; added an explaining comment. Build clean, 98/98 green. [scripts/ui/components/HandView.cs:184] (edge+auditor — two independent layers converged)

### Deferred (see deferred-work.md)

- [x] [Review][Defer] D1: `InputLock` is a non-thread-safe `bool` (no Interlocked/memory barrier). Safe today — all effects resolve synchronously (`Task.FromResult`) and Godot dispatches signal handlers on the main thread, so `TryAcquire`/`Release` never cross threads. When UIBroker introduces genuinely awaitable effects whose continuations may resume off the main thread, the guard needs a memory barrier or a guaranteed main-thread continuation. [scripts/ui/InputLock.cs] (blind) — deferred, latent until async ResolveAll
- [x] [Review][Defer] D2: InputLock coverage is partial — `OnPlayRequested` (HandView), `OnDiscardPressed` (RestView), and `OnDiscardSelected`/`Activate` (ImprovisationView) mutate deck/staging without the lock. Not reachable today: `ResolveAll` is synchronous so no await-window exists for cross-handler interleave, and per-handler self-re-entry is blocked by Godot single-threaded dispatch + button/row rebuilds. Includes the Improvisation double-discard race (a second discard tap before row teardown overwrites `_discardedCard`, silently losing a card). Extend InputLock to these mutating handlers when async await-windows land. [HandView.cs OnPlayRequested, RestView.cs OnDiscardPressed, ImprovisationView.cs OnDiscardSelected] (blind+edge) — deferred, scope-limited
- [x] [Review][Defer] D3: Dropped actions (`TryAcquire` returns false) are silent — no log, no UI feedback, in a codebase that otherwise logs liberally. A `Log.Debug("[UI]", "...input locked, action dropped")` on each guard's false branch would aid field diagnosis of the shared-lock drop behavior. [all guarded handlers] (blind) — deferred, low-priority polish
- [x] [Review][Defer] D4: The Improvisation discard step (between `Activate` and `OnResourceSelected`) holds no lock; it relies on the full-screen `ZIndex=5` overlay (PanelContainer, MouseFilter=Stop) to block cross-view input during the multi-step flow. If the overlay ever becomes non-blocking or non-full-screen, a cross-view action could interleave mid-flow. Document/assert the overlay's input-blocking invariant or guard `Activate`. [scripts/ui/components/ImprovisationView.cs] (edge) — deferred, mitigated by overlay today
- [x] [Review][Defer] D5: `OnPlaySidewaysRequested` (and the staging commit path) is not atomic — `_deck.PlayCard` removes the card before the effect resolves; a synchronous throw between `PlayCard` and `ResolveAll` would spend the card with no effect applied. Pre-existing; already tracked for the UIBroker/atomic-rollback story (Hand is not yet in `GameStateSnapshot`). [scripts/ui/components/HandView.cs] (blind) — deferred, pre-existing

**Dismissed (7):** Blind "shared lock shouldn't be shared / false mutual exclusion" (by-design and documented in project-context.md — shared instance is the deliberate Epic-1-retro decision); Blind "undo reads IsLocked but never acquires" (intentional and matches prior `_committing` semantics — undo bails while a commit is in flight, it never needs to hold the lock); Blind "`_improvCard is null` swallow" (pre-existing defensive guard, not introduced here); Blind "rest-declare re-entry via synchronous HandChanged" (Edge Case Hunter traced it and disproved — `RefreshView` does not call `TryAcquire`, so nothing is silently denied; exhaustion path additionally guarded by `!_inExhaustion`); Blind "guard theater on synchronous OnDeclareRestPressed" (harmless — synchronous non-awaiting method, lock is cheap defense-in-depth); Blind "double-release / unowned Release()" (theoretical, no reachable path given the synchronous dispatch model); Blind "`null!` lock field NRE before Initialize" (consistent with the codebase-wide `null!` + `Initialize` pattern for `_deck`/`_state`/etc.; `Initialize` always runs in `_Ready` before any user interaction is possible).
