using System.Collections.Generic;
using MagusWarrior.Cards;
using MagusWarrior.Cards.Effects;
using MagusWarrior.Core;
using MagusWarrior.Core.Types;
using Xunit;

namespace MagusWarrior.Tests;

public class GameStateTest {
    private static GameState EmptyState() => new(new List<CardDefinition>());

    [Fact]
    public void AddMovePoints_FiresResourcesChanged() {
        var state = EmptyState();
        int fired = 0;
        state.ResourcesChanged += () => fired++;
        state.AddMovePoints(3);
        Assert.Equal(1, fired);
    }

    [Fact]
    public void AddInfluencePoints_FiresResourcesChanged() {
        var state = EmptyState();
        int fired = 0;
        state.ResourcesChanged += () => fired++;
        state.AddInfluencePoints(2);
        Assert.Equal(1, fired);
    }

    [Fact]
    public void AddAttackPoints_FiresResourcesChanged() {
        var state = EmptyState();
        int fired = 0;
        state.ResourcesChanged += () => fired++;
        state.AddAttackPoints(4, EffectType.AttackMelee, AttackElement.Physical);
        Assert.Equal(1, fired);
    }

    [Fact]
    public void AddBlockPoints_FiresResourcesChanged() {
        var state = EmptyState();
        int fired = 0;
        state.ResourcesChanged += () => fired++;
        state.AddBlockPoints(5, AttackElement.Physical);
        Assert.Equal(1, fired);
    }

    [Fact]
    public void TotalAttackThisTurn_SumsAllPoolEntries() {
        var state = EmptyState();
        state.AddAttackPoints(3, EffectType.AttackMelee, AttackElement.Physical);
        state.AddAttackPoints(2, EffectType.AttackRanged, AttackElement.Physical);
        Assert.Equal(5, state.TotalAttackThisTurn);
    }

    [Fact]
    public void TotalBlockThisTurn_SumsAllPoolEntries() {
        var state = EmptyState();
        state.AddBlockPoints(4, AttackElement.Physical);
        state.AddBlockPoints(1, AttackElement.Fire);
        Assert.Equal(5, state.TotalBlockThisTurn);
    }
}
