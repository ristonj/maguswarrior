using System;
using System.Collections.Generic;
using MagusWarrior.Cards;
using MagusWarrior.Deck;
using Xunit;

namespace MagusWarrior.Tests;

public class DeckManagerTest {
    [Fact]
    public void Hand_IsEmpty_OnConstruction() {
        var dm = new DeckManager();
        Assert.Empty(dm.Hand);
    }

    [Fact]
    public void SetHand_UpdatesHand() {
        var dm = new DeckManager();
        var cards = new List<CardDefinition> {
            new CardDefinition { Id = "march", Name = "March" },
        };
        dm.SetHand(cards);
        Assert.Single(dm.Hand);
        Assert.Equal("march", dm.Hand[0].Id);
    }

    [Fact]
    public void SetHand_FiresHandChanged() {
        var dm = new DeckManager();
        int fired = 0;
        dm.HandChanged += () => fired++;
        dm.SetHand(new List<CardDefinition> { new CardDefinition { Id = "a", Name = "A" } });
        Assert.Equal(1, fired);
    }
}
