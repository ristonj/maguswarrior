# Story 3-0 — TripUndoGate + Remove Commit Button

**Epic:** 3 — Combat System (infrastructure prerequisite)
**Story ID:** 3-0
**Status:** done
**Created:** 2026-06-19
**Dependencies:** 2-0 (InputLock), 2-3 (WorldMap move path), 2-4 (RevealTile/ClearMovePath), 2-5 (StagingAreaView display)
**Reviewer model:** Use Opus 4.8 for code review (per project feedback convention)

---

## User Story

As a player, my card plays (including Improvisation) resolve immediately when I tap Play,
with no separate Commit step. I can undo any card play — including the cost card discard
from Improvisation — until I hit an undo gate (tile reveal, enemy token flip, etc.). Once
a gate fires, all prior actions lock and undo is unavailable for those plays.

---

## Context

### Why this story exists (the design flaw it corrects)

The Epic 2 retro identified a fundamental UX mistake in the staging → Commit model:

- In Mage Knight, undo is free until **new information is revealed** — not until you press
  a button. There is no voluntary "commit" — the undo gate IS the commit.
- The Commit button created false friction: Play → Commit before seeing any effect.
- On-device (Epic 2 S21 pass): after pressing Commit, move undo was silently blocked.

**Decision (John, 2026-06-19 retro):** Remove the Commit button entirely. Cards resolve
immediately when played and remain individually undoable until an undo gate fires.

### What prior stories built — do NOT recreate

- **`GameEventLog`** / **`EffectFiredEvent`** / **`GameStateSnapshot`** (`scripts/core/GameEventLog.cs`):
  The log that `EffectScheduler.ResolveAll` writes to. Each entry includes `SourceCardId`,
  `EffectType`, `Phase`, `Powered`, `StateBefore` (snapshot taken BEFORE the effect ran).
  `PopLast()` removes and returns the newest entry. **This is the undo mechanism.**
- **`EffectContext`** (in `EffectScheduler.cs` or nearby): holds `SourceCardId`, `ChosenType`,
  `Phase`, `Powered`. **This story adds `CostCardId?`** (nullable, default null) so Improvisation
  can tag its event with the discarded card's ID.
- **`GameDebug.UndoLastEvent`** — debug-only. Does NOT return the card to hand. The player-facing
  undo in this story handles that itself.
- **`DeckManager.PlayCard(id)`** — removes card from Hand only; does NOT add to DiscardPile.
  `DeckManager.ReturnCard(card)` adds card back to Hand. **`DeckManager.DiscardCard(id)`**
  removes from Hand AND adds to DiscardPile (used by Improvisation cost mechanic).
  **This story adds `DeckManager.RecallFromDiscard(id)`** — removes from DiscardPile, adds to Hand.
- **`StagingAreaView`** — has `CommitRequested` + `UndoRequested` signals. This story removes
  Commit and its signal; keeps Undo (but changes what it does).
- **`HandView.OnCommitRequested`** — resolves all staged cards. **Removed in this story.**
  Logic moves into `OnPlayRequested` as single-card immediate-resolve.
- **`HandView.OnUndoRequested`** — unstages the last staged card. **Repurposed** to undo the
  last played card via EventLog pop + snapshot restore + ReturnCard/RecallFromDiscard.
- **`ImprovisationView.OnResourceSelected`** — stages the Improvisation card. **Changes to
  resolve immediately.** Must pass `_discardedCard.Id` as `CostCardId` in `EffectContext`.
- **`WorldMap.RevealTile(tile)`** — already calls `ClearMovePath()`. Do NOT change it.
  `TripUndoGate` fires BEFORE `RevealTile`; double-clear of the move path is idempotent.
- **177 tests green** as of story 2-5 close.

### Event log + undo mechanics (read before implementing)

`EffectScheduler.ResolveAll` currently:
1. Takes `snapshot = state.TakeSnapshot()` before the effect
2. Executes the effect
3. Appends `EffectFiredEvent(SourceCardId, EffectType, Phase, Powered, StateBefore)` to log

