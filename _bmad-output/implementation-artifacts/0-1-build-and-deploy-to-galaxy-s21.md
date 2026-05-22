# Story 0.1: Build and Deploy to Galaxy S21

Status: in-progress

## Story

As a dev,
I want to complete the Godot 4.6.2 + C# project scaffold and deploy a signed debug APK to the Galaxy S21,
so that the Android build pipeline and project infrastructure are verified on target hardware before any gameplay systems are built.

## Acceptance Criteria

1. Godot 4.6.2 opens the project from `project.godot` at the repo root, detects C# support, and generates/validates `maguswarrior.csproj` cleanly
2. Full directory structure from the architecture spec is present (already scaffolded — verify nothing is missing)
3. `scripts/core/` contains implementations of `Result<T>`, `Log`, `GameConstants`, `GameState` stub, `GameDebug` stub, `SaveMigrator` stub, and all types in `scripts/core/types/`
4. A `PlaceholderMainMenu` scene displays static text ("Magus Warrior — PLACEHOLDER") on launch; a `PlaceholderGame` scene exists but is empty; `PlaceholderMainMenu` is set as the main scene in `project.godot`
5. Android export template configured: API 31 minimum, Galaxy S21 target (Adreno 660), Vulkan Mobile renderer, landscape-only orientation, package name `com.maguswarrior`
6. A signed debug APK builds cleanly from Godot (`Export → Android → Debug`) with zero errors
7. APK deploys and launches on the Galaxy S21 via `adb install`; placeholder text is visible on screen
8. GUT is installed and one passing unit test exists in `tests/unit/ResultTest.cs` that exercises `Result<T>` without a scene tree
9. No `GD.Print` calls exist in any file outside `scripts/core/Log.cs`; all log output in stub files uses `Log.Error/Warn/Debug`

## Tasks / Subtasks

- [x] Task 1: Open project in Godot on Windows and verify C# setup (AC: 1)
  - [x] Open Godot 4.6.3 on Windows, navigate to the cloned repo directory, open the project
  - [x] Godot will detect `project.godot` and initialize C# support — let it generate/update `maguswarrior.csproj`
  - [x] Verify Output log shows no errors on first C# build (Ctrl+Shift+B or Build button)
  - [x] Commit the Godot-generated files (updated `.csproj`, `.godot/` is gitignored so skip that)

- [x] Task 2: Verify directory structure (AC: 2)
  - [x] Confirm all 13 system folders under `scripts/` exist (see Project Structure Notes below)
  - [x] `scenes/screens/`, `scenes/components/`, `data/`, `assets/`, `tests/unit/`, `tests/integration/` all present
  - [x] All `.gitkeep` files in leaf directories (already done in scaffold)

- [x] Task 3: Implement `scripts/core/` (AC: 3, 9)
  - [x] `scripts/core/types/GamePhase.cs` — `GamePhase` enum (see exact values in Dev Notes)
  - [x] `scripts/core/types/ManaColor.cs` — `ManaColor` enum: `White, Blue, Red, Green, Gold, Black`
  - [x] `scripts/core/types/SiteType.cs` — `SiteType` enum: placeholder `Unknown` value only
  - [x] `scripts/core/types/EffectType.cs` — `EffectType` enum (see exact values in Dev Notes)
  - [x] `scripts/core/Result.cs` — implement the canonical `Result<T>` struct (exact code in Dev Notes)
  - [x] `scripts/core/Log.cs` — `Error`, `Warn`, `[Conditional("DEBUG")] Debug` using `GD.PrintErr`/`GD.Print`; `Error` also appends to `user://errors.log` (50 KB ring-buffer)
  - [x] `scripts/core/GameConstants.cs` — empty `static` class with `MaxHandSize = 8` placeholder
  - [x] `scripts/core/GameState.cs` — stub class with `public GamePhase CurrentPhase { get; private set; }` only
  - [x] `scripts/core/GameDebug.cs` — stub class with `[Conditional("DEBUG")]` attribute and one placeholder method
  - [x] `scripts/core/SaveMigrator.cs` — empty stub class

- [x] Task 4: Create placeholder scenes (AC: 4)
  - [x] `scenes/screens/PlaceholderMainMenu.tscn` — `CanvasLayer` → `Label` "Magus Warrior — PLACEHOLDER"
  - [x] `scenes/screens/PlaceholderGame.tscn` — empty `Node2D`
  - [x] Main scene set in `project.godot`: `run/main_scene="res://scenes/screens/PlaceholderMainMenu.tscn"`

