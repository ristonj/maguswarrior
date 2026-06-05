# Story 1b.5: Cannot Tap a Wound Card

Status: done

## Story

As a player,
I want Wound cards to render distinctly and be impossible to tap or play through the normal hand flow,
so that I immediately understand they are dead weight in my hand and cannot waste a tap trying to use them.

## Acceptance Criteria

1. `scripts/cards/WoundCard.cs` created — pure C#, namespace `MagusWarrior.Cards`, no Godot dependency:
   - `public static class WoundCard` with `public static CardDefinition Create()`
   - Returns `new CardDefinition { Id = "wound", Name = "Wound", Type = CardType.Wound }` — `Unpowered` is null, `Powered` is null, `ManaCost` is null (all by default)
   - File needs no `using` directives — `CardDefinition` and `CardType` are both in the same `MagusWarrior.Cards` namespace. (If the analyzer flags anything, leave the file using-free.)

2. `scripts/ui/components/CardCompact.cs` updated — Wound cards render red and are untappable:
   - Promote the tap-target `Button` to a field: `private Button _tapTarget = null!;` and assign it in `_Ready()` (currently a local `var tapTarget`)
   - Promote the `PanelContainer` to a field: `private PanelContainer _cardPanel = null!;` and assign it in `_Ready()` (currently a local `var cardPanel`)
   - In `Initialize(CardDefinition card, GamePhase currentPhase)`:
     - Add a branch at the top: `if (card.Type == CardType.Wound)` →
       - `_cardPanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color("8B1A1A") });` (UX token `color.state.wound` = `#8B1A1A`)
       - `_tapTarget.Disabled = true;` (primary tap gate — a disabled Button emits no `Pressed`)
       - `Modulate = new Color(1, 1, 1, 1f);` (full opacity — the red background IS the signal; do NOT dim)
       - `_cardId = card.Id; _cardName.Text = card.Name;` (still populate id/name so logs and any later lookup are correct; mana cost label stays empty — a wound has none)
       - `Log.Debug("[UI]", $"CardCompact wound rendered (untappable): {card.Id}");`
       - `return;` (skip the normal playable/dimming path)
     - The existing non-wound path (set `_cardId`, name, mana, `Modulate` via `IsCardPlayable`) remains unchanged for all other card types
   - Update the stale comment in `IsCardPlayable` — remove the "Wound card gating is Story 1b-5" line now that gating exists; the method still returns `true` for all non-wound cards (wounds never reach it because the wound branch returns early)
   - Required usings: `Godot` already imported (for `StyleBoxFlat`, `Color`, `Button`, `PanelContainer`); `MagusWarrior.Cards` already imported (for `CardType`). No new usings.

3. `scripts/ui/components/HandView.cs` updated — wound guards on every normal hand-play path:
   - `OnCardTapped(string cardId)`: after the `card is null` guard, add:
     ```csharp
     if (card.Type == CardType.Wound) {
         Log.Debug("[UI]", $"OnCardTapped: '{cardId}' is a Wound — not tappable, ignoring");
         return;
     }
     ```
     (placed BEFORE `_expandedPanel.Open(...)` so a wound never opens the expanded panel)
   - `OnPlayRequested(string cardId)`: after the `card is null` guard and before the `card.Unpowered is null` guard, add:
     ```csharp
     if (card.Type == CardType.Wound) {
         Log.Warn("[UI]", $"OnPlayRequested: '{cardId}' is a Wound — cannot be played");
         return;
     }
     ```
   - `OnPlaySidewaysRequested(string cardId)`: this method currently computes `sideways` BEFORE looking up the card. Add a wound guard that looks up the card first. Insert at the very top of the method body, before `var sideways = SidewaysRule.GetEffect(...)`:
     ```csharp
     var card = _deck.Hand.FirstOrDefault(c => c.Id == cardId);
     if (card is not null && card.Type == CardType.Wound) {
         Log.Warn("[UI]", $"OnPlaySidewaysRequested: '{cardId}' is a Wound — cannot be played sideways");
         return;
     }
     ```
     This closes the deferred 1b-2 item ("Wound cards can be played sideways — rules violation"). Rulebook p4: "Wound cards cannot be played in any way" — under normal play. (See Dev Notes "Skill Exception Is a Separate Path" for why this hard block is correct.)
   - `CardType` is available via the existing `using MagusWarrior.Cards;` (line 4). No new usings.

