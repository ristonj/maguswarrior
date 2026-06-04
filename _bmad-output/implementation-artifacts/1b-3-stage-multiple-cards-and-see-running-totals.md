# Story 1b.3: Stage Multiple Cards and See Running Totals

Status: done

## Story

As a player,
I want to stage multiple cards and see running totals,
so that I can plan before committing.

## Acceptance Criteria

1. `scripts/deck/StagingManager.cs` created — pure C#, namespace `MagusWarrior.Deck`, no Godot dependency:
   - `public record StagedEntry(CardDefinition Card, EffectType EffectType)` — nested public record
   - `public record StagingTotals(int Move, int Attack, int Block, int Influence)` — top-level in same namespace/file
   - `public event Action? StagingChanged` — fires after every `Stage()` and `Clear()` call
   - `public IReadOnlyList<StagedEntry> StagedCards` — read-only list (backed by `List<StagedEntry>`)
   - `public void Stage(CardDefinition card, EffectType effectType)` — appends to staged list, fires `StagingChanged`
   - `public void Clear()` — clears staged list, fires `StagingChanged`
   - `public StagingTotals GetTotals()` — sums `Unpowered.Move`, `Unpowered.Attack`, `Unpowered.Block`, `Unpowered.Influence` across all staged entries; entries with null `Unpowered` contribute 0 to all totals
   - Required usings: `using System; using System.Collections.Generic; using MagusWarrior.Cards; using MagusWarrior.Core.Types;`

2. `scripts/ui/components/StagingAreaView.cs` created — Godot `Control`, namespace `MagusWarrior.UI`:
   - `[Signal] public delegate void CommitRequestedEventHandler()`
   - Private fields: `_moveLabel`, `_attackLabel`, `_blockLabel`, `_influenceLabel` (all `Label`), `_commitButton` (`Button`), `_stagingManager` (`StagingManager`)
   - `_Ready()`: builds a full-width `HBoxContainer` containing the four resource labels and a "Commit" button; `AddThemeFontSizeOverride("font_size", 28)` on all labels; `AddThemeFontSizeOverride("font_size", 32)` on Commit button; commit button Pressed handler emits `CommitRequested` and logs `Log.Debug("[UI]", "Commit tapped")`
   - `public void Initialize(StagingManager staging)`: stores `_stagingManager = staging`; subscribes `staging.StagingChanged += Refresh`; calls `Refresh()` immediately
   - `private void Refresh()`: reads `_stagingManager.GetTotals()` and `_stagingManager.StagedCards.Count`; updates label text to `$"Move: {totals.Move}"`, `$"Attack: {totals.Attack}"`, `$"Block: {totals.Block}"`, `$"Influence: {totals.Influence}"`; sets `_commitButton.Disabled = _stagingManager.StagedCards.Count == 0`
   - Required usings: `using Godot; using MagusWarrior.Core; using MagusWarrior.Deck;`

