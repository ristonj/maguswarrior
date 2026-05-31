# Story 1b.1: Tap Card to See Options

Status: done

## Story

As a player,
I want to tap a card in my hand and see what I can do with it,
so that I know my available plays before committing.

## Acceptance Criteria

1. `scripts/deck/DeckManager.cs` created — pure C#, namespace `MagusWarrior.Deck`, **no Godot dependency**:
   - `public IReadOnlyList<CardDefinition> Hand { get; private set; }` — initialized to `Array.Empty<CardDefinition>()`
   - `public event Action? HandChanged`
   - `public void SetHand(IReadOnlyList<CardDefinition> hand)` — assigns `Hand`, fires `HandChanged`
   - Required usings: `using System; using System.Collections.Generic; using MagusWarrior.Cards;`

2. `scenes/components/CardCompact.tscn` created — root node `Control` named `CardCompact` with `min_custom_minimum_size = Vector2(120, 120)`. Internal layout:
   - `PanelContainer` named `CardPanel` (anchors_preset = 15, fills parent)
     - `VBoxContainer` named `Layout`
       - `Label` named `CardName`
       - `Label` named `ManaCostLabel` (text: empty by default)
   - `Button` named `TapTarget` (anchors_preset = 15, flat = true, z_index = 1) — covers entire card; mouse filter must allow clicks through to children only for the TapTarget itself

3. `scripts/ui/components/CardCompact.cs` created — namespace `MagusWarrior.UI`, `partial class CardCompact : Control`:
   - `[Signal] public delegate void CardTappedEventHandler(string cardId)`
   - Private: `private string _cardId = string.Empty`
   - `public void Initialize(CardDefinition card, GamePhase currentPhase)`:
     - Sets `_cardId = card.Id`
     - Sets `GetNode<Label>("CardPanel/Layout/CardName").Text = card.Name`
     - Sets `GetNode<Label>("CardPanel/Layout/ManaCostLabel").Text = card.ManaCost.HasValue ? card.ManaCost.ToString()! : string.Empty`
     - Sets `Modulate = IsCardPlayable(card, currentPhase) ? new Color(1, 1, 1, 1f) : new Color(1, 1, 1, 0.5f)` — ≥40% opacity reduction when not playable per UX spec
   - `private static bool IsCardPlayable(CardDefinition card, GamePhase phase)` — returns true if `card.Unpowered != null && PhaseGate.IsLegal(card.Unpowered.EffectType, phase)`; OR if card has `ManaCost` (powered option may be available regardless); OR always returns true for sideways (caller renders all cards, just dims when native play unavailable)
     - **Simplification for 1b-1:** return `true` for all cards so all are tappable; greying of the native-play button happens inside `CardExpanded`. Wound card gating is Story 1b-5 — do NOT special-case wounds here.
   - `public override void _Ready()`: connects `GetNode<Button>("TapTarget").Pressed += () => EmitSignal(SignalName.CardTapped, _cardId);`
   - `Log.Debug("[UI]", $"CardCompact initialized: {card.Id}")` at end of Initialize
   - Required usings: `using Godot; using MagusWarrior.Cards; using MagusWarrior.Cards.Effects; using MagusWarrior.Core; using MagusWarrior.Core.Types;`

4. `scenes/components/CardExpanded.tscn` created — root `Control` named `CardExpanded`, initially hidden (`visible = false`), `z_index = 3`. Internal layout:
   - `PanelContainer` named `CardPanel` (anchors_preset = 15)
     - `VBoxContainer` named `Layout`
       - `Button` named `CancelButton` (text: "✕ Cancel") — **positioned at top, separate from play actions**
       - `Label` named `CardNameLabel`
       - `Label` named `EffectTextLabel`
       - `HBoxContainer` named `ActionBar` — buttons minimum 44px apart (set `separation = 20` on HBoxContainer)
         - `Button` named `PlayButton` (text: "Play")
         - `Button` named `PlaySidewaysButton` (text: "Sideways")
         - `Button` named `PowerButton` (text: "Power", visible = false by default)

