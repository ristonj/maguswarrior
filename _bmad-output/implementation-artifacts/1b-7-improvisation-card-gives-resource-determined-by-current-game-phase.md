# Story 1b.7: Improvisation Card Gives Resource Determined by Current Game Phase

Status: done

## Story

As a player,
I want to play the Improvisation card by discarding another card from my hand to gain a resource determined by the current game phase,
so that Improvisation gives me exactly the resource I need right now — not a free choice — matching the phase I'm in.

## Acceptance Criteria

### AC 1 — `scripts/cards/ImprovisationRule.cs` (pure C#)

Create `scripts/cards/ImprovisationRule.cs` — pure C#, namespace `MagusWarrior.Cards`, no Godot dependency:

- `public static class ImprovisationRule`
- `public static EffectType? GetPhaseEffect(GamePhase phase)` — returns the single legal effect type for each phase, or null when none of Improvisation's options are legal:
  - `GamePhase.Movement` → `EffectType.Move`
  - `GamePhase.CombatMelee` → `EffectType.AttackMelee`
  - `GamePhase.CombatBlock` → `EffectType.Block`
  - `GamePhase.Interaction` → `EffectType.Influence`
  - All others (Rest, CombatStart, CombatRanged, CombatAssignDamage, EndOfTurn, Any) → `null`
- `public static bool CanPlay(IReadOnlyList<CardDefinition> hand, string improvisationId)` — returns true if the hand contains at least one card whose Id is not `improvisationId`; returns false for an empty hand or a hand that contains only Improvisation
- `public static int GetAmount(bool powered)` → `powered ? 5 : 3`
- Required usings: `System.Collections.Generic`, `MagusWarrior.Core.Types`

### AC 2 — `scripts/cards/effects/special/ImprovisationEffect.cs` (pure C#)

Create `scripts/cards/effects/special/ImprovisationEffect.cs` — pure C#, namespace `MagusWarrior.Cards.Effects.Special`, no Godot dependency. Directory `scripts/cards/effects/special/` is new — create it.

- `public class ImprovisationEffect : IEffect`
- Constructor: `public ImprovisationEffect(EffectType effectType, int amount)` — stores both
- `Execute(GameState state, EffectContext ctx)`:
  - Switches on `_effectType`:
    - `EffectType.Move` → `state.AddMovePoints(_amount)`
    - `EffectType.AttackMelee` → `state.AddAttackPoints(_amount, EffectType.AttackMelee, AttackElement.Physical)`
    - `EffectType.Block` → `state.AddBlockPoints(_amount, AttackElement.Physical)`
    - `EffectType.Influence` → `state.AddInfluencePoints(_amount)`
    - `_` → throw `InvalidOperationException($"ImprovisationEffect: unsupported EffectType {_effectType}")` — this path is unreachable; ImprovisationView only calls this for validated phase effects
  - Returns `Task.FromResult(EffectResult.Ok())`
- Required usings: `System.Threading.Tasks`, `MagusWarrior.Core`, `MagusWarrior.Core.Types`

### AC 3 — `scripts/ui/components/CardExpanded.cs` updated (multi-type Play button)

`CardExpanded.Open` currently gates the Play button on the primary effect type only. For multi-type cards like Improvisation (`card.Unpowered.EffectType = Move` with alternates `[Influence, AttackMelee, Block]`), this disables Play in all phases except Movement. Fix:

Change the Play-button disabled logic from:
```csharp
_playButton.Disabled = !(card.Unpowered != null &&
    PhaseGate.IsLegal(card.Unpowered.EffectType, currentPhase));
```
To:
```csharp
bool hasLegalPlay = card.Unpowered != null && (
    PhaseGate.IsLegal(card.Unpowered.EffectType, currentPhase) ||
    card.AlternateEffectTypes.Any(t => PhaseGate.IsLegal(t, currentPhase)));
_playButton.Disabled = !hasLegalPlay;
```

Add `using System.Linq;` to CardExpanded.cs (after `using Godot;`).

After fix, Improvisation's Play button is enabled in Movement, CombatMelee, CombatBlock, and Interaction. It remains disabled in Rest (correct — no Improvisation option is legal in Rest) and CombatRanged (correct — Improvisation has no ranged attack).

### AC 4 — `scripts/ui/components/ImprovisationView.cs` (new Godot component)

Create `scripts/ui/components/ImprovisationView.cs` — Godot `Control`, namespace `MagusWarrior.UI`, `public partial class ImprovisationView : Control`.

Fields:
- `private DeckManager _deck = null!;`
- `private GameState _state = null!;`
- `private EffectScheduler _scheduler = null!;`
- `private Label _statusLabel = null!;`
- `private VBoxContainer _discardPanel = null!;`
- `private HBoxContainer _resourcePanel = null!;`

**`_Ready()`** — builds full-screen overlay layout:
```
Anchor: full-rect (AnchorLeft=0, AnchorRight=1, AnchorTop=0, AnchorBottom=1)
ZIndex = 5  ← above HandView (0), StagingAreaView (1), CardExpanded (3)
Visible = false  ← hidden until Activate is called

PanelContainer (full-rect) contains:
  VBoxContainer (full-rect layout):
    _statusLabel (Label, font_size=28, AutowrapMode=Word)
    _discardPanel (VBoxContainer) ← populated dynamically
    _resourcePanel (HBoxContainer, Visible=false) ← 4 option buttons
```

