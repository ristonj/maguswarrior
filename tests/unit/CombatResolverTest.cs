using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MagusWarrior.Broker;
using MagusWarrior.Cards;
using MagusWarrior.Cards.Effects;
using MagusWarrior.Combat;
using MagusWarrior.Core;
using MagusWarrior.Core.Types;
using Xunit;

namespace MagusWarrior.Tests;

public class CombatResolverTest {
    private static GameState EmptyState() => new(new List<CardDefinition>());
    private static UIBroker  StubBroker() => new();

    private static CombatResolver MakeResolver(GameState state) =>
        new(state, StubBroker(), new EffectScheduler(), new EffectHookRegistry());

    private static EnemyTokenInstance TestEnemy() =>
        new(new EnemyTokenDefinition("test", "Test Enemy", TokenColor.Brown,
            Armor: 4,
            Attacks: new List<EnemyAttack> { new(2, AttackType.Physical) },
            FameValue: 1,
            Abilities: new List<EnemyAbility>(),
            IsRampaging: false,
            Summon: null));

    private static CombatGroup SingleEnemyGroup() =>
        new() { Enemies = new List<EnemyTokenInstance> { TestEnemy() } };

    [Fact]
    public void AllEnemiesDefeated_TrueWhenActiveEnemiesEmpty() {
        var combat = new CombatState { Group = SingleEnemyGroup() };
        Assert.True(combat.AllEnemiesDefeated);
    }

    [Fact]
    public void AllEnemiesDefeated_FalseWhenEnemiesPresent() {
        var combat = new CombatState { Group = SingleEnemyGroup() };
        combat.ActiveEnemies.Add(TestEnemy());
        Assert.False(combat.AllEnemiesDefeated);
    }

    [Fact]
    public async Task ResolveStartOfCombat_SetsPhaseToStart() {
        // ResolveStartOfCombat ends in CombatRanged (Phase 0 hands off to Phase 1),
        // so we verify it PASSED THROUGH CombatStart via a CombatStart phase callback.
        var state = EmptyState();
        var resolver = MakeResolver(state);
        var combat = new CombatState { Group = SingleEnemyGroup() };
        bool startEntered = false;
        combat.PhaseCallbacks.Add(new TestCallback(GamePhase.CombatStart, () => startEntered = true));

        await resolver.ResolveStartOfCombat(combat);

        Assert.True(startEntered);
    }

    [Fact]
    public async Task ResolveStartOfCombat_TransitionsToCombatRangedAfterPhase0() {
        var state = EmptyState();
        var resolver = MakeResolver(state);
        var combat = new CombatState { Group = SingleEnemyGroup() };

        await resolver.ResolveStartOfCombat(combat);

        // Phase 0 is setup-only; it hands off to Phase 1 (Ranged Attack).
        // No ranged resolution happens here — that is story 3-2.
        Assert.Equal(GamePhase.CombatRanged, combat.CurrentPhase);
    }

    [Fact]
    public async Task ResolveStartOfCombat_PopulatesActiveEnemiesFromGroup() {
        var state = EmptyState();
        var resolver = MakeResolver(state);
        var combat = new CombatState { Group = SingleEnemyGroup() };

        await resolver.ResolveStartOfCombat(combat);

        Assert.Single(combat.ActiveEnemies);
    }

    [Fact]
    public void SetPhase_UpdatesGameStateCurrentPhase() {
        var state = EmptyState();
        var resolver = MakeResolver(state);
        var combat = new CombatState { Group = new CombatGroup() };

        resolver.SetPhase(GamePhase.CombatRanged, combat);

        Assert.Equal(GamePhase.CombatRanged, state.CurrentPhase);
    }

    [Fact]
    public void SetPhase_UpdatesCombatStateCurrentPhase() {
        var state = EmptyState();
        var resolver = MakeResolver(state);
        var combat = new CombatState { Group = new CombatGroup() };

        resolver.SetPhase(GamePhase.CombatBlock, combat);

        Assert.Equal(GamePhase.CombatBlock, combat.CurrentPhase);
    }

    [Fact]
    public async Task FirePhaseCallbacks_ExecutesAndRemovesMatchingCallback() {
        var state = EmptyState();
        var resolver = MakeResolver(state);
        var combat = new CombatState { Group = new CombatGroup() };
        bool executed = false;
        combat.PhaseCallbacks.Add(new TestCallback(GamePhase.CombatMelee, () => executed = true));

        await resolver.FirePhaseCallbacks(GamePhase.CombatMelee, combat);

        Assert.True(executed);
        Assert.Empty(combat.PhaseCallbacks);
    }

    [Fact]
    public async Task FirePhaseCallbacks_IgnoresNonMatchingCallbacks() {
        var state = EmptyState();
        var resolver = MakeResolver(state);
        var combat = new CombatState { Group = new CombatGroup() };
        bool executed = false;
        combat.PhaseCallbacks.Add(new TestCallback(GamePhase.CombatBlock, () => executed = true));

        await resolver.FirePhaseCallbacks(GamePhase.CombatMelee, combat);

        Assert.False(executed);
        Assert.Single(combat.PhaseCallbacks);
    }

    private sealed class TestCallback : ICombatPhaseCallback {
        private readonly Action _action;
        public GamePhase TriggerPhase { get; }
        public TestCallback(GamePhase phase, Action action) { TriggerPhase = phase; _action = action; }
        public Task Execute(CombatState combat, UIBroker broker) { _action(); return Task.CompletedTask; }
    }
}
