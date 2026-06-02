# Project Context — Magus Warrior

Read this before writing any game code. These are the rules derived from the architecture
that agents most commonly get wrong. Non-compliance breaks integration.

---

## What This Project Is

**Magus Warrior** — solo Android adaptation of Mage Knight Ultimate Edition.
Engine: Godot 4.6.3 + C#. Target: Android 12+ (Galaxy S21), landscape only.
Single developer. No multiplayer.

Architecture document: `_bmad-output/game-architecture.md`
Card definitions: `data/cards.yaml`
Hero definitions: `data/heroes.yaml`

---

## The Most Important Rule

**Pure C# game logic. Scene tree is render-only.**

`GameState`, `CombatResolver`, `HexGrid`, `ManaPool`, `CardEffect` — none inherit
from `Node`. They are plain C# classes with no Godot dependency.

`scripts/ui/` is the **only** folder where `: Node`, `: Control`, or `partial class`
(Godot-generated) is permitted. If you are writing a class outside `scripts/ui/` and
you feel the urge to inherit from a Godot type — stop. You are in the wrong place.

---

## Critical Rules (Prior Failure Modes)

The previous implementation attempt failed in these specific ways. Do not repeat them.

### 1. Every player decision uses async/await — no exceptions

```csharp
// CORRECT — logic pauses here until the UI resolves
var chosen = await _uiBroker.ChooseOne(new ChooseOneRequest {
    Options = new[] { Move(2), Attack(2), Block(2), Influence(2) }
});
chosen.Apply(gameState);

// FORBIDDEN — polling, flags, callbacks instead of await
if (_pendingChoice != null) { ... }         // never
_uiBroker.OnChoiceResolved += HandleChoice; // never as a substitute for await
```

The combat loop, card resolution loop, and all effect pipelines **cannot advance past
a pending interaction** until it is resolved. This is enforced structurally by
async/await — not by flags or checks.

### 2. Never `async void`

All async methods return `Task` or `Task<T>`. `async void` swallows exceptions silently.

```csharp
// CORRECT
public async Task HandleCombatPhase() { ... }
public async Task<CombatResult> ResolveCombat(Enemy e) { ... }

// FORBIDDEN
async void HandleCombatPhase() { ... }
```

### 3. Screen contracts exist — follow them

Every screen has a documented contract in `_bmad-output/game-architecture.md` (Screen
Contracts section). Before implementing any screen, read its contract. It defines:
- What game state the screen reads
- What events trigger a re-render
- What user actions are handled and what events they emit

Do not infer screen behavior from context. Read the contract.

### 4. Never hardcode per-card logic in resolvers

```csharp
// FORBIDDEN — in CombatResolver, EffectScheduler, or anywhere outside effect files
if (card.Id == "cold_toughness") { ... }
if (card.Id == "mana_pull") { ... }

// CORRECT — register a hook; the resolver calls RunHooks()
registry.Register(HookPoint.BeforeBlockTargeting, new IceBlockScalingHook(), priority: 0);
```

---

## File Placement

When in doubt, check `_bmad-output/game-architecture.md` → System Location Map.
Quick reference:

| What you're writing | Where it goes |
| --- | --- |
| Shared enums (GamePhase, ManaColor, etc.) | `scripts/core/types/` |
| Result\<T\>, Log, GameDebug, SaveMigrator | `scripts/core/` |
| IEffect implementations | `scripts/cards/effects/<domain>/` |
| CardLoader, CardDefinition, HeroLoader | `scripts/cards/` |
| CombatResolver, CombatState | `scripts/combat/` |
| HexGrid | `scripts/hex/` |
| WorldMap, SiteInteraction | `scripts/map/` |
| ManaPool | `scripts/mana/` |
| DeckManager | `scripts/deck/` |
| UnitRoster | `scripts/units/` |
| UIBroker, ChoiceRequests | `scripts/broker/` |
| SaveManager, SaveData | `scripts/save/` |
| Godot nodes, screens, components | `scripts/ui/` ONLY |

