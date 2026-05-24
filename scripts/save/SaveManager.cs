using Godot;
using MagusWarrior.Core;

namespace MagusWarrior.Save;

public class SaveManager {
    private const string SavePath = "user://save.json";
    private const string TempPath = "user://save.json.tmp";

    public void Save(GameState state) {
        try {
            var data = new SaveData {
                schema_version = 1,
                current_phase = state.CurrentPhase,
            };
            var json = SaveMigrator.Serialize(data);
            // Atomic write: write to temp file, then rename over the real file
            using (var tmp = FileAccess.Open(TempPath, FileAccess.ModeFlags.Write)) {
                if (tmp == null) {
                    Log.Error("[Save]", $"Save failed: cannot open {TempPath} ({FileAccess.GetOpenError()})");
                    return;
                }
                tmp.StoreString(json);
            }
            var err = DirAccess.RenameAbsolute(
                ProjectSettings.GlobalizePath(TempPath),
                ProjectSettings.GlobalizePath(SavePath));
            if (err != Error.Ok) {
                Log.Error("[Save]", $"Save failed: rename error {err}");
                return;
            }
            Log.Debug("[Save]", "Save written");
        } catch (System.Exception ex) {
            Log.Error("[Save]", $"Save failed: {ex.Message}");
        }
    }

    public Result<SaveData> Load() {
        if (!FileAccess.FileExists(SavePath))
            return Result<SaveData>.Fail("No save file found");
        try {
            using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
            if (file == null)
                return Result<SaveData>.Fail($"Cannot open save file ({FileAccess.GetOpenError()})");
            var json = file.GetAsText();
            var data = SaveMigrator.Load(json);
            Log.Debug("[Save]", $"Save loaded schema_version={data.schema_version}");
            return Result<SaveData>.Ok(data);
        } catch (System.Exception ex) {
            return Result<SaveData>.Fail(ex.Message);
        }
    }
}
