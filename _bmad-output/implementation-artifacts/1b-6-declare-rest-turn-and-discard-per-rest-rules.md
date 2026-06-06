# Story 1b.6: Declare Rest Turn and Discard per Rest Rules

Status: done

## Story

As a player,
I want to declare a Rest turn and discard per rest rules (Standard Rest: discard 1 non-Wound + any Wounds; Exhaustion: only when all Wounds, discard exactly 1 Wound),
so that hand recovery is correctly governed — no movement, combat, or Influence during rest; Special and Healing effects still allowed.

## Acceptance Criteria

1. `scripts/cards/RestRule.cs` created — pure C#, namespace `MagusWarrior.Cards`, no Godot dependency:
   - `public static class RestRule`
   - `public static bool CanDeclareRest(IReadOnlyList<CardDefinition> hand)` → `hand.Count > 0`
   - `public static bool IsStandardRest(IReadOnlyList<CardDefinition> hand)` → `true` if `hand.Any(c => c.Type != CardType.Wound)` (at least one non-Wound in hand)
   - `public static bool IsExhaustion(IReadOnlyList<CardDefinition> hand)` → `true` if `hand.Count > 0 && hand.All(c => c.Type == CardType.Wound)` (non-empty, all Wounds)
   - Required usings: `System.Collections.Generic`, `System.Linq`

2. `scripts/deck/DeckManager.cs` updated — adds discard pile (no existing signatures change):
   - `public IReadOnlyList<CardDefinition> DiscardPile { get; private set; } = Array.Empty<CardDefinition>();`
   - `public Result<CardDefinition> DiscardCard(string cardId)`:
     - Finds first card in `Hand` with matching `cardId`; if not found → `Result.Fail($"Card '{cardId}' not in hand")`
     - Removes it from hand and calls `SetHand(updatedList)` (fires `HandChanged`)
     - Appends the discarded card to `DiscardPile`
     - Returns `Result.Ok(discarded)`
   - `DiscardPile` does NOT fire a separate event — callers that need to refresh on discard subscribe to `HandChanged`

3. `scripts/core/GameState.cs` updated — adds public phase setter:
   - `public void SetPhase(GamePhase phase) { CurrentPhase = phase; }`
   - `CurrentPhase` is already in `GameStateSnapshot` and restored via `RestoreSnapshot` — no snapshot change required
   - Update the LOCKSTEP comment to mention `SetPhase` as the new mutator for `CurrentPhase` (comment update only)

4. `scripts/ui/components/HandView.cs` updated — Rest-aware guard in `OnCardTapped` prevents non-legal cards from opening CardExpanded during Rest:
   - After the wound guard (and before `_expandedPanel.Open`), add:
     ```csharp
     if (_state.CurrentPhase == GamePhase.Rest) {
         bool hasLegalPlay = card.Unpowered != null
             && PhaseGate.IsLegal(card.Unpowered.EffectType, GamePhase.Rest);
         if (!hasLegalPlay) {
             Log.Debug("[UI]", $"OnCardTapped: '{cardId}' — no legal play in Rest phase; use rest controls to discard");
             return;
         }
     }
     ```
   - Effect: Move/Attack/Block/Influence cards are blocked from opening CardExpanded during Rest (they have no legal play in Rest). Heal and Special cards fall through — their Play button in CardExpanded is enabled by PhaseGate (`Heal` legal in Rest; `Special` legal in `Any`).
   - `OnPlayRequested`, `OnPlaySidewaysRequested`, `OnCommitRequested`, `OnUndoRequested` — unchanged.
   - `PhaseGate` is already imported via `using MagusWarrior.Cards.Effects;` (line 7). `GamePhase` is already imported via `using MagusWarrior.Core.Types;` (line 9). No new usings.