5. `scripts/ui/components/CardExpanded.cs` created — namespace `MagusWarrior.UI`, `partial class CardExpanded : Control`:
   - Signals: `[Signal] public delegate void PlayRequestedEventHandler(string cardId)`, `[Signal] public delegate void PlaySidewaysRequestedEventHandler(string cardId)`, `[Signal] public delegate void PowerRequestedEventHandler(string cardId)`, `[Signal] public delegate void CancelRequestedEventHandler()`
   - Private: `private string _cardId = string.Empty`
   - `public void Open(CardDefinition card, GamePhase currentPhase)`:
     - Sets `_cardId = card.Id`
     - Sets `GetNode<Label>("CardPanel/Layout/CardNameLabel").Text = card.Name`
     - Sets `GetNode<Label>("CardPanel/Layout/EffectTextLabel").Text = card.Unpowered?.Text ?? "(no effect)"`
     - Sets `PlayButton.Disabled = !(card.Unpowered != null && PhaseGate.IsLegal(card.Unpowered.EffectType, currentPhase))` — greyed when native play is not legal in current phase
     - Sets `PlaySidewaysButton.Disabled = false` — sideways always available in this story
     - Sets `PowerButton.Visible = card.ManaCost.HasValue` — only shown when card has mana cost
     - Sets `PowerButton.Disabled = true` — always greyed; ManaPool not implemented until later stories
     - Sets `Visible = true`
     - `Log.Debug("[UI]", $"CardExpanded opened: {card.Id} phase={currentPhase}")`
   - `public void Close()`: `Visible = false; Log.Debug("[UI]", $"CardExpanded closed: {_cardId}")`
   - `public override void _Ready()`:
     - Connects `CancelButton.Pressed += () => { EmitSignal(SignalName.CancelRequested); Close(); }`
     - Connects `PlayButton.Pressed += () => { EmitSignal(SignalName.PlayRequested, _cardId); Close(); Log.Debug("[UI]", $"Play tapped: {_cardId}"); }`
     - Connects `PlaySidewaysButton.Pressed += () => { EmitSignal(SignalName.PlaySidewaysRequested, _cardId); Close(); Log.Debug("[UI]", $"Sideways tapped: {_cardId}"); }`
     - Connects `PowerButton.Pressed += () => { EmitSignal(SignalName.PowerRequested, _cardId); Close(); Log.Debug("[UI]", $"Power tapped: {_cardId}"); }`
   - Required usings: `using Godot; using MagusWarrior.Cards; using MagusWarrior.Cards.Effects; using MagusWarrior.Core; using MagusWarrior.Core.Types;`

6. `scenes/components/HandView.tscn` created — root `Control` named `HandView`, anchored to full bottom strip of parent (anchor_left=0, anchor_right=1, anchor_top=1, anchor_bottom=1, offset_top=-350, offset_bottom=0). Layout:
   - `CardExpanded.tscn` instanced as child named `CardExpandedPanel` — anchored to fill upper portion (anchor_left=0, anchor_right=1, anchor_top=0, anchor_bottom=1, offset_bottom=-130)
   - `HBoxContainer` named `CardsContainer` — anchored to bottom strip (anchor_left=0, anchor_right=1, anchor_top=1, anchor_bottom=1, offset_top=-130, offset_bottom=0), `separation = 8`
   - Exported scene reference: `card_compact_scene = ExtResource(...)` pointing to `CardCompact.tscn`

7. `scripts/ui/components/HandView.cs` created — namespace `MagusWarrior.UI`, `partial class HandView : Control`:
   - `[Export] public PackedScene CardCompactScene { get; set; } = null!;`
   - Private: `private DeckManager _deck = null!;`, `private GameState _state = null!;`, `private CardExpanded _expandedPanel = null!;`
   - `public void Initialize(DeckManager deck, GameState state)`:
     - Stores refs
     - `_expandedPanel = GetNode<CardExpanded>("CardExpandedPanel")`
     - Subscribes `deck.HandChanged += RefreshHand`
     - Calls `RefreshHand()`
   - `private void RefreshHand()`:
     - Clears all children of `GetNode<HBoxContainer>("CardsContainer")` via `QueueFree()`
     - For each card in `_deck.Hand`: instantiates `CardCompactScene.Instantiate<CardCompact>()`, calls `Initialize(card, _state.CurrentPhase)`, connects `card.CardTapped += OnCardTapped`, adds to `CardsContainer`
   - `private void OnCardTapped(string cardId)`:
     - Finds the card: `var card = _deck.Hand.First(c => c.Id == cardId);` (needs `using System.Linq;`)
     - If `_expandedPanel.Visible`: calls `_expandedPanel.Close()` first (one-card-at-a-time rule)
     - Calls `_expandedPanel.Open(card, _state.CurrentPhase)`
   - Required usings: `using System.Linq; using Godot; using MagusWarrior.Cards; using MagusWarrior.Core; using MagusWarrior.Core.Types; using MagusWarrior.Deck;`

