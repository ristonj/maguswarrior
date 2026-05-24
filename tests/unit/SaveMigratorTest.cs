using System;
using Xunit;
using MagusWarrior.Core;
using MagusWarrior.Core.Types;
using MagusWarrior.Save;

namespace MagusWarrior.Tests.Unit;

public class SaveMigratorTest {
    [Fact]
    public void RoundTrip_SerializeAndLoad_PreservesPhase() {
        var data = new SaveData { schema_version = 1, current_phase = GamePhase.Rest };
        var json = SaveMigrator.Serialize(data);
        var result = SaveMigrator.Load(json);
        Assert.Equal(GamePhase.Rest, result.current_phase);
        Assert.Equal(1, result.schema_version);
    }

    [Fact]
    public void Load_JsonMissingSchemaVersion_Throws() {
        var json = """{"current_phase":"Movement"}""";
        Assert.Throws<InvalidOperationException>(() => SaveMigrator.Load(json));
    }

    [Fact]
    public void Load_EmptyString_Throws() {
        Assert.ThrowsAny<Exception>(() => SaveMigrator.Load(""));
    }

    [Fact]
    public void Serialize_ProducesStringPhase() {
        var data = new SaveData { current_phase = GamePhase.CombatBlock };
        var json = SaveMigrator.Serialize(data);
        Assert.Contains("CombatBlock", json);
    }
}
