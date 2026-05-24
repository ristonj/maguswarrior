using System.Text.Json.Serialization;
using MagusWarrior.Core.Types;

namespace MagusWarrior.Save;

public class SaveData {
    public int schema_version { get; set; } = 1;
    public GamePhase current_phase { get; set; } = GamePhase.Movement;
}

[JsonSerializable(typeof(SaveData))]
[JsonSourceGenerationOptions(UseStringEnumConverter = true)]
internal partial class SaveDataContext : JsonSerializerContext {}