8. `scenes/screens/PlaceholderMainMenu.tscn` updated — add `HandView.tscn` as instanced child named `HandView`; bump `load_steps` from `3` to `4`; add corresponding `ext_resource` entry for `HandView.tscn`.

9. `scripts/ui/screens/PlaceholderMainMenu.cs` updated:
   - Add `private DeckManager _deckManager = new();` field (namespace `MagusWarrior.Deck` — add `using MagusWarrior.Deck;`)
   - In `_Ready()`, after the existing save load block and `FireTestEffect`, add:
     ```csharp
     var handView = GetNode<HandView>("HandView");
     var testHand = _state.Cards
         .Where(c => c.Type != CardType.Wound)
         .Take(4)
         .ToList();
     _deckManager.SetHand(testHand);
     handView.Initialize(_deckManager, _state);
     ```
   - Add `using System.Linq; using MagusWarrior.Deck;` to the existing using block

10. `tests/unit/DeckManagerTest.cs` created — 3 xUnit tests for `DeckManager`:
    - `Hand_IsEmpty_OnConstruction` — `new DeckManager().Hand` is empty
    - `SetHand_UpdatesHand` — after `SetHand`, `Hand` equals the provided list
    - `SetHand_FiresHandChanged` — `HandChanged` event fires exactly once on `SetHand`
    - Namespace: `MagusWarrior.Tests`; required usings: `using System; using System.Collections.Generic; using MagusWarrior.Cards; using MagusWarrior.Deck; using Xunit;`

11. `tests/maguswarrior.Tests.csproj` updated — add `<Compile Include="../scripts/deck/DeckManager.cs" />` to the pure C# compile group.

12. `dotnet test tests/maguswarrior.Tests.csproj` passes — all 19 existing tests + 3 new `DeckManagerTest` tests = 22 green. `HandView.cs`, `CardCompact.cs`, `CardExpanded.cs` are **NOT** added to the test project (they have Godot dependencies).

## Tasks / Subtasks

- [x] Task 1: Create DeckManager (AC: 1, 11)
  - [x] Create `scripts/deck/DeckManager.cs` — pure C#, `MagusWarrior.Deck` namespace, Hand + SetHand + HandChanged
  - [x] Add `<Compile Include="../scripts/deck/DeckManager.cs" />` to `tests/maguswarrior.Tests.csproj`

- [x] Task 2: Write and verify DeckManager tests (AC: 10, 12)
  - [x] Create `tests/unit/DeckManagerTest.cs` with 3 tests (empty hand, SetHand updates, HandChanged fires)
  - [x] Run `dotnet test tests/maguswarrior.Tests.csproj` — confirm 19 existing still green, 3 new green (22 total)

- [x] Task 3: Create CardCompact scene + script (AC: 2, 3)
  - [x] Create `scenes/components/CardCompact.tscn` with node layout from AC 2
  - [x] Create `scripts/ui/components/CardCompact.cs` — `Initialize`, `IsCardPlayable`, `_Ready` with TapTarget wiring

- [x] Task 4: Create CardExpanded scene + script (AC: 4, 5)
  - [x] Create `scenes/components/CardExpanded.tscn` with node layout from AC 4 (hidden by default, z_index=3)
  - [x] Create `scripts/ui/components/CardExpanded.cs` — `Open`, `Close`, `_Ready` with button signals