5. `scripts/ui/components/RestView.cs` created — Godot `Control`, namespace `MagusWarrior.UI`:
   - `public partial class RestView : Control`
   - Fields: `_deck`, `_state`, `_declareRestButton` (Button), `_restPanel` (VBoxContainer), `_statusLabel` (Label), `_endRestButton` (Button), `_nonWoundDiscarded` (bool), `_inExhaustion` (bool)
   - `public void Initialize(DeckManager deck, GameState state)`:
     - Assigns `_deck` and `_state`
     - Subscribes `deck.HandChanged += RefreshView`
     - Calls `RefreshView()` once to set initial button states
   - **Layout (built in `_Ready`):**
     - Anchor full-width top strip (AnchorLeft=0, AnchorRight=1, AnchorTop=0, AnchorBottom=0, OffsetBottom=160f, GrowHorizontal=Both, GrowVertical=End)
     - `_declareRestButton` (Button, text "Declare Rest", font size 32) — top of strip
     - `_restPanel` (VBoxContainer) below button, hidden until Rest is declared; contains:
       - `_statusLabel` (Label, font size 26) — shows "Standard Rest: discard 1 non-Wound card" or "Exhaustion: discarding 1 Wound…"
       - One row per card (HBoxContainer): Label showing card name + Discard button
     - `_endRestButton` (Button, text "End Rest", font size 32) — at bottom of `_restPanel`
   - **`OnDeclareRestPressed()`** (called on `_declareRestButton.Pressed`):
     - Calls `_state.SetPhase(GamePhase.Rest)`
     - Logs `Log.Debug("[UI]", $"Rest declared: {(_isExhaustion ? "Exhaustion" : "Standard Rest")}")`
     - If `RestRule.IsExhaustion(_deck.Hand)`:
       - Sets `_inExhaustion = true`
       - Auto-discards first Wound: `_deck.DiscardCard("wound")` — `HandChanged` fires and `RefreshView` rebuilds the panel
       - Sets `_nonWoundDiscarded = true` (sentinel reused: means "discard requirement met")
       - Enables `_endRestButton`
       - `_statusLabel.Text = "Exhaustion rest — Wound discarded"`
     - If `RestRule.IsStandardRest(_deck.Hand)`:
       - Sets `_inExhaustion = false`, `_nonWoundDiscarded = false`
       - Disables `_endRestButton` (gated until non-Wound discarded)
       - `_statusLabel.Text = "Standard Rest: discard 1 non-Wound card"`
       - Calls `BuildDiscardRows()` to populate card rows
     - Shows `_restPanel`, disables `_declareRestButton`
   - **`BuildDiscardRows()`** — clears and rebuilds card rows in `_restPanel` (before `_endRestButton`):
     - For each non-Wound card in `_deck.Hand`: add row with card name label + "Discard" button (always enabled in Standard Rest)
     - For each Wound card in `_deck.Hand`: add row with card name label + "Discard" button (enabled only if `_nonWoundDiscarded == true`)
     - Each "Discard" button's `Pressed` signal: `() => OnDiscardPressed(card.Id, card.Type)`
   - **`OnDiscardPressed(string cardId, CardType cardType)`:**
     - Calls `var r = _deck.DiscardCard(cardId)` — if `!r.IsSuccess`, logs `Log.Warn("[UI]", $"DiscardCard failed: {r.Error}")` and returns
     - If `cardType != CardType.Wound`: sets `_nonWoundDiscarded = true`, enables `_endRestButton`, updates `_statusLabel.Text` to "Non-Wound discarded — optionally discard Wounds, then End Rest"
     - Logs `Log.Debug("[UI]", $"Rest discard: {cardId} (wound={cardType == CardType.Wound})")`
     - `HandChanged` fires → `RefreshView` rebuilds rows automatically
   - **`RefreshView()`:**
     - Updates `_declareRestButton.Disabled` = `_state.CurrentPhase == GamePhase.Rest || !RestRule.CanDeclareRest(_deck.Hand)`
     - If `_state.CurrentPhase == GamePhase.Rest && !_inExhaustion`: calls `BuildDiscardRows()` to reflect current hand after each discard
   - **`OnEndRestPressed()`** (called on `_endRestButton.Pressed`):
     - Calls `_state.SetPhase(GamePhase.Movement)`
     - Resets `_nonWoundDiscarded = false`, `_inExhaustion = false`
     - Hides `_restPanel`, re-enables `_declareRestButton`, disables `_endRestButton`
     - Logs `Log.Debug("[UI]", "Rest completed — phase returned to Movement")`
     - Calls `RefreshView()` to update button enabled state
   - Required usings: `Godot`, `MagusWarrior.Cards`, `MagusWarrior.Core`, `MagusWarrior.Core.Types`, `MagusWarrior.Deck`, `System.Linq`
   - NOT added to the test project (Godot dependency)

6. `scripts/ui/screens/PlaceholderMainMenu.cs` updated — wire up RestView:
   - Add field: `private RestView _restView = null!;`
   - In `_Ready()`, after `handView.Initialize(_deckManager, _state, _effectScheduler, _stagingManager);`, add:
     ```csharp
     _restView = new RestView();
     _restView.Name = "RestView";
     AddChild(_restView);
     _restView.Initialize(_deckManager, _state);
     ```
   - No new using required — `RestView` is in `MagusWarrior.UI` (already imported)
   - Test hand unchanged: still `[march, stamina, threaten, promise, wound]` — covers Standard Rest on device (4 non-Wounds present)

