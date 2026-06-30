using MagusWarrior.Combat;
using MagusWarrior.Core.Types;
using Xunit;

namespace MagusWarrior.Tests;

public class BlockEfficiencyTest {
    [Fact]
    public void Physical_vs_Physical_Efficient() =>
        Assert.Equal(4, BlockEfficiency.Effective(BlockType.Physical, AttackType.Physical, 4));

    [Fact]
    public void Physical_vs_Fire_Inefficient() =>
        Assert.Equal(2, BlockEfficiency.Effective(BlockType.Physical, AttackType.Fire, 4));

    [Fact]
    public void Fire_vs_Ice_Efficient() =>
        Assert.Equal(5, BlockEfficiency.Effective(BlockType.Fire, AttackType.Ice, 5));

    [Fact]
    public void Fire_vs_Fire_Inefficient() =>
        Assert.Equal(2, BlockEfficiency.Effective(BlockType.Fire, AttackType.Fire, 5)); // floor(5/2)

    [Fact]
    public void Ice_vs_Fire_Efficient() =>
        Assert.Equal(5, BlockEfficiency.Effective(BlockType.Ice, AttackType.Fire, 5));

    [Fact]
    public void Ice_vs_Ice_Inefficient() =>
        Assert.Equal(2, BlockEfficiency.Effective(BlockType.Ice, AttackType.Ice, 4));

    [Theory]
    [InlineData(AttackType.Physical)]
    [InlineData(AttackType.Fire)]
    [InlineData(AttackType.Ice)]
    [InlineData(AttackType.ColdFire)]
    public void ColdFire_vs_All_Efficient(AttackType attack) =>
        Assert.Equal(3, BlockEfficiency.Effective(BlockType.ColdFire, attack, 3));

    [Fact]
    public void Inefficient_OddValue_FloorsDown() =>
        Assert.Equal(1, BlockEfficiency.Effective(BlockType.Physical, AttackType.Fire, 3));

    [Fact]
    public void ZeroValue_ReturnsZero() =>
        Assert.Equal(0, BlockEfficiency.Effective(BlockType.Ice, AttackType.Ice, 0));
}
