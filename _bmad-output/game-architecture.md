---
title: 'Game Architecture'
project: 'maguswarrior'
date: '2026-05-18'
author: 'John'
version: '1.0'
stepsCompleted: [1, 2, 3, 4, 5, 6, 7, 8, 9]
status: 'complete'
engine: 'godot'
platform: 'android'

# Source Documents
gdd: '_bmad-output/gdd.md'
epics: '_bmad-output/epics.md'
brief: null
---

## Game Architecture

## Executive Summary

**Magus Warrior** architecture is designed for Godot 4.6.3 + C# targeting Android 12+ (Samsung Galaxy S21, landscape only).

**Key Architectural Decisions:**

- Pure C# game logic with no Godot node dependency — scene tree is render-only. Enables serialization, unit testing, and clean save/undo integration.
- PendingInteraction via `TaskCompletionSource<T>` + async/await, mediated by `UIBroker` — game logic pauses structurally at every player decision; cannot advance until resolved.
- Polymorphic `IEffect` system with data-driven YAML composition, `EffectScheduler` priority queue, and `EffectHookRegistry` for named extension points — handles all card, skill, and unit effects without per-card hardcoding.
- Pre-commit undo only (GDD-specified: "free until new information is revealed") — covers the felt need at low implementation cost.
- JSON saves with `schema_version` + `SaveMigrator.cs` from day one — interruption-safe atomic write on `NOTIFICATION_APPLICATION_PAUSED`.

**Project Structure:** Domain-driven, 13 system folders under `scripts/`, with `scripts/ui/` as the only folder where Godot node inheritance is permitted.

**Patterns Defined:** 9 standard + 2 novel (Effect Hook System, Site Interaction Template). Phase gate table and screen contracts included.

**LLD Status:** `docs/effect-lld.md` — cards, units, and skills complete. Remaining: `docs/enemy-token-lld.md`, `docs/site-interaction-lld.md`.

---

## Development Environment

### Prerequisites

| Requirement | Version | Notes |
| --- | --- | --- |
| Godot Engine | 4.6.3 | With C# / .NET support enabled |
| .NET SDK | 8.0+ | Required for Godot C# |
| Android SDK | API 31+ | Android 12 minimum target |
| Android NDK | Latest stable | Required for Godot Android export |
| GUT | Latest | Godot Unit Test — separate install via asset library |

### AI Tooling (MCP Servers)

| MCP Server | Repo | Purpose |
| --- | --- | --- |
| godot-mcp (bradypp) | `~/git/godot-mcp` | Scene inspection, node editing, debug output from Claude Code |
| Context7 | upstash/context7 | Current Godot 4.6 API docs in-context; prevents stale API usage |

Both MCPs live outside the project directory — `godot-mcp` at `~/git/godot-mcp`, not inside `maguswarrior/`.

### First Steps

1. Create new Godot 4.6.3 project at the `maguswarrior/` root — this is the Godot project root (`project.godot` lives here)
2. Enable C# support in Godot project settings
3. Create the directory structure defined in the Project Structure section
4. Implement `scripts/core/` (Result\<T\>, Log, GameConstants, GameState stub) before any other system
5. Wire GUT and write one passing test to validate the test pipeline before implementation begins

---

## Project Context

### Game Overview

**Magus Warrior** — a faithful solo mobile adaptation of Mage Knight Ultimate Edition.
Players control Thomas (Tovak), navigating a hex-grid world via a card-driven hand,
defeating enemies, conquering sites, and racing to discover the city tile before the
scenario timer runs out.

**Core fantasy:** "I am an unstoppable force becoming MORE unstoppable." Every system
serves the arc from fragile beginner to battlefield commander.

### Technical Scope

**Platform:** Android 12+ (API 31), Samsung Galaxy S21 target device, landscape only
**Engine:** Godot 4 + C#, GUT for testing
**Genre:** Card-Driven Tactical Adventure (hybrid: card game + hex tactics + RPG)
**Project Level:** High complexity — 10+ interdependent systems, novel interaction
patterns, continuous autosave requirement, single developer

### Core Systems

| System | Complexity | Notes |
| --- | --- | --- |
| Polymorphic Effect System | High | Cards, skills, units share one base Effect class — load-bearing wall |
| Player Choice / Pending Interaction | High | Prior failure mode — async/await pattern required; non-negotiable |
| Save System | High | Atomic write on NOTIFICATION_APPLICATION_PAUSED; mid-combat verified at Epic 3 |
| UI State Machine & Screen Contracts | High | Prior failure mode — screen contracts required for every screen |
| Combat Resolution | High | 4 phases, elemental/physical, fortification, knockdown, unit absorption |
| Site Interactions | High | 14 types; Template pattern established on first 4 before scaling |
| Undo System | High | Event-log, cross-cutting; scope decision required before implementation |
| Touch Input & Gesture Disambiguation | Medium | Stateful tap/pinch/long-press/pan disambiguation |
| Hex Map & Movement | Medium | V-shape, tile revelation, Day/Night terrain costs, undo extends here |
| Deck Building & Progression | Medium | Uniqueness constraint across all zones; single zone manager required |
| Resource Systems | Medium | Mana dice, crystals, Day/Night restrictions with override support |
| Effect Scheduling & Ordering | Medium | Timing effects ("at end of combat", "before you move") need explicit resolution order |
| Round Clock / Offer Advancer | Low | Dummy player draws until deck empty → end-of-round; advances offer between rounds. No AI, no decisions. |
| i18n Infrastructure | Low | All strings externalized from day one; English only for v1 |

