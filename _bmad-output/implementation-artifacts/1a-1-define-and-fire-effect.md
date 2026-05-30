# Story 1a.1: Define and Fire an Effect

Status: done

## Story

As a dev,
I want to define an effect and fire it through the system,
so that the Effect System architecture is proven before any UI is built on top of it.

## Acceptance Criteria

1. `scripts/cards/effects/IEffect.cs` exists — `IEffect` interface with `Task<EffectResult> Execute(GameState state, EffectContext ctx)`. Returns `Task<EffectResult>` (not `EffectResult`) so async player-decision effects compile without change in later stories.
2. `scripts/cards/effects/EffectContext.cs` exists — `record EffectContext(string SourceCardId, EffectType ChosenType, GamePhase Phase, bool Powered)`. Pure C#, no Godot dependency.
3. `scripts/cards/effects/EffectResult.cs` exists — `record EffectResult(bool Success, IReadOnlyList<TriggeredEffect> Triggered)` plus `record TriggeredEffect(IEffect Effect, EffectContext Ctx, int Priority)`. Static factory: `EffectResult.Ok() => new(true, Array.Empty<TriggeredEffect>())`.
4. `scripts/cards/effects/EffectScheduler.cs` exists — `Enqueue(IEffect, int priority, EffectContext)` and `async Task ResolveAll(GameState)` using `PriorityQueue<PendingEffect, int>`. Effects that return triggered effects are re-enqueued automatically.
5. `scripts/cards/effects/EffectHookRegistry.cs` exists — `Register(HookPoint, IEffectHook, int priority)` and `async Task<EffectContext> RunHooks(HookPoint, EffectContext, GameState)`. `HookPoint` enum and `IEffectHook` interface defined in same file. `HookPoint` values: `BeforeBlockTargeting`, `AfterSourceDieRoll`, `EndOfTurn`. If no hooks are registered for a point, `RunHooks` returns `ctx` unchanged.
6. `scripts/cards/effects/PhaseGate.cs` exists — `static bool IsLegal(EffectType, GamePhase)` implemented per the architecture's phase table. A separate `PhaseValidator` class in the same file exposes `Result<bool> ValidatePhase(CardDefinition card, EffectType chosenType, GamePhase phase)` — checks card-level `LegalPhases` override first, then `PhaseGate.IsLegal`.
7. `scripts/cards/CardDefinition.cs` exists — `class CardDefinition` with `string Id`, `string Name`, `CardType Type` (enum: `BasicAction, AdvancedAction, Spell, Artifact, Wound`), `ManaColor? ManaCost`, `EffectSpec? Unpowered`, `EffectSpec? Powered`, `EffectType[] AlternateEffectTypes`, `GamePhase[]? LegalPhases`. `EffectSpec` is a DTO: `EffectType EffectType`, `string Text`, `int Move`, `int Attack`, `int Block`, `int Influence`, `int Heal` — all integer fields default to 0.
8. `scripts/cards/CardLoader.cs` exists — `static IReadOnlyList<CardDefinition> LoadAll(string yamlPath)` reads `data/cards.yaml` using **YamlDotNet**, deserializes entries via concrete POCO classes (not `Dictionary<string, object>`), throws `InvalidOperationException` on any card with null/empty `id` or unrecognized `type`. `maguswarrior.csproj` has `<PackageReference Include="YamlDotNet" Version="16.3.0" />` added.
9. `scripts/cards/effects/movement/MoveEffect.cs` exists — `class MoveEffect : IEffect` that adds its move value to `GameState.MovePointsThisTurn` and returns `Task.FromResult(EffectResult.Ok())`. Constructor: `MoveEffect(int points)`.
10. `GameState` updated: adds `int MovePointsThisTurn { get; private set; }`, `void AddMovePoints(int n)`, and `IReadOnlyList<CardDefinition> Cards { get; private set; }`. Two constructors: `GameState()` (runtime — loads from `"data/cards.yaml"`) and `GameState(IReadOnlyList<CardDefinition> cards)` (test injection). The no-arg constructor throws `InvalidOperationException` on load failure — startup failure, throw is correct.
11. `GameDebug.FireTestEffect(GameState state, string cardId, GamePhase phase)` (wrapped in `[Conditional("DEBUG")]`) wires the pipeline: looks up card in `state.Cards`, validates with `PhaseGate.IsLegal`, creates `MoveEffect`, enqueues to a local `EffectScheduler`, calls `ResolveAll`, logs result via `Log.Debug("[Effect]", ...)`. `GameDebug.cs` has Godot dependency — it must NOT be added to `maguswarrior.Tests.csproj`.
12. `tests/unit/EffectSystemTest.cs` contains xUnit tests:
    - `MoveEffect_AddsMovePoints` — `MoveEffect(2).Execute(state, ctx)` adds 2 to `GameState.MovePointsThisTurn`
    - `PhaseGate_AllowsLegalMoveInMovementPhase` — `PhaseGate.IsLegal(EffectType.Move, GamePhase.Movement)` returns true
    - `PhaseGate_BlocksMoveInCombatMeleePhase` — `PhaseGate.IsLegal(EffectType.Move, GamePhase.CombatMelee)` returns false
    - `EffectScheduler_ResolvesQueuedEffect` — enqueue `MoveEffect(2)`, call `ResolveAll`, verify `MovePointsThisTurn == 2`
    - `CardLoader_LoadsMarchCard` — `CardLoader.LoadAll(csvPath)` returns a collection containing a card with `Id == "march"` and `Unpowered.Move == 2`