Effect subdirectories: `movement/`, `combat/`, `mana/`, `healing/`, `influence/`, `special/`

---

## Error Handling

Return `Result<T>` for expected failures. Never throw for invalid player actions.

```csharp
// CORRECT
public Result<PlayResult> PlayCard(Card card, Target target) {
    if (!PhaseGate.IsLegal(card.EffectType, _state.CurrentPhase))
        return Result<PlayResult>.Fail($"{card.EffectType} not legal in {_state.CurrentPhase}");
    ...
    return Result<PlayResult>.Ok(result);
}

// FORBIDDEN
public PlayResult PlayCard(Card card, Target target) {
    if (!PhaseGate.IsLegal(...)) throw new InvalidOperationException("Can't play");
    ...
}
```

Exceptions are only for unrecoverable startup failures: bad card data, corrupt save,
missing asset. These throw immediately and loudly. Normal game flow never throws.

---

## Logging

Always use `Log.cs` with a system tag. Never call `GD.Print` directly.

```csharp
Log.Debug("[Combat]", $"Resolving attack: power={attack}, armor={armor}");
Log.Warn("[Save]",    $"Schema version {v} higher than expected");
Log.Error("[Deck]",   $"Card {id} not found in registry");
```

Required system tags: `[Combat]`, `[Deck]`, `[HexGrid]`, `[Save]`, `[Effect]`,
`[UI]`, `[Input]`, `[Mana]`

Do not log inside effect resolution loops or combat phase transitions — log before
entry and after exit only.

---

## Events

Events are **data only**. No methods. No logic. Handlers do the work.

```csharp
// CORRECT
public record CardPlayedEvent(Card Card, Target Target, GamePhase Phase);

// FORBIDDEN — logic on an event type
public record CardPlayedEvent(Card Card) {
    public void Apply(GameState s) { ... }  // never
}
```

Naming: past tense. `CardPlayed`, `DamageResolved`, `TileRevealed`, `WoundReceived`.
C# events: PascalCase. Godot signals: `snake_case`.

---

## Phase Gate

Before a card can be played, validate with `PhaseGate.IsLegal()`. Use the **chosen**
effect type — some cards declare `AlternateEffectTypes` in their definition.

```csharp
// Check chosen effect type, not just card.EffectType
Result<bool> valid = phaseValidator.ValidatePhase(card, chosenEffectType, state.CurrentPhase);
if (!valid.IsSuccess) return Result<PlayResult>.Fail(valid.Error!);
```

Special cards with `legal_phases` in `cards.yaml` override the base table — the
validator checks card-level constraints before the base table.

---

## Card Data Pipeline

Cards are defined in `data/cards.yaml`. At startup, `CardLoader.LoadAll()`:
1. Deserializes all card definitions into typed `CardDefinition` objects
2. Registers any `IEffectHook` implementations declared by the card
3. Throws immediately on invalid data — no silent failures

**Never** read `cards.yaml` at runtime outside of `CardLoader`. **Never** define card
behavior in game logic code — it must be data-driven from the YAML definition.

---

## Hook Registration

Card hooks register during `CardLoader.LoadAll()`. System hooks register during
`GameState` construction. No deferred, lazy, or runtime registration.

```csharp
// In CardLoader — when a card with ice_block_scaling is loaded:
_hookRegistry.Register(HookPoint.BeforeBlockTargeting, new IceBlockScalingHook(), priority: 0);

// FORBIDDEN — registering inside Execute()
public EffectResult Execute(GameState state, EffectContext ctx) {
    _hookRegistry.Register(...);  // never — registration is one-time at startup
}
```

---

## Dependency Injection

All pure C# classes receive dependencies via constructor. No service locator.
No static access to game state except `Log` and `GameDebug`.