### Presentation / Feedback Layer

The architecture must define an explicit seam between game logic and the presentation layer:

1. Game logic computes a result (e.g., Attack 7 defeats enemy with Armor 6)
2. Immediately fires an event **with the result data** (e.g., `AttackResolved(damage:7, killed:true)`)
3. Presentation layer receives the event and begins animation/audio immediately
4. State commits and UI settles to new state after animation completes

Firing the event after state is fully committed creates a visible seam — the game feels
dead. The presentation layer must animate against the *result*, not against committed state.
Key moments that must fire at decision time, not commit time: card play, damage resolution,
wound entry, level-up, tile reveal, combat phase transition.

### Technical Requirements

- **Performance:** 30fps sustained minimum on Galaxy S21; battery/thermal headroom prioritized over visual effects
- **Offline-first:** Full gameplay without connection; cloud save opportunistic, never blocking
- **Interruption safety:** Full game state atomic-written on NOTIFICATION_APPLICATION_PAUSED
- **Save tiers:** Local autosave (fast, frequent) + Google Play cloud save (rate-limited, cross-device)
- **Uniqueness enforcement:** AAs, Spells, Artifacts — one copy across all zones simultaneously; enforced by a single zone manager

### Complexity Drivers

Requires explicit architectural decisions (not deferrable to story level):

1. **System dependency sequencing:** Effect System → PendingInteraction → UI State Machine → Screen Contracts form a dependency chain, not parallel concerns. The architecture must state explicitly: "the combat loop cannot advance until all pending interactions are resolved and acknowledged by the UI layer." Build order must follow this chain.

2. **PendingInteraction as async/await:** Every player decision uses C# async/await + Godot's `ToSignal()`. Execution pauses at the decision point and cannot resume until the UI resolves it. One system, one contract for all choice types (choose_one, target selection, color picker, unit picker, end-of-turn prompts).

3. **UI/Logic API boundary:** The interface between PendingInteraction and the UI layer is an explicit architectural mechanism — a typed, structured request with a defined signal/callback contract. Not left to per-story implementation.

4. **Undo scope decision:** Full event-sourced undo (tracks all state mutations, can walk back across PendingInteraction boundaries) vs. pre-commit only (cards can be un-staged before commit, but committed effects are final). Must be decided here — bolting on undo after the Effect System and Save System are built is expensive.

5. **Effect Scheduling and Ordering:** Resolution order for triggered and timing-based effects must be explicit. Either the Effect System owns a scheduler, or a separate scheduler exists. Not left to per-effect logic or per-card implementation.

6. **Mid-combat state serialization:** Combat Resolution's intermediate state must be designed for serialization from day one. Decision required: snapshot between phases, or make resolution idempotent and replay from the last committed checkpoint?

Novel interaction patterns (no standard Godot pattern applies directly):

- `ice_block_scaling` — block value computed dynamically from enemy token abilities at resolution time; requires query hook at block-targeting step
- `end_of_turn_return` — post-turn hook with conditional hand filter; requires a named end-of-turn hook registration system
- `black_source_die_as_any_color` — intercepted source die flow; requires a named extension point in the mana pipeline that implementations don't assume away
- Tactics structural effects — game state machinery operations (reserve a die, reshuffle deck, draw a specific card) distinct from resource effects; requires a separate resolution path or explicit extension to the Effect System

### Technical Risks

| Risk | Impact | Mitigation |
| --- | --- | --- |
| PendingInteraction under-specified before implementation starts | High — same failure mode as prior project | Async/await pattern defined in architecture; hard gate before any story touches these systems |
| Undo scope not decided at architecture time | High — bolted-on undo breaks mid-combat save integration | Decide scope in this document |
| Effect scheduling undefined (timing effects) | Medium | Explicit scheduler decision in Effect System section |
| Gesture disambiguation on Galaxy S21 | Medium | Validated on device in Epic 0/1b; fallback: tap-to-select before drag |
| Save schema versioning across epics | Medium | Schema version field + migration strategy designed in Epic 0 |
| Enemy Token LLD not complete | Medium | Architecture leaves explicit extension points; Template pattern isolates per-enemy logic — tracked at `docs/enemy-token-lld.md` (not yet written) |
| Site Interaction LLD not complete | Medium | Template pattern (Epic 4, first 4 sites) validates approach before scaling to all 14 — tracked at `docs/site-interaction-lld.md` (not yet written) |
| Card Data Schema scope | Medium | Schema exists (data/cards.yaml); must be tracked as a content authoring deliverable alongside code |

---

## Engine & Framework

### Selected Engine

Godot 4.6.3 + C#

Locked from GDD. Rationale: C# typing suits complex game state; no royalties; Godot 4.6 adds C# i18n parser support, Storage Access Framework compliance for Play Store, and Vulkan Mobile fixes for Adreno/Mali GPUs (Galaxy S21 Adreno 660).

### Project Initialization

Start from scratch — no starter template. The project structure is custom-designed around the game's specific system requirements.

### Engine-Provided Architecture

