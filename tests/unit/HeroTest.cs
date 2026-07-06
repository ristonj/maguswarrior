using System.Linq;
using MagusWarrior.Cards;
using MagusWarrior.Core;
using Xunit;

namespace MagusWarrior.Tests;

public class HeroTest {
    [Fact]
    public void FreshHero_HasArmorFromConstructor() {
        var hero = new Hero(2);

        Assert.Equal(2, hero.Armor);
    }

    [Fact]
    public void FreshHero_HasEmptyUnitsAndHand() {
        var hero = new Hero(2);

        Assert.Empty(hero.Units);
        Assert.Empty(hero.Hand);
    }

    [Fact]
    public void DrawWoundsToHand_AddsNWoundCards() {
        var hero = new Hero(2);

        hero.DrawWoundsToHand(3);

        Assert.Equal(3, hero.Hand.Count);
        Assert.All(hero.Hand, c => Assert.Equal(CardType.Wound, c.Type));
    }

    [Fact]
    public void AddWoundsToDiscard_AddsNWoundCards() {
        var hero = new Hero(2);

        hero.AddWoundsToDiscard(2);

        Assert.Equal(2, hero.DiscardPile.Count);
        Assert.All(hero.DiscardPile, c => Assert.Equal(CardType.Wound, c.Type));
    }
}