**`public void Initialize(DeckManager deck, GameState state, EffectScheduler scheduler)`** — assign fields; no event subscriptions in Initialize (overlay is activated on-demand, not event-driven).

**`public void Activate(CardDefinition improvCard)`** — called by HandView when Improvisation Play is tapped:
1. Remove Improvisation from hand first (atomicity, same discipline as OnPlayRequested):
   ```csharp
   var playResult = _deck.PlayCard(improvCard.Id);
   if (!playResult.IsSuccess) {
       Log.Warn("[UI]", $"ImprovisationView.Activate: PlayCard failed: {playResult.Error}");
       return;
   }
   ```
2. Check `ImprovisationRule.CanPlay(_deck.Hand, improvCard.Id)` — at this point improvCard is already removed, so check `_deck.Hand.Count > 0`. If false (hand is now empty after removing Improvisation), return the card, log warn, and close. In practice this is blocked at the `CardExpanded.Open` stage (Play button disabled would require a different guard — see Dev Notes), but the defensive check prevents a broken state.
3. `_statusLabel.Text = "Improvisation: discard a card to activate"`
4. `BuildDiscardPanel()`
5. `_resourcePanel.Visible = false`
6. `Visible = true`
7. `Log.Debug("[UI]", $"ImprovisationView activated in {_state.CurrentPhase}")`

**`BuildDiscardPanel()`** — clears and rebuilds discard rows from current hand:
- Remove all children from `_discardPanel`
- For each card in `_deck.Hand`:
  ```
  HBoxContainer row:
    Label (card.Name, font_size=28, SizeFlagsHorizontal=Expand+Fill)
    Button ("Discard", font_size=28)
      Pressed: () => OnDiscardSelected(card.Id)
  ```
- All cards are offered including Wounds (Improvisation can discard any card as cost)

**`OnDiscardSelected(string discardCardId)`**:
```csharp
var r = _deck.DiscardCard(discardCardId);
if (!r.IsSuccess) {
    Log.Warn("[UI]", $"ImprovisationView: DiscardCard failed: {r.Error}");
    return;
}
Log.Debug("[UI]", $"ImprovisationView: {discardCardId} discarded, building resource panel in {_state.CurrentPhase}");

// Clear discard rows
foreach (Node child in _discardPanel.GetChildren())
    child.QueueFree();

_statusLabel.Text = "Choose your resource:";
BuildResourcePanel();
_resourcePanel.Visible = true;
```

**`BuildResourcePanel()`** — clears and rebuilds the 4-option button row:
- Remove all children from `_resourcePanel`
- Options (in order): Move, Attack, Block, Influence:
  ```
  (EffectType.Move,       "Move 3")
  (EffectType.AttackMelee, "Attack 3")
  (EffectType.Block,       "Block 3")
  (EffectType.Influence,   "Influence 3")
  ```
  Amounts use `ImprovisationRule.GetAmount(false)` (unpowered; powered path deferred).
- For each option:
  ```csharp
  var btn = new Button();
  btn.Text = label;
  btn.AddThemeFontSizeOverride("font_size", 32);
  btn.Disabled = !PhaseGate.IsLegal(effectType, _state.CurrentPhase);
  var capturedType = effectType;
  var capturedAmount = ImprovisationRule.GetAmount(false);
  btn.Pressed += () => OnResourceSelected(capturedType, capturedAmount);
  _resourcePanel.AddChild(btn);
  ```
- Effect: in Movement phase, only "Move 3" is enabled; in CombatMelee only "Attack 3"; in CombatBlock only "Block 3"; in Interaction only "Influence 3". All four are disabled in Rest/CombatRanged (but Activate should never be called in those phases — see Dev Notes).

**`OnResourceSelected(EffectType effectType, int amount)`** — `async void` (Godot signal handler; safe because ResolveAll is currently synchronous):
```csharp
private async void OnResourceSelected(EffectType effectType, int amount) {
    Log.Debug("[UI]", $"Improvisation: applying {effectType} {amount} in {_state.CurrentPhase}");
    var effect = new ImprovisationEffect(effectType, amount);
    var ctx = new EffectContext("improvisation", effectType, _state.CurrentPhase, false);
    _scheduler.Enqueue(effect, 0, ctx);
    await _scheduler.ResolveAll(_state);
    Visible = false;
    Log.Debug("[UI]", "ImprovisationView closed");
}
```

Required usings: `System.Threading.Tasks`, `Godot`, `MagusWarrior.Cards`, `MagusWarrior.Cards.Effects`, `MagusWarrior.Cards.Effects.Special`, `MagusWarrior.Core`, `MagusWarrior.Core.Types`, `MagusWarrior.Deck`

NOT added to the test project (Godot dependency).

### AC 5 — `scripts/ui/components/HandView.cs` updated

**Add field:**
```csharp
private ImprovisationView? _improvView;
```

**Add method:**
```csharp
public void SetImprovisationView(ImprovisationView view) {
    _improvView = view;
}
```

