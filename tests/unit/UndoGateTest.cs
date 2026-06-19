using System.Collections.Generic;
using MagusWarrior.Cards;
using MagusWarrior.Cards.Effects;
using MagusWarrior.Core;
using MagusWarrior.Core.Types;
using Xunit;

namespace MagusWarrior.Tests;

public class UndoGateTest {
    private static GameState EmptyState() => new(new List<CardDefinition>());

    [Fact]
    public void TripUndoGate_FiresUndoGateCrossed() {
        var state = EmptyState();
        bool fired = false;
        state.UndoGateCrossed += () => fired = true;
        state.TripUndoGate();
        Assert.True(fired);
    }

    [Fact]
    public void TripUndoGate_WritesLastGateSnapshot() {
        var state = EmptyState();
        state.AddMovePoints(5);
        state.TripUndoGate();
        Assert.NotNull(state.LastGateSnapshot);
        Assert.Equal(5, state.LastGateSnapshot!.MovePointsThisTurn);
    }

    [Fact]
    public void TripUndoGate_SnapshotReflectsStateAtCallTime() {
        var state = EmptyState();
        state.AddMovePoints(3);
        state.TripUndoGate();
        state.AddMovePoints(2);
        Assert.Equal(3, state.LastGateSnapshot!.MovePointsThisTurn);
    }

    [Fact]
    public void TripUndoGate_ClearsEventLog() {
        var state = EmptyState();
        var snap = state.TakeSnapshot();
        state.EventLog.Append(new EffectFiredEvent("march", EffectType.Move, GamePhase.Movement, false, snap));
        state.TripUndoGate();
        Assert.Empty(state.EventLog.Events);
    }

    [Fact]
    public void TripUndoGate_LastGateSnapshot_IsNullBeforeFirstGate() {
        var state = EmptyState();
        Assert.Null(state.LastGateSnapshot);
    }
}
