using System.Collections.Generic;
using System.Threading.Tasks;
using MagusWarrior.Cards;
using MagusWarrior.Cards.Effects;
using MagusWarrior.Cards.Effects.Special;
using MagusWarrior.Core;
using MagusWarrior.Core.Types;
using Xunit;

namespace MagusWarrior.Tests;

public class ImprovisationEffectTest {
    private static GameState EmptyState() => new(new List<CardDefinition>());
    private static EffectContext Ctx(EffectType t) =>
        new("improvisation", t, GamePhase.Movement, false);

    [Fact]
    public async Task Execute_Move_AddsMovePoints() {
        var state = EmptyState();
        await new ImprovisationEffect(EffectType.Move, 3).Execute(state, Ctx(EffectType.Move));
        Assert.Equal(3, state.MovePointsThisTurn);
    }

    [Fact]
    public async Task Execute_AttackMelee_AddsAttackPoints() {
        var state = EmptyState();
        await new ImprovisationEffect(EffectType.AttackMelee, 3).Execute(state, Ctx(EffectType.AttackMelee));
        Assert.Equal(3, state.AttackPool[(EffectType.AttackMelee, AttackElement.Physical)]);
    }

    [Fact]
    public async Task Execute_Block_AddsBlockPoints() {
        var state = EmptyState();
        await new ImprovisationEffect(EffectType.Block, 3).Execute(state, Ctx(EffectType.Block));
        Assert.Equal(3, state.BlockPool[AttackElement.Physical]);
    }

    [Fact]
    public async Task Execute_Influence_AddsInfluencePoints() {
        var state = EmptyState();
        await new ImprovisationEffect(EffectType.Influence, 3).Execute(state, Ctx(EffectType.Influence));
        Assert.Equal(3, state.InfluencePointsThisTurn);
    }
}
