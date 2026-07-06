using MagusWarrior.Core.Types;
using MagusWarrior.Units;
using Xunit;

namespace MagusWarrior.Tests;

public class UnitInstanceTest {
    [Fact]
    public void Constructor_StoresArmor() {
        var unit = new UnitInstance(3);

        Assert.Equal(3, unit.Armor);
    }

    [Fact]
    public void TakeWound_Accumulates() {
        var unit = new UnitInstance(3);

        unit.TakeWound();
        unit.TakeWound();

        Assert.Equal(2, unit.WoundCount);
    }

    [Fact]
    public void TakeWound_NeverDestroysNoMatterHowManyTimes() {
        var unit = new UnitInstance(3);

        unit.TakeWound();
        unit.TakeWound();
        unit.TakeWound();

        Assert.False(unit.IsDestroyed);
    }

    [Fact]
    public void Heal_DecrementsOneAtATime() {
        var unit = new UnitInstance(3);
        unit.TakeWound();
        unit.TakeWound();

        unit.Heal();

        Assert.Equal(1, unit.WoundCount);
    }

    [Fact]
    public void Heal_AtZero_IsNoOp() {
        var unit = new UnitInstance(3);

        unit.Heal();

        Assert.Equal(0, unit.WoundCount);
    }

    [Fact]
    public void Destroy_SetsFlagIndependentOfWounds() {
        var unit = new UnitInstance(3);

        unit.Destroy();

        Assert.True(unit.IsDestroyed);
        Assert.Equal(0, unit.WoundCount);
    }

    [Fact]
    public void HasResistanceTo_SingleElement_TrueOnlyForThatElement() {
        var unit = new UnitInstance(3, new[] { AttackType.Fire });

        Assert.True(unit.HasResistanceTo(AttackType.Fire));
        Assert.False(unit.HasResistanceTo(AttackType.Ice));
    }

    [Fact]
    public void HasResistanceTo_ColdFire_RequiresBothFireAndIce() {
        var fireOnly = new UnitInstance(3, new[] { AttackType.Fire });
        var both      = new UnitInstance(3, new[] { AttackType.Fire, AttackType.Ice });

        Assert.False(fireOnly.HasResistanceTo(AttackType.ColdFire));
        Assert.True(both.HasResistanceTo(AttackType.ColdFire));
    }

    [Fact]
    public void HasResistanceTo_NoResistances_FalseForEveryType() {
        var unit = new UnitInstance(3);

        Assert.False(unit.HasResistanceTo(AttackType.Physical));
        Assert.False(unit.HasResistanceTo(AttackType.Fire));
        Assert.False(unit.HasResistanceTo(AttackType.Ice));
        Assert.False(unit.HasResistanceTo(AttackType.ColdFire));
    }

    [Fact]
    public void CanAbsorbDamage_TrueWhenFresh() {
        var unit = new UnitInstance(3);

        Assert.True(unit.CanAbsorbDamage);
    }

    [Fact]
    public void CanAbsorbDamage_FalseWhenWounded() {
        var unit = new UnitInstance(3);

        unit.TakeWound();

        Assert.False(unit.CanAbsorbDamage);
    }

    [Fact]
    public void CanAbsorbDamage_FalseWhenDestroyed() {
        var unit = new UnitInstance(3);

        unit.Destroy();

        Assert.False(unit.CanAbsorbDamage);
    }
}
