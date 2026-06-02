# Story 0.2: Write Game State to Local Save

Status: done

> **Device verification (2026-06-02):** AC #9 confirmed on Galaxy S21. Backgrounding the app
> logs `[Save] Save written` and writes `user://save.json`; relaunch logs
> `[Save] Save loaded schema_version=1` / `Existing save found: phase=Movement`. This was
> blocked by a blank-screen bug (the app showed only a grey screen) — root cause was an unrelated
> Godot-3 inline `script=` in `scenes/MainBootstrap.tscn` that Godot 4 silently ignores, so the
> main scene ran no script. Fixed in 8c06ca2. Note: AC #5's "script attached to
> PlaceholderMainMenu.tscn" was superseded earlier by programmatic instantiation
> (`PlaceholderMainMenu.cs.new()` from `MainBootstrap.gd`); the leftover `.tscn` was deleted in 2918f7d.

## Story

As a dev,
I want to write game state to local save and read it back,
so that the save pipeline is verified before any gameplay systems depend on it.

## Acceptance Criteria

1. `scripts/save/SaveData.cs` exists — pure C# DTO with `schema_version` (int, value=1) and `current_phase` (GamePhase enum). Includes a `SaveDataContext : JsonSerializerContext` partial class in the same file using `[JsonSerializable]` and `UseStringEnumConverter = true`. No Godot dependency.
2. `scripts/core/SaveMigrator.cs` implements `Load(string json) → SaveData` (static method): parses `schema_version`, throws `InvalidOperationException` for missing/invalid field (corrupt save is a startup failure — throw is correct), applies migration stubs for future versions, deserializes via `SaveDataContext`. Also implements `static string Serialize(SaveData data)`.
3. `scripts/save/SaveManager.cs` exists — pure C# class (no `: Node`, no Godot inheritance), constructor accepts no args. `Save(GameState state)` writes JSON to `user://save.json` via `Godot.FileAccess`, logs `[Save] Save written` on success and `[Save] Save failed: {error}` on failure. `Result<SaveData> Load()` reads the file, passes JSON to `SaveMigrator.Load`, catches `InvalidOperationException` and returns `Result.Fail` (corrupt save surfaced as Result, not re-thrown — the caller decides whether to abort). Returns `Result.Fail("No save file found")` if file does not exist.
4. `scripts/core/Log.cs` `AppendToErrorLog` is implemented: writes the given line to `user://errors.log` using `Godot.FileAccess` in append mode. If file size exceeds 50 KB, truncate by reading all lines and discarding the oldest half before re-writing.
5. `scripts/ui/screens/PlaceholderMainMenu.cs` exists as a `partial class PlaceholderMainMenu : CanvasLayer`. On `_Ready`: creates `SaveManager`, calls `_saveManager.Load()`, logs the result. On `NOTIFICATION_APPLICATION_PAUSED`: calls `_saveManager.Save(new GameState())`. No other logic. Script must be attached to `PlaceholderMainMenu.tscn` in Godot editor (user action).
6. `tests/unit/SaveMigratorTest.cs` contains xUnit tests covering: round-trip (serialize SaveData with Serialize, then Load, result matches original), schema_version preserved, `Load` throws `InvalidOperationException` on missing schema_version, `Load` throws on null/empty input.
7. `tests/maguswarrior.Tests.csproj` is updated to include `<Compile>` links for `scripts/save/SaveData.cs` and `scripts/core/SaveMigrator.cs`.
8. `dotnet test tests/maguswarrior.Tests.csproj` passes — all tests green, no Godot runtime needed.
9. On device: launch app, force-quit (or home button), re-launch — Godot logcat shows `[DEBUG] [Save] Save written` and `[DEBUG] [Save] Save loaded schema_version=1`.

## Tasks / Subtasks

