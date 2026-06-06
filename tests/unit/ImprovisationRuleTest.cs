using System.Collections.Generic;
using MagusWarrior.Cards;
using MagusWarrior.Core.Types;
using Xunit;

namespace MagusWarrior.Tests;

public class ImprovisationRuleTest {
    private static CardDefinition NonWound(string id) =>
        new CardDefinition { Id = id, Name = id };

    private static CardDefinition Improv() =>
        new CardDefinition { Id = "improvisation", Name = "Improvisation" };

    // ── GetPhaseEffect ──────────────────────────────────────────────────────

    [Fact]
    public void GetPhaseEffect_Movement_ReturnsMove() {
        Assert.Equal(EffectType.Move, ImprovisationRule.GetPhaseEffect(GamePhase.Movement));
    }

    [Fact]
    public void GetPhaseEffect_CombatMelee_ReturnsAttackMelee() {
        Assert.Equal(EffectType.AttackMelee, ImprovisationRule.GetPhaseEffect(GamePhase.CombatMelee));
    }

    [Fact]
    public void GetPhaseEffect_CombatBlock_ReturnsBlock() {
        Assert.Equal(EffectType.Block, ImprovisationRule.GetPhaseEffect(GamePhase.CombatBlock));
    }

    [Fact]
    public void GetPhaseEffect_Interaction_ReturnsInfluence() {
        Assert.Equal(EffectType.Influence, ImprovisationRule.GetPhaseEffect(GamePhase.Interaction));
    }

    [Fact]
    public void GetPhaseEffect_Rest_ReturnsNull() {
        Assert.Null(ImprovisationRule.GetPhaseEffect(GamePhase.Rest));
    }

    [Fact]
    public void GetPhaseEffect_CombatRanged_ReturnsNull() {
        Assert.Null(ImprovisationRule.GetPhaseEffect(GamePhase.CombatRanged));
    }

    [Fact]
    public void GetPhaseEffect_EndOfTurn_ReturnsNull() {
        Assert.Null(ImprovisationRule.GetPhaseEffect(GamePhase.EndOfTurn));
    }

    // ── CanPlay ─────────────────────────────────────────────────────────────

    [Fact]
    public void CanPlay_HandWithOtherCard_ReturnsTrue() {
        IReadOnlyList<CardDefinition> hand = new[] { Improv(), NonWound("march") };
        Assert.True(ImprovisationRule.CanPlay(hand, "improvisation"));
    }

    [Fact]
    public void CanPlay_HandImprovisationOnly_ReturnsFalse() {
        IReadOnlyList<CardDefinition> hand = new[] { Improv() };
        Assert.False(ImprovisationRule.CanPlay(hand, "improvisation"));
    }

    [Fact]
    public void CanPlay_EmptyHand_ReturnsFalse() {
        IReadOnlyList<CardDefinition> hand = new CardDefinition[0];
        Assert.False(ImprovisationRule.CanPlay(hand, "improvisation"));
    }

    // ── GetAmount ───────────────────────────────────────────────────────────

    [Fact]
    public void GetAmount_Unpowered_Returns3() {
        Assert.Equal(3, ImprovisationRule.GetAmount(false));
    }

    [Fact]
    public void GetAmount_Powered_Returns5() {
        Assert.Equal(5, ImprovisationRule.GetAmount(true));
    }
}
