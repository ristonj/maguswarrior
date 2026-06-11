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

    [Fact]
    public void IsDay_DefaultsToTrue() {
        var state = EmptyState();
        Assert.True(state.IsDay);
    }

    [Fact]
    public void SetIsDay_False_UpdatesFlag() {
        var state = EmptyState();
        state.SetIsDay(false);
        Assert.False(state.IsDay);
    }

    [Fact]
    public void TakeSnapshot_IncludesIsDay() {
        var state = EmptyState();
        state.SetIsDay(false);
        var snap = state.TakeSnapshot();
        Assert.False(snap.IsDay);
    }

    [Fact]
    public void RestoreSnapshot_RestoresIsDay() {
        var state = EmptyState();
        state.SetIsDay(false);
        var snap = state.TakeSnapshot();  // IsDay = false (non-default, so a no-op restore can't pass)
        state.SetIsDay(true);
        state.RestoreSnapshot(snap);
        Assert.False(state.IsDay);
    }

    [Fact]
    public void SpendMovePoints_DecrementsCorrectly() {
        var state = EmptyState();
        state.AddMovePoints(5);
        state.SpendMovePoints(3);
        Assert.Equal(2, state.MovePointsThisTurn);
    }

    [Fact]
    public void SpendMovePoints_FiresResourcesChanged() {
        var state = EmptyState();
        state.AddMovePoints(3);
        bool fired = false;
        state.ResourcesChanged += () => fired = true;
        state.SpendMovePoints(2);
        Assert.True(fired);
    }

    [Fact]
    public void ResetMovePoints_SetsToZero() {
        var state = EmptyState();
        state.AddMovePoints(7);
        state.ResetMovePoints();
        Assert.Equal(0, state.MovePointsThisTurn);
    }

    [Fact]
    public void ResetMovePoints_FiresResourcesChanged() {
        var state = EmptyState();
        state.AddMovePoints(3);
        bool fired = false;
        state.ResourcesChanged += () => fired = true;
        state.ResetMovePoints();
        Assert.True(fired);
    }

    [Fact]
    public void ResetMovePoints_WhenAlreadyZero_StillFiresResourcesChanged() {
        var state = EmptyState();
        bool fired = false;
        state.ResourcesChanged += () => fired = true;
        state.ResetMovePoints();
        Assert.True(fired);
    }
}