**Update `OnPlayRequested(string cardId)`** — add Improvisation intercept BEFORE the existing `_deck.PlayCard(cardId)` call (Activate handles PlayCard internally):
```csharp
// Intercept multi-step cards before the normal stage path.
if (cardId == "improvisation" && _improvView != null) {
    _improvView.Activate(card);
    return;
}
```
Insert this block after the wound guard and Rest guard, before the `card.Unpowered is null` check.

No other changes to HandView. `OnPlaySidewaysRequested`, `OnCommitRequested`, `OnUndoRequested` — unchanged.

### AC 6 — `scripts/ui/screens/PlaceholderMainMenu.cs` updated

**Add field:**
```csharp
private ImprovisationView _improvView = null!;
```

**Replace test hand construction** (currently takes first 4 non-Wound cards dynamically) with an explicit hand that guarantees Improvisation is present and testable:
```csharp
// Build test hand with Improvisation first so it's always verifiable on device.
var allNonWound = _state.Cards.Where(c => c.Type != CardType.Wound).ToList();
var improv = allNonWound.First(c => c.Id == "improvisation");
var others = allNonWound.Where(c => c.Id != "improvisation").Take(3).ToList();
var testHand = new List<CardDefinition> { improv };
testHand.AddRange(others);
testHand.Add(WoundCard.Create());
_deckManager.SetHand(testHand);
```

**After RestView wiring, add ImprovisationView:**
```csharp
_improvView = new ImprovisationView();
_improvView.Name = "ImprovisationView";
AddChild(_improvView);
_improvView.Initialize(_deckManager, _state, _effectScheduler);
handView.SetImprovisationView(_improvView);
```
Note: `SetImprovisationView` must be called AFTER `handView.Initialize(...)`. Order matters: Initialize subscribes signals; SetImprovisationView sets a nullable field. Both are idempotent but must not be reversed.

`using System.Collections.Generic;` is required for `new List<CardDefinition>` — check if already present. `using System.Linq;` is already on line 1.

### AC 7 — `tests/unit/ImprovisationRuleTest.cs` (12 xUnit tests)

Namespace: `MagusWarrior.Tests`. Usings: `MagusWarrior.Cards`, `MagusWarrior.Core.Types`, `Xunit`, `System.Collections.Generic`.

**GetPhaseEffect tests (7):**
- `GetPhaseEffect_Movement_ReturnsMove`
- `GetPhaseEffect_CombatMelee_ReturnsAttackMelee`
- `GetPhaseEffect_CombatBlock_ReturnsBlock`
- `GetPhaseEffect_Interaction_ReturnsInfluence`
- `GetPhaseEffect_Rest_ReturnsNull`
- `GetPhaseEffect_CombatRanged_ReturnsNull`
- `GetPhaseEffect_EndOfTurn_ReturnsNull`

**CanPlay tests (3):**
- `CanPlay_HandWithOtherCard_ReturnsTrue` — hand = [improv, march], id = "improvisation" → true
- `CanPlay_HandImprovisationOnly_ReturnsFalse` — hand = [improv], id = "improvisation" → false
- `CanPlay_EmptyHand_ReturnsFalse` — empty list, id = "improvisation" → false

**GetAmount tests (2):**
- `GetAmount_Unpowered_Returns3` — `GetAmount(false)` == 3
- `GetAmount_Powered_Returns5` — `GetAmount(true)` == 5

### AC 8 — `tests/unit/ImprovisationEffectTest.cs` (4 xUnit tests)

Namespace: `MagusWarrior.Tests`. Usings: `MagusWarrior.Cards.Effects`, `MagusWarrior.Cards.Effects.Special`, `MagusWarrior.Core`, `MagusWarrior.Core.Types`, `Xunit`.

Test construction pattern: `new GameState(cards)` needs a card list. Use the helper from prior tests if available, or pass an empty list `Array.Empty<CardDefinition>()` and use `new GameState(Array.Empty<CardDefinition>())` since ImprovisationEffect reads no card data.

- `Execute_Move_AddsMovePoints` — construct `ImprovisationEffect(EffectType.Move, 3)`, call `Execute(state, ctx)`, assert `state.MovePointsThisTurn == 3`
- `Execute_AttackMelee_AddsAttackPoints` — `ImprovisationEffect(EffectType.AttackMelee, 3)`, assert attack pool contains (AttackMelee, Physical) entry with 3
- `Execute_Block_AddsBlockPoints` — `ImprovisationEffect(EffectType.Block, 3)`, assert block pool has 3
- `Execute_Influence_AddsInfluencePoints` — `ImprovisationEffect(EffectType.Influence, 3)`, assert `state.InfluencePointsThisTurn == 3`

For `Execute_AttackMelee_AddsAttackPoints` and `Execute_Block_AddsBlockPoints`: inspect `state.AttackPool` and `state.BlockPool` (use the same pattern as existing `EffectSystemTest.cs`).

EffectContext: `new EffectContext("improvisation", <effectType>, GamePhase.Movement, false)`.

### AC 9 — `tests/maguswarrior.Tests.csproj` updated

Add compile entries (Godot-free files only):
```xml
<Compile Include="../scripts/cards/ImprovisationRule.cs" />
<Compile Include="../scripts/cards/effects/special/ImprovisationEffect.cs" />
```

`ImprovisationView.cs`, `CardExpanded.cs`, `HandView.cs`, `PlaceholderMainMenu.cs` — NOT added (Godot dependencies). Test files `ImprovisationRuleTest.cs` and `ImprovisationEffectTest.cs` are auto-discovered by the xUnit SDK.