13. All new Godot-free files added to `tests/maguswarrior.Tests.csproj` `<Compile>` links: `CardDefinition.cs`, `CardLoader.cs`, `IEffect.cs`, `EffectContext.cs`, `EffectResult.cs`, `EffectScheduler.cs`, `EffectHookRegistry.cs`, `PhaseGate.cs`, `MoveEffect.cs`. YamlDotNet package reference added to test project as well.
14. `dotnet test tests/maguswarrior.Tests.csproj` passes — all tests green (existing 10 + 5 new = 15 total), no Godot runtime needed.

## Tasks / Subtasks

- [x] Task 1: Add YamlDotNet and create core effect value types (AC: 1, 2, 3)
  - [x] Add `<PackageReference Include="YamlDotNet" Version="16.3.0" />` to `maguswarrior.csproj` AND `tests/maguswarrior.Tests.csproj`
  - [x] Create `scripts/cards/effects/IEffect.cs` — namespace `MagusWarrior.Cards.Effects`. Usings: `System.Threading.Tasks`, `MagusWarrior.Core`.
  - [x] Create `scripts/cards/effects/EffectContext.cs` — namespace `MagusWarrior.Cards.Effects`. Usings: `MagusWarrior.Core.Types`.
  - [x] Create `scripts/cards/effects/EffectResult.cs` — namespace `MagusWarrior.Cards.Effects`. Include static `EffectResult.Ok()` factory. Usings: `System`, `System.Collections.Generic`.

- [x] Task 2: Create EffectScheduler (AC: 4)
  - [x] Create `scripts/cards/effects/EffectScheduler.cs` — namespace `MagusWarrior.Cards.Effects`
  - [x] Private record `PendingEffect(IEffect Effect, EffectContext Ctx)`
  - [x] `PriorityQueue<PendingEffect, int> _queue = new()`
  - [x] `void Enqueue(IEffect effect, int priority, EffectContext ctx)`
  - [x] `async Task ResolveAll(GameState state)` — dequeues and awaits each, re-enqueues triggered effects
  - [x] Usings: `System.Collections.Generic`, `System.Threading.Tasks`, `MagusWarrior.Core`

- [x] Task 3: Create EffectHookRegistry (AC: 5)
  - [x] Create `scripts/cards/effects/EffectHookRegistry.cs` — namespace `MagusWarrior.Cards.Effects`
  - [x] Define `public enum HookPoint { BeforeBlockTargeting, AfterSourceDieRoll, EndOfTurn }` in same file
  - [x] Define `public interface IEffectHook { bool AppliesTo(EffectContext ctx); Task<EffectContext> Transform(EffectContext ctx, GameState state); }` in same file
  - [x] `Dictionary<HookPoint, List<(IEffectHook Hook, int Priority)>> _hooks = new()`
  - [x] `void Register(HookPoint, IEffectHook, int priority = 0)`
  - [x] `async Task<EffectContext> RunHooks(HookPoint, EffectContext, GameState)` — if no entries, return ctx; else run in priority order
  - [x] Usings: `System.Collections.Generic`, `System.Linq`, `System.Threading.Tasks`, `MagusWarrior.Core`

