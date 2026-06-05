using System.Collections.Generic;
using MagusWarrior.Cards;
using MagusWarrior.Core.Types;
using MagusWarrior.Deck;
using Xunit;

namespace MagusWarrior.Tests;

public class StagingManagerTest {
    private static CardDefinition MakeCard(string id, EffectType type,
        int move = 0, int attack = 0, int block = 0, int influence = 0) =>
        new() {
            Id = id,
            Name = id,
            Type = CardType.BasicAction,
            Unpowered = new EffectSpec { EffectType = type, Move = move, Attack = attack,
                                         Block = block, Influence = influence }
        };

    [Fact]
    public void Stage_AddsCardToStagedCards() {
        var mgr = new StagingManager();
        mgr.Stage(MakeCard("march", EffectType.Move, move: 2), EffectType.Move);
        Assert.Single(mgr.StagedCards);
        Assert.Equal("march", mgr.StagedCards[0].Card.Id);
    }

    [Fact]
    public void Stage_FiresStagingChanged() {
        var mgr = new StagingManager();
        int count = 0;
        mgr.StagingChanged += () => count++;
        mgr.Stage(MakeCard("march", EffectType.Move, move: 2), EffectType.Move);
        Assert.Equal(1, count);
    }

    [Fact]
    public void Clear_EmptiesStagedCards() {
        var mgr = new StagingManager();
        mgr.Stage(MakeCard("march", EffectType.Move, move: 2), EffectType.Move);
        mgr.Clear();
        Assert.Empty(mgr.StagedCards);
    }

    [Fact]
    public void Clear_FiresStagingChanged() {
        var mgr = new StagingManager();
        mgr.Stage(MakeCard("march", EffectType.Move, move: 2), EffectType.Move);
        int count = 0;
        mgr.StagingChanged += () => count++;
        mgr.Clear();
        Assert.Equal(1, count);
    }

    [Fact]
    public void GetTotals_SumsMoveTotals() {
        var mgr = new StagingManager();
        mgr.Stage(MakeCard("march",   EffectType.Move, move: 2), EffectType.Move);
        mgr.Stage(MakeCard("stamina", EffectType.Move, move: 2), EffectType.Move);
        var totals = mgr.GetTotals();
        Assert.Equal(4, totals.Move);
    }

    [Fact]
    public void GetTotals_SumsMultipleResourceTypes() {
        var mgr = new StagingManager();
        mgr.Stage(MakeCard("march",    EffectType.Move,      move: 2),      EffectType.Move);
        mgr.Stage(MakeCard("threaten", EffectType.Influence, influence: 2), EffectType.Influence);
        var totals = mgr.GetTotals();
        Assert.Equal(2, totals.Move);
        Assert.Equal(2, totals.Influence);
        Assert.Equal(0, totals.Attack);
        Assert.Equal(0, totals.Block);
    }

    [Fact]
    public void Unstage_RemovesLastStagedCard() {
        var mgr = new StagingManager();
        mgr.Stage(MakeCard("march",   EffectType.Move, move: 2), EffectType.Move);
        mgr.Stage(MakeCard("stamina", EffectType.Move, move: 2), EffectType.Move);
        mgr.Unstage();
        Assert.Single(mgr.StagedCards);
        Assert.Equal("march", mgr.StagedCards[0].Card.Id);
    }

    [Fact]
    public void Unstage_ReturnsMostRecentEntry() {
        var mgr = new StagingManager();
        mgr.Stage(MakeCard("march", EffectType.Move, move: 2), EffectType.Move);
        var entry = mgr.Unstage();
        Assert.NotNull(entry);
        Assert.Equal("march", entry!.Card.Id);
    }

    [Fact]
    public void Unstage_FiresStagingChanged() {
        var mgr = new StagingManager();
        mgr.Stage(MakeCard("march", EffectType.Move, move: 2), EffectType.Move);
        int count = 0;
        mgr.StagingChanged += () => count++;
        mgr.Unstage();
        Assert.Equal(1, count);
    }

    [Fact]
    public void Unstage_OnEmptyList_ReturnsNullAndNoEvent() {
        var mgr = new StagingManager();
        int count = 0;
        mgr.StagingChanged += () => count++;
        var entry = mgr.Unstage();
        Assert.Null(entry);
        Assert.Equal(0, count);
    }
}