- [x] Task 1: Complete `Log.AppendToErrorLog` (AC: 4)
  - [x] Open `scripts/core/Log.cs` — find the `// TODO: ring-buffer write to user://errors.log (50 KB max) — Story 0.2` stub
  - [x] Implement using `Godot.FileAccess.Open("user://errors.log", FileAccess.ModeFlags.ReadWrite)` — open in ReadWrite mode so you can check size
  - [x] If `fileAccess.GetLength() > 50_000`: read all text, split by `\n`, discard the first half of lines, write remainder back (reopen in Write mode to truncate)
  - [x] Append the line + `\n` to the end — use FileAccess.ModeFlags.ReadWriteKeepExisting or seek to end
  - [x] Use a try/finally to always close the file; silently swallow exceptions inside AppendToErrorLog (log errors cannot throw or you get infinite recursion)
  - [x] Note: `Godot.FileAccess` is only available at runtime; this method is not testable without Godot — that is expected

- [x] Task 2: Create `SaveData.cs` (AC: 1)
  - [x] Create `scripts/save/SaveData.cs` — namespace `MagusWarrior.Save`
  - [x] `SaveData` is a plain C# class (no `: Node`, no Godot using). Properties: `int schema_version { get; set; } = 1` and `GamePhase current_phase { get; set; } = GamePhase.Movement`
  - [x] Add `using MagusWarrior.Core.Types;` at top (for GamePhase); `using System.Text.Json.Serialization;`
  - [x] Define `SaveDataContext` partial class in the SAME file (not a separate file):
    ```csharp
    [JsonSerializable(typeof(SaveData))]
    [JsonSourceGenerationOptions(UseStringEnumConverter = true)]
    internal partial class SaveDataContext : JsonSerializerContext {}
    ```
  - [x] `SaveDataContext` uses string enum serialization — `current_phase` will serialize as `"Movement"` not `0`

- [x] Task 3: Implement `SaveMigrator.cs` (AC: 2)
  - [x] Open `scripts/core/SaveMigrator.cs` — replace the stub body
  - [x] Add `using System.Text.Json; using System.Text.Json.Nodes; using MagusWarrior.Save;`
  - [x] `public static SaveData Load(string json)` — do NOT make this an instance method; static is correct
    - Parse with `JsonNode.Parse(json)!.AsObject()` — null-forgiving operator is intentional (throws NullReferenceException on null input, which is correct startup-failure behavior)
    - Read `schema_version`: `int v = raw["schema_version"]?.GetValue<int>() ?? throw new InvalidOperationException("Save missing schema_version");`
    - Migration hook: `// v < 2: no migration needed yet — add here when schema changes`
    - Deserialize: `return raw.Deserialize<SaveData>(SaveDataContext.Default.SaveData) ?? throw new InvalidOperationException("Save deserialization returned null");`
  - [x] `public static string Serialize(SaveData data)` → `return JsonSerializer.Serialize(data, SaveDataContext.Default.SaveData);`
  - [x] No Godot imports — this file is included in the test project

- [x] Task 4: Create `SaveManager.cs` (AC: 3)
  - [x] Create `scripts/save/SaveManager.cs` — namespace `MagusWarrior.Save`
  - [x] Imports: `using Godot; using MagusWarrior.Core;`
  - [x] Class is NOT included in the test project (it imports Godot) — do NOT add a `<Compile>` link for it in `maguswarrior.Tests.csproj`
  - [x] `private const string SavePath = "user://save.json";`
  - [x] `public void Save(GameState state)`:
    - Create `new SaveData { schema_version = 1, current_phase = state.CurrentPhase }`
    - Serialize via `SaveMigrator.Serialize(data)`
    - Write via `FileAccess.Open(SavePath, FileAccess.ModeFlags.Write)` — Write mode truncates existing file (atomic enough for single-player local save)
    - `file.StoreString(json)` → `file.Close()`
    - Log success: `Log.Debug("[Save]", "Save written");`
    - Wrap write in try/catch — on exception: `Log.Error("[Save]", $"Save failed: {ex.Message}");`
  - [x] `public Result<SaveData> Load()`:
    - Check `FileAccess.FileExists(SavePath)` — if false, return `Result<SaveData>.Fail("No save file found")`
    - Read: `using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read)` → `var json = file.GetAsText()`
    - Deserialize: call `SaveMigrator.Load(json)` inside try/catch `InvalidOperationException` → return `Result<SaveData>.Fail(ex.Message)` on catch
    - On success: `Log.Debug("[Save]", $"Save loaded schema_version={data.schema_version}");` → return `Result<SaveData>.Ok(data)`
  - [x] No GameState mutation — Save is read-only with respect to GameState