After this story, step 3 also carries `CostCardId?` from the context.

Player-facing undo of a card play:
1. `ev = state.EventLog.PopLast()` — get newest event
2. `state.RestoreSnapshot(ev.StateBefore)` — roll back all effect mutations
3. `card = state.Cards.FirstOrDefault(c => c.Id == ev.SourceCardId)`; `deck.ReturnCard(card)` — card back in hand
4. If `ev.CostCardId != null`: `deck.RecallFromDiscard(ev.CostCardId)` — cost card back in hand

**`TripUndoGate` must clear the EventLog** — pre-gate plays must not be undoable post-gate.

---

## Acceptance Criteria

**AC1 — Card plays resolve immediately**
Play a Move card → `Move: N` increases in the HUD immediately, no Commit tap. Card leaves
hand immediately. Same for Attack, Block, Influence cards.

**AC2 — Commit button absent**
No Commit button in `StagingAreaView` or anywhere else. Move/Attack/Block/Influence labels
remain and update on each play.

**AC3 — Undo button undoes last card play**
Tap Undo after playing a Move card → card returns to hand, move point total reverts.

**AC4 — Improvisation undo works (including cost card)**
Play Improvisation (discard cost card, select resource) → tap Undo → Improvisation card
returns to hand, discarded cost card returns to hand, effect reverts.

**AC5 — Undo stack works (multiple plays)**
Play Move card → Play Move card again → Undo → only last play reversed (first play's points
remain). Undo again → both reversed. Each undo removes exactly one event.

**AC6 — Undo blocked after gate (tile reveal)**
Play a card (immediate) → move to (1,0) → reveal tile → tap Undo → nothing. EventLog was
cleared by `TripUndoGate` at reveal time.

**AC7 — Improvisation resolves immediately after resource selection**
Tap Improvisation → discard card → select resource → effect applies immediately, view closes.
No Commit tap.

**AC8 — `TripUndoGate` fires `UndoGateCrossed`, clears EventLog, writes snapshot**
On tile reveal: `UndoGateCrossed` fires (wired to `WorldMap.ClearMovePath`); EventLog is
empty afterward; `LastGateSnapshot` holds the pre-reveal state.

**AC9 — Tile reveal still fires correctly (no regression)**
Fog hexes → terrain colors, `TileRevealed` fires, tile count decrements.

**AC10 — Move undo via hex tap still works (no regression)**
Gain move points (via card play) → move to (1,0) → tap (1,0) → hero back at (0,0), refunded.

**AC11 — Move undo blocked after tile reveal (no regression from 2-4 AC7)**
Move to (1,0) → reveal → tap (1,0) → nothing (`CanUndoMove` false post-gate).

---

## Implementation Tasks

### Task 1 — Extend `EffectContext` and `EffectFiredEvent` with optional `CostCardId`

**File:** wherever `EffectContext` is defined (likely `scripts/cards/effects/EffectContext.cs`)
and `GameEventLog.cs`.

**`EffectContext`** — add optional `CostCardId`:
```csharp
public record EffectContext(
    string SourceCardId,
    EffectType ChosenType,
    GamePhase Phase,
    bool Powered,
    string? CostCardId = null);
```

**`EffectFiredEvent`** in `GameEventLog.cs` — add optional `CostCardId`:
```csharp
public record EffectFiredEvent(
    string SourceCardId,
    EffectType EffectType,
    GamePhase Phase,
    bool Powered,
    GameStateSnapshot StateBefore,
    string? CostCardId = null);
```

**`EffectScheduler.ResolveAll`** — pass `CostCardId` through to the event:
```csharp
state.EventLog.Append(new EffectFiredEvent(
    pending.Ctx.SourceCardId,
    pending.Ctx.ChosenType,
    pending.Ctx.Phase,
    pending.Ctx.Powered,
    snapshot,
    pending.Ctx.CostCardId));   // ← new
```