```csharp
// CORRECT
public class CombatResolver {
    public CombatResolver(GameState state, UIBroker broker,
                          EffectScheduler scheduler, EffectHookRegistry hooks) { ... }
}

// FORBIDDEN
public class CombatResolver {
    private GameState _state = GameState.Instance;  // never
}
```

---

## Data Access

All runtime game data is accessed through `GameState`. No system reads YAML
files at runtime. Loaders run once at startup; results live in `GameState`.

---

## Save System

- JSON with `schema_version` integer at root
- `SaveMigrator.cs` handles per-version migration chain
- State written atomically on `NOTIFICATION_APPLICATION_PAUSED`
- `System.Text.Json` with source generators (AOT-compatible for Android)

Never write save logic that assumes a specific schema version. Always go through
the migration chain.

---

## Testing

- `tests/unit/` — pure C# tests, no Godot scene tree, no `GutTest` base class
- `tests/integration/` — requires scene tree, extends `GutTest`
- Test file naming: `CombatResolverTest.cs`, `PhaseGateTest.cs`, etc.

Every story AC requires: "UI is connected and displays correct game state. All player
choices required by this story's card effects are surfaced as prompts and resolved
correctly before the effect is applied. No `Choice required` or unhandled state
errors occur during normal play."

---

## Why These Rules Exist

These are the questions John asked in the Epic 0 retrospective. If you're an agent reading this: don't skip it. John is the sole human reviewer — if he doesn't understand the reasoning, drift goes uncaught.

### Why async/await everywhere — no exceptions

The combat loop is sequential code: ranged phase, then block phase, then melee phase. Each phase needs to pause and wait for a player tap. Without async/await you'd need a state machine or event callbacks to track "we were in block phase, player tapped, now resume." With async/await the loop writes `var choice = await _uiBroker.ChooseOne(...)` and picks up exactly where it left off after the player acts. The Godot UI keeps running (player sees the buttons), but the game logic is frozen at that line until the choice arrives. No flags, no "which phase were we in" tracking.

`async void` is banned because it swallows exceptions silently — a thrown error in an unawaited void method disappears with no stack trace.

### Why `Result<T>` instead of exceptions for game logic

When `SaveManager.Load()` returns `Result<SaveData>`, the caller must check `IsSuccess` before reading `Value`. It cannot accidentally use a default. When a method throws instead, the exception can bubble silently through five call frames and crash somewhere unrelated with a useless stack trace.

The project distinguishes two classes of failure:

- *Expected failures* (save not found, invalid player action) → `Result.Fail`. Caller decides what to do.
- *Unrecoverable startup failures* (corrupt card data, bad save schema) → throw immediately and loudly. No recovery possible.

### Why pure C# classes — no Godot inheritance outside `scripts/ui/`

`GameState`, `EffectScheduler`, `CardLoader` — none inherit from Godot types. This means `dotnet test` runs all 19+ tests in about two seconds with no Godot editor, no device, no APK. If `GameState` inherited from `Node`, every test would require launching the engine.

It also enforces a hard boundary: business logic cannot accidentally call Godot APIs (which crash outside the engine runtime), and Godot node lifecycle (freed nodes, scene tree changes) cannot corrupt game state.

If you're writing a class outside `scripts/ui/` and feel the urge to inherit from a Godot type — stop. You are in the wrong place.

---

## LLDs Required Before Full Implementation

These design documents must exist before implementing the systems they cover:

| LLD | Status | Blocks |
| --- | --- | --- |
| Card Effect LLD | **Complete** (`docs/effect-lld.md`) | Phase gate finalization, all card stories |
| Enemy Token LLD | **Complete** (`docs/enemy-token-lld.md`) | Epic 3+ combat stories |
| Combat Flow LLD | **Complete** (`docs/combat-flow-lld.md`) | All combat stories |
| Site Interaction LLD | Not started | Epic 4+ site stories |
| Turn Structure LLD | **Complete** (`docs/turn-structure-lld.md`) | Turn loop implementation |