- [x] Task 5: Create `PlaceholderMainMenu.cs` bridge (AC: 5)
  - [x] Create `scripts/ui/screens/PlaceholderMainMenu.cs` — namespace `MagusWarrior.UI`
  - [x] `using Godot; using MagusWarrior.Core; using MagusWarrior.Save;`
  - [x] `public partial class PlaceholderMainMenu : CanvasLayer` — `partial` is required for Godot scene attachment
  - [x] Private field: `private SaveManager _saveManager = new();`
  - [x] `public override void _Ready()`: call `_saveManager.Load()` and log result with `Log.Debug("[Save]", ...)` — e.g. "Existing save found" or "No save: {error}"
  - [x] `public override void _Notification(int what)`:
    ```csharp
    if (what == NotificationApplicationPaused)
        _saveManager.Save(new GameState());
    ```
  - [x] Script attached to `scenes/screens/PlaceholderMainMenu.tscn` by direct .tscn edit (ext_resource + script= added). Godot editor will validate on next open — if it regenerates the resource ID, that is expected.

- [x] Task 6: Update test project and write `SaveMigratorTest.cs` (AC: 6, 7, 8)
  - [x] Open `tests/maguswarrior.Tests.csproj` — add these lines to the existing `<Compile>` ItemGroup:
    ```xml
    <Compile Include="../scripts/save/SaveData.cs" />
    <Compile Include="../scripts/core/SaveMigrator.cs" />
    ```
  - [x] `SaveData.cs` has no Godot imports — safe to compile in the test project
  - [x] `SaveMigrator.cs` has no Godot imports — safe to compile in the test project (required adding `using System;` explicitly since main project has ImplicitUsings=disable)
  - [x] Note: `SaveManager.cs` has Godot imports — do NOT link it in the test project
  - [x] Create `tests/unit/SaveMigratorTest.cs` — namespace `MagusWarrior.Tests.Unit`
  - [x] All 4 test cases written and passing
  - [x] Run: `dotnet test tests/maguswarrior.Tests.csproj` — 8/8 pass (4 ResultTest + 4 SaveMigratorTest)

- [ ] Task 7: Verify on device (AC: 9) — user action required

### Review Follow-ups (AI)

- [x] [AI-Review][BLOCKER] Log.cs:37 — Replace non-existent `ModeFlags.ReadWriteKeepExisting` with `ReadWrite` (file exists path) + `Write` (file absent path)
- [x] [AI-Review][HIGH] SaveManager.cs Load() — Broaden catch from `InvalidOperationException` to `System.Exception` so `JsonException` from malformed/truncated saves is caught and returned as `Result.Fail` instead of crashing the app
- [x] [AI-Review][HIGH] Log.cs AppendToErrorLog — Dispose reader before opening rewriter; use `ReadWrite` for file-exists append, `Write` for file-absent creation (fixes both sharing conflict and create-on-absent gap)
- [x] [AI-Review][MEDIUM] SaveManager.cs Save() — Implement atomic write: serialize to `user://save.json.tmp`, then `DirAccess.RenameAbsolute` over the real file to prevent corrupt save on mid-write kill
- [x] [AI-Review][LOW] Log.cs:33 — Reader disposed via scoped `using {}` block before rewriter opens (sharing conflict on Windows eliminated)
- [x] [AI-Review][LOW] SaveMigrator.cs:11 — Wrap `GetValue<int>()` in try/catch to convert `FormatException` (non-integer schema_version) into `InvalidOperationException`
  - [ ] Attach script per Task 5 if not done, rebuild APK (`Project → Export → Android → Export Project (Debug)`)
  - [ ] Install: `adb install -r maguswarrior.apk`
  - [ ] Monitor logcat: `adb logcat -s "Godot" | grep "\[Save\]"`
  - [ ] Launch app → expect to see `[DEBUG] [Save] No save: No save file found`
  - [ ] Home button → expect to see `[DEBUG] [Save] Save written`
  - [ ] Re-launch → expect to see `[DEBUG] [Save] Save loaded schema_version=1`