| Component | Solution | Notes |
| --- | --- | --- |
| Rendering | CanvasItem 2D, Vulkan Mobile | Correct Android backend; Adreno 660 fixes in 4.6 |
| Audio | AudioStreamPlayer + bus system | SFX/music bus layering; no positional audio needed |
| Input | Input singleton, InputEventScreenTouch/Drag | Touch built in; gesture disambiguation is custom logic |
| Scene Management | SceneTree, node hierarchy | UI screens as scenes; game logic as pure C# |
| Async/Await | C# async/await + ToSignal() | Native mechanism for PendingInteraction |
| Animation / Tween | AnimationPlayer, Tween | Tween for presentation-layer result events |
| Localization | Built-in i18n, CSV/PO, C# parser | Strings externalized from day one; English v1 |
| Build / Deploy | Gradle + Android export templates | SAF compliance in 4.6 |
| Testing | GUT (Godot Unit Test) | Separate install; already in scope |

### AI Development Tools (MCPs)

| Tool | Repo | Purpose |
| --- | --- | --- |
| godot-mcp (bradypp) | `~/git/godot-mcp` | Scene inspection, node editing, debug output from Claude Code |
| Context7 | upstash/context7 | Current Godot 4.6 API docs in-context; prevents stale API usage |

### Remaining Architectural Decisions

The following are decided in Step 4:

1. Game logic / scene boundary
2. PendingInteraction mechanism
3. Effect System class hierarchy
4. Undo scope
5. UI State Machine
6. Hex grid implementation
7. Card data pipeline
8. Save file format and schema versioning
9. Effect scheduler

---

## Architectural Decisions

### Decision Summary

| # | Category | Decision |
| --- | --- | --- |
| 1 | Game Logic / Scene Boundary | Pure C# classes for all game state; scene tree is render-only |
| 2 | PendingInteraction | `TaskCompletionSource<T>` + async/await; `UIBroker` mediates Godot signals |
| 3 | Effect System Hierarchy | `IEffect` interface + data-driven composition from YAML; flat to start |
| 4 | Undo Scope | Pre-commit only — staging area only; locked once new information is revealed |
| 5 | UI State Machine | Custom `UIStateMachine.cs` — required for nested targeting sub-states |
| 6 | Hex Grid | Pure C# coordinate logic (`HexGrid.cs`); `TileMapLayer` for rendering only |
| 7 | Card Data Pipeline | `cards.yaml` → typed C# objects at startup; fail loudly on bad data |
| 8 | Save Format | JSON + `schema_version` root field; `SaveMigrator.cs` written from day one |
| 9 | Effect Scheduler | `PriorityQueue<PendingEffect, int>`; effects can spawn effects during resolution |
| 10 | State Management | Singleton `GameState` with constructor injection; no Godot AutoLoad for logic |
| 11 | Asset Loading | Preload all at startup — vector/flat art is small; no streaming needed |
| 12 | Audio Architecture | Godot bus system — SFX bus + Music bus; no custom audio manager |

### Game Logic / Scene Boundary

All game state lives in pure C# classes with no Godot node dependency. `GameState`, `CombatResolver`, `ManaPool`, `CardEffect`, `HexGrid` — none inherit from `Node`. The scene tree is a rendering and input surface only.

**Rationale:** Pure C# state is serializable, unit-testable without a running Godot instance, and makes the save system, undo system, and effect system straightforward. Scene-coupled state contaminates every downstream system.

**Boundary pattern:**

```csharp
// Pure C# — no Godot dependency
public class GameState {
    public event Action<CardPlayedEvent> OnCardPlayed;
    public PlayResult PlayCard(Card card, Target target) { ... }
}
// Scene subscribes to events; game logic never references the scene
public partial class GameBoard : Node {
    private GameState _state;
    public override void _Ready() => _state.OnCardPlayed += HandleCardPlayed;
}
```

### PendingInteraction System

Every player decision (choose_one, target selection, color picker, unit picker, end-of-turn prompts) uses C# async/await. Game logic pauses at the decision point and cannot resume until the UI resolves it.

**Pattern:** `TaskCompletionSource<T>` wrapped in a `UIBroker` singleton. The broker translates between Godot signals (UI layer) and C# tasks (logic layer). The logic layer has no Godot signal dependency.

```csharp
// Game logic — awaits the decision, cannot proceed until resolved
var chosen = await _uiBroker.ChooseOne(new ChooseOneRequest {
    Options = new[] { Move(2), Attack(2), Block(2), Influence(2) }
});
chosen.Apply(gameState);

// UIBroker — the signal/task boundary
public async Task<T> ChooseOne<T>(ChooseOneRequest req) {
    var tcs = new TaskCompletionSource<T>();
    EmitSignal(SignalName.ChoiceRequired, req);  // UI receives and renders
    // UI calls broker.Resolve(choice) when player taps
    return await tcs.Task;
}
```

**Critical rule:** The combat loop, card resolution loop, and all effect pipelines cannot advance past a pending interaction until it is resolved. This is enforced structurally by async/await — not by a flag or a check.

### Effect System

All effects — card effects, skill effects, unit effects — implement a shared `IEffect` interface. Data-driven composition from `cards.yaml`: a card's effects are deserialized at startup into typed `IEffect` objects. No per-card hard-coding in game logic.

```csharp
public interface IEffect {
    EffectResult Execute(GameState state, EffectContext ctx);
}
public record EffectContext(Card Source, Target Target, GamePhase Phase);
public record EffectResult(bool Success, IReadOnlyList<TriggeredEffect> Triggered);
```

Effects are flat to start. Shared behavior is expressed via composition, not deep inheritance. Resist the urge to build type hierarchies before the pain is felt.

