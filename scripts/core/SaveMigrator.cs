using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using MagusWarrior.Save;

namespace MagusWarrior.Core;

public class SaveMigrator {
    public static SaveData Load(string json) {
        var raw = JsonNode.Parse(json)!.AsObject();
        int v;
        try {
            v = raw["schema_version"]?.GetValue<int>()
                ?? throw new InvalidOperationException("Save missing schema_version");
        } catch (FormatException ex) {
            throw new InvalidOperationException("Save schema_version is not a valid integer", ex);
        }
        // v < 2: no migration needed yet — add here when schema changes
        _ = v;
        return raw.Deserialize<SaveData>(SaveDataContext.Default.SaveData)
            ?? throw new InvalidOperationException("Save deserialization returned null");
    }

    public static string Serialize(SaveData data) =>
        JsonSerializer.Serialize(data, SaveDataContext.Default.SaveData);
}