No test change needed for this step — the change is additive with a null default, so all
existing `EffectFiredEvent` construction sites compile without modification. Verify:
`dotnet build maguswarrior.csproj` — 0 errors.

### Task 2 — Add `DeckManager.RecallFromDiscard` (TDD)

**File:** `tests/unit/DeckManagerDiscardTest.cs` (existing file — add new tests)

Write RED first:

```csharp
[Fact]
public void RecallFromDiscard_MovesCardFromDiscardToHand() {
    var deck = new DeckManager();
    var card = TestCards.March();
    deck.SetHand(new[] { card });
    deck.DiscardCard(card.Id);
    Assert.Empty(deck.Hand);
    Assert.Single(deck.DiscardPile);

    var result = deck.RecallFromDiscard(card.Id);

    Assert.True(result.IsSuccess);
    Assert.Single(deck.Hand);
    Assert.Empty(deck.DiscardPile);
}

[Fact]
public void RecallFromDiscard_Fails_WhenCardNotInDiscard() {
    var deck = new DeckManager();
    var result = deck.RecallFromDiscard("nonexistent");
    Assert.False(result.IsSuccess);
}
```

Confirm RED. Then add to `DeckManager.cs`:

```csharp
public Result<CardDefinition> RecallFromDiscard(string cardId) {
    var pile = DiscardPile.ToList();
    var idx = pile.FindIndex(c => c.Id == cardId);
    if (idx < 0)
        return Result<CardDefinition>.Fail($"Card '{cardId}' not in discard pile");
    var card = pile[idx];
    pile.RemoveAt(idx);
    DiscardPile = pile;
    ReturnCard(card);
    return Result<CardDefinition>.Ok(card);
}
```

Run GREEN.

### Task 3 — Add `TripUndoGate`, `UndoGateCrossed`, and `GameEventLog.Clear` (TDD)

**File:** `tests/unit/UndoGateTest.cs` (new file)

Write RED first:

```csharp
[Fact]
public void TripUndoGate_FiresUndoGateCrossed() {
    var state = new GameState(new List<CardDefinition>());
    bool fired = false;
    state.UndoGateCrossed += () => fired = true;
    state.TripUndoGate();
    Assert.True(fired);
}

[Fact]
public void TripUndoGate_WritesLastGateSnapshot() {
    var state = new GameState(new List<CardDefinition>());
    state.AddMovePoints(5);
    state.TripUndoGate();
    Assert.NotNull(state.LastGateSnapshot);
    Assert.Equal(5, state.LastGateSnapshot!.MovePointsThisTurn);
}

[Fact]
public void TripUndoGate_SnapshotReflectsStateAtCallTime() {
    var state = new GameState(new List<CardDefinition>());
    state.AddMovePoints(3);
    state.TripUndoGate();
    state.AddMovePoints(2);
    Assert.Equal(3, state.LastGateSnapshot!.MovePointsThisTurn);
}

[Fact]
public void TripUndoGate_ClearsEventLog() {
    var state = new GameState(new List<CardDefinition>());
    var snap = state.TakeSnapshot();
    state.EventLog.Append(new EffectFiredEvent("march", EffectType.Move, GamePhase.Movement, false, snap));
    state.TripUndoGate();
    Assert.Empty(state.EventLog.Events);
}

[Fact]
public void TripUndoGate_LastGateSnapshot_IsNullBeforeFirstGate() {
    var state = new GameState(new List<CardDefinition>());
    Assert.Null(state.LastGateSnapshot);
}
```

Confirm RED. Then implement:

**`scripts/core/GameEventLog.cs`** — add `Clear()`:
```csharp
public void Clear() => _events.Clear();
```

**`scripts/core/GameState.cs`** — add gate fields and method:
```csharp
public event Action? UndoGateCrossed;
public GameStateSnapshot? LastGateSnapshot { get; private set; }

public void TripUndoGate() {
    LastGateSnapshot = TakeSnapshot();
    EventLog.Clear();
    UndoGateCrossed?.Invoke();
    Log.Debug("[Core]", "Undo gate crossed — snapshot written, event log cleared");
}
```

