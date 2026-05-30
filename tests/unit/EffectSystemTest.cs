using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MagusWarrior.Cards;
using MagusWarrior.Cards.Effects;
using MagusWarrior.Cards.Effects.Movement;
using MagusWarrior.Core;
using MagusWarrior.Core.Types;
using Xunit;

namespace MagusWarrior.Tests.Unit;

public class EffectSystemTest {
    private static readonly string YamlPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "../../../../data/cards.yaml"));

    private static GameState EmptyState() => new(new List<CardDefinition>());
    private static EffectContext MoveCtx() =>
        new("march", EffectType.Move, GamePhase.Movement, Powered: false);

    [Fact]
    public async Task MoveEffect_AddsMovePoints() {
        var state = EmptyState();
        var effect = new MoveEffect(2);
        await effect.Execute(state, MoveCtx());
        Assert.Equal(2, state.MovePointsThisTurn);
    }

    [Fact]
    public void PhaseGate_AllowsLegalMoveInMovementPhase() {
        Assert.True(PhaseGate.IsLegal(EffectType.Move, GamePhase.Movement));
    }

    [Fact]
    public void PhaseGate_BlocksMoveInCombatMeleePhase() {
        Assert.False(PhaseGate.IsLegal(EffectType.Move, GamePhase.CombatMelee));
    }

    [Fact]
    public async Task EffectScheduler_ResolvesQueuedEffect() {
        var state = EmptyState();
        var scheduler = new EffectScheduler();
        scheduler.Enqueue(new MoveEffect(2), priority: 0, MoveCtx());
        await scheduler.ResolveAll(state);
        Assert.Equal(2, state.MovePointsThisTurn);
    }

    [Fact]
    public void CardLoader_LoadsMarchCard() {
        Assert.True(File.Exists(YamlPath), $"Expected cards.yaml at {YamlPath}");
        var cards = CardLoader.LoadAll(YamlPath);
        var march = cards.FirstOrDefault(c => c.Id == "march");
        Assert.NotNull(march);
        Assert.Equal(2, march!.Unpowered!.Move);
    }

    [Fact]
    public async Task EventLog_HasOneEntry_AfterEffectFires() {
        var state = EmptyState();
        var scheduler = new EffectScheduler();
        scheduler.Enqueue(new MoveEffect(2), priority: 0, MoveCtx());
        await scheduler.ResolveAll(state);
        Assert.Single(state.EventLog.Events);
    }

    [Fact]
    public async Task EventLog_Entry_CapturesContext_And_Snapshot() {
        var state = EmptyState();
        var scheduler = new EffectScheduler();
        scheduler.Enqueue(new MoveEffect(2), priority: 0, MoveCtx());
        await scheduler.ResolveAll(state);
        var entry = state.EventLog.Events[0];
        Assert.Equal("march", entry.SourceCardId);
        Assert.Equal(EffectType.Move, entry.EffectType);
        // Snapshot is the PRE-state (0); live state moved to 2 — proves before-timing.
        Assert.Equal(2, state.MovePointsThisTurn);
        Assert.Equal(0, entry.StateBefore.MovePointsThisTurn);
    }

    [Fact]
    public async Task EventLog_Undo_RestoresState_AndRemovesEntry() {
        var state = EmptyState();
        var scheduler = new EffectScheduler();
        scheduler.Enqueue(new MoveEffect(2), priority: 0, MoveCtx());
        await scheduler.ResolveAll(state);
        Assert.Equal(2, state.MovePointsThisTurn);
        var ev = state.EventLog.PopLast();
        state.RestoreSnapshot(ev!.StateBefore);
        Assert.Equal(0, state.MovePointsThisTurn);
        Assert.Empty(state.EventLog.Events);
    }

    [Fact]
    public void EventLog_UndoOnEmpty_IsNoOp() {
        var log = new GameEventLog();
        var result = log.PopLast();
        Assert.Null(result);
    }
}
