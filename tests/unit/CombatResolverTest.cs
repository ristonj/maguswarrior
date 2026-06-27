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
    public async Task ResolveStartOfCombat_LeavesPhaseAtCombatStart() {
        var state = EmptyState();
        var resolver = MakeResolver(state);
        var combat = new CombatState { Group = SingleEnemyGroup() };

        await resolver.ResolveStartOfCombat(combat);

        // Phase 0 (setup) sets CombatStart only; CombatRanged is set by ResolveRangedPhase.
        Assert.Equal(GamePhase.CombatStart, combat.CurrentPhase);
    }

    [Fact]
    public async Task ResolveStartOfCombat_ClearsActiveEnemiesBeforeAddRange() {
        var state = EmptyState();
        var resolver = MakeResolver(state);
        var combat = new CombatState { Group = SingleEnemyGroup() };
        combat.ActiveEnemies.Add(TestEnemy()); // pre-populated from a hypothetical prior call

        await resolver.ResolveStartOfCombat(combat);

        // Should have exactly 1 enemy (from Group), not 2 (pre-population + re-add).
        Assert.Single(combat.ActiveEnemies);
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

    // --- Task 5: ResolveRangedPhase ---

    private static RangedAttackDeclaration Decl(
        IReadOnlyList<EnemyTokenInstance> targets,
        params AttackContribution[] contribs) =>
        new(new List<AttackContribution>(contribs), targets);

    private static IReadOnlyList<EnemyTokenInstance> Targets(params EnemyTokenInstance[] enemies) =>
        new List<EnemyTokenInstance>(enemies);

    [Fact]
    public async Task ResolveRangedPhase_ExactArmorKill_DefeatsSingleEnemy() {
        var state  = EmptyState();
        var enemy  = TestEnemyWithArmor(4);
        var combat = new CombatState { Group = MultiEnemyGroup(enemy) };
        combat.ActiveEnemies.Add(enemy);
        var decl   = Decl(Targets(enemy),
            new AttackContribution(AttackType.Physical, AttackDelivery.Ranged, 4));
        var resolver = MakeResolverWith(state, new TestBroker(new List<RangedAttackDeclaration> { decl }));

        await resolver.ResolveRangedPhase(combat);

        Assert.Empty(combat.ActiveEnemies);
        Assert.Single(combat.DefeatedEnemies);
    }

    [Fact]
    public async Task ResolveRangedPhase_OverArmor_DefeatsSingleEnemy() {
        var state  = EmptyState();
        var enemy  = TestEnemyWithArmor(3);
        var combat = new CombatState { Group = MultiEnemyGroup(enemy) };
        combat.ActiveEnemies.Add(enemy);
        var decl   = Decl(Targets(enemy),
            new AttackContribution(AttackType.Physical, AttackDelivery.Siege, 6));
        var resolver = MakeResolverWith(state, new TestBroker(new List<RangedAttackDeclaration> { decl }));

        await resolver.ResolveRangedPhase(combat);

        Assert.Empty(combat.ActiveEnemies);
    }

    [Fact]
    public async Task ResolveRangedPhase_UnderArmor_EnemySurvives() {
        var state  = EmptyState();
        var enemy  = TestEnemyWithArmor(5);
        var combat = new CombatState { Group = MultiEnemyGroup(enemy) };
        combat.ActiveEnemies.Add(enemy);
        var decl   = Decl(Targets(enemy),
            new AttackContribution(AttackType.Physical, AttackDelivery.Ranged, 4));
        var resolver = MakeResolverWith(state, new TestBroker(new List<RangedAttackDeclaration> { decl }));

        await resolver.ResolveRangedPhase(combat);

        Assert.Single(combat.ActiveEnemies);
        Assert.Empty(combat.DefeatedEnemies);
    }

    [Fact]
    public async Task ResolveRangedPhase_EmptyDeclarations_NoEnemiesDefeated() {
        var state  = EmptyState();
        var enemy  = TestEnemyWithArmor(3);
        var combat = new CombatState { Group = MultiEnemyGroup(enemy) };
        combat.ActiveEnemies.Add(enemy);
        var resolver = MakeResolverWith(state, new TestBroker(new List<RangedAttackDeclaration>()));

        await resolver.ResolveRangedPhase(combat);

        Assert.Single(combat.ActiveEnemies);
    }

    [Fact]
    public async Task ResolveRangedPhase_CombinedTarget_TotalMeetsSum_DefeatsAll() {
        var state  = EmptyState();
        var e1     = TestEnemyWithArmor(3);
        var e2     = TestEnemyWithArmor(3);
        var combat = new CombatState { Group = MultiEnemyGroup(e1, e2) };
        combat.ActiveEnemies.Add(e1);
        combat.ActiveEnemies.Add(e2);
        var decl   = Decl(Targets(e1, e2),
            new AttackContribution(AttackType.Physical, AttackDelivery.Siege, 6));
        var resolver = MakeResolverWith(state, new TestBroker(new List<RangedAttackDeclaration> { decl }));

        await resolver.ResolveRangedPhase(combat);

        Assert.Empty(combat.ActiveEnemies);
        Assert.Equal(2, combat.DefeatedEnemies.Count);
    }

    [Fact]
    public async Task ResolveRangedPhase_CombinedTarget_BelowSum_DefeatsNone() {
        var state  = EmptyState();
        var e1     = TestEnemyWithArmor(3);
        var e2     = TestEnemyWithArmor(3);
        var combat = new CombatState { Group = MultiEnemyGroup(e1, e2) };
        combat.ActiveEnemies.Add(e1);
        combat.ActiveEnemies.Add(e2);
        var decl   = Decl(Targets(e1, e2),
            new AttackContribution(AttackType.Physical, AttackDelivery.Siege, 5));
        var resolver = MakeResolverWith(state, new TestBroker(new List<RangedAttackDeclaration> { decl }));

        await resolver.ResolveRangedPhase(combat);

        Assert.Equal(2, combat.ActiveEnemies.Count);
        Assert.Empty(combat.DefeatedEnemies);
    }

    [Fact]
    public async Task ResolveRangedPhase_UnionResistance_HalvesResistantType() {
        // Physical 6 vs two enemies armor 3+3=6, but one resists Physical → 3 < 6 → no defeat
        var state  = EmptyState();
        var e1     = TestEnemyWithArmor(3, physicalResistance: true);
        var e2     = TestEnemyWithArmor(3);
        var combat = new CombatState { Group = MultiEnemyGroup(e1, e2) };
        combat.ActiveEnemies.Add(e1);
        combat.ActiveEnemies.Add(e2);
        var decl   = Decl(Targets(e1, e2),
            new AttackContribution(AttackType.Physical, AttackDelivery.Ranged, 6));
        var resolver = MakeResolverWith(state, new TestBroker(new List<RangedAttackDeclaration> { decl }));

        await resolver.ResolveRangedPhase(combat);

        Assert.Equal(2, combat.ActiveEnemies.Count);
    }

    [Fact]
    public async Task ResolveRangedPhase_MixedTypeUnionResistance_OnlyResistantTypeHalved() {
        // Physical 4 + Fire 4 vs enemy resisting Physical only → floor(4/2)+4=6 vs armor 5 → defeats
        var state  = EmptyState();
        var enemy  = TestEnemyWithArmor(5, physicalResistance: true);
        var combat = new CombatState { Group = MultiEnemyGroup(enemy) };
        combat.ActiveEnemies.Add(enemy);
        var decl   = Decl(Targets(enemy),
            new AttackContribution(AttackType.Physical, AttackDelivery.Ranged, 4),
            new AttackContribution(AttackType.Fire, AttackDelivery.Ranged, 4));
        var resolver = MakeResolverWith(state, new TestBroker(new List<RangedAttackDeclaration> { decl }));

        await resolver.ResolveRangedPhase(combat);

        Assert.Empty(combat.ActiveEnemies);
    }

    [Fact]
    public async Task ResolveRangedPhase_FortifiedSite_BlocksRangedDelivery() {
        // Ranged delivery is blocked at fortification level 1 (fortified site) → no damage
        var state  = EmptyState();
        var enemy  = TestEnemyWithArmor(3);
        var combat = new CombatState { Group = FortifiedSiteGroup(enemy) };
        combat.ActiveEnemies.Add(enemy);
        var decl   = Decl(Targets(enemy),
            new AttackContribution(AttackType.Physical, AttackDelivery.Ranged, 10));
        var resolver = MakeResolverWith(state, new TestBroker(new List<RangedAttackDeclaration> { decl }));

        await resolver.ResolveRangedPhase(combat);

        Assert.Single(combat.ActiveEnemies);
    }

    [Fact]
    public async Task ResolveRangedPhase_FortifiedSite_SiegeDeliveryStillHits() {
        // Siege delivery allowed at fortification level 1
        var state  = EmptyState();
        var enemy  = TestEnemyWithArmor(3);
        var combat = new CombatState { Group = FortifiedSiteGroup(enemy) };
        combat.ActiveEnemies.Add(enemy);
        var decl   = Decl(Targets(enemy),
            new AttackContribution(AttackType.Physical, AttackDelivery.Siege, 5));
        var resolver = MakeResolverWith(state, new TestBroker(new List<RangedAttackDeclaration> { decl }));

        await resolver.ResolveRangedPhase(combat);

        Assert.Empty(combat.ActiveEnemies);
    }

    [Fact]
    public async Task ResolveRangedPhase_FortifiedSiteAndFortifiedEnemy_BlocksSiege() {
        // Fortification level 2 (site + Fortified enemy): both Ranged and Siege blocked
        var state  = EmptyState();
        var enemy  = new EnemyTokenInstance(new EnemyTokenDefinition(
            "f", "Fort", TokenColor.Brown, Armor: 3,
            Attacks: new List<EnemyAttack>(), FameValue: 0,
            Abilities: new List<EnemyAbility> { EnemyAbility.Fortified },
            IsRampaging: false, Summon: null));
        var combat = new CombatState { Group = FortifiedSiteGroup(enemy) };
        combat.ActiveEnemies.Add(enemy);
        var decl   = Decl(Targets(enemy),
            new AttackContribution(AttackType.Physical, AttackDelivery.Siege, 10));
        var resolver = MakeResolverWith(state, new TestBroker(new List<RangedAttackDeclaration> { decl }));

        await resolver.ResolveRangedPhase(combat);

        Assert.Single(combat.ActiveEnemies);
    }

    [Fact]
    public async Task ResolveRangedPhase_SetsPhaseToRanged() {
        var state  = EmptyState();
        var combat = new CombatState { Group = new CombatGroup() };
        var resolver = MakeResolverWith(state, new TestBroker(new List<RangedAttackDeclaration>()));

        await resolver.ResolveRangedPhase(combat);

        Assert.Equal(GamePhase.CombatRanged, state.CurrentPhase);
    }

    [Fact]
    public async Task ResolveRangedPhase_ContributionsAppendedToAttackPool() {
        var state  = EmptyState();
        var enemy  = TestEnemyWithArmor(10);
        var combat = new CombatState { Group = MultiEnemyGroup(enemy) };
        combat.ActiveEnemies.Add(enemy);
        var contrib = new AttackContribution(AttackType.Physical, AttackDelivery.Ranged, 3);
        var decl    = Decl(Targets(enemy), contrib);
        var resolver = MakeResolverWith(state, new TestBroker(new List<RangedAttackDeclaration> { decl }));

        await resolver.ResolveRangedPhase(combat);

        Assert.Contains(contrib, combat.AttackPool);
    }

    [Fact]
    public async Task ResolveRangedPhase_EmptyTargets_SkipsDeclarationWithoutThrowing() {
        // A declaration with no targets must not crash the phase (Targets.Max on an
        // empty list throws). The resolver skips it and leaves enemies untouched.
        var state  = EmptyState();
        var enemy  = TestEnemyWithArmor(3);
        var combat = new CombatState { Group = MultiEnemyGroup(enemy) };
        combat.ActiveEnemies.Add(enemy);
        var decl   = Decl(Targets(),
            new AttackContribution(AttackType.Physical, AttackDelivery.Siege, 10));
        var resolver = MakeResolverWith(state, new TestBroker(new List<RangedAttackDeclaration> { decl }));

        await resolver.ResolveRangedPhase(combat);

        Assert.Single(combat.ActiveEnemies);
        Assert.Empty(combat.DefeatedEnemies);
    }

    [Fact]
    public async Task ResolveRangedPhase_SameEnemyDefeatedTwice_NoPhantomDefeatEntry() {
        // Two declarations each strong enough to defeat the same enemy: the second
        // declaration's target is already gone, so it must not be added to
        // DefeatedEnemies a second time (which would inflate downstream Fame).
        var state  = EmptyState();
        var enemy  = TestEnemyWithArmor(3);
        var combat = new CombatState { Group = MultiEnemyGroup(enemy) };
        combat.ActiveEnemies.Add(enemy);
        var decl1  = Decl(Targets(enemy),
            new AttackContribution(AttackType.Physical, AttackDelivery.Siege, 6));
        var decl2  = Decl(Targets(enemy),
            new AttackContribution(AttackType.Physical, AttackDelivery.Siege, 6));
        var resolver = MakeResolverWith(state,
            new TestBroker(new List<RangedAttackDeclaration> { decl1, decl2 }));

        await resolver.ResolveRangedPhase(combat);

        Assert.Empty(combat.ActiveEnemies);
        Assert.Single(combat.DefeatedEnemies);
    }

    // --- Task 4: FortificationLevel ---

    [Fact]
    public void FortificationLevel_UnfortifiedSitePlainEnemy_IsZero() {
        var state    = EmptyState();
        var resolver = MakeResolver(state);
        var enemy    = TestEnemyWithArmor(3);
        var combat   = new CombatState { Group = new CombatGroup() };

        Assert.Equal(0, resolver.FortificationLevel(enemy, combat));
    }

    [Fact]
    public void FortificationLevel_FortifiedSitePlainEnemy_IsOne() {
        var state    = EmptyState();
        var resolver = MakeResolver(state);
        var enemy    = TestEnemyWithArmor(3);
        var combat   = new CombatState { Group = new CombatGroup { IsAtFortifiedSite = true } };

        Assert.Equal(1, resolver.FortificationLevel(enemy, combat));
    }

    [Fact]
    public void FortificationLevel_UnfortifiedSiteFortifiedEnemy_IsOne() {
        var state    = EmptyState();
        var resolver = MakeResolver(state);
        var enemy    = new EnemyTokenInstance(new EnemyTokenDefinition(
            "f", "Fort", TokenColor.Brown, Armor: 3,
            Attacks: new List<EnemyAttack>(), FameValue: 0,
            Abilities: new List<EnemyAbility> { EnemyAbility.Fortified },
            IsRampaging: false, Summon: null));
        var combat   = new CombatState { Group = new CombatGroup() };

        Assert.Equal(1, resolver.FortificationLevel(enemy, combat));
    }

    [Fact]
    public void FortificationLevel_FortifiedSiteAndFortifiedEnemy_IsTwo() {
        var state    = EmptyState();
        var resolver = MakeResolver(state);
        var enemy    = new EnemyTokenInstance(new EnemyTokenDefinition(
            "f", "Fort", TokenColor.Brown, Armor: 3,
            Attacks: new List<EnemyAttack>(), FameValue: 0,
            Abilities: new List<EnemyAbility> { EnemyAbility.Fortified },
            IsRampaging: false, Summon: null));
        var combat   = new CombatState { Group = new CombatGroup { IsAtFortifiedSite = true } };

        Assert.Equal(2, resolver.FortificationLevel(enemy, combat));
    }

    // --- Task 3: ComputeEffectiveAttack ---

    [Fact]
    public void ComputeEffectiveAttack_EmptyModifiers_ReturnsInputValue() {
        var state   = EmptyState();
        var resolver = MakeResolver(state);
        var combat  = new CombatState { Group = new CombatGroup() };
        var contrib = new AttackContribution(AttackType.Physical, AttackDelivery.Ranged, 5);

        int result = resolver.ComputeEffectiveAttack(contrib, combat);

        Assert.Equal(5, result);
    }

    [Fact]
    public void ComputeEffectiveAttack_WithDoubler_DoublesPhysicalContrib() {
        var state   = EmptyState();
        var resolver = MakeResolver(state);
        var combat  = new CombatState { Group = new CombatGroup() };
        combat.AttackModifiers.Add(new PhysicalDoublerModifier());
        var contrib = new AttackContribution(AttackType.Physical, AttackDelivery.Ranged, 4);

        int result = resolver.ComputeEffectiveAttack(contrib, combat);

        Assert.Equal(8, result);
    }

    private sealed class PhysicalDoublerModifier : ICombatAttackModifier {
        public AttackContribution Modify(AttackContribution contrib) =>
            contrib.Type == AttackType.Physical
                ? contrib with { Value = contrib.Value * 2 }
                : contrib;
    }

    // --- helpers ---

    private static EnemyTokenInstance TestEnemyWithArmor(int armor, bool physicalResistance = false) {
        var abilities = physicalResistance
            ? new List<EnemyAbility> { EnemyAbility.PhysicalResistance }
            : new List<EnemyAbility>();
        return new EnemyTokenInstance(new EnemyTokenDefinition(
            "t", "T", TokenColor.Brown,
            Armor: armor,
            Attacks: new List<EnemyAttack>(),
            FameValue: 0,
            Abilities: abilities,
            IsRampaging: false,
            Summon: null));
    }

    private static CombatGroup MultiEnemyGroup(params EnemyTokenInstance[] enemies) =>
        new() { Enemies = new List<EnemyTokenInstance>(enemies) };

    private static CombatGroup FortifiedSiteGroup(params EnemyTokenInstance[] enemies) =>
        new() { Enemies = new List<EnemyTokenInstance>(enemies), IsAtFortifiedSite = true };

    // --- inner types ---

    private sealed class TestCallback : ICombatPhaseCallback {
        private readonly Action _action;
        public GamePhase TriggerPhase { get; }
        public TestCallback(GamePhase phase, Action action) { TriggerPhase = phase; _action = action; }
        public Task Execute(CombatState combat, UIBroker broker) { _action(); return Task.CompletedTask; }
    }

    // Scriptable test double for UIBroker — override PromptHeroRangedAttacks to
    // supply declarations without touching any live UI.
    private sealed class TestBroker : UIBroker {
        private readonly IReadOnlyList<RangedAttackDeclaration> _declarations;
        public TestBroker(IReadOnlyList<RangedAttackDeclaration> declarations) {
            _declarations = declarations;
        }
        public override Task<IReadOnlyList<RangedAttackDeclaration>> PromptHeroRangedAttacks(CombatState combat) =>
            Task.FromResult(_declarations);
    }

    private static CombatResolver MakeResolverWith(GameState state, UIBroker broker) =>
        new(state, broker, new EffectScheduler(), new EffectHookRegistry());
}