- [ ] Task 5: Configure Android export (AC: 5) — requires Godot editor on Windows
  - [ ] Editor → Manage Export Templates → install Godot 4.6.3 Android templates
  - [ ] Project → Export → Add → Android
  - [ ] Set: Min SDK = 31, Target SDK = 34, package name = `com.maguswarrior`
  - [ ] Renderer = Vulkan Mobile (already set in project.godot)
  - [ ] Orientation = Landscape (already set via `window/handheld/orientation=1` in project.godot)
  - [ ] Verify Android SDK and NDK paths in Editor Settings → Export → Android

- [ ] Task 6: Build and deploy (AC: 6, 7) — requires Godot editor + Galaxy S21
  - [ ] Project → Export → Android → Export Project (Debug)
  - [ ] `adb install -r maguswarrior.apk` on connected Galaxy S21
  - [ ] Launch app; confirm "Magus Warrior — PLACEHOLDER" visible in landscape

- [x] Task 7: Write and run .NET unit tests (AC: 8)
  - [x] Create `tests/maguswarrior.Tests.csproj` — `Microsoft.NET.Sdk` + xUnit; includes pure C# files via `<Compile>` links (no Godot SDK needed)
  - [x] Create `tests/unit/ResultTest.cs` — 4 xUnit `[Fact]` tests covering Ok and Fail for value and reference types
  - [x] `dotnet test tests/maguswarrior.Tests.csproj` — 4/4 pass, 0 failed, no Godot required

## Dev Notes

### This project already has a scaffold — what was pre-created

The following already exist in the repo from the initial scaffold:
- `project.godot` — minimal Godot 4.6.2 config; Godot may update this on first open
- `maguswarrior.csproj` — Godot C# project file; Godot will validate/update SDK version on first open
- Full directory structure under `scripts/`, `scenes/`, `assets/`, `tests/`
- `.gitignore` updated with Godot entries

**Your first action:** Clone the repo on Windows and open in Godot. Let Godot initialize C# before doing anything else.

### Core type exact values

**`GamePhase` (scripts/core/types/GamePhase.cs):**
```csharp
namespace MagusWarrior.Core.Types;

public enum GamePhase {
    Movement,
    Interaction,
    CombatRanged,
    CombatBlock,
    CombatDamage,
    CombatMelee,
    Rest,
    EndOfTurn,
    Any,
}
```

**`EffectType` (scripts/core/types/EffectType.cs):**
```csharp
namespace MagusWarrior.Core.Types;

public enum EffectType {
    Move,
    AttackMelee,
    AttackRanged,
    AttackSiege,
    Block,
    Influence,
    Heal,
    Mana,
    Crystal,
    Fame,
    Reputation,
    Special,
}
```

### Canonical `Result<T>` — do not deviate

```csharp
namespace MagusWarrior.Core;

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

### `Log.cs` — thin wrapper, GD.Print ONLY here

```csharp
using System.Diagnostics;
using Godot;

namespace MagusWarrior.Core;

public static class Log {
    private const int ErrorLogMaxBytes = 50_000;

    public static void Error(string tag, string msg) {
        GD.PrintErr($"[ERROR]{tag} {msg}");
        AppendToErrorLog($"[ERROR]{tag} {msg}");
    }

    public static void Warn(string tag, string msg) =>
        GD.Print($"[WARN]{tag} {msg}");

    [Conditional("DEBUG")]
    public static void Debug(string tag, string msg) =>
        GD.Print($"[DEBUG]{tag} {msg}");

    private static void AppendToErrorLog(string line) {
        // Ring-buffer write to user://errors.log (50 KB max)
        // Implement in full during Story 0.2 when save path is established
    }
}
```

### `ResultTest.cs` — pure C# test, no scene tree

GUT supports a "script" mode for pure C# tests. Use the following pattern (no `GutTest` inheritance — GUT will auto-discover it if the filename matches `*Test.cs`):

```csharp
using GdUnit4;
using static GdUnit4.Assertions;

namespace MagusWarrior.Tests.Unit;

