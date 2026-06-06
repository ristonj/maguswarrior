using System.Collections.Generic;
using MagusWarrior.Cards;
using Xunit;

namespace MagusWarrior.Tests;

public class RestRuleTest {
    private static CardDefinition NonWound(string id = "march") =>
        new CardDefinition { Id = id, Name = id };

    private static CardDefinition Wound() =>
        new CardDefinition { Id = "wound", Name = "Wound", Type = CardType.Wound };

    [Fact]
    public void CanDeclareRest_EmptyHand_ReturnsFalse() {
        Assert.False(RestRule.CanDeclareRest(new List<CardDefinition>()));
    }

    [Fact]
    public void CanDeclareRest_NonEmptyHand_ReturnsTrue() {
        Assert.True(RestRule.CanDeclareRest(new[] { NonWound() }));
    }

    [Fact]
    public void IsStandardRest_HandWithNonWound_ReturnsTrue() {
        Assert.True(RestRule.IsStandardRest(new[] { NonWound() }));
    }

    [Fact]
    public void IsStandardRest_MixedHand_ReturnsTrue() {
        Assert.True(RestRule.IsStandardRest(new[] { NonWound(), Wound(), Wound() }));
    }

    [Fact]
    public void IsStandardRest_AllWounds_ReturnsFalse() {
        Assert.False(RestRule.IsStandardRest(new[] { Wound(), Wound() }));
    }

    [Fact]
    public void IsStandardRest_EmptyHand_ReturnsFalse() {
        Assert.False(RestRule.IsStandardRest(new List<CardDefinition>()));
    }

    [Fact]
    public void IsExhaustion_AllWounds_ReturnsTrue() {
        Assert.True(RestRule.IsExhaustion(new[] { Wound(), Wound() }));
    }

    [Fact]
    public void IsExhaustion_MixedHand_ReturnsFalse() {
        Assert.False(RestRule.IsExhaustion(new[] { NonWound(), Wound() }));
    }

    [Fact]
    public void IsExhaustion_EmptyHand_ReturnsFalse() {
        Assert.False(RestRule.IsExhaustion(new List<CardDefinition>()));
    }
}