### AC 10 — Pass-all gate + deferred-work update

`dotnet test tests/maguswarrior.Tests.csproj` — all 65 existing tests + 12 `ImprovisationRuleTest` + 4 `ImprovisationEffectTest` = **81 green**. Zero warnings.

`dotnet build maguswarrior.csproj` — 0 errors, 0 warnings.

Update `_bmad-output/implementation-artifacts/deferred-work.md`:
- Partially resolve the 1a-1 deferred item: "`AlternateEffectTypes` and `LegalPhases` never populated from YAML" → add note that `AlternateEffectTypes` IS populated by `CardLoader` (multi-type YAML list deserialization was already implemented). `CardExpanded` now checks `AlternateEffectTypes` for multi-type cards (resolved user-visible half). `PhaseValidator.ValidatePhase`'s alternate-effect path is still never called from any game-logic code path (the other half remains deferred).

---

## Tasks / Subtasks

- [x] Task 1: ImprovisationRule pure C# logic (AC 1, 7, 9)
  - [x] Create `scripts/cards/ImprovisationRule.cs`
  - [x] Add `<Compile Include="../scripts/cards/ImprovisationRule.cs" />` to `tests/maguswarrior.Tests.csproj`
  - [x] Create `tests/unit/ImprovisationRuleTest.cs` — 12 tests
  - [x] Run `dotnet test` — confirm 12 new tests green (77 total)

- [x] Task 2: ImprovisationEffect pure C# (AC 2, 8, 9)
  - [x] Create `scripts/cards/effects/special/` directory (new)
  - [x] Create `scripts/cards/effects/special/ImprovisationEffect.cs`
  - [x] Add `<Compile Include="../scripts/cards/effects/special/ImprovisationEffect.cs" />` to `tests/maguswarrior.Tests.csproj`
  - [x] Create `tests/unit/ImprovisationEffectTest.cs` — 4 tests
  - [x] Run `dotnet test` — confirm 4 new tests green (81 total)

- [x] Task 3: CardExpanded multi-type Play button fix (AC 3)
  - [x] Add `using System.Linq;` to CardExpanded.cs
  - [x] Update `_playButton.Disabled` logic in `Open()` to check AlternateEffectTypes
  - [x] `dotnet build maguswarrior.csproj` — 0 errors, 0 warnings

- [x] Task 4: ImprovisationView Godot component (AC 4)
  - [x] Create `scripts/ui/components/ImprovisationView.cs`
  - [x] `dotnet build maguswarrior.csproj` — 0 errors, 0 warnings

- [x] Task 5: HandView + PlaceholderMainMenu wiring (AC 5, 6)
  - [x] Add `_improvView` field and `SetImprovisationView` method to HandView.cs
  - [x] Add Improvisation intercept in `OnPlayRequested`
  - [x] Add ImprovisationView instantiation and wiring in PlaceholderMainMenu._Ready()
  - [x] Update test hand to explicitly include improvisation
  - [x] `dotnet build maguswarrior.csproj` — 0 errors, 0 warnings

- [x] Task 6: Full regression + deferred-work update (AC 10)
  - [x] Run `dotnet test tests/maguswarrior.Tests.csproj` — 81/81 green, 0 warnings
  - [x] Run `dotnet build maguswarrior.csproj` — 0 errors, 0 warnings
  - [x] Update `deferred-work.md` — partially resolve AlternateEffectTypes deferred item

---

## Dev Notes

### Epics Story Discrepancy — "Healing during special"

The epics story says: "Move during movement, Block during block, Attack during melee, Influence during interaction, **Healing during special**." The effect-lld.md (the authoritative resolved design document) specifies Improvisation's four options as Move / Attack / Block / Influence. Healing is NOT an Improvisation option. `cards.yaml` confirms: `effect_type: [Move, Influence, Combat, Block]` — no Heal. The "Healing during special" in the epic appears to be an authoring error. The AC above follows the effect-lld.

Similarly, there is no "Special" phase in `GamePhase` enum — Special is an `EffectType`, not a `GamePhase`. `ImprovisationRule.GetPhaseEffect` returns `null` for all phases where none of Move/AttackMelee/Block/Influence are legal (including Rest, CombatRanged, CombatStart, CombatAssignDamage).

### Why Improvisation Bypasses the Normal Stage Path

The staging area (`StagingManager`, `StagingAreaView`) was designed for cards where the effect type is chosen before staging — the player commits to "I'm playing March as Move". Improvisation's effect type isn't known until after the discard AND the resource panel interaction (phase determines it, but the player must still see and tap the phase-filtered option). There is no sensible "staged Improvisation with EffectType=unknown" to represent.

Additionally, Improvisation has no numeric values in its `EffectSpec` fields — `cards.yaml` stores the amount under `discard_for.amount` (unparsed by CardLoader). `card.Unpowered.Move == 0`, `.Attack == 0`, etc. Staging Improvisation via the normal path would show "Move: 0" in the running totals and apply nothing on commit. The ImprovisationView handles the full flow end-to-end (discard → resource panel → apply) without touching the staging manager.

