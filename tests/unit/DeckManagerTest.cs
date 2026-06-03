using System;
using System.Collections.Generic;
using MagusWarrior.Cards;
using MagusWarrior.Core;
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

    [Fact]
    public void PlayCard_RemovesCardFromHand() {
        var dm = new DeckManager();
        dm.SetHand(new[] { new CardDefinition { Id = "march", Name = "March" } });
        dm.PlayCard("march");
        Assert.Empty(dm.Hand);
    }

    [Fact]
    public void PlayCard_FiresHandChanged() {
        var dm = new DeckManager();
        dm.SetHand(new[] { new CardDefinition { Id = "march", Name = "March" } });
        int fired = 0;
        dm.HandChanged += () => fired++;
        dm.PlayCard("march");
        Assert.Equal(1, fired);
    }

    [Fact]
    public void PlayCard_ReturnsPlayedCard() {
        var dm = new DeckManager();
        dm.SetHand(new[] { new CardDefinition { Id = "march", Name = "March" } });
        var result = dm.PlayCard("march");
        Assert.True(result.IsSuccess);
        Assert.Equal("march", result.Value!.Id);
    }

    [Fact]
    public void PlayCard_FailsWhenCardNotInHand() {
        var dm = new DeckManager();
        dm.SetHand(Array.Empty<CardDefinition>());
        var result = dm.PlayCard("march");
        Assert.False(result.IsSuccess);
    }
}