- [x] Task 4: Create PhaseGate and PhaseValidator (AC: 6)
  - [x] Create `scripts/cards/effects/PhaseGate.cs` — namespace `MagusWarrior.Cards.Effects`
  - [x] `static class PhaseGate` with `static readonly HashSet<(EffectType, GamePhase)> _legal`
  - [x] `static bool IsLegal(EffectType effectType, GamePhase currentPhase)` — checks Any bucket first, then specific phase
  - [x] `class PhaseValidator` in same file: `Result<bool> ValidatePhase(CardDefinition card, EffectType chosenType, GamePhase phase)`
  - [x] Usings: `System.Collections.Generic`, `MagusWarrior.Cards`, `MagusWarrior.Core`, `MagusWarrior.Core.Types`

- [x] Task 5: Create CardDefinition and CardLoader (AC: 7, 8)
  - [x] Create `scripts/cards/CardDefinition.cs` — namespace `MagusWarrior.Cards`
  - [x] Define `CardType` enum in same file: `BasicAction, AdvancedAction, Spell, Artifact, Wound`
  - [x] `class EffectSpec` — all int fields default to 0
  - [x] `class CardDefinition` — `AlternateEffectTypes` defaults to `Array.Empty<EffectType>()`; `LegalPhases` defaults to null
  - [x] Create `scripts/cards/CardLoader.cs` — namespace `MagusWarrior.Cards`
  - [x] Private POCOs `CardFileRoot`, `YamlCardEntry`, `YamlEffectSpec` for YamlDotNet deserialization
  - [x] `static IReadOnlyList<CardDefinition> LoadAll(string yamlPath)`: read file, deserialize, convert to `CardDefinition` list, throw on invalid entries
  - [x] `StringOrListConverter` handles `effect_type` as scalar or list; `IgnoreUnmatchedProperties()` skips complex fields

- [x] Task 6: Create MoveEffect and update GameState (AC: 9, 10)
  - [x] Create `scripts/cards/effects/movement/MoveEffect.cs` — namespace `MagusWarrior.Cards.Effects.Movement`
  - [x] `class MoveEffect : IEffect` — `MoveEffect(int points)`, Execute calls `state.AddMovePoints(_points)`, returns `Task.FromResult(EffectResult.Ok())`
  - [x] Update `scripts/core/GameState.cs`: added `MovePointsThisTurn`, `AddMovePoints`, `Cards`, two constructors

- [x] Task 7: Wire GameDebug.FireTestEffect (AC: 11)
  - [x] Added `FireTestEffect(GameState state, string cardId, GamePhase phase)` to `GameDebug.cs`
  - [x] Wired call in `PlaceholderMainMenu._Ready()` — fire-and-forget `async void`

- [x] Task 8: Write tests and update test project (AC: 12, 13, 14)
  - [x] Created `tests/unit/EffectSystemTest.cs` with all 5 tests
  - [x] Updated `tests/maguswarrior.Tests.csproj` with YamlDotNet + all 9 Compile links
  - [x] `dotnet test` — 15/15 pass

### Review Findings

_From `/gds-code-review` on 2026-05-29 (Blind Hunter + Edge Case Hunter + Acceptance Auditor)._

#### Patches

