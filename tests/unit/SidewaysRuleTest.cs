using MagusWarrior.Cards;
using MagusWarrior.Core.Types;
using Xunit;

namespace MagusWarrior.Tests;

public class SidewaysRuleTest {
    [Fact]
    public void GetEffect_Movement_ReturnsMove1() {
        var result = SidewaysRule.GetEffect(GamePhase.Movement);
        Assert.NotNull(result);
        Assert.Equal(EffectType.Move, result!.Value.EffectType);
        Assert.Equal(1, result.Value.Amount);
    }

    [Fact]
    public void GetEffect_Interaction_ReturnsInfluence1() {
        var result = SidewaysRule.GetEffect(GamePhase.Interaction);
        Assert.NotNull(result);
        Assert.Equal(EffectType.Influence, result!.Value.EffectType);
        Assert.Equal(1, result.Value.Amount);
    }

    [Fact]
    public void GetEffect_CombatBlock_ReturnsBlock1() {
        var result = SidewaysRule.GetEffect(GamePhase.CombatBlock);
        Assert.NotNull(result);
        Assert.Equal(EffectType.Block, result!.Value.EffectType);
        Assert.Equal(1, result.Value.Amount);
    }

    [Fact]
    public void GetEffect_CombatMelee_ReturnsAttackMelee1() {
        var result = SidewaysRule.GetEffect(GamePhase.CombatMelee);
        Assert.NotNull(result);
        Assert.Equal(EffectType.AttackMelee, result!.Value.EffectType);
        Assert.Equal(1, result.Value.Amount);
    }

    [Fact]
    public void GetEffect_CombatRanged_ReturnsNull() {
        var result = SidewaysRule.GetEffect(GamePhase.CombatRanged);
        Assert.Null(result);
    }
}
