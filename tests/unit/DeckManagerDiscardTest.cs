using System;
using System.Collections.Generic;
using MagusWarrior.Cards;
using MagusWarrior.Core;
using MagusWarrior.Deck;
using Xunit;

namespace MagusWarrior.Tests;

public class DeckManagerDiscardTest {
    [Fact]
    public void DiscardPile_IsEmpty_OnConstruction() {
        var dm = new DeckManager();
        Assert.Empty(dm.DiscardPile);
    }

    [Fact]
    public void DiscardCard_RemovesCardFromHand_AddsToDiscardPile() {
        var dm = new DeckManager();
        var card = new CardDefinition { Id = "march", Name = "March" };
        dm.SetHand(new[] { card });
        var result = dm.DiscardCard("march");
        Assert.True(result.IsSuccess);
        Assert.Empty(dm.Hand);
        Assert.Single(dm.DiscardPile);
        Assert.Equal("march", dm.DiscardPile[0].Id);
    }

    [Fact]
    public void DiscardCard_WhenCardNotInHand_ReturnsFailure_AndPileUnchanged() {
        var dm = new DeckManager();
        dm.SetHand(Array.Empty<CardDefinition>());
        var result = dm.DiscardCard("march");
        Assert.False(result.IsSuccess);
        Assert.Empty(dm.DiscardPile);
    }

    [Fact]
    public void DiscardCard_FiresHandChanged() {
        var dm = new DeckManager();
        dm.SetHand(new[] { new CardDefinition { Id = "march", Name = "March" } });
        int fired = 0;
        dm.HandChanged += () => fired++;
        dm.DiscardCard("march");
        Assert.Equal(1, fired);
    }

    [Fact]
    public void DiscardCard_PreservesOtherHandCards() {
        var dm = new DeckManager();
        dm.SetHand(new[] {
            new CardDefinition { Id = "march", Name = "March" },
            new CardDefinition { Id = "stamina", Name = "Stamina" },
        });
        dm.DiscardCard("march");
        Assert.Single(dm.Hand);
        Assert.Equal("stamina", dm.Hand[0].Id);
    }
}