- [x] [Review][Patch] Document that ranged/siege are intentionally legal in `CombatMelee` phase — confirmed by John as official Mage Knight rule, not a house rule; added comment in PhaseGate [scripts/cards/effects/PhaseGate.cs:13]
- [x] [Review][Patch] `FireTestEffect` missing `[Conditional("DEBUG")]` attribute — added [scripts/core/GameDebug.cs:16]
- [x] [Review][Patch] `ParseEffectType` silently mapped unknown effect_type strings to `EffectType.Special` — now throws `InvalidOperationException`. Surfaced two real data gaps: `Healing` (aliased to `Heal`) and `Banner` (added as new `EffectType.Banner`) [scripts/cards/CardLoader.cs, scripts/core/types/EffectType.cs]
- [x] [Review][Patch] `ParseManaColor` silently returned `null` for unknown mana strings — now throws; preserves null only for genuinely absent values [scripts/cards/CardLoader.cs]
- [x] [Review][Patch] `GameState()` no-arg ctor exception contract — wrapped `CardLoader.LoadAll` so non-`InvalidOperationException` failures are re-thrown as `InvalidOperationException` with inner [scripts/core/GameState.cs]
- [x] [Review][Patch] `CardLoader.LoadAll` now guards against `root.Cards == null` [scripts/cards/CardLoader.cs]
- [x] [Review][Patch] Duplicate card IDs in YAML now throw at load time via `HashSet<string>` check [scripts/cards/CardLoader.cs]
- [x] [Review][Patch] `async void FireTestEffect` body wrapped in try/catch with `Log.Debug` on exception [scripts/core/GameDebug.cs]
- [x] [Review][Patch] Test variable renamed `CsvPath` → `YamlPath` [tests/unit/EffectSystemTest.cs]

#### Deferred (real but out of scope)

- [x] [Review][Defer] Relative `data/cards.yaml` path will fail on Android device (CWD differs) — needs `ProjectSettings.GlobalizePath` rework; ties to story-0.2 device blocker [scripts/core/GameState.cs:9]
- [x] [Review][Defer] `AlternateEffectTypes` and `LegalPhases` never populated from YAML — `PhaseValidator` alternate/override paths are dead code; needed for 1b multi-type cards [scripts/cards/CardLoader.cs]
- [x] [Review][Defer] `EffectScheduler` lacks exception handling, cancellation, cycle detection — minimal-by-spec; address as effect chains grow [scripts/cards/effects/EffectScheduler.cs]
- [x] [Review][Defer] `PriorityQueue<T,int>` is not stable; equal-priority effects nondeterministic — no conflicts today (all priorities 0); add insertion counter when priorities matter [scripts/cards/effects/EffectScheduler.cs]
- [x] [Review][Defer] `EffectHookRegistry` sorts on every call and is not thread-safe — stub today, no hooks registered [scripts/cards/effects/EffectHookRegistry.cs]
- [x] [Review][Defer] `GameState.MovePointsThisTurn` has no reset path — end-of-turn structure out of scope for 1a-1 [scripts/core/GameState.cs:11]
- [x] [Review][Defer] `CardDefinition` exposes public mutable setters — required by YamlDotNet; revisit when adding `init` accessors via custom deserializer [scripts/cards/CardDefinition.cs]
- [x] [Review][Defer] `PhaseGate.GamePhase.Any` semantics ambiguous when passed as `currentPhase` argument — not exercised today [scripts/cards/effects/PhaseGate.cs:27-29]
- [x] [Review][Defer] Test coverage gaps — `PhaseValidator`, `EffectHookRegistry`, `Powered` branch, multi-type, `StringOrListConverter` sequence form, scheduler priority/triggered/exception paths — expand as system grows [tests/unit/EffectSystemTest.cs]
- [x] [Review][Defer] CardLoader test path uses fragile `../../../../data/cards.yaml` — works currently; refactor if TFM/output paths change [tests/unit/EffectSystemTest.cs]
- [x] [Review][Defer] YamlDotNet `16.3.0` version duplicated across `maguswarrior.csproj` and tests csproj — centralize via `Directory.Packages.props` [maguswarrior.csproj, tests/maguswarrior.Tests.csproj]
- [x] [Review][Defer] Tests csproj uses per-file `<Compile Include="../scripts/...">` — every new effect needs a manual entry; switch to a glob with Godot excludes [tests/maguswarrior.Tests.csproj]

## Dev Notes

### This story is the minimal effect skeleton

Per our design conversation: this story proves the architecture, not implements all cards. `MoveEffect` is the only concrete implementation needed. The design will evolve naturally in story 1b as real usage reveals what the abstractions actually need. Do not add effects for attack, block, influence, etc. — those come later.