[TestSuite]
public class ResultTest {
    [TestCase]
    public void OkResult_HasValue() {
        var result = Result<int>.Ok(42);
        AssertThat(result.IsSuccess).IsTrue();
        AssertThat(result.Value).IsEqual(42);
        AssertThat(result.Error).IsNull();
    }

    [TestCase]
    public void FailResult_HasError() {
        var result = Result<int>.Fail("bad input");
        AssertThat(result.IsSuccess).IsFalse();
        AssertThat(result.Error).IsEqual("bad input");
    }
}
```

Note: GUT for Godot 4 is now GdUnit4. If the Asset Library shows "GdUnit4" or "GUT", install GdUnit4 for Godot 4.x compatibility. Check the library name carefully.

### What NOT to build in this story

- No gameplay logic
- No save read/write implementation (Story 0.2)
- No i18n/string table (Story 0.3)
- No card loading, no YAML parsing
- No UIBroker signal wiring
- No async/await usage
- No scenes beyond the two placeholders

Stories 0.2 and 0.3 handle save and i18n. Create stub classes (empty body or `// TODO`) for all other systems so the directory structure is populated — the stub presence in the right location prevents a future agent from creating the file in the wrong place.

### Namespace convention

All C# files use `namespace MagusWarrior.<Domain>` matching their directory:
- `scripts/core/` → `MagusWarrior.Core`
- `scripts/core/types/` → `MagusWarrior.Core.Types`
- `scripts/cards/` → `MagusWarrior.Cards`
- `scripts/combat/` → `MagusWarrior.Combat`
- `scripts/ui/` → `MagusWarrior.UI` (only folder where `: Node` / `: Control` allowed)

### Android ADB commands

```bash
# Verify device connected
adb devices

# Install
adb install -r maguswarrior.apk

# View logs (filter to your package)
adb logcat -s "Godot"

# Pull error log after a run
adb pull /sdcard/Android/data/com.maguswarrior/files/errors.log
```

### MCP servers

- `godot-mcp` at `~/git/godot-mcp` — scene inspection and debug output via Claude Code
- `context7` — current Godot 4.6 C# API docs (use when unsure about any API signature)

### Project Context Rules

- **`scripts/ui/` only** for Godot node inheritance — `GameBoard.cs`, scene scripts, UI components. All other `scripts/` folders: pure C#, no `: Node`, no `: Control`.
- **No `async void`** — every async method returns `Task` or `Task<T>`.
- **`Log.cs` only** for console output — never call `GD.Print` directly anywhere except inside `Log.cs`.
- **`Result<T>` for expected failures** — never throw for invalid states in game logic; exceptions only for unrecoverable startup failures.
- **Constructor injection everywhere** — no singleton game state access except `Log` and `GameDebug`.

### References

- Architecture: `_bmad-output/game-architecture.md` → Development Environment, Project Structure, Naming Conventions, Cross-cutting Concerns (Result<T>, Log), Architectural Decisions
- Epics: `_bmad-output/epics.md` → Epic 0: Foundation
- Project context: `docs/project-context.md`

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

- Tasks 1–2 completed by user (Godot 4.6.3 opened with no errors, directory structure verified)
- Task 3: All `scripts/core/` files created — `Result<T>`, `Log`, `GameConstants`, `GameState`, `GameDebug`, `SaveMigrator`, plus all four type enums in `scripts/core/types/`
- Task 4: `PlaceholderMainMenu.tscn` and `PlaceholderGame.tscn` created; `run/main_scene` set in `project.godot`
- Task 7 (partial): `tests/unit/ResultTest.cs` created with 6 test cases; GdUnit4 install + test run requires Godot editor on Windows
- Tasks 5–6: Require Godot editor + Galaxy S21 — user action needed

### File List

- scripts/core/types/GamePhase.cs
- scripts/core/types/ManaColor.cs
- scripts/core/types/SiteType.cs
- scripts/core/types/EffectType.cs
- scripts/core/Result.cs
- scripts/core/Log.cs
- scripts/core/GameConstants.cs
- scripts/core/GameState.cs
- scripts/core/GameDebug.cs
- scripts/core/SaveMigrator.cs
- scenes/screens/PlaceholderMainMenu.tscn
- scenes/screens/PlaceholderGame.tscn
- project.godot (run/main_scene added)
- tests/maguswarrior.Tests.csproj
- tests/unit/ResultTest.cs