Run GREEN.

### Task 4 — Remove Commit button from `StagingAreaView`; keep Undo button

**File:** `scripts/ui/components/StagingAreaView.cs`

**Remove completely:**
- `[Signal] public delegate void CommitRequestedEventHandler();`
- `private Button _commitButton = null!;`
- All `_commitButton` construction + `EmitSignal(SignalName.CommitRequested)` wiring in `_Ready()`
- `_commitButton.Disabled = ...` line in `Refresh()`

**Keep:**
- `[Signal] public delegate void UndoRequestedEventHandler();`
- `_undoButton` field, construction, and `EmitSignal(SignalName.UndoRequested)` Pressed handler
- Remove `_undoButton.Disabled = ...` in `Refresh()` — Undo button is always enabled
  (handler gracefully no-ops when EventLog is empty; no need for Disabled tracking here)

**Remove `StagingManager` dependency:**
- Remove `private StagingManager _stagingManager = null!;`
- Change `Initialize(StagingManager staging, GameState state)` → `Initialize(GameState state)`
- Remove `staging.StagingChanged += Refresh;` subscription

**Update `Refresh()` — show committed GameState values only:**
```csharp
private void Refresh() {
    _moveLabel.Text      = $"Move: {_state.MovePointsThisTurn}";
    _attackLabel.Text    = $"Attack: {_state.TotalAttackThisTurn}";
    _blockLabel.Text     = $"Block: {_state.TotalBlockThisTurn}";
    _influenceLabel.Text = $"Influence: {_state.InfluencePointsThisTurn}";
}
```

### Task 5 — Change `HandView` to immediate-resolve; repurpose `OnUndoRequested`

**File:** `scripts/ui/components/HandView.cs`

**Remove:**
- `private StagingManager _stagingManager = null!;` field
- `staging` parameter from `Initialize` signature
- `_stagingAreaView.CommitRequested += OnCommitRequested;` from Initialize
- `_stagingManager = staging;` assignment in Initialize
- `_stagingAreaView.Initialize(staging, state);` → becomes `_stagingAreaView.Initialize(state);`
- `OnCommitRequested()` method (entire method)

**Change `Initialize` signature:**
```csharp
public void Initialize(DeckManager deck, GameState state, EffectScheduler scheduler, InputLock inputLock)
```

**Change `OnPlayRequested` to resolve immediately** (matches `OnPlaySidewaysRequested` pattern):
```csharp
// async void accepted: Godot signal handler. InputLock prevents re-entry.
private async void OnPlayRequested(string cardId) {
    if (!_lock.TryAcquire()) return;
    try {
        var card = _deck.Hand.FirstOrDefault(c => c.Id == cardId);
        if (card is null) {
            Log.Debug("[UI]", $"OnPlayRequested: '{cardId}' not found in hand — ignoring stale tap");
            return;
        }
        if (card.Type == CardType.Wound) {
            Log.Warn("[UI]", $"OnPlayRequested: '{cardId}' is a Wound — cannot be played");
            return;
        }
        if (_state.CurrentPhase == GamePhase.Rest
            && !(card.Unpowered != null && PhaseGate.IsLegal(card.Unpowered.EffectType, GamePhase.Rest))) {
            Log.Warn("[UI]", $"OnPlayRequested: '{cardId}' has no legal play in Rest phase");
            return;
        }
        if (cardId == "improvisation" && _improvView != null) {
            _improvView.Activate(card);
            return;
        }
        if (card.Unpowered is null) {
            Log.Warn("[UI]", $"OnPlayRequested: '{cardId}' has no unpowered spec");
            return;
        }
        var result = _deck.PlayCard(cardId);
        if (!result.IsSuccess) {
            Log.Warn("[UI]", $"OnPlayRequested: PlayCard failed for '{cardId}': {result.Error}");
            return;
        }
        var effect = BuildEffect(card, card.Unpowered.EffectType);
        if (effect is null) {
            Log.Warn("[UI]", $"OnPlayRequested: unsupported effect type {card.Unpowered.EffectType} for '{cardId}'");
            return;
        }
        var ctx = new EffectContext(card.Id, card.Unpowered.EffectType, _state.CurrentPhase, false);
        _scheduler.Enqueue(effect, 0, ctx);
        await _scheduler.ResolveAll(_state);
        Log.Debug("[UI]", $"Play resolved: {cardId} → {card.Unpowered.EffectType}");
    } finally {
        _lock.Release();
    }
}
```