4. `scripts/ui/screens/PlaceholderMainMenu.cs` updated — inject a Wound into the test hand for on-device verification:
   - The current test-hand build is:
     ```csharp
     var testHand = _state.Cards
         .Where(c => c.Type != CardType.Wound)
         .Take(4)
         .ToList();
     ```
   - After `.ToList()`, append a wound: `testHand.Add(WoundCard.Create());`
   - Result: a 5-card test hand `[march, stamina, threaten, promise, wound]`. The wound renders red and is untappable; the other four behave as before.
   - No new using needed — `WoundCard` is in `MagusWarrior.Cards`, already imported (line 3).

5. `tests/unit/WoundCardTest.cs` created — 3 xUnit tests:
   - `Create_ProducesWoundType` — `WoundCard.Create().Type == CardType.Wound`
   - `Create_HasNoUnpoweredSpec` — `WoundCard.Create().Unpowered is null` (a wound has no playable effect)
   - `Create_HasNoManaCost` — `WoundCard.Create().ManaCost is null` (a wound cannot be powered)
   - Namespace: `MagusWarrior.Tests`; required usings: `using MagusWarrior.Cards; using Xunit;`

6. `tests/maguswarrior.Tests.csproj` updated — add compile entry:
   ```xml
   <Compile Include="../scripts/cards/WoundCard.cs" />
   ```
   `CardCompact.cs`, `HandView.cs`, and `PlaceholderMainMenu.cs` are **NOT** added (Godot dependencies).

7. `dotnet test tests/maguswarrior.Tests.csproj` passes — all 48 existing tests + 3 new tests = **51 green**. Zero warnings. Godot build clean (`dotnet build maguswarrior.csproj` → 0 errors, 0 warnings).

## Tasks / Subtasks

- [x] Task 1: Create WoundCard factory (AC: 1, 6, 5)
  - [x] Create `scripts/cards/WoundCard.cs` — pure C# static factory `Create()` returning a `CardType.Wound` `CardDefinition` with null `Unpowered`/`Powered`/`ManaCost`
  - [x] Add `<Compile Include="../scripts/cards/WoundCard.cs" />` to `tests/maguswarrior.Tests.csproj`
  - [x] Create `tests/unit/WoundCardTest.cs` with 3 tests
  - [x] Run `dotnet test` — confirm 3 new tests green (51 total)

- [x] Task 2: Render Wound red + untappable in CardCompact (AC: 2)
  - [x] Promote `_tapTarget` and `_cardPanel` to fields, assign in `_Ready()`
  - [x] Add wound branch in `Initialize`: red `#8B1A1A` panel stylebox, `_tapTarget.Disabled = true`, full opacity, populate id/name, early return
  - [x] Remove the stale "Wound card gating is Story 1b-5" comment in `IsCardPlayable`

- [x] Task 3: Wound guards in HandView (AC: 3)
  - [x] `OnCardTapped` — guard wound before opening CardExpanded
  - [x] `OnPlayRequested` — guard wound before staging
  - [x] `OnPlaySidewaysRequested` — look up card first, hard-block wound before computing sideways (closes deferred 1b-2 item)

- [x] Task 4: Inject Wound into test hand (AC: 4)
  - [x] Append `WoundCard.Create()` to the test hand in `PlaceholderMainMenu._Ready()`

- [x] Task 5: Verify all tests and no regressions (AC: 7)
  - [x] Run `dotnet test tests/maguswarrior.Tests.csproj` — 51/51 green, 0 warnings
  - [x] Run `dotnet build maguswarrior.csproj` — 0 errors, 0 warnings

## Dev Notes

### What This Story Does and Does NOT Do