### Effect Scheduler

A dedicated `EffectScheduler` with a `PriorityQueue<PendingEffect, int>` manages all effect resolution order. Effects can spawn new effects during resolution — the scheduler handles this without stack overflow.

```csharp
public class EffectScheduler {
    private readonly PriorityQueue<PendingEffect, int> _queue = new();

    public void Enqueue(IEffect effect, int priority, EffectContext ctx) =>
        _queue.Enqueue(new PendingEffect(effect, ctx), priority);

    public async Task ResolveAll(GameState state) {
        while (_queue.Count > 0) {
            var pending = _queue.Dequeue();
            var result = await pending.Effect.Execute(state, pending.Ctx);
            foreach (var triggered in result.Triggered)
                Enqueue(triggered.Effect, triggered.Priority, triggered.Ctx);
        }
    }
}
```

Lower priority number = resolves first. Timing-based effects ("at end of combat", "before you move") are expressed as explicit priority values defined in a shared `EffectPriority` constants class.

### Undo System

Pre-commit only. Before any action commits to `GameState`, a snapshot of relevant state is taken. Players can un-stage cards and cancel targeting freely. Once an action commits (revealing new information — card draw, tile reveal, die roll, enemy draw), it is final.

**Rationale:** The GDD defines this explicitly: "Undo is free until new information is revealed." Full event-sourced undo is not implemented in v1 — it is a second game inside the first. Pre-commit undo covers the felt need at low implementation cost.

### Hex Grid

`HexGrid.cs` — a pure C# class implementing axial coordinate math (distance, adjacency, range queries, line queries). Based on the redblobgames hex grid reference. No Godot dependency. `TileMapLayer` used for rendering only — it calls `SetCell()` based on `HexGrid` data.

**Rationale:** Godot's TileMap coordinate assumptions conflict with game-logic queries ("all hexes in movement range that aren't forests"). Owning the coordinate math in 200 lines of pure C# eliminates this friction. Rendering is trivial once the data layer is clean.

### Card Data Pipeline

`data/cards.yaml` is the authoritative card definition source. At startup, a `CardLoader` deserializes all card definitions into typed C# `CardDefinition` objects. Invalid data throws immediately — no silent failures. Cards are never defined in game logic code.

`data/heroes.yaml` defines the `base_deck` and per-hero `replacements`. The `HeroLoader` applies replacements at game start to construct each hero's 16-card starting deck.

### Save System

- **Format:** JSON with `schema_version` integer field at root
- **Serializer:** `System.Text.Json` with source generators (AOT-compatible for Android)
- **Tiers:** Local autosave (fast, every decision boundary) + Google Play cloud save (rate-limited, opportunistic sync)
- **Interruption:** Full state written atomically on `NOTIFICATION_APPLICATION_PAUSED`; if killed before completion, player resumes from last committed state
- **Migration:** `SaveMigrator.cs` exists from day one with a per-version migration chain

```csharp
public static SaveData Load(string json) {
    var raw = JsonNode.Parse(json)!.AsObject();
    int v = raw["schema_version"]!.GetValue<int>();
    if (v < 2) raw = MigrateV1ToV2(raw);
    if (v < 3) raw = MigrateV2ToV3(raw);
    return raw.Deserialize<SaveData>(JsonOptions.Default)!;
}
```

### State Management

Single `GameState` instance created at scenario start, passed via constructor injection to all subsystems. No Godot AutoLoad nodes for game logic. The scene tree never owns game state.

### Asset Loading and Audio

Assets preloaded at startup — vector/flat art is small, no streaming needed. Godot bus system for audio: `SFX` bus and `Music` bus off master; no custom audio manager required.

### Presentation / Feedback Seam

Events fire with result data immediately after calculation — before state fully commits. The presentation layer animates against the result event, not against committed state.

```text
Player taps Confirm
  → Logic computes result instantly
  → Fires ResultEvent(data) immediately
  → Presentation layer begins animation/audio
  → State commits; UI settles after animation
```

Key moments that must fire at decision time: card play, damage resolution, wound entry, level-up, tile reveal, combat phase transition, city discovery.

---

## Cross-cutting Concerns

These patterns apply to **all** systems and must be followed by every implementation agent. Deviating without an explicit architectural decision is not permitted.

### Error Handling

**Strategy:** Two-tier — `Result<T>` for expected failure paths in game logic; exceptions only for unrecoverable startup failures (bad card data, corrupt save, missing asset). Game logic never throws for invalid player actions. Unhandled exceptions caught at the top-level Godot handler; game returns to main menu with an `ERROR` log entry.

**Canonical type — do not redefine per-system:**

```csharp
public readonly struct Result<T> {
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? Error { get; }
    private Result(bool ok, T? value, string? error) =>
        (IsSuccess, Value, Error) = (ok, value, error);
    public static Result<T> Ok(T value) => new(true, value, null);
    public static Result<T> Fail(string error) => new(false, default, error);
}
```

### Logging

**Wrapper:** `Log.cs` — thin static class over `GD.Print`. All log calls go through `Log`, never `GD.Print` directly.

**System tags:** Every log call includes a bracketed system tag as the first token. Required tags: `[Combat]`, `[Deck]`, `[HexGrid]`, `[Save]`, `[Effect]`, `[UI]`, `[Input]`, `[Mana]`.

**Levels:**