3. `scripts/ui/components/HandView.cs` updated:
   - Add field: `private StagingManager _stagingManager = null!;`
   - Add field: `private StagingAreaView _stagingAreaView = null!;`
   - In `_Ready()`: after creating `_expandedPanel`, create `_stagingAreaView = new StagingAreaView(); _stagingAreaView.Name = "StagingAreaView"; AddChild(_stagingAreaView);` — position it identically to `_expandedPanel` (same anchor/offset values: `AnchorLeft=0, AnchorRight=1, AnchorTop=0, AnchorBottom=1, OffsetBottom=-130f, GrowHorizontal=Both, GrowVertical=Both`) with `ZIndex = 1` (lower than CardExpanded's `ZIndex = 3`, so CardExpanded overlays StagingAreaView when open)
   - `Initialize` signature extended: `public void Initialize(DeckManager deck, GameState state, EffectScheduler scheduler, StagingManager staging)`
     - Store `_stagingManager = staging`
     - Connect `_expandedPanel.PlayRequested += OnPlayRequested` (in addition to existing `PlaySidewaysRequested` connection)
     - Connect `_stagingAreaView.CommitRequested += OnCommitRequested`
     - Call `_stagingAreaView.Initialize(staging)` after subscribing `deck.HandChanged`
   - Add method `private void OnPlayRequested(string cardId)` — **NOT async, no effects applied during staging:**
     - `var card = _deck.Hand.FirstOrDefault(c => c.Id == cardId);`
     - If `card is null`: `Log.Debug("[UI]", $"OnPlayRequested: card '{cardId}' not found in hand — ignoring stale tap"); return;`
     - If `card.Unpowered is null`: `Log.Warn("[UI]", $"OnPlayRequested: card '{cardId}' has no unpowered spec — cannot stage"); return;`
     - `var result = _deck.PlayCard(cardId);` — remove card from hand FIRST (same atomicity discipline as `OnPlaySidewaysRequested`)
     - If `!result.IsSuccess`: `Log.Warn("[UI]", $"OnPlayRequested: PlayCard failed for '{cardId}': {result.Error}"); return;`
     - `_stagingManager.Stage(card, card.Unpowered.EffectType);`
     - `Log.Debug("[UI]", $"Play staged: {cardId} → {card.Unpowered.EffectType}");`
   - Add method `private async void OnCommitRequested()` — **async void accepted: Godot signal handler, same exception as `OnPlaySidewaysRequested`:**
     - If `_stagingManager.StagedCards.Count == 0`: return early (guard against spurious double-tap)
     - `foreach (var entry in _stagingManager.StagedCards.ToList())` — snapshot before clearing
       - `var effect = BuildEffect(entry);`
       - If `effect is null`: `Log.Warn("[UI]", $"OnCommitRequested: unsupported effect type {entry.EffectType} for card '{entry.Card.Id}' — skipping"); continue;`
       - `var ctx = new EffectContext(entry.Card.Id, entry.EffectType, _state.CurrentPhase, false);`
       - `_scheduler.Enqueue(effect, 0, ctx);`
     - `await _scheduler.ResolveAll(_state);`
     - `_stagingManager.Clear();`
     - `Log.Debug("[UI]", $"Commit resolved: {_stagingManager.StagedCards.Count} cards applied");`
     - Note: `Clear()` fires `StagingChanged` which triggers `StagingAreaView.Refresh()` — no explicit refresh needed
   - Add private static helper `BuildEffect(StagingManager.StagedEntry entry)`:
     ```csharp
     private static IEffect? BuildEffect(StagingManager.StagedEntry entry) {
         var spec = entry.Card.Unpowered;
         if (spec is null) return null;
         return entry.EffectType switch {
             EffectType.Move        => new MoveEffect(spec.Move),
             EffectType.AttackMelee => new AttackEffect(spec.Attack, EffectType.AttackMelee, AttackElement.Physical),
             EffectType.AttackRanged => new AttackEffect(spec.Attack, EffectType.AttackRanged, AttackElement.Physical),
             EffectType.Block       => new BlockEffect(spec.Block, AttackElement.Physical),
             EffectType.Influence   => new InfluenceEffect(spec.Influence),
             _                      => null
         };
     }
     ```
   - Add using: `using MagusWarrior.Deck;` (not yet present)

4. `scripts/ui/screens/PlaceholderMainMenu.cs` updated:
   - Add field: `private StagingManager _stagingManager = null!;`
   - In `_Ready()`: add `_stagingManager = new StagingManager();` immediately after `_effectScheduler = new EffectScheduler();`
   - Update `handView.Initialize(...)` call: `handView.Initialize(_deckManager, _state, _effectScheduler, _stagingManager)`
   - No new using needed — `StagingManager` is in `MagusWarrior.Deck`, already imported

5. `tests/unit/StagingManagerTest.cs` created — 6 xUnit tests:
   - `Stage_AddsCardToStagedCards` — after `Stage(march, Move)`, `StagedCards` has 1 entry with Card.Id == "march"
   - `Stage_FiresStagingChanged` — counter increments once when `Stage()` called
   - `Clear_EmptiesStagedCards` — after `Stage()` + `Clear()`, `StagedCards` is empty
   - `Clear_FiresStagingChanged` — counter increments when `Clear()` called (separate from any Stage() fires)
   - `GetTotals_SumsMoveTotals` — staging march(Move 2) + stamina(Move 2) → `totals.Move == 4`
   - `GetTotals_SumsMultipleResourceTypes` — staging march(Move 2) + threaten(Influence 2) → `Move==2, Influence==2, Attack==0, Block==0`
   - Namespace: `MagusWarrior.Tests`; required usings: `using System.Collections.Generic; using MagusWarrior.Cards; using MagusWarrior.Core.Types; using MagusWarrior.Deck; using Xunit;`

6. `tests/maguswarrior.Tests.csproj` updated — add compile entry:
   ```xml
   <Compile Include="../scripts/deck/StagingManager.cs" />
   ```
   `StagingAreaView.cs` and `HandView.cs` are **NOT** added (Godot dependencies).

7. `dotnet test tests/maguswarrior.Tests.csproj` passes — all 36 existing tests + 6 new tests = **42 green**. Zero warnings.

## Tasks / Subtasks

- [x] Task 1: Create StagingManager (AC: 1, 6, 5)
  - [x] Create `scripts/deck/StagingManager.cs` — pure C#, `StagedEntry` record, `StagingTotals` record, `Stage`, `Clear`, `GetTotals`, `StagingChanged` event
  - [x] Add `<Compile Include="../scripts/deck/StagingManager.cs" />` to `tests/maguswarrior.Tests.csproj`
  - [x] Create `tests/unit/StagingManagerTest.cs` with 6 tests
  - [x] Run `dotnet test` — confirm 6 new tests green (42 total)

- [x] Task 2: Create StagingAreaView (AC: 2)
  - [x] Create `scripts/ui/components/StagingAreaView.cs` — Godot Control, `CommitRequested` signal, `Initialize(StagingManager)`, `Refresh()` with labels and Commit button

- [x] Task 3: Wire HandView (AC: 3)
  - [x] Add `_stagingAreaView` and `_stagingManager` fields
  - [x] Create `_stagingAreaView` in `_Ready()` with correct anchors and ZIndex=1
  - [x] Extend `Initialize` signature to accept `StagingManager`
  - [x] Connect `PlayRequested += OnPlayRequested` and `CommitRequested += OnCommitRequested`
  - [x] Add `OnPlayRequested` (sync, stages card)
  - [x] Add `OnCommitRequested` (async void, resolves effects)
  - [x] Add `BuildEffect` static helper
  - [x] `using MagusWarrior.Deck;` already present — confirmed

- [x] Task 4: Update PlaceholderMainMenu (AC: 4)
  - [x] Add `_stagingManager = new StagingManager();`
  - [x] Pass `_stagingManager` to `handView.Initialize`

- [x] Task 5: Verify all tests and no regressions (AC: 7)
  - [x] Run `dotnet test tests/maguswarrior.Tests.csproj` — 42/42 green, 0 warnings, Godot build clean

## Dev Notes

### What This Story Does and Does NOT Do

**Does:**
- `StagingManager` (pure C#) — tracks staged cards, fires events, computes running totals from `EffectSpec` fields
- `StagingAreaView` (Godot UI) — displays Move/Attack/Block/Influence totals + Commit button
- Wire "Play" button → stage card (removes from `DeckManager.Hand`, enters `StagingManager`)
- "Commit" button → build effects from staged EffectSpecs, schedule, resolve, clear staging
- Running totals update in real time as cards are staged (via `StagingChanged` → `Refresh()`)

**Does NOT:**
- Undo of staged cards — Story 1b-4
- `choose_one` cards (Rage, Determination) — not in the test hand today; deferred
- Powered card staging — Power button stays greyed; Epic 6 (ManaPool)
- Phase indicator HUD — Epic 8
- Wound card restriction — Story 1b-5
- Rest turn — Story 1b-6
- Improvisation card — Story 1b-7

### Test Hand Reminder

`PlaceholderMainMenu` builds the test hand as `_state.Cards.Where(c => c.Type != CardType.Wound).Take(4)`. From `cards.yaml`, that yields **[march, stamina, threaten, promise]** in document order. In `GamePhase.Movement` (the default):

| Card | EffectType | Play enabled? |
|------|-----------|---------------|
| march | `Move` | ✅ (Move legal in Movement) |
| stamina | `Move` | ✅ |
| threaten | `Influence` | ❌ (Influence not legal in Movement) |
| promise | `Influence` | ❌ |

Only march and stamina have active Play buttons in the default phase. Their `Unpowered.Move = 2`, so staging both → **Move: 4** in the running totals.

### `OnPlayRequested` Is NOT Async

Staging does not apply any effects — no `async/await` needed. The card is removed from `DeckManager.Hand` (fires `HandChanged` → `RefreshHand` shrinks the hand display) and added to `StagingManager` (fires `StagingChanged` → `StagingAreaView.Refresh()` updates totals). Both are synchronous.

### `OnCommitRequested` Is `async void` — Accepted Exception

`CommitRequested` is a Godot signal. Signal handlers cannot return `Task`. This is the same accepted exception as `OnPlaySidewaysRequested` and `GameDebug.FireTestEffect` (documented in `deferred-work.md`). Safe because all current effects use `Task.FromResult` (synchronous path). Do NOT write `async void` anywhere else.

### Atomicity Pattern — Same Discipline as 1b-2

`OnPlayRequested` removes the card from hand FIRST (via `_deck.PlayCard(cardId)`), then stages it. This matches the 1b-2 review decision:  card leaves hand before any resource is granted, preventing double-bank and orphaned resources. If `PlayCard` fails, staging does not occur.

In `OnCommitRequested`, effects are applied only after the card is already gone from the hand (removed during staging). No further atomicity work required for this story.

### No New GameState Fields

Staging is entirely pre-commit. `StagingManager` lives outside `GameState`. No changes to `GameStateSnapshot`, `TakeSnapshot()`, `RestoreSnapshot()`, or the LOCKSTEP comment. When effects are committed via `ResolveAll`, the existing `AddMovePoints`, `AddAttackPoints`, etc. handle the GameState mutation — they are already LOCKSTEP-compliant from 1b-2.

### `BuildEffect` — Effect Type Mapping

`CardLoader.ParseEffectType` maps YAML strings to `EffectType`:
- `"move"` → `EffectType.Move`
- `"combat"` → `EffectType.AttackMelee` (important: "Combat" in YAML maps to AttackMelee)
- `"block"` → `EffectType.Block`
- `"influence"` → `EffectType.Influence`

`BuildEffect` switch must cover `AttackMelee`, `AttackRanged`, and `Block`. The `_ => null` arm is intentional — unsupported types log a Warn and skip, not throw. (Contrast with `OnPlaySidewaysRequested` which throws — there, `SidewaysRule.GetEffect` guarantees only the 4 covered types. Here, unknown types from staged cards are a more graceful failure path.)

### StagingAreaView Layout

`StagingAreaView` is a sibling to `CardExpanded` and `CardsContainer` inside `HandView`. It uses the same anchor position as `CardExpanded` (`AnchorTop=0, AnchorBottom=1, OffsetBottom=-130f`). `ZIndex = 1` while `CardExpanded.ZIndex = 3`, so `CardExpanded` overlays `StagingAreaView` when open. No show/hide coordination needed — they coexist by Z-ordering.

### `StagingManager.GetTotals()` Implementation

Sum directly from `Unpowered.Move/Attack/Block/Influence` fields on each staged entry's card. Do NOT sum based on `EffectType` — a card's spec may have multiple non-zero fields (even if uncommon in the starting deck). Using the raw EffectSpec fields ensures correctness for future cards.

```csharp
public StagingTotals GetTotals() {
    int move = 0, attack = 0, block = 0, influence = 0;
    foreach (var entry in _staged) {
        var spec = entry.Card.Unpowered;
        if (spec is null) continue;
        move      += spec.Move;
        attack    += spec.Attack;
        block     += spec.Block;
        influence += spec.Influence;
    }
    return new StagingTotals(move, attack, block, influence);
}
```

### `Initialize` Signature Change — One Caller

`HandView.Initialize` gains a 4th parameter: `StagingManager staging`. There is exactly **one caller**: `PlaceholderMainMenu._Ready()`. Update it to pass `_stagingManager`.

### HandView — New Usings Required

`HandView.cs` currently does NOT import `MagusWarrior.Deck` (DeckManager is referenced but via the field type from the same namespace chain — actually wait, let me check). Looking at the current HandView.cs — it already has `using MagusWarrior.Deck;` at line 12. `StagingManager` is also in `MagusWarrior.Deck`, so no new using is needed for `StagingManager`. The `StagingAreaView` type is in `MagusWarrior.UI` (same namespace as `HandView`), so no using needed for it either.

Wait — actually `HandView.cs` currently does NOT have `using MagusWarrior.Deck;`. Let me check the actual imports again:

```
using System;
using System.Linq;
using Godot;
using MagusWarrior.Cards;
using MagusWarrior.Cards.Effects;
using MagusWarrior.Cards.Effects.Combat;
using MagusWarrior.Cards.Effects.Influence;
using MagusWarrior.Cards.Effects.Movement;
using MagusWarrior.Core;
using MagusWarrior.Core.Types;
using MagusWarrior.Deck;
```

Line 12 of `HandView.cs` is `using MagusWarrior.Deck;`. ✅ Already present. No new using needed.

### StagingManager — No Effect Dependencies (by design)

`StagingManager` only imports `MagusWarrior.Cards` (for `CardDefinition`, `EffectSpec`) and `MagusWarrior.Core.Types` (for `EffectType`). It does NOT import effect class namespaces. This keeps the deck layer clean. The effect-building logic (`BuildEffect` switch) lives in `HandView` (UI orchestration layer), which already imports all effect namespaces.

### File Placement

| File | Location | Type |
|------|----------|------|
| `StagingManager.cs` | `scripts/deck/` | Pure C# |
| `StagingAreaView.cs` | `scripts/ui/components/` | Godot Control |
| `StagingManagerTest.cs` | `tests/unit/` | xUnit test |

### Test Patterns — StagingManagerTest.cs

```csharp
using System.Collections.Generic;
using MagusWarrior.Cards;
using MagusWarrior.Core.Types;
using MagusWarrior.Deck;
using Xunit;

namespace MagusWarrior.Tests;

public class StagingManagerTest {
    private static CardDefinition MakeCard(string id, EffectType type,
        int move = 0, int attack = 0, int block = 0, int influence = 0) =>
        new() {
            Id = id,
            Name = id,
            Type = CardType.BasicAction,
            Unpowered = new EffectSpec { EffectType = type, Move = move, Attack = attack,
                                         Block = block, Influence = influence }
        };

    [Fact]
    public void Stage_AddsCardToStagedCards() {
        var mgr = new StagingManager();
        mgr.Stage(MakeCard("march", EffectType.Move, move: 2), EffectType.Move);
        Assert.Single(mgr.StagedCards);
        Assert.Equal("march", mgr.StagedCards[0].Card.Id);
    }

    [Fact]
    public void Stage_FiresStagingChanged() {
        var mgr = new StagingManager();
        int count = 0;
        mgr.StagingChanged += () => count++;
        mgr.Stage(MakeCard("march", EffectType.Move, move: 2), EffectType.Move);
        Assert.Equal(1, count);
    }

    [Fact]
    public void Clear_EmptiesStagedCards() {
        var mgr = new StagingManager();
        mgr.Stage(MakeCard("march", EffectType.Move, move: 2), EffectType.Move);
        mgr.Clear();
        Assert.Empty(mgr.StagedCards);
    }

    [Fact]
    public void Clear_FiresStagingChanged() {
        var mgr = new StagingManager();
        mgr.Stage(MakeCard("march", EffectType.Move, move: 2), EffectType.Move);
        int count = 0;
        mgr.StagingChanged += () => count++;
        mgr.Clear();
        Assert.Equal(1, count);
    }

    [Fact]
    public void GetTotals_SumsMoveTotals() {
        var mgr = new StagingManager();
        mgr.Stage(MakeCard("march",   EffectType.Move, move: 2), EffectType.Move);
        mgr.Stage(MakeCard("stamina", EffectType.Move, move: 2), EffectType.Move);
        var totals = mgr.GetTotals();
        Assert.Equal(4, totals.Move);
    }

    [Fact]
    public void GetTotals_SumsMultipleResourceTypes() {
        var mgr = new StagingManager();
        mgr.Stage(MakeCard("march",   EffectType.Move,      move: 2),      EffectType.Move);
        mgr.Stage(MakeCard("threaten", EffectType.Influence, influence: 2), EffectType.Influence);
        var totals = mgr.GetTotals();
        Assert.Equal(2, totals.Move);
        Assert.Equal(2, totals.Influence);
        Assert.Equal(0, totals.Attack);
        Assert.Equal(0, totals.Block);
    }
}
```

### Carry-Forward Notes from 1b-2

- `async void` in signal handlers: accepted exception, documented in `deferred-work.md`. Do NOT use elsewhere.
- `ImplicitUsings=disable`: all using statements must be explicit in every `.cs` file.
- No `GD.Print` — use `Log.Debug/Warn/Error` with system tag `[UI]` for all HandView and StagingAreaView output.
- `DeckManager.PlayCard` removes first occurrence by index. If the same card ID appears twice (not possible with the test hand but theoretically), it removes only the first.
- The `HandChanged` event subscription is never unsubscribed — pre-existing deferred item from 1b-1, not re-opened here.

### Namespace Conventions

| Folder | Namespace |
|--------|-----------|
| `scripts/deck/` | `MagusWarrior.Deck` |
| `scripts/ui/components/` | `MagusWarrior.UI` |
| `tests/unit/` | `MagusWarrior.Tests` |

### References

- Epics: `_bmad-output/epics.md` §Epic 1b — staging area scope, deliverable description
- Architecture Screen Contract (Hand Display / Card Play Flow): `_bmad-output/game-architecture.md:812-820`
- Architecture Undo System (pre-commit only): `_bmad-output/game-architecture.md:323-325`
- Architecture Presentation Seam: `_bmad-output/game-architecture.md:367-374`
- Project Context Rules: `docs/project-context.md` (pure C# boundary, async/await, Result<T>, logging, file placement)
- Previous story (1b-2) Dev Notes — atomicity pattern, LOCKSTEP rule, async void accepted exception: `_bmad-output/implementation-artifacts/1b-2-play-card-sideways-for-basic-resource.md`
- Deferred work log (async void context, HandView unsubscribe): `_bmad-output/implementation-artifacts/deferred-work.md`
- CardLoader EffectType mapping ("combat" → AttackMelee): `scripts/cards/CardLoader.cs:79`
- DeckManager.PlayCard (remove first occurrence): `scripts/deck/DeckManager.cs`
- HandView.OnPlaySidewaysRequested (atomicity model to replicate): `scripts/ui/components/HandView.cs:92-121`
- CardExpanded signals (PlayRequested already defined): `scripts/ui/components/CardExpanded.cs:10`
- EffectSpec field layout (Move, Attack, Block, Influence ints): `scripts/cards/CardDefinition.cs`

### Project Context Rules

**Pure C# boundary:** `StagingManager.cs` is pure C# — no Godot dependency. Add to `tests/maguswarrior.Tests.csproj`. `StagingAreaView.cs` and updated `HandView.cs` are Godot nodes — do NOT add to test project.

**No service locator:** `StagingManager` is constructed in `PlaceholderMainMenu._Ready()` and injected into `HandView.Initialize` as a constructor/init parameter. Not accessed via static or singleton.

**async void only in Godot signal handlers:** `OnCommitRequested` is `async void`. `OnPlayRequested` is `void` (not async). No other `async void` permitted.

**Result<T>:** `DeckManager.PlayCard` returns `Result<CardDefinition>`. In `OnPlayRequested`, check `IsSuccess` before staging.

**Logging:** `Log.Debug("[UI]", ...)` for all normal flow. `Log.Warn("[UI]", ...)` for `PlayCard` failure and unsupported `BuildEffect` type. Never `GD.Print`.

**ImplicitUsings=disable:** All `.cs` files require explicit using statements. StagingManager.cs needs `using System;`, `using System.Collections.Generic;`, `using MagusWarrior.Cards;`, `using MagusWarrior.Core.Types;`. StagingAreaView.cs needs `using Godot;`, `using MagusWarrior.Core;`, `using MagusWarrior.Deck;`.

**Events are data-only:** `StagingChanged` is `event Action?` (no data needed — consumers call `GetTotals()` and `StagedCards` themselves). Do not put logic in the event payload.

**LOCKSTEP:** No new fields added to `GameState` in this story. The LOCKSTEP comment at `scripts/core/GameState.cs:48` does not need updating.

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

- All 5 tasks completed. 42/42 xUnit tests pass (36 existing + 6 new). Godot build: 0 errors, 0 warnings.
- `StagingManager.cs` created — pure C# in `scripts/deck/`; `StagedEntry` record, `StagingTotals` record, `Stage`, `Clear`, `GetTotals`, `StagingChanged` event. No effect class dependencies (deck layer stays clean).
- `StagingAreaView.cs` created — Godot Control in `scripts/ui/components/`; HBoxContainer with four resource labels (Move/Attack/Block/Influence) + Commit button; `CommitRequested` signal; `Initialize(StagingManager)` subscribes to `StagingChanged` → `Refresh()`.
- `HandView.cs` updated — `_stagingAreaView` created in `_Ready()` with ZIndex=1 (CardExpanded ZIndex=3 overlays it when open); `Initialize` signature extended with `StagingManager`; `OnPlayRequested` (sync, removes from hand then stages) and `OnCommitRequested` (async void, builds effects, resolves, clears) wired; static `BuildEffect` helper covers Move/AttackMelee/AttackRanged/Block/Influence with `null` fallback for unsupported types.
- `PlaceholderMainMenu.cs` updated — `_stagingManager = new StagingManager()` constructed alongside `_effectScheduler`; passed as 4th arg to `handView.Initialize`.
- `HandView.cs` already had `using MagusWarrior.Deck;` — no new using required.
- `OnPlayRequested` is NOT async — staging is synchronous (no effects applied). `OnCommitRequested` is async void — accepted Godot signal handler exception, same as `OnPlaySidewaysRequested`.
- Atomicity pattern from 1b-2 applied: `PlayCard` removes card from hand FIRST in `OnPlayRequested` before staging.

### File List

- `scripts/deck/StagingManager.cs` (new)
- `scripts/ui/components/StagingAreaView.cs` (new)
- `scripts/ui/components/HandView.cs` (modified — StagingAreaView, staging wiring)
- `scripts/ui/screens/PlaceholderMainMenu.cs` (modified — StagingManager field + injection)
- `tests/unit/StagingManagerTest.cs` (new)
- `tests/maguswarrior.Tests.csproj` (modified — StagingManager compile entry)

## Review Findings

Code review 2026-06-03 (Opus 4.8) — Blind Hunter + Edge Case Hunter + Acceptance Auditor. All 7 ACs functionally satisfied; 42/42 tests verified green. 1 patch, 5 defer, 6 dismissed. No decision-needed items — the headline divergence is spec-sanctioned and unreachable with the current test hand.

- [x] [Review][Patch] Commit re-entrancy — Commit button not disabled during async `ResolveAll`, enabling double-resolve [scripts/ui/components/HandView.cs] — **RESOLVED.** Added a `_committing` re-entrancy guard: `OnCommitRequested` returns early if already in flight, sets the flag, and clears it in a `finally`. Prevents a second tap from re-enqueueing the same `StagedCards` and double-applying every effect once `ResolveAll` becomes genuinely awaitable (UIBroker). Build clean, 42/42 tests green. Same class as the 1b-2 reorder patch — closed before async lands.

- [x] [Review][Defer] Running totals diverge from committed effect for multi-stat & `choose_one` cards [scripts/deck/StagingManager.cs GetTotals + scripts/ui/components/HandView.cs BuildEffect] — `GetTotals` sums all four `EffectSpec` fields (`Move+Attack+Block+Influence`) per staged card; `BuildEffect` applies only the single `entry.EffectType` field. For single-stat cards (the entire test hand: march/stamina/threaten/promise) they agree, so nothing fails today. They diverge for any card whose `Unpowered` spec carries more than one non-zero field. Worse: `choose_one` cards (Rage, Determination — the epic's named vertical-slice card) load with **all-zero** stat fields because `CardLoader.ConvertSpec` never reads `choose_one`, so staging Rage would show "Attack: 0" and Commit would apply nothing. **Both halves are spec-sanctioned** — AC1 mandates summing EffectSpec fields, AC3 mandates the EffectType switch, and Dev Notes explicitly defer `choose_one`. The fix (a unified representation of "what this staged card contributes," plus the choose-attack-or-block prompt) belongs to the `choose_one` story (1b-7 / combat scope). Flagging loudly because adding Rage to the test hand for the Epic 1b demo would surface a confusing silent zero. Deferred — design decision, not reachable now.

- [x] [Review][Defer] Unsupported `EffectType` staged card is removed from hand then silently dropped on Commit (data loss) [scripts/ui/components/HandView.cs OnPlayRequested + BuildEffect `_ => null`] — `OnPlayRequested` removes the card from hand (`PlayCard`) at stage time, but `BuildEffect` returns `null` at commit time for `Special`/`Heal`/`Banner`/`Mana`/`Crystal`/`Fame`/`Reputation`/`AttackSiege`; the entry is logged + `continue`d, then `Clear()` drops it. Net: card gone from hand, no effect, no recovery. Reachable for `Special` cards (their Play button is enabled because `PhaseGate` maps `Special` to `Any`), though none are in the test hand. The `remove-from-hand-first` atomicity model (correct for sideways, where the effect always succeeds) has a gap for staging, where buildability isn't checked until commit. Fix: validate buildability at stage time before removing from hand, or return un-buildable cards to hand on commit. Deferred — unreachable with the current test hand; ties to the broader card-type/phase-gate validation work.

- [x] [Review][Defer] No phase-legality gate inside `OnPlayRequested` [scripts/ui/components/HandView.cs OnPlayRequested] — the `CardExpanded` Play button is phase-gated (`PhaseGate.IsLegal`, disabled when illegal), but the signal handler itself performs no `PhaseGate`/`PhaseValidator` check before staging — contrast `OnPlaySidewaysRequested`'s `SidewaysRule.GetEffect(phase) is null` guard. Defense-in-depth gap only: reachable via a replayed/stale signal, not the normal UI flow. `CurrentPhase` is always `Movement` today (never reassigned). Deferred — button gate covers the live path; add a handler-side guard when phase transitions land.

- [x] [Review][Defer] `StagingChanged` / `HandChanged` subscriptions never unsubscribed [scripts/ui/components/StagingAreaView.cs Initialize + scripts/ui/components/HandView.cs Initialize] — `staging.StagingChanged += Refresh` and `deck.HandChanged += RefreshHand` have no matching `-=`/`_ExitTree`. `StagingManager`/`DeckManager` are created in `PlaceholderMainMenu` and outlive the view nodes; if a view is freed while a manager persists (screen rebuild), the handler fires on a freed Godot node. Single-instance screen today, so not triggered. Consistent with the existing documented `HandChanged`-unsubscribe deferral from 1b-1. Deferred — same lifecycle item, address when scene lifecycle gets complex.

- [x] [Review][Defer] `BuildEffect` / `PhaseGate` attack-distance coverage mismatch [scripts/ui/components/HandView.cs BuildEffect + scripts/cards/effects/PhaseGate.cs] — `BuildEffect` handles `AttackRanged` but not `AttackSiege`, while `PhaseGate` blesses both ranged and siege as legal in `CombatRanged`/`CombatMelee`. A Siege card would pass the Play-button gate once combat phases exist, stage, then drop to `null` on commit (same silent-consume as above). Inconsistent coverage across `PhaseGate`, `BuildEffect`, and the sideways switch. Deferred — combat-phase scope (Epic 3); unreachable while `CurrentPhase` is always `Movement`.

**Dismissed (6):** redundant `card.Unpowered is null` double-guard in `OnPlayRequested` + `BuildEffect` (intentional, harmless); `StagedCards => _staged` live-reference (the one mutating-iteration site uses `.ToList()`, safe); empty-staging guarded twice (verified safe — disabled button + `Count == 0` early return); `AttackElement.Physical` hardcoded (correct by design — basic/sideways resources are always physical; elements are Epic 3); Commit log message simplified from the spec's `"... {StagedCards.Count} cards applied"` to `"Commit resolved"` (improvement — the spec's version logs after `Clear()` and would always print `0`); warn-message wording omitting the word "card" + AC7 count-not-provable-from-diff (cosmetic; 42/42 confirmed green by the reviewer).