7. `tests/unit/RestRuleTest.cs` created — 9 xUnit tests:
   - `CanDeclareRest_EmptyHand_ReturnsFalse`
   - `CanDeclareRest_NonEmptyHand_ReturnsTrue`
   - `IsStandardRest_HandWithNonWound_ReturnsTrue`
   - `IsStandardRest_MixedHand_ReturnsTrue` (at least one non-Wound among multiple Wounds)
   - `IsStandardRest_AllWounds_ReturnsFalse`
   - `IsStandardRest_EmptyHand_ReturnsFalse`
   - `IsExhaustion_AllWounds_ReturnsTrue`
   - `IsExhaustion_MixedHand_ReturnsFalse`
   - `IsExhaustion_EmptyHand_ReturnsFalse`
   - Namespace: `MagusWarrior.Tests`; usings: `MagusWarrior.Cards`, `Xunit`, `System.Collections.Generic`

8. `tests/unit/DeckManagerDiscardTest.cs` created — 5 xUnit tests:
   - `DiscardPile_IsEmpty_OnConstruction`
   - `DiscardCard_RemovesCardFromHand_AddsToDiscardPile`
   - `DiscardCard_WhenCardNotInHand_ReturnsFailure_AndPileUnchanged`
   - `DiscardCard_FiresHandChanged`
   - `DiscardCard_PreservesOtherHandCards`
   - Namespace: `MagusWarrior.Tests`; usings: `MagusWarrior.Cards`, `MagusWarrior.Core`, `MagusWarrior.Deck`, `Xunit`, `System.Collections.Generic`, `System`

9. `tests/maguswarrior.Tests.csproj` updated — add compile entry:
   ```xml
   <Compile Include="../scripts/cards/RestRule.cs" />
   ```
   `DeckManager.cs` and `GameState.cs` are already compiled. `RestView.cs`, `HandView.cs`, `PlaceholderMainMenu.cs` are NOT added (Godot dependencies). `RestRuleTest.cs` and `DeckManagerDiscardTest.cs` are auto-discovered by the test SDK.

10. `dotnet test tests/maguswarrior.Tests.csproj` passes — all 51 existing tests + 14 new tests = **65 green**. Zero warnings. `dotnet build maguswarrior.csproj` → 0 errors, 0 warnings.

## Tasks / Subtasks

- [x] Task 1: RestRule pure-C# logic (AC: 1, 7, 9)
  - [x] Create `scripts/cards/RestRule.cs` — `CanDeclareRest`, `IsStandardRest`, `IsExhaustion`
  - [x] Add `<Compile Include="../scripts/cards/RestRule.cs" />` to `tests/maguswarrior.Tests.csproj`
  - [x] Create `tests/unit/RestRuleTest.cs` — 9 tests
  - [x] Run `dotnet test` — confirm 9 new tests green (60 total)

- [x] Task 2: DeckManager discard pile (AC: 2, 8)
  - [x] Add `DiscardPile` property and `DiscardCard` method to `scripts/deck/DeckManager.cs`
  - [x] Create `tests/unit/DeckManagerDiscardTest.cs` — 5 tests
  - [x] Run `dotnet test` — confirm 5 new tests green (65 total)

- [x] Task 3: GameState.SetPhase (AC: 3)
  - [x] Add `public void SetPhase(GamePhase phase)` to `scripts/core/GameState.cs`
  - [x] Update LOCKSTEP comment to list `SetPhase` as `CurrentPhase` mutator
  - [x] Run `dotnet test` — all 65 still green (no regression)

- [x] Task 4: HandView Rest guard (AC: 4)
  - [x] Add phase-aware guard in `OnCardTapped` (after wound check, before `_expandedPanel.Open`)
  - [x] `dotnet build maguswarrior.csproj` — 0 errors, 0 warnings

- [x] Task 5: RestView Godot component + PlaceholderMainMenu wiring (AC: 5, 6)
  - [x] Create `scripts/ui/components/RestView.cs` with Declare Rest button, discard panel, End Rest button
  - [x] Wire up in `PlaceholderMainMenu._Ready()` after `handView.Initialize`
  - [x] `dotnet build maguswarrior.csproj` — 0 errors, 0 warnings

- [x] Task 6: Full regression check (AC: 10)
  - [x] Run `dotnet test tests/maguswarrior.Tests.csproj` — 65/65 green, 0 warnings
  - [x] Run `dotnet build maguswarrior.csproj` — 0 errors, 0 warnings

## Dev Notes

### What This Story Does and Does NOT Do