| Level | Emits In | Destination |
| --- | --- | --- |
| `ERROR` | All builds | Logcat + `user://errors.log` (ring-buffer, 50 KB max) |
| `WARN` | Dev + staging | Logcat only |
| `DEBUG` | Dev only (`#if DEBUG`) | Logcat only |

**Hot paths:** No logging inside effect resolution loops or combat phase transitions. Log before entry and after exit only.

```csharp
public static class Log {
    public static void Error(string tag, string msg) { /* always, writes to errors.log */ }
    public static void Warn(string tag, string msg) { /* dev + staging */ }
    [Conditional("DEBUG")]
    public static void Debug(string tag, string msg) { /* dev only */ }
}
```

**ERROR log retrieval:**

```text
adb pull /sdcard/Android/data/com.maguswarrior/files/errors.log
```

### Configuration

Three categories, three mechanisms — no mixing:

| Category | Mechanism | Location |
| --- | --- | --- |
| Game constants / balance values | `static readonly` in `GameConstants.cs` | `scripts/core/GameConstants.cs` |
| Structured card/hero data | YAML loaded at startup | `data/cards.yaml`, `data/heroes.yaml` |
| Player preferences | Godot `ConfigFile` | `user://settings.cfg` |

No remote config. No hot-reload — change `GameConstants.cs` and recompile. Constants are Ctrl+click navigable; the file is the source of truth.

### Event System

**Pattern:** Typed C# `event Action<TEvent>` declared on the owning class. No global event bus. `UIBroker` is the only cross-layer signal boundary.

**Naming:** Past tense. `CardPlayed`, `DamageResolved`, `TileRevealed`, `WoundReceived`, `LevelGained`.

**Invariant — enforced in every implementation:**

> Events are data. No methods, no logic on event record types. All logic lives in handlers.

```csharp
public record CardPlayedEvent(Card Card, Target Target, GamePhase Phase);

private void HandleCardPlayed(CardPlayedEvent e) {
    _animator.PlayCardAnimation(e.Card);
}
```

### Debug Tools

**`GameDebug.cs`:** Static class with `[Conditional("DEBUG")]` on all methods — zero cost in release builds.

**State inspector overlay:** Toggle via 5-tap in the top-left corner. Dumps serialized `GameState` to an on-screen scrollable panel. **DEBUG builds only** — the wiring is compiled under `#if DEBUG`, so no debug surface is reachable in a release build. (The inspector/toggle nodes may still exist dormant in a scene, but are never wired in release.) Debug builds are produced after each story so the developer can test on device; release builds ship with zero active debug tooling.

**Undo is a player feature, not a debug feature.** The event-log undo *mechanism* — `GameEventLog`, `GameState.RestoreSnapshot`, `EffectScheduler` log append — is plain non-conditional C# and ships in release. Players undo freely until an "undo gate" (new information revealed); the player-facing undo UI lands in story 1b-4 on top of this mechanism. Only the `GameDebug.UndoLastEvent` convenience wrapper (used by the DEBUG-only inspector panel) is `[Conditional("DEBUG")]`.

**Scenario shortcuts (dev only):**

```csharp
GameDebug.SpawnCard("cold_toughness");
GameDebug.SpawnEnemy("orc", tokens: new[] { "cold_fire", "physical" });
GameDebug.SetMana(red: 3, blue: 1);
GameDebug.SkipToPhase(GamePhase.Combat);
GameDebug.SetHandTo(new[] { "cold_toughness", "stamina", "march" });
```

**ADB launch flags:**

```text
adb shell am start -n com.maguswarrior/.MainActivity \
  --es scenario solo_conquest --ez skip_intro true
```

---

## Project Structure

### Organization Pattern

**Pattern:** Domain-driven — organized by game system within `scripts/`, not by file type. Scenes are thin render wrappers; they mirror `scripts/ui/` but stay shallow.

**Rationale:** Pure C# game logic is the load-bearing layer. Grouping by system keeps each system's files co-located and makes the logic/scene boundary explicit structurally, not just by convention.

### Directory Structure

```text
maguswarrior/               # Godot project root — project.godot lives here
├── scripts/
│   ├── core/               # Shared infrastructure
│   │   ├── types/          # Shared enums used across systems
│   │   │   ├── GamePhase.cs
│   │   │   ├── ManaColor.cs
│   │   │   ├── SiteType.cs
│   │   │   └── EffectType.cs
│   │   ├── GameState.cs
│   │   ├── GameConstants.cs
│   │   ├── Result.cs
│   │   ├── Log.cs
│   │   ├── GameDebug.cs
│   │   └── SaveMigrator.cs
│   ├── cards/
│   │   ├── CardDefinition.cs
│   │   ├── CardLoader.cs
│   │   ├── HeroLoader.cs
│   │   └── effects/
│   │       ├── IEffect.cs
│   │       ├── EffectContext.cs
│   │       ├── EffectResult.cs
│   │       ├── EffectScheduler.cs
│   │       ├── movement/
│   │       ├── combat/
│   │       ├── mana/
│   │       ├── healing/
│   │       ├── influence/
│   │       └── special/    # EndOfTurnReturnEffect, IceBlockEffect, etc.
│   ├── combat/
│   │   ├── CombatResolver.cs
│   │   └── CombatState.cs  # Will likely split by phase in Epic 3
│   ├── hex/
│   │   └── HexGrid.cs
│   ├── map/
│   │   ├── WorldMap.cs
│   │   └── SiteInteraction.cs
│   ├── mana/
│   │   └── ManaPool.cs
│   ├── deck/
│   │   └── DeckManager.cs
│   ├── units/
│   │   └── UnitRoster.cs
│   ├── broker/             # UIBroker + typed request definitions
│   │   ├── UIBroker.cs
│   │   └── ChoiceRequests.cs
│   ├── save/
│   │   ├── SaveData.cs
│   │   └── SaveManager.cs
│   └── ui/                 # ← ONLY folder where : Node / : Control / partial class allowed
│       ├── GameBoard.cs
│       ├── UIStateMachine.cs
│       ├── screens/
│       └── components/
├── scenes/                 # .tscn files — mirrors scripts/ui/ structure
│   ├── screens/
│   └── components/
├── data/
│   ├── cards.yaml
│   └── heroes.yaml
├── assets/
│   ├── art/
│   │   ├── cards/
│   │   ├── enemies/
│   │   ├── map/
│   │   └── ui/
│   ├── audio/
│   │   ├── music/
│   │   └── sfx/
│   └── fonts/
├── tests/
│   ├── unit/               # Pure C# — no GutTest, no scene tree required
│   └── integration/        # Requires scene tree; extend GutTest here
└── docs/
```