- [x] Task 5: Create HandView scene + script (AC: 6, 7)
  - [x] Create `scenes/components/HandView.tscn` — CardExpandedPanel + CardsContainer, card_compact_scene export wired
  - [x] Create `scripts/ui/components/HandView.cs` — `Initialize`, `RefreshHand`, `OnCardTapped`

- [x] Task 6: Integrate into PlaceholderMainMenu (AC: 8, 9)
  - [x] Update `scenes/screens/PlaceholderMainMenu.tscn` — add HandView instance, bump load_steps to 4
  - [x] Update `scripts/ui/screens/PlaceholderMainMenu.cs` — add DeckManager field, seed hand with 4 non-Wound cards, call handView.Initialize

- [x] Task 7: Verify no regressions (AC: 12)
  - [x] Run `dotnet test tests/maguswarrior.Tests.csproj` — confirm 22/22 green

### Review Findings

_From `/gds-code-review` on 2026-05-31 (Blind Hunter + Edge Case Hunter + Acceptance Auditor)._

#### Patch

- [x] [Review][Patch] **Unused `using MagusWarrior.Cards.Effects` in CardCompact.cs** — import not referenced anywhere in the file (`IsCardPlayable` returns `true` unconditionally in 1b-1, so no `PhaseGate` call exists). Remove the using. [scripts/ui/components/CardCompact.cs:3] **RESOLVED**: removed.

- [x] [Review][Patch] **`HandView.OnCardTapped` uses `First()` with no null guard — throws if card not found** — `_deck.Hand.First(c => c.Id == cardId)` throws `InvalidOperationException` if the card is absent. While the hand never changes in 1b-1 (only set once at startup), this is a crash risk once any future story calls `SetHand` with a different set. Replace with `FirstOrDefault` + early return. [scripts/ui/components/HandView.cs:42] **RESOLVED**: changed to `FirstOrDefault` + null guard with `Log.Debug` + `return`.

#### Deferred (real but out of scope)

- [x] [Review][Defer] **`HandView` never unsubscribes `HandChanged` from `DeckManager`** — `Initialize` subscribes `deck.HandChanged += RefreshHand` with no matching `_ExitTree` unsubscribe. If `HandView` is freed before `DeckManager`, the delegate dangles and `RefreshHand` fires on a freed node. Not triggered in 1b-1 (both owned by `PlaceholderMainMenu` for the whole session), but needs an `_ExitTree` override when scene lifecycle gets more complex. [scripts/ui/components/HandView.cs:21]

- [x] [Review][Defer] **`QueueFree`'d `CardCompact` nodes can still emit `CardTapped` before frame deletion — ghost tap risk** — `RefreshHand` calls `QueueFree` on old nodes but they remain alive until end of frame. A tap event queued in the same frame against an old node's `TapTarget` fires `CardTapped` after the hand has been rebuilt. The resulting `OnCardTapped` call tries `First()` lookup with the old `_cardId` on a potentially different hand. Not triggerable in 1b-1 (hand never changes post-init), but a latent hazard for any future story that calls `SetHand` during play. Fix when `SetHand` starts being called dynamically: either disconnect signals before `QueueFree`, or use a generation counter to discard stale taps. [scripts/ui/components/HandView.cs:27-35]

- [x] [Review][Defer] **`SetHand` fires `HandChanged` before `handView.Initialize` subscribes — silent ordering dependency** — `PlaceholderMainMenu._Ready()` calls `_deckManager.SetHand(testHand)` then `handView.Initialize(...)`. At the `SetHand` call, `HandView` has not yet subscribed to `HandChanged`, so the event fires into the void. `Initialize` then calls `RefreshHand()` directly, so the hand does populate correctly — this works today. The risk: if `SetHand` is ever moved after `Initialize` in a future refactor, or if code relies on `HandChanged` being observable from the first `SetHand`, the ordering assumption will silently break. Consider calling `SetHand` after `Initialize`, or accepting the current pattern and documenting it. [scripts/ui/screens/PlaceholderMainMenu.cs:47-50]

## Dev Notes

### Scope — what this story does and does not do

