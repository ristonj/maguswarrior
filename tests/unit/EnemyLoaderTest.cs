using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MagusWarrior.Combat;
using MagusWarrior.Core.Types;
using Xunit;

namespace MagusWarrior.Tests;

public class EnemyLoaderTest {
    // Resolve from test binary output dir (bin/Debug/net9.0) up to repo root.
    private static readonly string YamlPath =
        Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "../../../../data/enemies.yaml"));

    private static List<EnemyTokenDefinition> Load() => EnemyLoader.LoadAll(YamlPath);

    [Fact]
    public void LoadAll_LoadsAtLeastOneEnemy() {
        var defs = Load();
        Assert.NotEmpty(defs);
    }

    [Fact]
    public void LoadAll_ProwlersStats() {
        var prowlers = Load().Single(d => d.Id == "prowlers");
        Assert.Equal("Prowlers",        prowlers.Name);
        Assert.Equal(TokenColor.Green,  prowlers.Color);
        Assert.Equal(3,                 prowlers.Armor);
        Assert.Equal(2,                 prowlers.FameValue);
        var atk = Assert.Single(prowlers.Attacks);
        Assert.Equal(AttackType.Physical, atk.Type);
        Assert.Equal(4,                   atk.Value);
        Assert.Empty(prowlers.Abilities);
    }

    [Fact]
    public void LoadAll_IroncladsPhysicalResistance() {
        var ironclads = Load().Single(d => d.Id == "ironclads");
        Assert.Contains(EnemyAbility.PhysicalResistance, ironclads.Abilities);
    }

    [Fact]
    public void LoadAll_DiggersFortified() {
        var diggers = Load().Single(d => d.Id == "diggers");
        Assert.Contains(EnemyAbility.Fortified, diggers.Abilities);
    }

    [Fact]
    public void LoadAll_CursedHagsPoison() {
        var hags = Load().Single(d => d.Id == "cursed_hags");
        Assert.Contains(EnemyAbility.Poison, hags.Abilities);
    }

    [Fact]
    public void LoadAll_WolfRidersSwift() {
        var wolves = Load().Single(d => d.Id == "wolf_riders");
        Assert.Contains(EnemyAbility.Swift, wolves.Abilities);
    }

    [Fact]
    public void LoadAll_OrcSummonersSummonAbility() {
        var summoners = Load().Single(d => d.Id == "orc_summoners");
        Assert.Contains(EnemyAbility.Summon, summoners.Abilities);
        var atk = Assert.Single(summoners.Attacks);
        Assert.Equal(AttackType.None, atk.Type);
    }

    [Fact]
    public void LoadAll_WhiteColorToken() {
        var freezers = Load().Single(d => d.Id == "freezers");
        Assert.Equal(TokenColor.White, freezers.Color);
    }

    [Fact]
    public void LoadAll_VioletColorToken() {
        var monks = Load().Single(d => d.Id == "monks");
        Assert.Equal(TokenColor.Violet, monks.Color);
    }

    [Fact]
    public void LoadAll_IceGolems_DualResistance() {
        var golems = Load().Single(d => d.Id == "ice_golems");
        Assert.Contains(EnemyAbility.PhysicalResistance, golems.Abilities);
        Assert.Contains(EnemyAbility.IceResistance,      golems.Abilities);
    }
}