Note: the 1b-3 deferred item says "belongs to the `choose_one` story (1b-7 / combat)." That was an incorrect prediction. This story is about Improvisation (phase-determined resource), NOT about `choose_one` cards (Rage's Attack-or-Block choice). The `choose_one` deferred item remains open.

### AlternateEffectTypes — Status

`CardLoader.ParseAll` already populates `AlternateEffectTypes` from the YAML list when `effect_type` is a sequence of more than one item. For Improvisation:
- `card.Unpowered.EffectType` = `EffectType.Move` (first item)
- `card.AlternateEffectTypes` = `[EffectType.Influence, EffectType.AttackMelee, EffectType.Block]`

The 1a-1 deferred item stating these are "never populated from YAML" is stale for `AlternateEffectTypes`. AC 3 closes the user-visible gap by using `AlternateEffectTypes` in `CardExpanded.Open`. `PhaseValidator.ValidatePhase`'s `AlternateEffectTypes` check has no call site (dead code) — that half stays deferred.

### Can Play Check After PlayCard

In `ImprovisationView.Activate`, `_deck.PlayCard(improv.Id)` is called first (atomicity discipline: card leaves hand before resource is granted). After that, `_deck.Hand` is the remaining hand. The `CanPlay` check (`_deck.Hand.Count > 0`) confirms there is at least one card to discard. If somehow Activate is called with a hand of size 1 (only Improvisation), after PlayCard the hand is empty — the defensive check returns early and logs a warning, preventing an empty discard panel from opening.

In normal play, this edge case is unreachable: `CardExpanded.Open` disables the Play button when `!hasLegalPlay` (AC 3). A hand of size 1 containing only Improvisation would still show Play enabled in Movement (Move is legal), but the discard step would have no cards. This is an implicit "can't play Improvisation alone" gate. The effect-lld notes: "Precondition: player must have at least 1 other card in hand to discard. If hand contains only Improvisation, it can only be played sideways." A future story should add this precondition check to disable the Play button in CardExpanded when CanPlay would return false. Not in scope here — the defensive check in Activate is sufficient.

### Resource Panel — "Not My Free Choice" vs 4-Option Panel

The epics story says "not my free choice." The effect-lld says "show 4-option panel, phase filter." These are consistent: in any standard game phase, exactly one of the four options is enabled. The player taps the only enabled button — there is no real choice. The 4-option panel with grayed-out options:
- Makes it visually clear what Improvisation CAN produce in other phases
- Matches the resolved LLD decision ("shown disabled, not hidden")
- Is consistent with the "not my free choice" experience (one enabled = forced)

The Resolved Questions in effect-lld.md (§2027) confirms: "grayed-out vs unplayable: Card can be played; illegal options are shown disabled in the panel (grayed-out), not hidden."

### HandView `OnPlayRequested` Intercept Point

The intercept goes AFTER the wound guard and Rest guard, BEFORE the `card.Unpowered is null` check:

```csharp
private void OnPlayRequested(string cardId) {
    var card = _deck.Hand.FirstOrDefault(c => c.Id == cardId);
    if (card is null) { Log.Debug(...); return; }           // existing: stale tap
    if (card.Type == CardType.Wound) { Log.Warn(...); return; }  // existing: wound guard
    if (Rest guard) { Log.Warn(...); return; }               // existing: Rest guard
    // NEW — route Improvisation before the staging path
    if (cardId == "improvisation" && _improvView != null) {
        _improvView.Activate(card);
        return;
    }
    if (card.Unpowered is null) { Log.Warn(...); return; }   // existing
    var result = _deck.PlayCard(cardId);                      // existing
    ...
}
```

The intercept returns immediately — `_improvView.Activate(card)` calls `_deck.PlayCard` internally. Do NOT call `_deck.PlayCard` in `OnPlayRequested` before routing to Activate.

### Async void in ImprovisationView

`OnResourceSelected` is `async void` because it's wired to a Godot `Pressed` signal which cannot return `Task`. This is the same accepted exception as `HandView.OnCommitRequested` and `HandView.OnPlaySidewaysRequested`. Safe because `_scheduler.ResolveAll` currently resolves synchronously (`Task.FromResult` in all effects). When UIBroker introduces genuinely awaitable effects, this handler will need a re-entrancy guard (same pattern as HandView's `_committing` flag).

### On-Device Verification (Movement Phase Only)

The test hand is `[improvisation, <3 others>, wound]`. Default phase is Movement.

Happy path (verify step by step):
1. Tap Improvisation card → CardExpanded opens; "Play" button enabled (Move is legal in Movement)
2. Tap "Play" → ImprovisationView opens showing the 3 non-Wound cards with "Discard" buttons (the Wound is NOT shown — rulebook p4–5: Wounds are not a legal discard cost); status = "Improvisation: discard a card to activate"
3. Tap "Discard" on any card → discard rows replaced by resource panel; status = "Choose your resource:"
4. Resource panel shows: **Move 3** (enabled), Attack 3 (disabled), Block 3 (disabled), Influence 3 (disabled)
5. Tap "Move 3" → ImprovisationView closes; effect inspector shows `MoveEffect(3)` fired from "improvisation"; running Move total increases by 3