**Does:**
- `WoundCard` (pure C#) — the code-defined Wound card factory (`cards.yaml` explicitly defers Wound definitions to code: "Wound cards have no effect fields — they are defined in code only (WoundCard)")
- Wound renders with a full red background (`#8B1A1A`) at compact size (the UX-specified treatment)
- Wound is untappable — the `CardCompact` tap-target button is disabled, so tapping a wound does nothing (no expand, no play) **regardless of phase**
- Hard-blocks wounds on every normal hand-play path in `HandView` (tap, play, sideways) so a wound can never be played through the hand UI
- Closes the deferred 1b-2 item: `OnPlaySidewaysRequested` now rejects wounds
- Injects one wound into the test hand so the behavior is verifiable on device

**Does NOT:**
- The skill exception (a skill that lets you play a Wound sideways ×2) — see "Skill Exception Is a Separate Path" below. That skill is Epic 5 (skills/progression) and drives wound play through its own selection UI, not the normal tap→expand→Sideways flow this story blocks.
- Long-press tooltip explaining *why* a wound is unplayable — that is `showHelpText`/tooltip work in Epic 8 (UX spec §Tooltip). This story only blocks the tap and renders red.
- Wound discard / rest cycling — Story 1b-6 (Rest turn)
- Healing (removing wounds from the deck) — Epic 5/site interactions
- Knockdown state (hand fully wounds) — Epic 3 (Combat)
- Drawing wounds from combat damage — Epic 3
- i18n of the wound card name — card names are not yet localized (all `card.Name` is rendered literally today); "Wound" literal is consistent with the current placeholder approach

### Skill Exception Is a Separate Path (design decision — John, 2026-06-04)

Wounds "cannot be played in any way" under normal circumstances, but there is at least one skill that lets a player play a Wound as if it were a card played sideways ×2. The decision for this story: **hard-block wounds on the normal hand-play path now.** The skill exception is NOT a loosening of these guards — when that skill lands (Epic 5), it will be its own explicit action that *requires the player to select a Wound* and plays it through the skill's own entry point, bypassing the normal tap→expand→Sideways flow entirely. So the absolute `card.Type == Wound` guard here is correct and does not need a pre-built extensibility seam; the skill will not route through `OnCardTapped`/`OnPlaySidewaysRequested`. If a future review questions the hard guard as "hardcoded per-card logic," this Dev Note is the rationale: it gates the *normal* flow, and the exception is a deliberately separate, skill-initiated path.

### Spec Tension: Red vs. Greyed — Red Wins

Two artifacts describe the wound's visual treatment:
- **GDD line 846** (an SFX/feedback table): "Wound card greyed out | Subtle 'locked' cue — no play allowed"
- **UX spec line 351 + token line 630**: "Full card background red at compact size", `color.state.wound = #8B1A1A`

The **UX design specification is the authoritative visual contract** — it is the later, more specific design artifact and defines the exact color token. Implement **full red `#8B1A1A`**, not grey. The GDD's "greyed" phrasing is superseded.

### Wound Cards Are Code-Defined (not YAML)

`data/cards.yaml` contains 81 cards, none of type `wound`. The yaml header comment is explicit: "Wound cards have no effect fields — they are defined in code only (WoundCard)." `CardLoader.cs:100` maps the string `"wound"` → `CardType.Wound`, but no yaml card uses it. So `WoundCard.Create()` is the canonical Wound source. Do NOT add a wound entry to `cards.yaml`.

A Wound has:
- `Type = CardType.Wound`
- `Unpowered = null` (no effect — it cannot be played)
- `Powered = null`, `ManaCost = null` (it cannot be powered)
- `Id = "wound"`, `Name = "Wound"` (all wounds are identical and fungible per GDD line 453)

### Why the Tap Gate Lives in CardCompact (Primary) + HandView (Defense)

The **primary** gate is `_tapTarget.Disabled = true` in `CardCompact` — a disabled Godot `Button` emits no `Pressed` signal, so `CardTapped` never fires for a wound. This is the cleanest block: the wound is inert at the source.

The **defense-in-depth** guards in `HandView.OnCardTapped` / `OnPlayRequested` / `OnPlaySidewaysRequested` exist because:
- Godot signals can be replayed/stale (see the 1b-1 ghost-tap deferred item)
- The wound must be unplayable through *every* normal path, matching the rulebook's "cannot be played in any way" (normal circumstances)
- It closes the 1b-2 deferred item directly (sideways play of a wound)

This mirrors the project's established pattern: gate at the UI source AND guard in the handler.

### CardCompact Rendering Detail

`CardCompact._Ready()` currently builds `cardPanel` (a `PanelContainer`) and `tapTarget` (a `Button` overlay with `Flat = true`) as locals. Promote both to fields so `Initialize` can mutate them:

```csharp
private Button _tapTarget = null!;
private PanelContainer _cardPanel = null!;
```

For the red background, override the PanelContainer's `panel` stylebox:
```csharp
_cardPanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color("8B1A1A") });
```
`new Color("8B1A1A")` uses Godot's hex-string Color constructor. `StyleBoxFlat` is in the `Godot` namespace (already imported).

The wound branch returns early, so the existing `Modulate = IsCardPlayable(...) ? ... : ...` dimming line is skipped for wounds (we explicitly set full opacity in the branch). Note: populate `_cardId`/`_cardName.Text` inside the wound branch too, since the early return skips the normal assignment lines.

### `OnPlaySidewaysRequested` — Guard Ordering Matters

The current method computes `sideways = SidewaysRule.GetEffect(_state.CurrentPhase)` BEFORE it ever references the card. To reject wounds, you must look the card up first (the method otherwise only resolves the card id inside `_deck.PlayCard(cardId)`). Insert the wound lookup+guard at the very top of the method body. Use `FirstOrDefault` (the method already relies on `System.Linq`, imported at line 2). If the card is not found (`card is null`), fall through to the existing flow — `PlayCard` will fail-safe with a `Result.Fail` as it does today; do not change that path.

### Atomicity / No GameState Changes

This story adds no `GameState` fields and no `GameStateSnapshot` changes. Wounds are pure hand content. `WoundCard.Create()` produces a `CardDefinition` data object. The LOCKSTEP comment at `scripts/core/GameState.cs:48` is untouched.

### Test Hand After This Story

`PlaceholderMainMenu` builds `[march, stamina, threaten, promise]` (4 non-wound), then appends `WoundCard.Create()` → `[march, stamina, threaten, promise, wound]`. On device: the wound is the 5th card, rendered solid red, and tapping it does nothing. The other four behave exactly as in 1b-4 (march/stamina tappable→playable in Movement; threaten/promise tappable but Play disabled).

### Testability Boundary

`WoundCard` is pure C# and fully unit-tested (3 tests). The red rendering and tap-blocking live in `CardCompact`/`HandView` (Godot `Control` nodes) and **cannot be compiled into the test project** — they are verified by the Godot build (0 warnings) and on-device. This is the same Godot/test-project boundary documented for every prior UI story (1b-1 through 1b-4). Manual on-device check: tap the red wound → nothing happens; tap march → expands normally.

### Carry-Forward Notes from 1b-4 (Unchanged)

- `async void` only in Godot signal handlers that await effects (`OnCommitRequested`, `OnPlaySidewaysRequested`). `OnCardTapped` and `OnPlayRequested` are synchronous `void` — the wound guards added to them must NOT make them async. `OnPlaySidewaysRequested` stays `async void` (its wound guard is a synchronous early return at the top, before any await).
- `ImplicitUsings=disable`: all using statements must be explicit in every `.cs` file.
- No `GD.Print` — use `Log.Debug`/`Log.Warn` with the `[UI]` tag.
- The subscription-leak and ghost-tap items remain deferred (see `deferred-work.md`); this story does not re-open them.

### Namespace Conventions

| Folder | Namespace |
|--------|-----------|
| `scripts/cards/` | `MagusWarrior.Cards` |
| `scripts/ui/components/` | `MagusWarrior.UI` |
| `scripts/ui/screens/` | `MagusWarrior.UI` |
| `tests/unit/` | `MagusWarrior.Tests` |

### File Placement

| File | Action | Type |
|------|--------|------|
| `scripts/cards/WoundCard.cs` | new | Pure C# |
| `scripts/ui/components/CardCompact.cs` | modified | Godot Control |
| `scripts/ui/components/HandView.cs` | modified | Godot Control |
| `scripts/ui/screens/PlaceholderMainMenu.cs` | modified | Godot CanvasLayer |
| `tests/unit/WoundCardTest.cs` | new | xUnit test |
| `tests/maguswarrior.Tests.csproj` | modified | add WoundCard compile entry |

### Project Context Rules

**Pure C# boundary:** `WoundCard.cs` is pure C# — no Godot dependency. Add to `tests/maguswarrior.Tests.csproj`. `CardCompact.cs`, `HandView.cs`, `PlaceholderMainMenu.cs` are Godot nodes — do NOT add to the test project.

**No service locator:** `WoundCard.Create()` is a static factory producing a data object — not a managed service. It is called directly in `PlaceholderMainMenu` (a Godot screen) to seed the test hand. No DI involved.

**No hardcoded per-card logic — exception is documented:** the `card.Type == CardType.Wound` guards are a *card-type* gate (one of five `CardType` enum values), not per-card-id logic like `if (card.Id == "rage")`. Type-level gating is legitimate. The hard block of the normal play path is the deliberate design decision recorded in "Skill Exception Is a Separate Path."

**Result<T>:** not applicable — `WoundCard.Create()` cannot fail. The wound guards in `HandView` return early (void), they do not produce a `Result`.

**Logging:** `Log.Debug("[UI]", ...)` for the wound-render and the non-playable tap; `Log.Warn("[UI]", ...)` for attempted play/sideways of a wound (an abnormal path that should never be reached via the UI). Never `GD.Print`. No logging inside `WoundCard.cs` (pure C#, no `Log` dependency).

**ImplicitUsings=disable:** explicit usings everywhere. `WoundCardTest.cs` needs `using MagusWarrior.Cards;` and `using Xunit;`. `WoundCard.cs` needs no usings (only same-namespace `CardDefinition`/`CardType`).

**Events are data-only:** no new events introduced.

**LOCKSTEP:** no new `GameState` fields. The LOCKSTEP comment at `scripts/core/GameState.cs:48` is untouched.

### References

- Epics: `_bmad-output/epics.md` §Epic 1b — "Wound card red/untappable treatment (rendered in red, never tappable regardless of phase)"; story "As a player, I cannot tap a Wound card so I know it is unplayable"; UI Verification: "Wound renders red."
- GDD Wound rules (cannot be played in any way): `_bmad-output/gdd.md:298`, `:416`, `:445-454`
- UX wound treatment (full red background, color token): `_bmad-output/planning-artifacts/ux-design-specification.md:351`, `:630` (`color.state.wound = #8B1A1A`)
- UX unavailable-card / disabled-card states: `_bmad-output/planning-artifacts/ux-design-specification.md:340-351`
- Project Context Rules: `docs/project-context.md` (pure C# boundary, logging, file placement, async/await, no hardcoded per-card logic)
- Previous story (1b-4) Dev Notes — undo flow, sync void handlers, async void exception: `_bmad-output/implementation-artifacts/1b-4-undo-staged-cards-before-new-information-revealed.md`
- Deferred 1b-2 item closed here (wound sideways guard): `_bmad-output/implementation-artifacts/deferred-work.md` (1b-2 section, first bullet)
- `cards.yaml` Wound-is-code-defined note: `data/cards.yaml:104`
- `CardLoader` wound mapping: `scripts/cards/CardLoader.cs:100`
- `CardDefinition` / `CardType` enum: `scripts/cards/CardDefinition.cs:7-11`
- `CardCompact` current render (tap target, panel): `scripts/ui/components/CardCompact.cs:16-57`
- `HandView` handlers to guard: `scripts/ui/components/HandView.cs` (`OnCardTapped`, `OnPlayRequested`, `OnPlaySidewaysRequested`)
- `PlaceholderMainMenu` test-hand build: `scripts/ui/screens/PlaceholderMainMenu.cs:68-72`

## Dev Agent Record

### Agent Model Used

claude-opus-4-8 — DEVIATION from the usual develop-in-Sonnet / review-in-Opus split: this dev pass ran on Opus 4.8 because the session's `/model` was still Opus from the preceding 1b-4 review (the code-review `on_complete` hook restored Sonnet in settings.json, which only takes effect on the next session, not the running one). Decision (John, 2026-06-04): keep the Opus-written code (small, fully green) and run the **review in Sonnet** — the split is inverted but cross-model independence is preserved (reviewer ≠ developer).

### Debug Log References

### Completion Notes List

- All 5 tasks completed. 51/51 xUnit tests pass (48 existing + 3 new WoundCard tests). Godot build: 0 errors, 0 warnings.
- `WoundCard.cs` created — pure C# static factory in `scripts/cards/`; `Create()` returns `CardDefinition { Id="wound", Name="Wound", Type=Wound }` with null Unpowered/Powered/ManaCost. Added to test csproj compile list. Wounds are code-defined (not in cards.yaml).
- `CardCompact.cs` — promoted `_tapTarget` (Button) and `_cardPanel` (PanelContainer) to fields; added wound branch in `Initialize`: red `#8B1A1A` StyleBoxFlat panel override, `_tapTarget.Disabled = true` (inert — a disabled Button emits no Pressed), full opacity, populate id/name, early return. Stale 1b-5 comment removed from `IsCardPlayable`.
- `HandView.cs` — wound guards added to all three normal play paths: `OnCardTapped` (Debug log, no expand), `OnPlayRequested` (Warn, no stage), `OnPlaySidewaysRequested` (Warn, hard block — card lookup moved before `sideways` computation). Closes the deferred 1b-2 item (wound sideways play).
- `PlaceholderMainMenu.cs` — appended `WoundCard.Create()` to the test hand → `[march, stamina, threaten, promise, wound]` for on-device verification.
- Design decision baked in (John): wounds hard-blocked on the normal hand flow; the "play wound sideways x2" skill is a separate skill-initiated path (Epic 5), not a loosening of these guards. See Dev Notes "Skill Exception Is a Separate Path."
- Red (`#8B1A1A`, UX token) chosen over the GDD's "greyed" phrasing — UX spec is the authoritative visual contract.
- **On-device verification still pending** — red render + tap-inertness are Godot-layer behavior outside the test project's reach (same boundary as all prior UI stories). Manual check: tap the red wound → nothing happens; tap march → expands normally.

### File List

- `scripts/cards/WoundCard.cs` (new)
- `scripts/ui/components/CardCompact.cs` (modified — fields + wound render branch)
- `scripts/ui/components/HandView.cs` (modified — wound guards on 3 play paths)
- `scripts/ui/screens/PlaceholderMainMenu.cs` (modified — wound injected into test hand)
- `tests/unit/WoundCardTest.cs` (new — 3 tests)
- `tests/maguswarrior.Tests.csproj` (modified — WoundCard compile entry)

## Review Findings

Code review 2026-06-04 (Sonnet 4.6, inverted-split: Opus dev, Sonnet review) — Blind Hunter + Edge Case Hunter + Acceptance Auditor. All 7 ACs satisfied; 51/51 tests verified green, 0 warnings. 2 patch, 1 defer, 3 dismissed. No decision-needed items.

- [x] [Review][Patch] `_tapTarget.Disabled` never reset to `false` in the non-wound path — a node initialized as a Wound and then re-initialized as a non-Wound retains the disabled state and becomes permanently untappable [scripts/ui/components/CardCompact.cs Initialize] — **RESOLVED.** Added `_tapTarget.Disabled = false;` and `_cardPanel.RemoveThemeStyleboxOverride("panel");` at the top of `Initialize` (before `_cardId`/`_cardName` assignment), making `Initialize` idempotent for any card-type sequence. `RefreshHand` always recreates nodes today so this was latent; the resets close the gap before any in-place refresh optimization lands. Build clean, 51/51 tests green.

- [x] [Review][Patch] `AddThemeStyleboxOverride("panel", ...)` never cleared on the non-wound path — a `CardCompact` initialized as Wound then re-initialized as a non-Wound retains the red `#8B1A1A` panel background; the non-wound path has no `RemoveThemeStyleboxOverride` or reset [scripts/ui/components/CardCompact.cs Initialize] — **RESOLVED** together with the `Disabled` patch above via `_cardPanel.RemoveThemeStyleboxOverride("panel");` at the top of `Initialize`. Godot ignores this call if no override exists, so it is always safe.

- [x] [Review][Defer] `OnPlaySidewaysRequested` wound guard uses `card is not null && card.Type == Wound`; the null-card path (stale tap) is handled silently by the downstream `PlayCard` fail-safe, not by the wound guard — the intent gap is invisible to a future reader [scripts/ui/components/HandView.cs OnPlaySidewaysRequested] — deferred, latent. Not a current bug; the Dev Notes document the skip-on-null intent. A comment at the fallthrough point ("null = stale tap, handled by PlayCard below") would close the readability gap when touching this method next.

**Dismissed (3):** Multi-wound `Id = "wound"` collision (wound guards fire correctly for any wound found regardless of which instance is returned; no new bug — the pre-existing `DeckManager.ReturnCard` Id-uniqueness issue is already in `deferred-work.md`); staged-wound impossible via `OnUndoRequested` (correctly blocked at all three play-path guards — Acceptance Auditor and Edge Case Hunter both confirmed this); `_cardId`/`_cardName.Text` assigned before vs. inside the wound branch (identical runtime outcome, Acceptance Auditor confirmed).
