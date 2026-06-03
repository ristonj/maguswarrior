using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MagusWarrior.Cards;
using MagusWarrior.Cards.Effects;
using MagusWarrior.Cards.Effects.Combat;
using MagusWarrior.Cards.Effects.Influence;
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
    public void CardLoader_LoadsImprovisationAlternateEffectTypes() {
        Assert.True(File.Exists(YamlPath), $"Expected cards.yaml at {YamlPath}");
        var cards = CardLoader.LoadAll(YamlPath);
        var improv = cards.FirstOrDefault(c => c.Id == "improvisation");
        Assert.NotNull(improv);
        Assert.NotEmpty(improv!.AlternateEffectTypes);
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

    [Fact]
    public async Task AttackEffect_AddsToAttackPool() {
        var state = EmptyState();
        var ctx = new EffectContext("rage", EffectType.AttackMelee, GamePhase.CombatMelee, Powered: false);
        await new AttackEffect(3, EffectType.AttackMelee, AttackElement.Physical).Execute(state, ctx);
        Assert.Equal(3, state.AttackPool[(EffectType.AttackMelee, AttackElement.Physical)]);
    }

    [Fact]
    public async Task BlockEffect_AddsToBlockPool() {
        var state = EmptyState();
        var ctx = new EffectContext("stamina", EffectType.Block, GamePhase.CombatBlock, Powered: false);
        await new BlockEffect(2, AttackElement.Physical).Execute(state, ctx);
        Assert.Equal(2, state.BlockPool[AttackElement.Physical]);
    }

    [Fact]
    public async Task InfluenceEffect_AddsInfluencePoints() {
        var state = EmptyState();
        var ctx = new EffectContext("threaten", EffectType.Influence, GamePhase.Interaction, Powered: false);
        await new InfluenceEffect(1).Execute(state, ctx);
        Assert.Equal(1, state.InfluencePointsThisTurn);
    }

    [Fact]
    public void GameStateSnapshot_CapturesAttackPool() {
        var state = EmptyState();
        state.AddAttackPoints(5, EffectType.AttackMelee, AttackElement.Fire);
        var snapshot = state.TakeSnapshot();
        Assert.Equal(5, snapshot.AttackPool[(EffectType.AttackMelee, AttackElement.Fire)]);
    }
}