**Change `OnUndoRequested` to undo last played card via EventLog:**
```csharp
private void OnUndoRequested() {
    if (_lock.IsLocked) return;
    var ev = _state.EventLog.Events.LastOrDefault();
    if (ev is null) {
        Log.Debug("[UI]", "OnUndoRequested: nothing to undo");
        return;
    }
    _state.EventLog.PopLast();

    // Return main card to hand
    var card = _state.Cards.FirstOrDefault(c => c.Id == ev.SourceCardId);
    if (card != null) _deck.ReturnCard(card);

    // Return cost card from discard (Improvisation only)
    if (ev.CostCardId != null) {
        var recall = _deck.RecallFromDiscard(ev.CostCardId);
        if (!recall.IsSuccess)
            Log.Warn("[UI]", $"OnUndoRequested: cost card '{ev.CostCardId}' not in discard — {recall.Error}");
    }

    _state.RestoreSnapshot(ev.StateBefore);
    Log.Debug("[UI]", $"Undo play: {ev.SourceCardId}/{ev.EffectType} reversed" +
        (ev.CostCardId != null ? $", cost card '{ev.CostCardId}' recalled" : ""));
}
```

**Update `BuildEffect` signature** (takes card + effectType directly; ImprovisationEffect
is now handled by ImprovisationView — no OverrideAmount branch needed here):
```csharp
private static IEffect? BuildEffect(CardDefinition card, EffectType effectType) {
    var spec = card.Unpowered;
    if (spec is null) return null;
    return effectType switch {
        EffectType.Move         => new MoveEffect(spec.Move),
        EffectType.AttackMelee  => new AttackEffect(spec.Attack, EffectType.AttackMelee, AttackElement.Physical),
        EffectType.AttackRanged => new AttackEffect(spec.Attack, EffectType.AttackRanged, AttackElement.Physical),
        EffectType.Block        => new BlockEffect(spec.Block, AttackElement.Physical),
        EffectType.Influence    => new InfluenceEffect(spec.Influence),
        _                       => null
    };
}
```

### Task 6 — Change `ImprovisationView` to immediate-resolve; pass cost card ID

**File:** `scripts/ui/components/ImprovisationView.cs`

**Remove:** `private StagingManager _stagingManager = null!;`
**Add:** `private EffectScheduler _scheduler = null!;`

**Change `Initialize` signature:**
```csharp
public void Initialize(DeckManager deck, GameState state, EffectScheduler scheduler, InputLock inputLock)
```
Assign `_scheduler = scheduler;`.

**Change `OnResourceSelected` to resolve immediately and pass `CostCardId`:**
```csharp
// async void accepted: Godot button callback; InputLock prevents re-entry.
private async void OnResourceSelected(EffectType effectType, int amount) {
    if (!_lock.TryAcquire()) return;
    try {
        if (_improvCard is null) return;
        var costCardId = _discardedCard?.Id;  // capture before clearing fields
        var effect = new ImprovisationEffect(effectType, amount);
        // Pass cost card ID so the EventLog entry tracks what was discarded (enables undo)
        var ctx = new EffectContext(_improvCard.Id, effectType, _state.CurrentPhase, false, costCardId);
        _scheduler.Enqueue(effect, 0, ctx);
        await _scheduler.ResolveAll(_state);
        Log.Debug("[UI]", $"Improvisation resolved: {effectType} {amount} in {_state.CurrentPhase}");
        _improvCard = null;
        _discardedCard = null;
        Visible = false;
        Log.Debug("[UI]", "ImprovisationView closed");
    } finally {
        _lock.Release();
    }
}
```