This story establishes the hand UI scaffold: `DeckManager` as the pure C# hand owner, `CardCompact` as the compact hand display, `CardExpanded` as the options panel. Tapping a card shows Play / Play Sideways / Power (if mana cost) / Cancel.

**Does NOT implement:**
- Actual card play logic — Play/Sideways/Power buttons log and close (no EffectScheduler.Enqueue)
- Staging area — Story 1b-3
- ManaPool / powered play — Power button always greyed; ManaPool introduced in Epic 6 stories
- Wound card tapping gate — Story 1b-5; wound cards are tappable in 1b-1 (just show their limited options)
- `UIBroker` / async pending interactions — not needed until actual card play wires up (1b-2+)
- CardExpanded expand animation (<150ms per UX spec) — noted for future polish; instantaneous Visible=true is acceptable here

### Critical architecture rules (from project-context.md)

- **Godot boundary:** `DeckManager.cs` must have zero Godot dependency — pure C#, can be tested in `tests/`
- `scripts/ui/` is the ONLY folder where `: Control`, `: PanelContainer`, or `partial class` (Godot-generated) is permitted. `CardCompact.cs`, `CardExpanded.cs`, `HandView.cs` go in `scripts/ui/components/` (subdirectory of `scripts/ui/` — correct)
- `ImplicitUsings=disable` — every `.cs` file needs all `using` statements written out explicitly
- `Log.Debug("[UI]", ...)` for all UI-layer debug output — no `GD.Print` anywhere
- No `async void` — not applicable here but keep in mind for future stories

### File placement

| File | Location |
|------|----------|
| DeckManager.cs | `scripts/deck/` |
| CardCompact.cs | `scripts/ui/components/` |
| CardExpanded.cs | `scripts/ui/components/` |
| HandView.cs | `scripts/ui/components/` |
| CardCompact.tscn | `scenes/components/` |
| CardExpanded.tscn | `scenes/components/` |
| HandView.tscn | `scenes/components/` |
| DeckManagerTest.cs | `tests/unit/` |

### PhaseGate usage

`PhaseGate.IsLegal(EffectType, GamePhase)` is in `scripts/cards/effects/PhaseGate.cs`, namespace `MagusWarrior.Cards.Effects`. Use it for Play button greying in `CardExpanded.Open()`:

```csharp
PlayButton.Disabled = !(card.Unpowered != null && PhaseGate.IsLegal(card.Unpowered.EffectType, currentPhase));
```

Test hand uses march (Move, legal in Movement), swiftness (Move, legal in Movement), rage (AttackMelee, greyed in Movement — shows greying behavior). GameState.CurrentPhase starts as `GamePhase.Movement` (the default). This gives one grey Play button (rage) and two active Play buttons — a good demo of the phase filter.

### Test hand seeding

PlaceholderMainMenu seeds the hand using:
```csharp
var testHand = _state.Cards
    .Where(c => c.Type != CardType.Wound)
    .Take(4)
    .ToList();
```
The first 4 non-wound cards from `data/cards.yaml` are march, stamina, threaten, concentration — all have unpowered effects. This gives a valid demo hand.

### Godot 4 C# signal pattern

```csharp
// Declaration
[Signal]
public delegate void CardTappedEventHandler(string cardId);

// Emission
EmitSignal(SignalName.CardTapped, cardId);

// Connection in HandView
cardNode.CardTapped += OnCardTapped;
```

### Godot 4 C# packed scene instantiation

```csharp
// In HandView.cs — for each card in the hand
var node = CardCompactScene.Instantiate<CardCompact>();
GetNode<HBoxContainer>("CardsContainer").AddChild(node);
node.Initialize(card, _state.CurrentPhase);
node.CardTapped += OnCardTapped;
```
`CardCompactScene` is exported (set in HandView.tscn via `card_compact_scene = ExtResource(...)`).

### PlaceholderMainMenu.tscn scene update pattern

