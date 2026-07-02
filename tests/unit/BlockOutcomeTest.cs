using System.Collections.Generic;
using MagusWarrior.Combat;
using MagusWarrior.Core.Types;
using Xunit;

namespace MagusWarrior.Tests;

public class BlockOutcomeTest {
    // Helpers: single-contribution shorthand.
    private static IEnumerable<BlockContribution> Contribs(params BlockContribution[] cs) => cs;
    private static BlockContribution C(BlockType t, int v) => new(t, v);

    // --- exact-threshold: effectiveBlock == threshold -> true ---

    [Fact]
    public void ExactThreshold_ReturnsTrue() {
        // Physical block 4 vs Physical attack 4, not Swift (threshold = 4)
        var attack = new EnemyAttack(4, AttackType.Physical);
        Assert.True(BlockOutcome.IsFullyBlocked(attack, swift: false,
            Contribs(C(BlockType.Physical, 4))));
    }

    // --- one-short: effectiveBlock == threshold - 1 -> false ---

    [Fact]
    public void OneShort_ReturnsFalse() {
        var attack = new EnemyAttack(4, AttackType.Physical);
        Assert.False(BlockOutcome.IsFullyBlocked(attack, swift: false,
            Contribs(C(BlockType.Physical, 3))));
    }

    // --- Swift doubles threshold ---

    [Fact]
    public void Swift_BlockAtNormalThreshold_ReturnsFalse() {
        // Swift Physical 4 -> threshold 8; block 4 < 8 -> false
        var attack = new EnemyAttack(4, AttackType.Physical);
        Assert.False(BlockOutcome.IsFullyBlocked(attack, swift: true,
            Contribs(C(BlockType.Physical, 4))));
    }

    [Fact]
    public void Swift_BlockAtDoubleThreshold_ReturnsTrue() {
        // Swift Physical 4 -> threshold 8; block 8 >= 8 -> true
        var attack = new EnemyAttack(4, AttackType.Physical);
        Assert.True(BlockOutcome.IsFullyBlocked(attack, swift: true,
            Contribs(C(BlockType.Physical, 8))));
    }

    // --- efficient element: Ice block vs Fire attack, 1:1 ---

    [Fact]
    public void EfficientElement_CountsFullValue() {
        // Ice block 4 vs Fire attack 4 -> efficient 1:1 -> effective 4 >= 4 -> true
        var attack = new EnemyAttack(4, AttackType.Fire);
        Assert.True(BlockOutcome.IsFullyBlocked(attack, swift: false,
            Contribs(C(BlockType.Ice, 4))));
    }

    // --- inefficient element: Physical block vs Fire attack, 2:1 ---

    [Fact]
    public void InefficientElement_HalvedBeforeComparison() {
        // Physical block 7 vs Fire attack 4 -> inefficient 7/2=3 (floor) < 4 -> false
        var attack = new EnemyAttack(4, AttackType.Fire);
        Assert.False(BlockOutcome.IsFullyBlocked(attack, swift: false,
            Contribs(C(BlockType.Physical, 7))));
    }

    [Fact]
    public void InefficientElement_SufficientAmountReturnsTrue() {
        // Physical block 8 vs Fire attack 4 -> 8/2=4 >= 4 -> true
        var attack = new EnemyAttack(4, AttackType.Fire);
        Assert.True(BlockOutcome.IsFullyBlocked(attack, swift: false,
            Contribs(C(BlockType.Physical, 8))));
    }

    // --- mixed-type sum: multiple block types combined ---

    [Fact]
    public void MixedTypes_CombinedEffectiveBlockMeetsThreshold_ReturnsTrue() {
        // Ice 2 (eff 2 vs Fire) + Physical 4 (eff 2 vs Fire, 2:1) = 4 >= 4 -> true
        var attack = new EnemyAttack(4, AttackType.Fire);
        Assert.True(BlockOutcome.IsFullyBlocked(attack, swift: false,
            Contribs(C(BlockType.Ice, 2), C(BlockType.Physical, 4))));
    }

    [Fact]
    public void MixedTypes_CombinedEffectiveBlockBelowThreshold_ReturnsFalse() {
        // Ice 1 (eff 1 vs Fire) + Physical 2 (eff 1 vs Fire) = 2 < 4 -> false
        var attack = new EnemyAttack(4, AttackType.Fire);
        Assert.False(BlockOutcome.IsFullyBlocked(attack, swift: false,
            Contribs(C(BlockType.Ice, 1), C(BlockType.Physical, 2))));
    }

    // --- empty allocation: always false ---

    [Fact]
    public void EmptyAllocation_ReturnsFalse() {
        var attack = new EnemyAttack(4, AttackType.Physical);
        Assert.False(BlockOutcome.IsFullyBlocked(attack, swift: false,
            new List<BlockContribution>()));
    }
}