## Dev Notes

### What to build vs. what not to build

This story establishes the save **pipeline** — plumbing only. No gameplay state is saved yet beyond `GamePhase`. All fields added to `SaveData` in future stories will be additive — do not design a "complete" schema now.

**DO NOT:**
- Add any GameState fields beyond `CurrentPhase` to SaveData (those come in the stories that build those systems)
- Implement cloud save (Epic 10)
- Implement save migration logic beyond the `schema_version` guard (no actual v1→v2 migration exists yet)
- Use `GD.Print` directly (always `Log.Debug/Warn/Error`)
- Use `async void` anywhere

### System.Text.Json source generator — AOT critical

Android uses IL2CPP/AOT compilation. Reflection-based JSON serialization breaks at runtime. The `[JsonSerializable]` source generator approach generates the serialization code at compile time — this is non-negotiable for Android compatibility.

The `SaveDataContext.Default.SaveData` pattern is the correct source-generator-compatible call. Do NOT use `JsonSerializer.Serialize(data)` without the context argument anywhere in game code — it falls back to reflection.

```csharp
// CORRECT — source generator path, AOT-safe
var json = JsonSerializer.Serialize(data, SaveDataContext.Default.SaveData);
var data = JsonSerializer.Deserialize<SaveData>(json, SaveDataContext.Default.SaveData);

// ALSO CORRECT — equivalent shorthand via context
var data = SaveDataContext.Default.SaveData.Deserialize(json);  // if needed

// FORBIDDEN in game code — uses reflection, breaks on Android AOT
var json = JsonSerializer.Serialize(data);
```