Add using if not present:
```csharp
using MagusWarrior.Cards.Effects.Special;
```

### Task 7 — Wire `TripUndoGate` in `HexMapView` tile-reveal branch

**File:** `scripts/ui/components/HexMapView.cs`

In `HandleHexTap`, Branch 3, the affordable-and-adjacent block — add `TripUndoGate`
**after `SpendMovePoints` and before `RevealTile`**:

```csharp
if (isAdjacent && _state.MovePointsThisTurn >= RevealCost) {
    _previewedHex = null;
    _previewIsExplore = false;
    _previewLabel.Visible = false;
    _state.SpendMovePoints(RevealCost);
    _state.TripUndoGate();       // write snapshot + clear EventLog + fire UndoGateCrossed → ClearMovePath
    _map.RevealTile(unrevealedTile);  // also calls ClearMovePath (idempotent double-clear is fine)
    Log.Debug("[Input]", $"Tile '{unrevealedTile.TileId}' revealed from {coord.Q},{coord.R} remaining={_state.MovePointsThisTurn}");
}
```

`_state` is already injected — no Initialize change needed.

### Task 8 — Update `PlaceholderMainMenu` wiring

**File:** `scripts/ui/screens/PlaceholderMainMenu.cs`

**1. Wire `UndoGateCrossed → ClearMovePath`** (near other WorldMap subscriptions):
```csharp
_state.UndoGateCrossed += _worldMap.ClearMovePath;
```

**2. Update HandView.Initialize** — drop `_stagingManager`:
```csharp
handView.Initialize(_deckManager, _state, _effectScheduler, _inputLock);
```

**3. Update ImprovisationView.Initialize** — replace `_stagingManager` with `_effectScheduler`:
```csharp
_improvView.Initialize(_deckManager, _state, _effectScheduler, _inputLock);
```

### Task 9 — Run tests, fix fallout

```bash
dotnet test tests/maguswarrior.Tests.csproj
```

177 existing tests + 7 new tests (5 gate + 2 RecallFromDiscard) = **184+ green**.

**Expected fallout to investigate:**
- `EffectSystemTest.cs` / `ImprovisationEffectTest.cs` — if any test constructs `EffectContext`
  positionally, the new optional 5th parameter is backwards-compatible; no change needed.
  If any test constructs `EffectFiredEvent` without `CostCardId`, same — null default.
- Any test constructing `HandView` or `ImprovisationView` directly — update Initialize calls.
- `StagingManagerTest.cs` — **unaffected** (StagingManager class unchanged).

---

## Deferred Items

| Item | Reason / Owning Story |
|------|----------------------|
| Save snapshot to disk at gate crossing | Story 3-6 — `LastGateSnapshot` is in-memory only for now |
| Full multi-step effect undo (combat actions) | Epic 3+ — each combat effect will add EventLog entries; same mechanism applies |
| StagingManager removal from codebase | Keep it; StagingManagerTest.cs still valid; future epics may revive it |

**Resolved deferred item:** D2 from story 2-0 review — "`HandView.OnPlayRequested` not covered
by InputLock." ✅ **RESOLVED**: `OnPlayRequested` now acquires `_lock` in the same try/finally
pattern as `OnPlaySidewaysRequested`. Remove from `deferred-work.md` when story is done.

---

## Files Changed