### System Location Map

| System | Location |
| --- | --- |
| GameState, GameConstants | `scripts/core/` |
| Shared enums (GamePhase, ManaColor, etc.) | `scripts/core/types/` |
| Result\<T\>, Log, GameDebug, SaveMigrator | `scripts/core/` |
| IEffect, EffectScheduler, effect implementations | `scripts/cards/effects/` |
| CardLoader, HeroLoader, CardDefinition | `scripts/cards/` |
| CombatResolver, CombatState | `scripts/combat/` |
| HexGrid | `scripts/hex/` |
| WorldMap, SiteInteraction | `scripts/map/` |
| ManaPool, source dice | `scripts/mana/` |
| DeckManager (uniqueness enforcement) | `scripts/deck/` |
| UnitRoster | `scripts/units/` |
| UIBroker, ChoiceRequests | `scripts/broker/` |
| SaveManager, SaveData | `scripts/save/` |
| UIStateMachine, screens, components | `scripts/ui/` |
| Scene files | `scenes/` mirroring `scripts/ui/` |

### Naming Conventions

**Files:** PascalCase for C# scripts (`CombatResolver.cs`), PascalCase for scenes (`GameBoard.tscn`), `snake_case` for data and assets (`cards.yaml`, `card_cold_toughness.png`).

**Code:**

| Element | Convention | Example |
| --- | --- | --- |
| Classes / Records | PascalCase | `CombatResolver`, `CardPlayedEvent` |
| Interfaces | `I` prefix + PascalCase | `IEffect` |
| Methods | PascalCase | `ResolveAll()`, `PlayCard()` |
| Private fields | `_camelCase` | `_queue`, `_state` |
| Public properties | PascalCase | `IsSuccess`, `CurrentPhase` |
| Constants | PascalCase in static class | `GameConstants.MaxHandSize` |
| C# events | PascalCase, past tense | `OnCardPlayed`, `OnDamageResolved` |
| Godot signals | `snake_case` | `choice_required`, `phase_changed` |
| Test files | `Test` suffix | `CombatResolverTest.cs` |

**Assets:** `snake_case` throughout, prefixed by type: `card_cold_toughness.png`, `sfx_sword_hit.wav`, `music_exploration.ogg`, `btn_confirm.png`.

---

## Implementation Patterns

These patterns ensure consistent implementation across all AI agents.

### Novel Pattern: Effect Hook System

**Purpose:** Named extension points in the effect pipeline. Implementations register hooks at specific points; the pipeline runs them in priority order. Prevents per-card hardcoding in core resolvers.

```csharp
public interface IEffectHook {
    bool AppliesTo(EffectContext ctx);
    Task<EffectContext> Transform(EffectContext ctx, GameState state);
}

public class EffectHookRegistry {
    private readonly Dictionary<HookPoint, List<(IEffectHook Hook, int Priority)>> _hooks = new();

    public void Register(HookPoint point, IEffectHook hook, int priority = 0) =>
        _hooks.GetOrCreate(point).Add((hook, priority));

    public async Task<EffectContext> RunHooks(HookPoint point, EffectContext ctx, GameState state) {
        if (!_hooks.TryGetValue(point, out var entries)) return ctx;
        foreach (var (hook, _) in entries.OrderBy(e => e.Priority))
            if (hook.AppliesTo(ctx))
                ctx = await hook.Transform(ctx, state);
        return ctx;
    }
}

public enum HookPoint {
    BeforeBlockTargeting,   // ice_block_scaling
    AfterSourceDieRoll,     // black_source_die_as_any_color
    EndOfTurn,              // end_of_turn_return
}
```

**Registration:** Card hooks register during `CardLoader.LoadAll()` — when a card definition is deserialized, its associated hooks are registered. System-level hooks register during `GameState` construction. No deferred or lazy registration.

**EndOfTurn hook context:** The `EndOfTurn` hook context carries `IReadOnlyList<(Card Card, bool Powered)> PlayedThisTurn` so hooks like `EndOfTurnReturnHook` can identify which cards were played and with what powered state — required for Crystal Joy's discard filter logic.