Multi-phase verification (requires manually switching phase via `GameDebug.SkipToPhase` or similar — not all phases are reachable in the current scaffold):
- CombatMelee: only "Attack 3" enabled
- CombatBlock: only "Block 3" enabled
- Interaction: only "Influence 3" enabled
- Rest: all 4 options disabled (Activate should not be called — HandView's Rest guard blocks Improvisation from opening CardExpanded)

### Carry-Forward Notes from 1b-6

- `async void` only in Godot signal handlers that await. `OnResourceSelected` is `async void` — acceptable for the same reasons as `OnCommitRequested` and `OnPlaySidewaysRequested`.
- `ImplicitUsings=disable`: all `using` statements must be explicit in every `.cs` file.
- No `GD.Print` — use `Log.Debug`/`Log.Warn` with `[UI]` tag in ImprovisationView.
- Subscription leak: no subscriptions in `ImprovisationView.Initialize` (overlay is demand-activated). No unsubscribe needed.
- Test construction: `new GameState(Array.Empty<CardDefinition>())` for effect-only tests that don't need card data.

### File Placement

| File | Action | Type |
|------|--------|------|
| `scripts/cards/ImprovisationRule.cs` | new | Pure C# |
| `scripts/cards/effects/special/ImprovisationEffect.cs` | new (new dir) | Pure C# |
| `scripts/ui/components/CardExpanded.cs` | modified — Linq + multi-type Play button | Godot Control |
| `scripts/ui/components/ImprovisationView.cs` | new | Godot Control |
| `scripts/ui/components/HandView.cs` | modified — `_improvView` field, `SetImprovisationView`, intercept in `OnPlayRequested` | Godot Control |
| `scripts/ui/screens/PlaceholderMainMenu.cs` | modified — ImprovisationView field + wiring + test hand | Godot CanvasLayer |
| `tests/unit/ImprovisationRuleTest.cs` | new — 12 tests | xUnit |
| `tests/unit/ImprovisationEffectTest.cs` | new — 4 tests | xUnit |
| `tests/maguswarrior.Tests.csproj` | modified — add 2 compile entries | project |
| `_bmad-output/implementation-artifacts/deferred-work.md` | modified — partially resolve AlternateEffectTypes item | doc |

### Namespace Conventions

| Folder | Namespace |
|--------|-----------|
| `scripts/cards/` | `MagusWarrior.Cards` |
| `scripts/cards/effects/special/` | `MagusWarrior.Cards.Effects.Special` |
| `scripts/ui/components/` | `MagusWarrior.UI` |
| `scripts/ui/screens/` | `MagusWarrior.UI` |
| `tests/unit/` | `MagusWarrior.Tests` |

### Project Context Rules

**Pure C# boundary:** `ImprovisationRule.cs`, `ImprovisationEffect.cs` — no Godot dependency; added to test csproj. `ImprovisationView.cs`, `CardExpanded.cs`, `HandView.cs`, `PlaceholderMainMenu.cs` — Godot nodes, NOT in the test project.

**Constructor injection / No service locator:** `ImprovisationView.Initialize(DeckManager, GameState, EffectScheduler)` follows the same pattern as `RestView.Initialize` and `HandView.Initialize`. `ImprovisationRule` and `ImprovisationEffect` have no instance state beyond constructor args.

**async/await:** `OnResourceSelected` is `async void` (Godot signal exception). Currently safe because effects are synchronous. `ImprovisationRule`, `ImprovisationEffect`, `BuildDiscardPanel`, `OnDiscardSelected` — all synchronous void/return.

**Result<T>:** `_deck.PlayCard` and `_deck.DiscardCard` both return `Result<T>`. `Activate` and `OnDiscardSelected` check `IsSuccess`; on failure they log and return. Never throw for "card not in hand."

**Logging tags:** `[UI]` in ImprovisationView and HandView. No logging inside ImprovisationRule or ImprovisationEffect.

**Events are data-only:** No new C# events or Godot signals introduced.

**No hardcoded per-card logic in resolvers:** `ImprovisationEffect` is a generic phase-resource effect. `HandView.OnPlayRequested` intercepts by `cardId == "improvisation"` — this is routing, not per-card logic in a resolver. The effect resolver (`EffectScheduler`) never sees the "improvisation" id directly.

**Phase Gate:** The resource panel's button enabled state is determined by `PhaseGate.IsLegal`, not by hardcoded phase checks. If the PhaseGate table ever changes, the resource panel automatically reflects it.

**LOCKSTEP:** No new GameState mutators. `AddMovePoints`, `AddAttackPoints`, `AddBlockPoints`, `AddInfluencePoints` are existing methods. `DiscardCard` mutates Hand (already noted as outside snapshot in 1b-6). No snapshot change required.

### References

- GDD Improvisation card: `_bmad-output/gdd.md` (Improvisation card description)
- Effect LLD Improvisation section: `docs/effect-lld.md:182-206`
- Effect LLD Resolved Questions (grayed-out vs unplayable): `docs/effect-lld.md:2027-2032`
- Epics 1b-7 story: `_bmad-output/epics.md:129-132`
- Architecture UIBroker pattern: `_bmad-output/game-architecture.md:263-278`
- Architecture PhaseGate table: `_bmad-output/game-architecture.md:756-793`
- Architecture file placement map: `_bmad-output/game-architecture.md:583-593`
- cards.yaml Improvisation entry: `data/cards.yaml:217-233`
- GamePhase enum: `scripts/core/types/GamePhase.cs`
- EffectType enum: `scripts/core/types/EffectType.cs`
- PhaseGate.IsLegal: `scripts/cards/effects/PhaseGate.cs:28`
- CardExpanded.Open phase-gate logic: `scripts/ui/components/CardExpanded.cs:91-93`
- HandView.OnPlayRequested (insertion point for intercept): `scripts/ui/components/HandView.cs:134-175`
- DeckManager.DiscardCard (from 1b-6): `scripts/deck/DeckManager.cs`
- DeckManager.PlayCard: `scripts/deck/DeckManager.cs`
- GameState resource mutators: `scripts/core/GameState.cs:41-49`
- RestView (pattern reference for overlay component): `scripts/ui/components/RestView.cs`
- Previous story 1b-6 dev notes: `_bmad-output/implementation-artifacts/1b-6-declare-rest-turn-and-discard-per-rest-rules.md`
- Deferred work tracker: `_bmad-output/implementation-artifacts/deferred-work.md`
- Existing attack/block effect implementations: `scripts/cards/effects/combat/AttackEffect.cs`, `scripts/cards/effects/combat/BlockEffect.cs`
- EffectContext record: `scripts/cards/effects/EffectContext.cs`
- ImplicitUsings setting: `maguswarrior.csproj` (disabled — all usings must be explicit)

---

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

- All 6 tasks completed. 81/81 xUnit tests pass (65 existing + 12 ImprovisationRuleTest + 4 ImprovisationEffectTest). Godot build: 0 errors, 0 warnings.
- `ImprovisationRule.cs` created — pure C# static class in `scripts/cards/`; `GetPhaseEffect` maps Movement→Move, CombatMelee→AttackMelee, CombatBlock→Block, Interaction→Influence, null otherwise. `CanPlay` checks for at least one other card to discard. `GetAmount` returns 3 (unpowered) or 5 (powered). Added to test csproj.
- `scripts/cards/effects/special/` directory created (new domain subdirectory). `ImprovisationEffect.cs` implements `IEffect` — applies Move/AttackMelee/Block/Influence to `GameState` based on constructor-injected `EffectType`. Added to test csproj.
- `CardExpanded.cs` updated — added `using System.Linq;`; `_playButton.Disabled` now checks primary `EffectType` OR any `AlternateEffectType` against `PhaseGate.IsLegal`. Improvisation's Play button now enables in all four valid game phases (Movement, CombatMelee, CombatBlock, Interaction).
- `ImprovisationView.cs` created — Godot `Control` overlay (ZIndex=5) in `scripts/ui/components/`. `Activate(improvCard)` calls `PlayCard` first (atomicity), then shows discard panel. `OnDiscardSelected` calls `DiscardCard` then reveals the 4-option resource panel. `BuildResourcePanel` uses `PhaseGate.IsLegal` to enable only the phase-legal option. `OnResourceSelected` (async void) dispatches `ImprovisationEffect` via `EffectScheduler` and closes overlay.
- `HandView.cs` updated — `_improvView` nullable field + `SetImprovisationView` setter added. `OnPlayRequested` intercepts `cardId == "improvisation"` before the normal stage path, calling `_improvView.Activate(card)` instead.
- `PlaceholderMainMenu.cs` updated — added `_improvView` field; test hand now explicitly includes Improvisation as first card + 3 others + wound; `ImprovisationView` instantiated, initialized, and wired to `HandView` via `SetImprovisationView` after `handView.Initialize`.
- `deferred-work.md` updated — AlternateEffectTypes item partially resolved (CardExpanded now uses it; PhaseValidator call-site gap remains deferred).
- Epics discrepancy ("Healing during special") was already corrected in `epics.md` before dev started; story Dev Notes document the fix.

### File List

- `scripts/cards/ImprovisationRule.cs` (new)
- `scripts/cards/effects/special/ImprovisationEffect.cs` (new — new directory)
- `scripts/ui/components/ImprovisationView.cs` (new)
- `scripts/ui/components/CardExpanded.cs` (modified — System.Linq + AlternateEffectTypes check)
- `scripts/ui/components/HandView.cs` (modified — _improvView field, SetImprovisationView, intercept in OnPlayRequested)
- `scripts/ui/screens/PlaceholderMainMenu.cs` (modified — ImprovisationView field + wiring + explicit test hand)
- `tests/unit/ImprovisationRuleTest.cs` (new — 12 tests)
- `tests/unit/ImprovisationEffectTest.cs` (new — 4 tests)
- `tests/maguswarrior.Tests.csproj` (modified — 2 new compile entries)
- `_bmad-output/implementation-artifacts/deferred-work.md` (modified — AlternateEffectTypes partially resolved)

## Review Findings

Code review 2026-06-06 (Opus 4.8, develop-in-Sonnet / review-in-Opus split) — Blind Hunter + Edge Case Hunter + Acceptance Auditor. All 10 ACs satisfied (Acceptance Auditor: 81/81 tests green, 12+4 new confirmed, 0 project-context violations). Triage: 1 decision-needed, 2 patch, 3 defer, 10 dismissed.

### Decision Needed — RESOLVED 2026-06-06 (reclassified to Patch P3)

- [x] [Review][Decision→Patch] Wound is discardable as Improvisation's cost — RESOLVED: rulebook is explicit, this is a bug. **MKUE Rulebook p5 ("Discarding and Throwing Away"): "You may never discard a Wound card, unless the effect explicitly allows it."** **p4 (Basic Game Concepts): "'any card' refers to any card in your hand, except Wound cards."** Improvisation's text ("Discard another card from your hand") grants no explicit Wound permission, so Wounds are NOT a legal discard cost. Spec AC 4's "All cards are offered including Wounds" was incorrect and the code inherited it. Reclassified to patch P3 below. (blind+edge — Edge Case Hunter correctly flagged a real rules violation.)

### Patches

- [x] [Review][Patch] P1: `OnResourceSelected` has no re-entrancy guard and no try/finally — a double-tap (or a future genuinely-async `ResolveAll`) double-applies a resource, and a thrown effect leaves the overlay stuck `Visible=true`. Every sibling handler (`HandView.OnCommitRequested`) guards this window with a flag + try/finally. [scripts/ui/components/ImprovisationView.cs OnResourceSelected] (blind+edge, Medium) — **APPLIED.** Added `_resolving` guard field; `OnResourceSelected` now early-returns if re-entered, wraps the body in try/finally that guarantees `Visible=false` and resets the flag. Build clean, 81/81 green.
- [x] [Review][Patch] P2: `Activate` can strand the player if the resource panel opens in a phase where none of Move/Attack/Block/Influence is legal — all four buttons disabled after the Improvisation card was played AND a hand card discarded, with no way out. Unreachable today (Rest is blocked upstream by `HandView.OnPlayRequested`'s Rest guard), but a defensive check in `Activate` — refuse before `PlayCard` if no option is legal in the current phase — closes it permanently and matches the project's proactive-guarding culture. [scripts/ui/components/ImprovisationView.cs Activate] (blind+edge, Low/latent-High) — **APPLIED.** `Activate` now computes `anyOptionLegal` over the four resource types via `PhaseGate.IsLegal` BEFORE `PlayCard` and refuses (no rollback needed) if none is legal. Build clean.
- [x] [Review][Patch] P3: Wounds must not be discardable for Improvisation (rulebook p4–5, see resolved decision above). Fix `BuildDiscardPanel` to skip `CardType.Wound` cards. Because filtering Wounds means an all-Wound remaining hand would open an empty discard panel, also change `Activate`'s precondition from `_deck.Hand.Count == 0` to "no non-Wound card remains" (refuse + `ReturnCard` rollback in that case). [scripts/ui/components/ImprovisationView.cs BuildDiscardPanel, Activate] (blind+edge, High — confirmed rules violation, reachable today) — **APPLIED.** `BuildDiscardPanel` skips `CardType.Wound`; `Activate` precondition is now `!_deck.Hand.Any(c => c.Type != CardType.Wound)` → log + `ReturnCard` rollback. On-device verification step 2 updated (Wound no longer shown in discard list). Build clean.

### Deferred (see deferred-work.md)

- [x] [Review][Defer] `CardExpanded` Play-enable now passes when any `AlternateEffectType` is legal, but non-Improvisation multi-type cards (cards.yaml lines 352/627/727/828/942) have no intercept and stage their *primary* `Unpowered.EffectType`, which may be illegal in the current phase. Latent until phase transitions exist AND such a card is in hand; needs phase-aware effect-type selection at stage time (Epic 7 turn loop / future multi-type-play story). [scripts/ui/components/CardExpanded.cs, scripts/ui/components/HandView.cs] (blind)
- [x] [Review][Defer] `ImprovisationRule.GetPhaseEffect` and `CanPlay` are production-dead (called only from tests). `BuildResourcePanel` and `CardExpanded` independently re-derive phase legality, so the two can drift. Consider making `GetPhaseEffect` the single source of truth. Low priority. [scripts/cards/ImprovisationRule.cs] (blind+edge)
- [x] [Review][Defer] `ImprovisationView` overlay has no cancel/escape affordance — a player who opens Improvisation by mistake must complete a discard + resource pick to dismiss it (pre-discard rollback uses `ReturnCard`; post-discard there is no un-discard). A Cancel button with rollback semantics is a UX addition beyond this story's scope. [scripts/ui/components/ImprovisationView.cs] (blind+edge)

**Dismissed (10):** `AlternateEffectTypes` null-deref (false positive — defaults to `Array.Empty<EffectType>()`); `ImprovisationEffect` hardcodes `AttackElement.Physical` (correct per AttackEffect/BlockEffect convention); `PlaceholderMainMenu.First()` throws on missing improvisation (consistent with project "bad card data throws loud at startup"; scaffold); `Take(3)` smaller hand (scaffold cosmetic); magic priority `0` / `"improvisation"` source id (matches established `Enqueue`/`EffectContext` convention); `SetImprovisationView` no null guard / fall-through (wiring always calls it; field intentionally nullable to drive the intercept); powered Improvisation unreachable / `GetAmount(true)` (spec explicitly defers powered play); `Activate` doesn't clear `_resourcePanel` children between activations (`BuildResourcePanel` QueueFrees before rebuild); `_resourcePanel` stale re-entry buttons (handled by rebuild clear); `HandView`/`RestView` `HandChanged` never unsubscribed (pre-existing, already tracked in deferred-work.md — ImprovisationView itself has no subscription leak).
