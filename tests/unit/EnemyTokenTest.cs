using System.Collections.Generic;
using MagusWarrior.Combat;
using MagusWarrior.Core.Types;
using Xunit;

namespace MagusWarrior.Tests;

public class EnemyTokenTest {
    private static EnemyTokenInstance MakeEnemy(int armor, int armorModifier = 0) {
        var instance = new EnemyTokenInstance(new EnemyTokenDefinition(
            Id: "t", Name: "T", Color: TokenColor.Brown,
            Armor: armor,
            Attacks: new List<EnemyAttack>(),
            FameValue: 0,
            Abilities: new List<EnemyAbility>(),
            IsRampaging: false,
            Summon: null));
        instance.ArmorModifier = armorModifier;
        return instance;
    }

    [Fact]
    public void EffectiveArmor_IsBaseArmorPlusModifier() {
        var enemy = MakeEnemy(armor: 4, armorModifier: 2);
        Assert.Equal(6, enemy.EffectiveArmor);
    }

    [Fact]
    public void EffectiveArmor_FloorsAtOne() {
        var enemy = MakeEnemy(armor: 2, armorModifier: -5);
        Assert.Equal(1, enemy.EffectiveArmor);
    }

    [Fact]
    public void EffectiveArmor_ZeroModifier_ReturnsBaseArmor() {
        var enemy = MakeEnemy(armor: 3);
        Assert.Equal(3, enemy.EffectiveArmor);
    }
}