| File | Change |
|------|--------|
| `tests/unit/UndoGateTest.cs` | **NEW** — 5 unit tests for TripUndoGate |
| `tests/unit/DeckManagerDiscardTest.cs` | 2 new tests for RecallFromDiscard |
| `scripts/core/GameEventLog.cs` | Add `Clear()` method |
| `scripts/core/GameState.cs` | Add `UndoGateCrossed`, `LastGateSnapshot`, `TripUndoGate()` |
| `scripts/cards/effects/EffectContext.cs` | Add optional `CostCardId` parameter |
| `scripts/cards/effects/EffectScheduler.cs` | Pass `CostCardId` through to `EffectFiredEvent` |
| `scripts/deck/DeckManager.cs` | Add `RecallFromDiscard(string cardId)` |
| `scripts/ui/components/StagingAreaView.cs` | Remove Commit button + CommitRequested signal; remove StagingManager; update Refresh |
| `scripts/ui/components/HandView.cs` | Remove StagingManager + OnCommitRequested; change OnPlayRequested (immediate); change OnUndoRequested (EventLog undo); update BuildEffect |
| `scripts/ui/components/ImprovisationView.cs` | Replace StagingManager with EffectScheduler; resolve immediately; pass CostCardId |
| `scripts/ui/components/HexMapView.cs` | Add `_state.TripUndoGate()` in tile-reveal branch |
| `scripts/ui/screens/PlaceholderMainMenu.cs` | Wire UndoGateCrossed; update Initialize calls |
| `_bmad-output/implementation-artifacts/deferred-work.md` | Mark D2 resolved |
| `_bmad-output/implementation-artifacts/sprint-status.yaml` | Mark 3-0 done (at review time) |

---

## Architecture Compliance Checklist

- [ ] `TripUndoGate`, `UndoGateCrossed`, `LastGateSnapshot` on pure C# `GameState` — no Godot dep
- [ ] `GameEventLog.Clear()` and `DeckManager.RecallFromDiscard()` are pure C# — no Godot dep
- [ ] `CostCardId?` is a nullable optional on both `EffectContext` and `EffectFiredEvent` — default null, so all existing callers compile unchanged
- [ ] `UndoGateCrossed` is `event Action?` — no Godot signals on pure C# classes
- [ ] `OnPlayRequested` and `OnResourceSelected` acquire `_lock`; release in `finally`
- [ ] `OnUndoRequested` checks `_lock.IsLocked` before mutating (synchronous — no acquisition needed, no deadlock risk)
- [ ] All `async void` are Godot signal handlers; each follows acquire/try/finally
- [ ] `Log.Debug` uses tags: `[Core]` for GameState, `[UI]` for views
- [ ] No per-card `if (card.Id == ...)` in BuildEffect — switch on `EffectType`
- [ ] `StagingManager` NOT deleted — tests still valid
- [ ] LOCKSTEP comment in `GameState.cs` not violated — `TripUndoGate` reads existing fields via `TakeSnapshot()`

---

## Definition of Done

- [ ] 7 new tests (5 gate + 2 RecallFromDiscard) written RED-first, then GREEN
- [ ] All 177 prior tests still green (no regression)
- [ ] `dotnet build maguswarrior.csproj` — 0 errors, 0 warnings
- [ ] Opus 4.8 code review passed
- [ ] Manual verification (WSLg desktop):
  - [ ] AC1: Play card → effect immediate, no Commit tap
  - [ ] AC2: No Commit button
  - [ ] AC3: Undo button undoes last card play (card returns to hand, total reverts)
  - [ ] AC4: Improvisation undo returns both Improv card and cost card to hand
  - [ ] AC6: Play card → move → reveal tile → Undo no-ops (EventLog cleared)
  - [ ] AC7: Improvisation → select resource → resolves immediately, view closes
  - [ ] AC10: Move undo via hex tap still works
  - [ ] AC11: Move undo blocked after tile reveal

---

## Review Findings

Opus 4.8 adversarial review (Blind Hunter + Edge Case Hunter + Acceptance Auditor), 2026-06-19.
Build clean, 184 tests green, all 11 ACs implemented. Findings below.

### Decisions needed

**RESOLVED 2026-06-19 (John): all three are handed to a dedicated successor story — `3-0b-unify-undo-controller` — to be created via create-story. They converge into one redesign: a tested pure-C# `UndoController` that owns a single ordered, group-aware undo stack for both card plays and hero moves. 3-0's own spec/ACs are met; this is net-new scope. Full context lives here + in deferred-work.md.**