**Does:**
- `RestRule` (pure C#) — determines which rest type applies from hand composition
- `DeckManager.DiscardCard` — first discard-pile operation; hand card removed, appended to `DiscardPile`
- `GameState.SetPhase` — explicit phase-transition API (phase was only mutated via `RestoreSnapshot` before this story)
- `RestView` (Godot) — Declare Rest button → Standard Rest discard flow (select 1 non-Wound + optional Wounds) or Exhaustion auto-discard (1 Wound immediately) → End Rest returns to Movement
- `HandView` guard — blocks Move/Attack/Block/Influence cards from opening CardExpanded during Rest; Heal and Special cards still expand normally (their Play button is enabled by PhaseGate)
- Standard Rest path fully on-device-verifiable with the existing test hand `[march, stamina, threaten, promise, wound]`

**Does NOT:**
- Full `TurnManager` / `TurnState` loop (Epic 7). The `TurnState.IsRest` flag from the LLD is not added here; `GameState.CurrentPhase == GamePhase.Rest` serves as the equivalent in the placeholder.
- Exhaustion on-device verification — current test hand always satisfies Standard Rest (4 non-Wounds + 1 Wound). Exhaustion is unit-tested via `RestRuleTest` and `DeckManagerDiscardTest`.
- "Slow Recovery emotional design" (UX spec §1963 — weighted animation, distinct pacing) — functional auto-discard only; animation is a UX-polish pass.
- Space benefits during Rest (Magical Glade wound discard, Crystal Mine crystal gain) — End-of-Turn hooks in Epic 6.
- Deck reshuffle (DiscardPile → Deck) — `DiscardPile` accumulates correctly but no reshuffle logic exists yet.
- End-of-Round declaration — Epic 7.
- Wound healing (permanent removal from deck) — Epic 4/5 site interactions.

### Rest Rules (Critical — Do Not Misread)

From GDD (lines 330-331, 526-527) and LLD §8.1:

| Condition | Rest Type | Mandatory Discard |
|-----------|-----------|-------------------|
| Hand has ≥1 non-Wound | Standard Rest | Exactly 1 non-Wound + any number of Wounds (optional) |
| All cards are Wounds | Exhaustion | Exactly 1 Wound (auto-discarded in RestView) |
| Empty hand | Cannot declare Rest | N/A |

**"Standard Rest" and "Exhaustion" are mutually exclusive** — IsStandardRest and IsExhaustion cannot both be true for the same hand.

**The mandatory non-Wound discard in Standard Rest is separate from playing Heal/Special effects.** If the player plays a Heal card during Rest (legal via PhaseGate), the End Rest button remains gated until they also explicitly discard a non-Wound card via RestView. The Heal card going to the discard pile (via `PlayCard`) does NOT satisfy the mandatory rest discard. These are different game actions.

The discard in Standard Rest goes to `DiscardPile` (not permanent removal — wounds cycle back on next shuffle; non-wounds also cycle back unlike "throw away"). This matches the GDD distinction: rest-discard ≠ heal-discard ≠ throw-away.

### Why SetPhase Lives in GameState

Phase is game logic, not UI state. The "pure C# boundary" prohibits phase tracking in any Godot node. Before this story, `CurrentPhase` was only mutated via `RestoreSnapshot` (undo). `SetPhase` is the first explicit phase-mutation API for the normal turn loop. When `TurnManager` lands (Epic 7), it will drive phase transitions exclusively via `SetPhase` — this story's usage pattern is the same one TurnManager will use.

### Why DiscardCard Is on DeckManager

`DeckManager` owns both `Hand` and `DiscardPile` — they are two zones of the same deed deck. Splitting into a separate DiscardManager would require cross-manager coordination for reshuffling (DiscardPile → Deck). Keeping them together aligns with how `PlayCard` and `ReturnCard` work.

Note: `PlayCard` removes a card from hand to play it (effect applied, card goes to discard at end of turn via a future mechanism). `DiscardCard` removes a card from hand to the discard pile directly (rest discard path). These are semantically different game actions that happen to share the same destination (discard pile) for now.

### HandView Guard — Placement and Precedence

The guard in `OnCardTapped` reads:
```csharp
if (card is null) { ... return; }                // existing: stale tap
if (card.Type == CardType.Wound) { ... return; } // existing: 1b-5
if (_state.CurrentPhase == GamePhase.Rest) {     // NEW: this story
    bool hasLegalPlay = card.Unpowered != null
        && PhaseGate.IsLegal(card.Unpowered.EffectType, GamePhase.Rest);
    if (!hasLegalPlay) {
        Log.Debug("[UI]", $"OnCardTapped: '{cardId}' — no legal play in Rest phase; use rest controls to discard");
        return;
    }
}
_expandedPanel.Open(card, _state.CurrentPhase);  // existing
```

Order matters: wound check stays first (wounds are always blocked regardless of phase — 1b-5 invariant). The Rest guard comes after because it only applies to non-Wound, phase-restricted cards.

The test hand `[march, stamina, threaten, promise, wound]` has no Heal/Special cards, so during Rest, ALL non-Wound taps will be blocked and fall through to RestView. This is the correct behavior for the current test scaffold.

### CardExpanded Is Already Phase-Aware

`CardExpanded.Open(card, currentPhase)` already disables the Play button for cards whose `Unpowered.EffectType` is illegal in the given phase (line 92: `_playButton.Disabled = !PhaseGate.IsLegal(...)`). For Heal/Special cards reaching CardExpanded during Rest, Play is enabled. Sideways is enabled but `SidewaysRule.GetEffect(GamePhase.Rest)` returns `null` → HandView logs "no sideways effect in Rest" and returns harmlessly. This is existing behavior — no changes needed to CardExpanded.

### Exhaustion Auto-Discard

In the Exhaustion path, `RestView.OnDeclareRestPressed()` calls `_deck.DiscardCard("wound")` immediately. All wounds share `Id = "wound"` (fungible per GDD line 453). `DiscardCard` removes the first matching card — correct and intentional. The player has no choice in Exhaustion (rulebook: "reveal hand, discard exactly 1 Wound"). The `HandChanged` event fires from inside `DiscardCard` → `RefreshView` rebuilds the panel showing the remaining wounds (if any) without discard buttons (since we're in Exhaustion and one wound was already auto-discarded).

### RestView Discard Panel Rebuild

After each `DiscardCard` call, `HandChanged` fires → `RefreshView()` is called. `RefreshView()` calls `BuildDiscardRows()` to rebuild the card rows from the current hand. This means:
- The discard panel always reflects the actual current hand (no stale buttons)
- After discarding the last non-Wound card in Standard Rest, the panel shows only Wound rows (if any remain)
- After all Wounds are discarded, the panel is empty and End Rest is still enabled (discard requirement was already met)

### LOCKSTEP Discipline

`SetPhase` mutates `CurrentPhase`. `CurrentPhase` is already captured by `TakeSnapshot()` and restored by `RestoreSnapshot()`. The undo mechanism (1b-4) rolls back card effects — it does NOT roll back phase transitions. This is correct: Rest is a turn-level declaration, not an effect event. A player cannot undo "declaring rest." When TurnManager lands, it will manage phase as a turn-level concern separately from effect undo.

`DeckManager.DiscardCard` mutates `Hand` and `DiscardPile`. `Hand` is NOT currently in `GameStateSnapshot` (deferred — noted in LOCKSTEP comment at `GameState.cs:48`). This means discarding a card via Rest is not undoable — but rest discard is explicitly an undo gate (new information revealed: you've chosen what to discard). Correct by design.

### Deferred Items to Add to deferred-work.md

1. **Latent: stale Play signal during Rest** — if a ghost tap from a prior `CardTapped` signal propagates after `OnDeclareRestPressed` (same Godot signal-replay risk noted in 1b-1), a non-Heal card could reach `OnPlayRequested` during Rest. The card has no staging guard for phase during Rest. Mitigation: `OnCommitRequested` resolves effects against `_state.CurrentPhase` at commit time — if `PhaseGate.IsLegal` is checked at that point, it would fail. But currently `OnCommitRequested` does not check phase gate — it delegates to `BuildEffect`. Harmless today; log-and-track.

2. **Deck reshuffle**: `DiscardPile` accumulates but is never reshuffled into the deck. Draw actions (card draw effects) would draw from an empty deck. Track for story that implements deck cycling.

### Carry-Forward Notes from 1b-5

- `async void` only in Godot signal handlers that await. All new RestView handlers are synchronous `void` — no await needed (all DeckManager/GameState calls are synchronous).
- `ImplicitUsings=disable`: all using statements must be explicit in every `.cs` file.
- No `GD.Print` — use `Log.Debug`/`Log.Warn` with appropriate system tag. Use `[UI]` in RestView.
- Subscription leak: `deck.HandChanged += RefreshView` is never unsubscribed when RestView is freed. Same deferred pattern as existing `HandView` subscription (`deck.HandChanged += RefreshHand` is never unsubscribed). Track in deferred-work.md if it surfaces.

### Test Construction Patterns (Follow These)

From `DeckManagerTest.cs`:
```csharp
var card = new CardDefinition { Id = "march", Name = "March" };                    // non-Wound
var wound = new CardDefinition { Id = "wound", Name = "Wound", Type = CardType.Wound };
var dm = new DeckManager();
dm.SetHand(new[] { card });
```

From `RestRuleTest.cs` (new):
```csharp
var nonWound = new CardDefinition { Id = "march", Name = "March" };                // Type defaults to BasicAction (0)
var wound    = new CardDefinition { Id = "wound", Name = "Wound", Type = CardType.Wound };
// Test helper: explicit IReadOnlyList
IReadOnlyList<CardDefinition> hand = new[] { nonWound, wound };
```

### File Placement

| File | Action | Type |
|------|--------|------|
| `scripts/cards/RestRule.cs` | new | Pure C# |
| `scripts/deck/DeckManager.cs` | modified — add DiscardPile + DiscardCard | Pure C# |
| `scripts/core/GameState.cs` | modified — add SetPhase + comment | Pure C# |
| `scripts/ui/components/HandView.cs` | modified — Rest guard in OnCardTapped | Godot Control |
| `scripts/ui/components/RestView.cs` | new | Godot Control |
| `scripts/ui/screens/PlaceholderMainMenu.cs` | modified — wire RestView | Godot CanvasLayer |
| `tests/unit/RestRuleTest.cs` | new — 9 tests | xUnit |
| `tests/unit/DeckManagerDiscardTest.cs` | new — 5 tests | xUnit |
| `tests/maguswarrior.Tests.csproj` | modified — add RestRule compile entry | project |

### Namespace Conventions

| Folder | Namespace |
|--------|-----------|
| `scripts/cards/` | `MagusWarrior.Cards` |
| `scripts/deck/` | `MagusWarrior.Deck` |
| `scripts/core/` | `MagusWarrior.Core` |
| `scripts/ui/components/` | `MagusWarrior.UI` |
| `scripts/ui/screens/` | `MagusWarrior.UI` |
| `tests/unit/` | `MagusWarrior.Tests` |

### Project Context Rules

**Pure C# boundary:** `RestRule.cs` — no Godot dependency, added to test csproj. `RestView.cs`, `HandView.cs`, `PlaceholderMainMenu.cs` — Godot nodes, NOT in the test project.

**Constructor injection / No service locator:** `RestView.Initialize(DeckManager, GameState)` follows the same pattern as `HandView.Initialize`. `RestRule` is a static utility class (no instance needed).

**async/await — no new async methods:** All RestView event handlers are synchronous `void`. The `_deck.DiscardCard` and `_state.SetPhase` calls are synchronous.

**Result<T>:** `DiscardCard` returns `Result<CardDefinition>`. `RestView.OnDiscardPressed` checks `result.IsSuccess` before proceeding; on failure it logs `Log.Warn` and returns. Never throw for "card not in hand."

**Logging tags:** `Log.Debug("[UI]", ...)` in RestView and HandView. No logging inside DeckManager.cs (callers log). Never `GD.Print`.

**Events are data-only:** No new C# events or Godot signals introduced in this story.

**No hardcoded per-card logic:** `RestRule` operates on `card.Type` (type-level gate — same exemption as 1b-5). `DiscardCard` takes cardId (data-driven).

**LOCKSTEP:** `SetPhase` is documented as a `CurrentPhase` mutator; `CurrentPhase` is already in the snapshot — no snapshot change. Snapshot comment updated (comment-only change).

### References

- GDD Rest rules: `_bmad-output/gdd.md:330-331`, `:526-527`
- Turn Structure LLD §8.1 (Rest Turn): `docs/turn-structure-lld.md` §8.1
- Turn Structure LLD §2.2 (TurnState.IsRest — spec, not yet implemented): `docs/turn-structure-lld.md` §2.2
- Turn Structure LLD §2.6 (GamePhase additions including TurnStart, ActionDeclaration): `docs/turn-structure-lld.md` §2.6
- Architecture Phase Gate table (Heal in Rest; Special in Any): `_bmad-output/game-architecture.md:756-773`
- Architecture Screen Contract (End-of-Turn / Cleanup — for contrast): `_bmad-output/game-architecture.md:843-848`
- UX spec RestDeclarationPrompt (§1651-1659): `_bmad-output/planning-artifacts/ux-design-specification.md:1651`
- UX spec RestChoiceAffordance (§1663-1678): `_bmad-output/planning-artifacts/ux-design-specification.md:1663`
- UX spec Slow Recovery emotional design note: `_bmad-output/planning-artifacts/ux-design-specification.md:963`
- UX spec turn-loop flow (Rest or Regular branch): `_bmad-output/planning-artifacts/ux-design-specification.md:947-965`
- Project Context Rules: `docs/project-context.md`
- Previous story (1b-5) Dev Notes — wound guards, sync void handlers, LOCKSTEP: `_bmad-output/implementation-artifacts/1b-5-cannot-tap-wound-card.md`
- Deferred work tracker: `_bmad-output/implementation-artifacts/deferred-work.md`
- `DeckManager.cs` current state: `scripts/deck/DeckManager.cs`
- `GameState.cs` current state + LOCKSTEP comment: `scripts/core/GameState.cs:48`
- `HandView.cs` `OnCardTapped` insertion point: `scripts/ui/components/HandView.cs:104-120`
- `CardExpanded.Open` phase-aware Play button: `scripts/ui/components/CardExpanded.cs:90-93`
- `PhaseGate.IsLegal` signature: `scripts/cards/effects/PhaseGate.cs:28`
- `SidewaysRule.GetEffect` returns null for Rest: `scripts/cards/SidewaysRule.cs`
- `WoundCard` wound Id: `scripts/cards/WoundCard.cs` (Id = "wound")
- `CardType` enum values: `scripts/cards/CardDefinition.cs:7-11`
- Existing DeckManager tests (pattern reference): `tests/unit/DeckManagerTest.cs`

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

- All 6 tasks completed. 65/65 xUnit tests pass (51 existing + 9 RestRuleTest + 5 DeckManagerDiscardTest). Godot build: 0 errors, 0 warnings.
- `RestRule.cs` created — pure C# static class in `scripts/cards/`; `CanDeclareRest`, `IsStandardRest`, `IsExhaustion` implement hand-composition logic from GDD §8.1. Added to test csproj compile list.
- `DeckManager.cs` updated — `DiscardPile` property + `DiscardCard(string cardId)` method added. `DiscardCard` removes from hand (fires `HandChanged`) and appends to `DiscardPile`. No existing signatures changed.
- `GameState.cs` updated — `public void SetPhase(GamePhase phase)` added. LOCKSTEP comment updated to name `SetPhase` as the `CurrentPhase` mutator. `CurrentPhase` was already in `GameStateSnapshot` — no snapshot change required. Also resolves the deferred item from 0-1 review ("CurrentPhase private set — needs SetPhase method").
- `HandView.cs` updated — Rest-phase guard added to `OnCardTapped` after wound check: blocks cards with no legal play in `GamePhase.Rest` from opening `CardExpanded`. Heal/Special cards (legal in Rest via `PhaseGate`) fall through normally. Uses existing `PhaseGate.IsLegal` — no new usings.
- `RestView.cs` created — Godot `Control` in `scripts/ui/components/`. Implements: "Declare Rest" button (disabled during Rest or empty hand); Standard Rest discard flow (card rows with per-card Discard buttons; non-Wound required first; Wound optional; End Rest gated); Exhaustion auto-discard (immediately calls `DiscardCard("wound")` on Rest declaration). Subscribes `HandChanged` for reactive rebuild. "End Rest" returns phase to Movement.
- `PlaceholderMainMenu.cs` updated — `_restView` field added, `RestView` instantiated and initialized after `handView.Initialize`. Test hand unchanged: `[march, stamina, threaten, promise, wound]` — Standard Rest path verifiable on device (4 non-Wounds present).
- `deferred-work.md` updated — 3 new items logged (stale Play signal during Rest, deck reshuffle, RestView subscription leak). 0-1 deferred item for `SetPhase` marked resolved.
- On-device Exhaustion path: unit-tested only (RestRuleTest + DeckManagerDiscardTest cover the logic). Current test hand never triggers Exhaustion (has 4 non-Wounds). Manual verification of Standard Rest on device: tap "Declare Rest" → card rows appear → tap Discard on a non-Wound → "End Rest" unlocks → tap "End Rest" → phase returns to Movement, "Declare Rest" re-enables.

### File List

- `scripts/cards/RestRule.cs` (new)
- `scripts/deck/DeckManager.cs` (modified — DiscardPile + DiscardCard)
- `scripts/core/GameState.cs` (modified — SetPhase + LOCKSTEP comment)
- `scripts/ui/components/HandView.cs` (modified — Rest guard in OnCardTapped)
- `scripts/ui/components/RestView.cs` (new)
- `scripts/ui/screens/PlaceholderMainMenu.cs` (modified — RestView field + wiring)
- `tests/unit/RestRuleTest.cs` (new — 9 tests)
- `tests/unit/DeckManagerDiscardTest.cs` (new — 5 tests)
- `tests/maguswarrior.Tests.csproj` (modified — RestRule compile entry)
- `_bmad-output/implementation-artifacts/deferred-work.md` (modified — 3 new items + 0-1 resolved)

## Review Findings

Code review 2026-06-06 (Opus 4.8, develop-in-Sonnet / review-in-Opus split) — Blind Hunter + Edge Case Hunter + Acceptance Auditor. All 10 ACs satisfied (AC 10 re-verified by reviewer: 65/65 tests green, 0 warnings, Godot build clean). 2 patch, 4 defer, 9 dismissed. No decision-needed items.

- [x] [Review][Patch] Standard Rest re-entrancy strands the optional Wound discard — `OnDiscardPressed` sets `_nonWoundDiscarded = true` AFTER `_deck.DiscardCard(cardId)`, but `DiscardCard` fires `HandChanged` synchronously, re-entering `RefreshView → BuildDiscardRows` while the flag is still false. The rebuilt Wound rows get `Disabled = isWound && !_nonWoundDiscarded` = disabled, and the rows are never rebuilt again after the flag flips. Result: after discarding the one required non-Wound, the player cannot discard any Wound (the AC's "+ any Wounds") without illegally discarding a second non-Wound to fire another `HandChanged`. [scripts/ui/components/RestView.cs OnDiscardPressed] (blind+edge, High) — **RESOLVED.** `OnDiscardPressed` now calls `BuildDiscardRows()` again after setting `_nonWoundDiscarded = true` on a non-Wound discard, so the Wound rows are rebuilt enabled. Build clean, 65/65 tests green.
- [x] [Review][Patch] `OnPlayRequested` has no Rest-phase guard — a `CardExpanded` panel opened in Movement stays open after "Declare Rest"; tapping Play routes to `OnPlayRequested`, which has no phase check and stages/plays the card during Rest, bypassing the new `OnCardTapped` Rest guard (rulebook: no card play during Rest except Heal/Special). [scripts/ui/components/HandView.cs OnPlayRequested] (edge, High) — **RESOLVED.** Added a Rest legality guard (Warn + early return) after the Wound guard in `OnPlayRequested`, mirroring `OnCardTapped`; Heal/Special fall through via `PhaseGate`. Build clean, 65/65 tests green.
- [x] [Review][Defer] Exhaustion auto-discard uses magic string `DiscardCard("wound")` instead of discarding by `CardType.Wound` [scripts/ui/components/RestView.cs OnDeclareRestPressed] — deferred, works today (all Wounds share `Id = "wound"` via `WoundCard.Create`); latent soft-lock only if Wound ids ever diverge. Aligning the discard with the type-based `IsExhaustion` check is the future fix. (blind+edge)
- [x] [Review][Defer] `OnDeclareRestPressed` sets phase to Rest before the fallible exhaustion discard and never re-checks `CanDeclareRest`, with no rollback on failure [scripts/ui/components/RestView.cs] — deferred, unreachable in the current scaffold (`IsExhaustion` guarantees a Wound exists; hand is static before Rest is declared). A soft-lock only if the discard fails. (blind+edge)
- [x] [Review][Defer] HandView Rest-gate checks only `card.Unpowered` legality, not a Powered effect [scripts/ui/components/HandView.cs OnCardTapped] — deferred, powered play is not wired up yet (the Power button is always disabled in CardExpanded); a card with a Powered-only Rest-legal effect would be wrongly blocked, but no such path exists. Revisit when powered play lands. (blind)
- [x] [Review][Defer] Declaring Rest neither closes an open `CardExpanded` nor clears/guards pre-staged cards at commit [scripts/ui/components/RestView.cs, scripts/ui/components/HandView.cs OnCommitRequested] — deferred, cross-component coordination. The reachable Play-during-Rest path is closed by the `OnPlayRequested` patch above; the narrower stage-in-Movement → declare-Rest → Commit path remains. Proper fix needs a turn coordinator (Epic 7 TurnManager) that resets HandView interaction state on phase change. (edge)

**Dismissed (9):** `SetPhase` unguarded/no transition validation (implemented exactly per AC 3; transition table is Epic 7 / TurnManager scope); End Rest hard-codes Movement (matches AC 5 literal spec; real turn-flow is Epic 7); `DiscardPile` fires no event (AC 2 explicitly specifies this — callers use `HandChanged`); `DiscardCard`/Hand outside `GameStateSnapshot` undo (documented design — rest discard is a deliberate undo gate; Hand-into-snapshot already tracked under the UIBroker deferral); `RefreshView` doesn't null-guard `_deck` (false positive — `_deck` is assigned in `Initialize` before any `RefreshView` is reachable); no cap on optional Wound discards (correct per GDD — Standard Rest discards 1 non-Wound + *any number* of Wounds); `capturedType` drift risk (not real — `capturedId`/`capturedType` captured together from the same card; Wound non-uniqueness is type-consistent); Exhaustion hides remaining Wound rows (correct — Exhaustion discards exactly one Wound); Acceptance Auditor cosmetic deviations (nested `_cardRows` container, slightly different label/log strings, safer conditional ordering — all improvements, no AC violated).