Following the established pattern from 1a-3:
```
[gd_scene load_steps=4 format=3]

[ext_resource type="Script" path="res://scripts/ui/screens/PlaceholderMainMenu.cs" id="1_savemgr"]
[ext_resource type="PackedScene" path="res://scenes/debug/EffectEventLogPanel.tscn" id="2_efflp"]
[ext_resource type="PackedScene" path="res://scenes/components/HandView.tscn" id="3_hview"]

[node name="PlaceholderMainMenu" type="CanvasLayer" script=ExtResource("1_savemgr")]
[node name="EffectEventLogPanel" parent="." instance=ExtResource("2_efflp")]
[node name="DebugToggleArea" type="Button" parent="."]
... (existing properties preserved)
[node name="Label" type="Label" parent="."]
... (existing properties preserved)
[node name="HandView" parent="." instance=ExtResource("3_hview")]
```

### One-card-at-a-time rule

```csharp
private void OnCardTapped(string cardId) {
    if (_expandedPanel.Visible)
        _expandedPanel.Close();
    var card = _deck.Hand.First(c => c.Id == cardId);
    _expandedPanel.Open(card, _state.CurrentPhase);
}
```
This matches the UX spec: "Tapping a second card collapses the first instantly, then expands second."

### Clearing Godot container children

```csharp
var container = GetNode<HBoxContainer>("CardsContainer");
foreach (Node child in container.GetChildren())
    child.QueueFree();
```
`QueueFree()` defers until end of frame — acceptable for a hand refresh triggered by `HandChanged`.

### Power button — always greyed in this story

Power button visible when `card.ManaCost.HasValue`, but always `Disabled = true`. Comment in Open():
```csharp
// ManaPool not implemented until Epic 6 stories — Power always greyed for now
PowerButton.Disabled = true;
```

### Namespace conventions

| Folder | Namespace |
|--------|-----------|
| `scripts/deck/` | `MagusWarrior.Deck` |
| `scripts/ui/components/` | `MagusWarrior.UI` |
| `scripts/ui/screens/` | `MagusWarrior.UI` |

### Deferred work (noted, not blocked)

- **CardExpanded expand animation** — UX spec requires <150ms animate; instantaneous Visible=true is the placeholder; add Tween-based scale/fade in a later polish story
- **PlaySideways greying in CombatRanged** — effect LLD says "no useful sideways option during Ranged phase"; always-enabled for 1b-1; gate it when CombatRanged phase is exercised
- **`HandChanged` on phase change** — `HandView.RefreshHand()` needs to be called when `GameState.CurrentPhase` changes (so button states update); no `PhaseChanged` event on GameState yet; wired when phase advance is implemented

### Previous story patterns (Epic 1a)

- `partial class` nodes need `_Ready()` for node wiring (not constructors)
- Signals connected in `_Ready()` — never in constructors
- `Log.Debug("[UI]", ...)` — required system tag for all UI output
- `#if DEBUG` gates only apply to debug-inspector tooling; hand mechanics ship in release

### Project Context Rules

**Godot dependency boundary:**
- `DeckManager.cs` — pure C#; must compile in test project with no Godot SDK; added to `tests/maguswarrior.Tests.csproj`
- `CardCompact.cs`, `CardExpanded.cs`, `HandView.cs` — inherit Godot types; do NOT add to test project
- Never use `GD.Print`; always `Log.Debug`

**Dependency injection:**
- `HandView` receives `DeckManager` and `GameState` via `Initialize()` — not static access
- `CardCompact` receives `CardDefinition` and `GamePhase` via `Initialize()` — not from scene signals
- `CardExpanded` receives `CardDefinition` and `GamePhase` via `Open()` — not constructed with them

**Error handling:**
- `DeckManager.SetHand` does not validate — callers own the validity of the list
- `HandView.OnCardTapped`: `First()` will throw if card not found — acceptable; this would indicate a bug in the hand population, not a player action

**UX contract from specification:**
- ≥40% opacity reduction on non-playable state (`Modulate = new Color(1, 1, 1, 0.5f)`)
- Minimum 44px button separation (`HBoxContainer.separation = 20` plus default button padding exceeds this)
- Cancel positionally distinct — at top of VBoxContainer, above card name and action bar
- One expanded card at a time — enforced in `OnCardTapped`

### References