**What this story does NOT include:**
- Effects for any type other than Move
- ChooseOneEffect, targeting, UIBroker calls
- HookRegistry hooks beyond the empty stub
- Full event log inspector (story 1a-3)
- Undo (story 1a-2)
- HeroLoader or deck construction

### IEffect.Execute must return Task<EffectResult>

The architecture doc shows `EffectResult Execute(...)` (sync) but the scheduler shows `await Effect.Execute(...)`. Use `Task<EffectResult>` — the scheduler usage is authoritative. `MoveEffect` returns `Task.FromResult(EffectResult.Ok())` (synchronous wrapped in Task). This cannot be changed later without breaking every IEffect implementation, so get it right now.

### YamlDotNet and Android AOT

Use `DeserializerBuilder` with concrete POCO types (not `Dictionary<string, object>`). Concrete types are statically known so IL2CPP can handle them. If AOT issues appear at export time, add `[Preserve]` attributes to the POCO classes. Don't pre-optimize for this — handle it when it's actually a problem.

### cards.yaml structure — POCO classes for CardLoader

```
Root object has a `cards:` key (list).
```

```csharp
private class CardFileRoot { public List<YamlCardEntry> Cards { get; set; } = new(); }
private class YamlCardEntry {
    public string? Id { get; set; }
    public string? Type { get; set; }
    public string? Name { get; set; }
    public string? ManaCost { get; set; }
    public YamlEffectSpec? Unpowered { get; set; }
    public YamlEffectSpec? Powered { get; set; }
}
private class YamlEffectSpec {
    public string? EffectType { get; set; }
    public string? Text { get; set; }
    public int Move { get; set; }
    public int Attack { get; set; }
    public int Block { get; set; }
    public int Influence { get; set; }
    public int Heal { get; set; }
}
```

`UnderscoredNamingConvention` maps `ManaCost` → `mana_cost`, `EffectType` → `effect_type`, etc.

### GameState two-constructor pattern

```csharp
public GameState() : this(CardLoader.LoadAll("data/cards.yaml")) { }
public GameState(IReadOnlyList<CardDefinition> cards) { Cards = cards; }
```

All unit tests use `new GameState(new List<CardDefinition>())` to avoid file I/O. The no-arg constructor is runtime-only. This pattern is established here and used by all future stories that test GameState.

### PhaseGate — full entry list

```csharp
(EffectType.Move,          GamePhase.Movement),
(EffectType.AttackMelee,   GamePhase.CombatMelee),
(EffectType.AttackRanged,  GamePhase.CombatRanged),
(EffectType.AttackRanged,  GamePhase.CombatMelee),
(EffectType.AttackSiege,   GamePhase.CombatRanged),
(EffectType.AttackSiege,   GamePhase.CombatMelee),
(EffectType.Block,         GamePhase.CombatBlock),
(EffectType.Influence,     GamePhase.Interaction),
(EffectType.Heal,          GamePhase.Movement),
(EffectType.Heal,          GamePhase.Interaction),
(EffectType.Heal,          GamePhase.Rest),
(EffectType.Heal,          GamePhase.EndOfTurn),
(EffectType.Mana,          GamePhase.Any),
(EffectType.Crystal,       GamePhase.Any),
(EffectType.Special,       GamePhase.Any),
```

`Fame` and `Reputation` are side-effects applied by the effect system (not primary play types) and are not in the phase gate table.

### Namespace conventions

| Folder | Namespace |
|--------|-----------|
| `scripts/core/` | `MagusWarrior.Core` |
| `scripts/core/types/` | `MagusWarrior.Core.Types` |
| `scripts/cards/` | `MagusWarrior.Cards` |
| `scripts/cards/effects/` | `MagusWarrior.Cards.Effects` |
| `scripts/cards/effects/movement/` | `MagusWarrior.Cards.Effects.Movement` |

`ImplicitUsings=disable` — every `using` must be explicit in every C# file in the main project.

### Project Context Rules