In tests, the source generator also runs (it's a standard .NET compiler feature, not Godot-specific). The same `SaveDataContext.Default.SaveData` path works in the test project. No special handling needed for tests.

### Godot.FileAccess — thread and lifetime notes

`FileAccess.Open()` returns `null` if the file can't be opened (not an exception). Always null-check or use the `using var` pattern which handles null gracefully.

```csharp
// CORRECT pattern
using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
if (file == null) {
    Log.Error("[Save]", $"Cannot open {SavePath} for write: {FileAccess.GetOpenError()}");
    return;
}
file.StoreString(json);
// file is closed automatically by the using block (Godot FileAccess implements IDisposable)
```

`FileAccess.FileExists(path)` is the correct way to check file existence without opening it.

### pure C# vs. Godot-dependent files — test project gate

| File | Has Godot imports | Include in test .csproj |
|------|------------------|------------------------|
| `scripts/save/SaveData.cs` | No | YES |
| `scripts/core/SaveMigrator.cs` | No | YES |
| `scripts/save/SaveManager.cs` | Yes (Godot.FileAccess) | NO |
| `scripts/core/Log.cs` | Yes (Godot.GD) | NO |
| `scripts/ui/screens/PlaceholderMainMenu.cs` | Yes (Godot.CanvasLayer) | NO |

### Namespace convention

| Folder | Namespace |
|--------|-----------|
| `scripts/save/` | `MagusWarrior.Save` |
| `scripts/core/` | `MagusWarrior.Core` |
| `scripts/ui/` | `MagusWarrior.UI` |

`ImplicitUsings` is disabled in this project (`maguswarrior.csproj` has `<ImplicitUsings>disable</ImplicitUsings>`) — every `using` must be explicit.

### Log.AppendToErrorLog — silently swallow exceptions

Logging must never fail loudly. If the file write fails, do nothing. If you call `Log.Error` inside `AppendToErrorLog`, you create infinite recursion. The ring-buffer is best-effort.

```csharp
private static void AppendToErrorLog(string line) {
    try {
        const long MaxBytes = 50_000;
        if (FileAccess.FileExists("user://errors.log")) {
            using var existing = FileAccess.Open("user://errors.log", FileAccess.ModeFlags.Read);
            if (existing != null && existing.GetLength() > MaxBytes) {
                var content = existing.GetAsText();
                var lines = content.Split('\n');
                var trimmed = string.Join('\n', lines.Skip(lines.Length / 2));
                using var rewrite = FileAccess.Open("user://errors.log", FileAccess.ModeFlags.Write);
                rewrite?.StoreString(trimmed);
            }
        }
        using var file = FileAccess.Open("user://errors.log", FileAccess.ModeFlags.ReadWriteKeepExisting);
        if (file != null) {
            file.SeekEnd(0);
            file.StoreString(line + "\n");
        }
    } catch { /* swallow — logging must not throw */ }
}
```

Note: `FileAccess.ModeFlags.ReadWriteKeepExisting` creates the file if absent and keeps existing content — correct for append mode.

### NOTIFICATION_APPLICATION_PAUSED bridge

This is a thin Godot-to-logic bridge. The `PlaceholderMainMenu.cs` is intentionally minimal. In later epics, a `GameRoot` node will own this responsibility. For now, the placeholder scene is the only entry point.

The `_Notification` override is the standard Godot pattern for lifecycle events. `NotificationApplicationPaused` is the constant.

### SaveMigrator is static — not registered via DI

`SaveMigrator` is a static utility class because it has no instance state. `SaveManager` is an instance class because future stories will pass it via constructor injection to other systems (e.g., the turn loop needs to call Save at decision boundaries). Do not make `SaveManager` static.

### GameState.CurrentPhase access

`GameState.CurrentPhase` has `private set` — meaning external code cannot SET it, but CAN read it. `SaveManager.Save(GameState state)` reads `state.CurrentPhase` — this is fine.

When `PlaceholderMainMenu.cs` calls `_saveManager.Save(new GameState())`, it creates a GameState with the default phase (`Movement`). This is only for demonstration — later the real GameState instance will be threaded through properly.

### Project Context Rules (required for compliance)

**From `docs/project-context.md`:**
- `scripts/ui/` is the ONLY folder where `: Node`, `: Control`, or `partial class` (Godot-generated) is permitted — `PlaceholderMainMenu.cs` must live in `scripts/ui/screens/`, not `scripts/save/`
- Never `async void` — if any save operation becomes async later, it must return `Task`
- Use `Result<T>` for expected failures (file not found) — throw only for unrecoverable startup failures (corrupt JSON)
- `Log.cs` only for output — never `GD.Print` directly
- Constructor injection for all dependencies — `SaveManager` takes no args in this story; when GameState is later passed to SaveManager, use constructor injection, not a static getter
- Required log tag for all save operations: `[Save]`

### Project structure notes

Files created in this story:
- `scripts/save/SaveData.cs` (new)
- `scripts/save/SaveManager.cs` (new)
- `scripts/ui/screens/PlaceholderMainMenu.cs` (new — Godot script)
- `tests/unit/SaveMigratorTest.cs` (new)

Files modified in this story:
- `scripts/core/SaveMigrator.cs` (stub → implementation)
- `scripts/core/Log.cs` (AppendToErrorLog TODO → implementation)
- `tests/maguswarrior.Tests.csproj` (add 2 Compile links)
- `scenes/screens/PlaceholderMainMenu.tscn` (Godot editor attaches script — adds ext_resource + script= line)

### References

- Save system specification: `_bmad-output/game-architecture.md` → Save System section
- File placement: `_bmad-output/game-architecture.md` → System Location Map
- Namespace conventions: `_bmad-output/game-architecture.md` → Naming Conventions
- Error handling: `docs/project-context.md` → Error Handling section
- Previous story patterns (test project setup, csproj structure): `_bmad-output/implementation-artifacts/0-1-build-and-deploy-to-galaxy-s21.md`
- Log ring-buffer requirement: `_bmad-output/game-architecture.md` → Logging table (ERROR level writes to errors.log)

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

- Tasks 1–6 completed by agent. All code implemented per spec.
- `using System;` required explicitly in `SaveMigrator.cs` — `ImplicitUsings=disable` in main project propagates to linked test files.
- `scenes/screens/PlaceholderMainMenu.tscn` edited directly to add script ext_resource reference. Godot may regenerate the resource ID on first open — that is expected and benign.
- Task 7 (device verification) requires user action: rebuild APK in Godot editor on Windows, deploy via adb, verify logcat shows `[Save]` messages.
- 8/8 xUnit tests pass (`dotnet test tests/maguswarrior.Tests.csproj`).
- ✅ Resolved review finding [BLOCKER]: Replaced `ModeFlags.ReadWriteKeepExisting` (CS0117) with `ReadWrite`/`Write` branch in `Log.AppendToErrorLog`
- ✅ Resolved review finding [HIGH]: `SaveManager.Load` catch broadened to `System.Exception` — `JsonException` from corrupt saves now returned as `Result.Fail` instead of crashing
- ✅ Resolved review finding [HIGH]: `Log.AppendToErrorLog` reader now disposed (scoped `using {}`) before rewriter opens; file-absent path uses `Write` to create file
- ✅ Resolved review finding [MEDIUM]: `SaveManager.Save` now atomic — writes to `user://save.json.tmp`, then renames via `DirAccess.RenameAbsolute`
- ✅ Resolved review finding [LOW]: Reader/rewriter sharing conflict eliminated by scoped `using {}` block
- ✅ Resolved review finding [LOW]: `SaveMigrator.Load` wraps `GetValue<int>()` in try/catch; `FormatException` rethrown as `InvalidOperationException`
- All 6 review findings resolved; 8/8 tests still green after fixes.

### File List

- scripts/core/Log.cs (modified — AppendToErrorLog ring-buffer implemented; review fixes applied)
- scripts/core/SaveMigrator.cs (modified — stub replaced with Load/Serialize implementation; FormatException guard added)
- scripts/save/SaveData.cs (new)
- scripts/save/SaveManager.cs (new; atomic save + broadened catch applied)
- scripts/ui/screens/PlaceholderMainMenu.cs (new)
- scenes/screens/PlaceholderMainMenu.tscn (modified — script attached)
- tests/unit/SaveMigratorTest.cs (new)
- tests/maguswarrior.Tests.csproj (modified — added SaveData.cs and SaveMigrator.cs compile links)

## Senior Developer Review (AI)

**Review date:** 2026-05-22
**Review outcome:** Changes Requested
**Action items:** 6 total — 1 Blocker, 2 High, 1 Medium, 2 Low

### Action Items

- [x] [BLOCKER] `Log.cs:37` — `ModeFlags.ReadWriteKeepExisting` does not exist in Godot 4.6 (`CS0117`); causes compile failure. Replace with `ReadWrite` (file-exists) / `Write` (file-absent) branch.
- [x] [HIGH] `SaveManager.cs:39` — `catch (InvalidOperationException)` is too narrow; `JsonException` from malformed/truncated JSON escapes and crashes the app on next launch. Broaden to `catch (Exception)`.
- [x] [HIGH] `Log.cs:37` (runtime) — Even after fixing the enum name, `ReadWrite` (`rb+`) cannot create a missing file, so `errors.log` is never initialized on first run. Use `Write` mode when file is absent.
- [x] [MEDIUM] `SaveManager.cs:16` — `ModeFlags.Write` is non-atomic (truncate-then-write); a mid-write kill leaves a zero-byte save file. Use temp-file + rename pattern.
- [x] [LOW] `Log.cs:33` — Reader is open when rewriter opens the same path; Windows sharing violation risk. Dispose reader in its own block before opening rewriter.
- [x] [LOW] `SaveMigrator.cs:11` — `GetValue<int>()` throws `FormatException` for non-integer `schema_version` values; this is not caught anywhere. Wrap and rethrow as `InvalidOperationException`.