- Architecture Hand Display contract: `_bmad-output/game-architecture.md` §Screen Contracts → Hand Display / Card Play Flow
- Effect LLD sideways rules: `docs/effect-lld.md` §Universal Rules → Sideways Play
- Effect LLD rage: `docs/effect-lld.md` §rage (unpowered choose_one, powered Attack 4)
- Phase gate: `scripts/cards/effects/PhaseGate.cs` — `PhaseGate.IsLegal(EffectType, GamePhase)`
- Card definitions: `data/cards.yaml` (march = Move, stamina = Move, rage = AttackMelee, swiftness = Move)
- Epics: `_bmad-output/epics.md` §Epic 1b — scope, stories, deliverable
- UX spec component architecture: `_bmad-output/planning-artifacts/ux-design-specification.md` §Component Architecture (`CardCompact`, `CardExpanded`)
- UX spec UX contract: `_bmad-output/planning-artifacts/ux-design-specification.md` §UX Contract Requirements (expand timing, one-at-a-time, opacity, button separation)
- Project context rules: `docs/project-context.md` (Godot boundary, logging, DI, file placement)
- Previous story patterns: `_bmad-output/implementation-artifacts/1a-3-full-effect-event-log-in-inspector.md` §Dev Notes (namespace table, partial class pattern, `_Ready` wiring, `QueueFree` convention)

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

- All 7 tasks completed. 22/22 xUnit tests pass (19 existing + 3 new DeckManagerTest).
- `DeckManager.cs` created in `scripts/deck/` — pure C#, no Godot dependency. `Hand` (IReadOnlyList), `SetHand`, `HandChanged` event. Added to test project compile list.
- `CardCompact.tscn` + `CardCompact.cs` created in `scenes/components/` / `scripts/ui/components/`. Root Control with PanelContainer layout and flat Button overlay (`TapTarget`). Emits `CardTapped(string)` signal. `IsCardPlayable` returns true for all cards in 1b-1 (greying delegated to CardExpanded).
- `CardExpanded.tscn` + `CardExpanded.cs` created. Root Control, hidden by default, z_index=3. CancelButton at top, CardNameLabel, EffectTextLabel, ActionBar with Play/Sideways/Power. `Open()` sets Play disabled based on `PhaseGate.IsLegal`, PowerButton always disabled (no ManaPool yet). `Close()` hides. Signals: PlayRequested, PlaySidewaysRequested, PowerRequested, CancelRequested.
- `HandView.tscn` + `HandView.cs` created. Anchored to bottom strip (offset_top=-350). CardExpandedPanel instanced from CardExpanded.tscn. CardsContainer (HBoxContainer) at bottom 130px. `[Export] PackedScene CardCompactScene` wired in scene to CardCompact.tscn. `Initialize(DeckManager, GameState)` → `RefreshHand()` → instantiates CardCompact per card, connects CardTapped. One-card-at-a-time enforced in `OnCardTapped`.
- `PlaceholderMainMenu.tscn` updated: load_steps=4, HandView.tscn instanced as child. `PlaceholderMainMenu.cs` updated: `_deckManager = new()`, seeds hand with first 4 non-Wound cards from `_state.Cards`, calls `handView.Initialize(_deckManager, _state)`.
- Godot project builds with 0 warnings, 0 errors.
- `HandView.cs`, `CardCompact.cs`, `CardExpanded.cs` NOT added to test project (Godot dependencies).

### File List

- `scripts/deck/DeckManager.cs` (new)
- `scripts/ui/components/CardCompact.cs` (new)
- `scripts/ui/components/CardExpanded.cs` (new)
- `scripts/ui/components/HandView.cs` (new)
- `scenes/components/CardCompact.tscn` (new)
- `scenes/components/CardExpanded.tscn` (new)
- `scenes/components/HandView.tscn` (new)
- `tests/unit/DeckManagerTest.cs` (new)
- `tests/maguswarrior.Tests.csproj` (modified — added DeckManager.cs compile entry)
- `scenes/screens/PlaceholderMainMenu.tscn` (modified — added HandView instance, load_steps=4)
- `scripts/ui/screens/PlaceholderMainMenu.cs` (modified — DeckManager field, hand seeding, handView.Initialize)