- [x] [Review][Decision→Story 3-0b] **Undo of a multi-effect play only reverses the LAST effect** (blind+edge, High, latent) — chose (a) play-group grouping. `EffectScheduler.ResolveAll` appends one `EffectFiredEvent` per dequeued effect, including every `result.Triggered` child. One Play that yields triggered effects produces N log entries, but `OnUndoRequested` does `PopLast()` once — half-rollback. No effect emits `Triggered` today, but Epic 3 combat (attack/wound chains) is the source, so this must land before 3-1. [scripts/cards/effects/EffectScheduler.cs:20, scripts/ui/components/HandView.cs OnUndoRequested]
- [x] [Review][Decision→Story 3-0b] **Card-undo after a hero move desyncs move points from board position** (edge, Medium, reachable today) — chose (b) unify moves and card plays onto one ordered undo stack. Play Move, walk (spends points, hero relocates, no log entry), tap Undo → points rewind + card returns but hero stays moved = free move. Hero-move undo (today `WorldMap.UndoLastMove` + `HexMapView` Branch 1) reroutes through the new `UndoController`. [scripts/ui/components/HexMapView.cs:114-141, scripts/ui/components/HandView.cs OnUndoRequested]
- [x] [Review][Decision→Story 3-0b] **Core undo logic (AC3/AC4/AC5) has zero automated coverage** (auditor) — chose (a) extract undo into a pure-C# `UndoController` seam and unit-test it (red-first). Also makes the grouping above testable. [scripts/ui/components/HandView.cs OnUndoRequested]

### Patches

- [x] [Review][Patch] **Failed `RecallFromDiscard` still proceeds with `RestoreSnapshot` (partial rollback, cost card lost)** [scripts/ui/components/HandView.cs OnUndoRequested] — FIXED 2026-06-19: reordered `OnUndoRequested` to recall the cost card first; a recall failure now aborts the undo before `PopLast`/`ReturnCard`/`RestoreSnapshot`, so nothing is mutated. Build clean, 184 tests green.

### Deferred (pre-existing or per-spec; logged to deferred-work.md)

- [x] [Review][Defer] **`OnUndoRequested` checks `_lock.IsLocked` but never acquires it** [scripts/ui/components/HandView.cs] — per-spec (synchronous handler); same class as deferred D1/D2, only a race once awaitable effects land (UIBroker). Deferred.
- [x] [Review][Defer] **`StagingManager` is now dead production code with a live test** [scripts/deck/StagingManager.cs, tests/unit/StagingManagerTest.cs] — spec deliberately retained it for possible combat reuse; revisit when Epic 3 confirms. Deferred.
- [x] [Review][Defer] **Undo button always enabled, no disabled-state feedback** [scripts/ui/components/StagingAreaView.cs] — per-spec (handler no-ops on empty log); minor UX. Could disable when `EventLog.Events` is empty. Deferred.
- [x] [Review][Defer] **Undo recovers cards by Id, not by played instance** [scripts/ui/components/HandView.cs, scripts/deck/DeckManager.cs] — harmless while `CardDefinition` is value-by-Id; recalls an arbitrary match if duplicate Ids sit in discard, and returns null for cards not in the master list (e.g. Wounds, currently unplayable). Store the actual played instance in the event when hand-state hardening is tackled. Deferred.
- [x] [Review][Defer] **`UndoGateCrossed` has no unsubscribe on teardown** [scripts/ui/screens/PlaceholderMainMenu.cs] — single bootstrap today; latent handler leak once scene reload exists. Deferred.

### Dismissed (4)

Duplicate-card-in-discard after undo (false premise — `PlayCard` never adds to the discard pile); stale `_discardedCard` after early return (cleared by `Activate` on every entry); `LastGateSnapshot` captures post-spend state (per-spec, only consumer is `ClearMovePath`, save-checkpoint use deferred to 3-6); missing `_discardedCard` non-null assert (UI flow enforces discard-before-resource).