**Usage:** `IceBlockScalingHook` registers at `BeforeBlockTargeting`, queries enemy tokens, mutates block value in `EffectContext`. `BlackSourceDieHook` registers at `AfterSourceDieRoll`, injects a color picker via `UIBroker` when die shows black. `EndOfTurnReturnHook` registers at `EndOfTurn`, shows optional discard prompt with appropriate hand filter.

### Novel Pattern: Site Interaction Template

**Purpose:** 14 site types share a resolution skeleton but vary in their steps. Template method fixes the sequence; concrete subclasses override only what differs.

```csharp
public abstract class SiteInteraction {
    public async Task<InteractionResult> Resolve(GameState state) {
        if (!MeetsRequirements(state)) return InteractionResult.Fail("Requirements not met");
        await RunInteractionStep(state);   // fight, influence, or no-op
        var reward = ComputeReward(state);
        Apply(state, reward);
        return InteractionResult.Ok(reward);
    }

    protected abstract bool MeetsRequirements(GameState state);
    protected abstract Task RunInteractionStep(GameState state);
    protected abstract Reward ComputeReward(GameState state);
    protected abstract void Apply(GameState state, Reward reward);
}
```

First four site types (Epic 4) validate this template before scaling to all 14.

### Standard Patterns

**Communication:** Constructor injection for all pure C# dependencies. C# events for logic-to-logic notification. `UIBroker` for the logic→scene boundary. No service locator, no static singletons except `GameDebug` and `Log`.

```csharp
public class CombatResolver {
    private readonly GameState _state;
    private readonly UIBroker _broker;
    private readonly EffectScheduler _scheduler;
    private readonly EffectHookRegistry _hooks;

    public CombatResolver(GameState state, UIBroker broker,
                          EffectScheduler scheduler, EffectHookRegistry hooks) =>
        (_state, _broker, _scheduler, _hooks) = (state, broker, scheduler, hooks);
}
```

**Entity / Card creation:** Factory pattern, data-driven. `CardLoader` constructs `CardDefinition` objects from YAML at startup; `EnemyFactory` constructs enemies from token data. No `new CardDefinition(...)` in game logic — always through the factory. Invalid data throws immediately.

**State transitions:** `UIStateMachine` for all UI state. Combat phases are explicit named async methods on `CombatResolver`, called in sequence. Skipped phases return immediately with no-op results — no flag checking, no phase-skip enum.

```csharp
public async Task<CombatResult> ResolveCombat(Enemy enemy) {
    await ResolveRangedPhase(enemy);       // no-op if no ranged cards
    await ResolveBlockPhase(enemy);
    await ResolveAssignDamagePhase(enemy);
    await ResolveMeleePhase(enemy);
    return BuildResult();
}
```

**Data access:** All runtime game data accessed through `GameState`. No system reads YAML directly at runtime. Loaders run once at startup; results live in `GameState`. No Godot `Resources` or `AutoLoad` for game data.

### Consistency Rules

| Pattern | Rule | Violation Example |
| --- | --- | --- |
| Async | All async methods return `Task` or `Task<T>` — never `async void` | `async void HandleChoice(...)` |
| Communication | Always constructor injection | Calling `GameState.Instance` inside an effect |
| Events | Past-tense record types, no logic on event types | `CardPlayedEvent.Apply()` method |
| Errors | Return `Result<T>`, never throw for invalid player action | `throw new InvalidOperationException("Can't play")` |
| Logging | Always via `Log.cs` with system tag | `GD.Print("attack resolved")` |
| Data access | Only via `GameState`, never direct YAML read at runtime | `File.ReadAllText("data/cards.yaml")` in an effect |
| Godot deps | Only in `scripts/ui/` | `: Node` in `CombatResolver.cs` |
| Hook registration | Card hooks in `CardLoader.LoadAll()`, system hooks in `GameState` constructor | Hook registered inside `IEffect.Execute()` |
| Effects | Register via `EffectHookRegistry` — never hardcode per-card in resolvers | `if (card.Id == "cold_toughness")` in `CombatResolver` |

---

## Phase Gate

Cards are validated against this table at play time. The validator checks the player's *chosen* effect type (base or alternate), not the card's base `effect_type`.

| Effect Type | Movement | Interaction | Combat — Ranged | Combat — Block | Combat — Damage | Combat — Melee | During Rest | End of Turn | Any Phase |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| move | ✅ | | | | | | | | |
| attack (melee) | | | | | | ✅ | | | |
| attack (ranged) | | | ✅ | | | ✅ | | | |
| attack (siege) | | | ✅ | | | ✅ | | | |
| block | | | | ✅ | | | | | |
| influence | | ✅ | | | | | | | |
| heal | ✅ | ✅ | | | | | ✅ | ✅ | |
| mana / crystal | | | | | | | | | ✅ |
| special | | | | | | | | | ✅ * |
| end_of_turn_return hook | | | | | | | | ✅ | |

*\* Default any phase — card text overrides via `legal_phases` field in `cards.yaml`.*

**Implementation:**