- `scripts/ui/` is the ONLY folder where `: Node`, `: Control`, or `partial class` is permitted — none of the new files are in `scripts/ui/`
- `IEffect.Execute` returns `Task<EffectResult>` — not `EffectResult`, not `async void`
- No `async void` in game logic — `PlaceholderMainMenu._Ready()` becoming `async void` is acceptable (Godot lifecycle method); `GameDebug.FireTestEffect` as `async void` is acceptable (dev tool); game logic methods must return `Task`
- Return `Result<T>` for expected failures — `PhaseValidator.ValidatePhase` returns `Result<bool>`; throw only for startup failures (`CardLoader.LoadAll`)
- Always `Log.Debug("[Effect]", ...)` — never `GD.Print`
- Constructor injection — `EffectScheduler` and `EffectHookRegistry` are passed in or created locally; no singleton access
- No per-card hardcoding in resolvers — `MoveEffect` reads its value from constructor, not from a card id check
- Hook registration: no hooks registered in this story; the registry is a stub that will be populated in future stories

### References

- Effect System: `_bmad-output/game-architecture.md` §Effect System, §Effect Scheduler, §Novel Pattern: Effect Hook System
- Phase gate: `_bmad-output/game-architecture.md` §Phase Gate
- File placement: `_bmad-output/game-architecture.md` §System Location Map
- Async non-negotiable: `_bmad-output/game-architecture.md` §PendingInteraction System
- Test path pattern: `_bmad-output/implementation-artifacts/0-3-add-ui-string-via-string-table.md` (AppContext.BaseDirectory 4-level-up)
- cards.yaml format: `data/cards.yaml` lines 1–112 (comment block)
- EffectType enum: `scripts/core/types/EffectType.cs` (already exists — do not redefine)
- GamePhase enum: `scripts/core/types/GamePhase.cs` (already exists — do not redefine)

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

- All 8 tasks completed. 15/15 xUnit tests pass (10 existing + 5 new).
- `EffectPriority` constants class dropped — no priority conflicts exist yet with a single effect; add when needed.
- `CardLoader` uses `StringOrListConverter` (custom `IYamlTypeConverter`) to handle `effect_type` being either a scalar or a list (e.g. `[Move, Influence, Combat]` on `improvisation`). `IgnoreUnmatchedProperties()` silently skips all other complex YAML fields (choose_one, mana_token, discard_for, etc.).
- `MoveEffect.Execute` does not call `Log.Debug` — game logic must not reference `Log.cs` (Godot dependency). Logging belongs in the entry/exit scaffolding (`GameDebug.FireTestEffect`).
- `GameState` two-constructor pattern established: no-arg constructor for Godot runtime (loads cards.yaml), `GameState(IReadOnlyList<CardDefinition>)` constructor for test injection (no file I/O).
- `PlaceholderMainMenu._Ready()` calls `GameDebug.FireTestEffect` fire-and-forget — confirms pipeline end-to-end on device via logcat `[Effect]` tag. On-device verification (Task 7 equivalent) requires APK rebuild.

### File List

- `scripts/cards/effects/IEffect.cs` (new)
- `scripts/cards/effects/EffectContext.cs` (new)
- `scripts/cards/effects/EffectResult.cs` (new)
- `scripts/cards/effects/EffectScheduler.cs` (new)
- `scripts/cards/effects/EffectHookRegistry.cs` (new)
- `scripts/cards/effects/PhaseGate.cs` (new)
- `scripts/cards/effects/movement/MoveEffect.cs` (new)
- `scripts/cards/CardDefinition.cs` (new)
- `scripts/cards/CardLoader.cs` (new)
- `scripts/core/GameState.cs` (modified — added MovePointsThisTurn, AddMovePoints, Cards, two constructors)
- `scripts/core/GameDebug.cs` (modified — added FireTestEffect)
- `scripts/ui/screens/PlaceholderMainMenu.cs` (modified — added FireTestEffect call in _Ready)
- `tests/unit/EffectSystemTest.cs` (new)
- `tests/maguswarrior.Tests.csproj` (modified — added YamlDotNet + 9 Compile links)
- `maguswarrior.csproj` (modified — added YamlDotNet)