```csharp
public static class PhaseGate {
    private static readonly HashSet<(EffectType, GamePhase)> _legal = new() {
        (EffectType.Move,         GamePhase.Movement),
        (EffectType.AttackMelee,  GamePhase.CombatMelee),
        (EffectType.AttackRanged, GamePhase.CombatRanged),
        (EffectType.AttackRanged, GamePhase.CombatMelee),
        (EffectType.AttackSiege,  GamePhase.CombatRanged),
        (EffectType.AttackSiege,  GamePhase.CombatMelee),
        (EffectType.Block,        GamePhase.CombatBlock),
        (EffectType.Influence,    GamePhase.Interaction),
        (EffectType.Heal,         GamePhase.Movement),
        (EffectType.Heal,         GamePhase.Interaction),
        (EffectType.Heal,         GamePhase.Rest),
        (EffectType.Heal,         GamePhase.EndOfTurn),
        (EffectType.Mana,         GamePhase.Any),
        (EffectType.Crystal,      GamePhase.Any),
        (EffectType.Special,      GamePhase.Any),
    };

    public static bool IsLegal(EffectType effectType, GamePhase currentPhase) =>
        _legal.Contains((effectType, GamePhase.Any)) ||
        _legal.Contains((effectType, currentPhase));
}

public Result<bool> ValidatePhase(CardDefinition card, EffectType chosenType, GamePhase phase) {
    var isValidChoice = card.EffectType == chosenType
                     || card.AlternateEffectTypes.Contains(chosenType);
    if (!isValidChoice) return Result<bool>.Fail("Invalid alternate effect type for this card");
    if (card.LegalPhases != null)
        return card.LegalPhases.Contains(phase)
            ? Result<bool>.Ok(true)
            : Result<bool>.Fail($"Card restricts play to: {string.Join(", ", card.LegalPhases)}");
    return PhaseGate.IsLegal(chosenType, phase)
        ? Result<bool>.Ok(true)
        : Result<bool>.Fail($"{chosenType} not legal in {phase}");
}
```

**Note:** The phase gate data will be finalized after the Card Effect LLD. The mechanism is complete; the table may gain rows as all card effects are audited.

---

## Screen Contracts

For each screen: what game state it reads, what events trigger UI updates, what user actions it handles and what game events they emit.

### Game Board (MapView)

- **Reads:** `WorldMap` (hex states, fog-of-war, site states), player position, `CurrentPhase`, movement points remaining, round clock
- **Triggered by:** `TileRevealed`, `PlayerMoved`, `PhaseChanged`, `SiteStateChanged`, `RoundClockAdvanced`
- **User actions → emits:**
  - Tap hex → `MoveRequested(HexCoord)`
  - Tap site → `SiteInteractionRequested(SiteId)`
  - Long-press hex → `HexInfoRequested(HexCoord)`
  - Tap end-phase → `EndPhaseRequested`

### Hand Display / Card Play Flow

- **Reads:** `DeckManager.Hand`, `CurrentPhase`, staged cards, `ManaPool`, active `PendingInteraction`
- **Triggered by:** `HandChanged`, `PhaseChanged`, `ManaChanged`, `CardStaged`, `CardUnstaged`, `ChoiceRequired`
- **User actions → emits:**
  - Tap card → `CardStaged(CardId)` / `CardUnstaged(CardId)` (toggle)
  - Tap powered toggle → `PoweredModeToggled(CardId)`
  - Tap confirm → `CardPlayConfirmed(CardId, ChosenEffectType, Target, Powered)`
  - Tap cancel → `CardUnstaged(CardId)`
  - Choice prompt resolved → `ChoiceResolved(Choice)`

### Combat Resolution

- **Reads:** `CombatState` (phase, enemy tokens, player attack/block totals, damage assignments), active `PendingInteraction`
- **Triggered by:** `CombatPhaseChanged`, `AttackResolved`, `BlockResolved`, `DamageResolved`, `ChoiceRequired`
- **User actions → emits:**
  - Tap enemy (block phase) → `BlockTargetSelected(EnemyId)`
  - Tap assign damage → `DamageAssigned(EnemyId, Amount)`
  - Tap confirm phase → `CombatPhaseConfirmed`

### Offer Screen

- **Reads:** `OfferState` (available cards/units, costs), player influence total, reputation
- **Triggered by:** `InteractionStarted`, `OfferRefreshed`, `InfluenceChanged`
- **User actions → emits:**
  - Tap item → `PurchaseRequested(ItemId)`
  - Tap pass → `PassRequested`
  - Tap close → `InteractionCompleted`

### End-of-Turn / Cleanup

- **Reads:** `DeckManager.Hand`, registered `EndOfTurnHooks`, active discard filter
- **Triggered by:** `EndOfTurnStarted`, `HookPromptRequired`, `HandCleanupRequired`
- **User actions → emits:**
  - Tap card to discard → `CardDiscarded(CardId)`
  - Tap confirm hook → `HookResolved(HookId, Choice)`
  - Tap done → `EndOfTurnConfirmed`

---

## Architecture Validation

### Validation Summary

| Check | Result | Notes |
| --- | --- | --- |
| Decision Compatibility | ✅ Pass | All 12 decisions coherent, no conflicts |
| GDD Coverage | ✅ Pass | All 14 core systems addressed |
| Pattern Completeness | ✅ Pass | All scenarios covered with examples |
| Document Completeness | ✅ Pass | No placeholders remaining |

**Systems Covered:** 14/14 | **Patterns Defined:** 9 standard + 2 novel | **Decisions Made:** 12

### Issues Resolved

- Phase gate table + dynamic alternate-mode validator defined
- Screen contracts written for all 5 required screens

### Deferred (by design)

- **Card Effect LLD** — required before phase gate data is finalized; must precede implementation
- **Enemy Effect LLD** — extension points defined; Template pattern validates in Epic 4
- **Site Interaction LLD** — Template pattern validates on first 4 sites before scaling to all 14

### Validation Date

2026-05-18
