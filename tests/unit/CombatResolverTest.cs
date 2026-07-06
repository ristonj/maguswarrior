using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MagusWarrior.Broker;
using MagusWarrior.Cards;
using MagusWarrior.Cards.Effects;
using MagusWarrior.Combat;
using MagusWarrior.Core;
using MagusWarrior.Core.Types;
using MagusWarrior.Units;
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

    // --- Story 3-3: ResolveBlockPhase ---

    private static EnemyTokenInstance BlockEnemy(int attackValue, AttackType type,
            params EnemyAbility[] abilities) =>
        new(new EnemyTokenDefinition("be", "Block Enemy", TokenColor.Brown,
            Armor: 4,
            Attacks: new List<EnemyAttack> { new(attackValue, type) },
            FameValue: 0,
            Abilities: new List<EnemyAbility>(abilities),
            IsRampaging: false, Summon: null));

    // Default overload: attack index 0 (all existing single-attack tests use this).
    private static BlockDeclaration BlockDecl(EnemyTokenInstance target,
            params BlockContribution[] contribs) =>
        new(new List<BlockContribution>(contribs), target, 0);

    // Indexed overload for per-attack multi-attack tests.
    private static BlockDeclaration BlockDecl(EnemyTokenInstance target, int attackIndex,
            params BlockContribution[] contribs) =>
        new(new List<BlockContribution>(contribs), target, attackIndex);

    private static CombatResolver MakeResolverWithBlock(GameState state,
            params BlockDeclaration[] blocks) =>
        new(state, new TestBroker(new List<RangedAttackDeclaration>(),
            new List<BlockDeclaration>(blocks)),
            new EffectScheduler(), new EffectHookRegistry());

    [Fact]
    public async Task BlockPhase_FullyBlockedAttack_NoDamageAssignment() {
        var state  = EmptyState();
        var enemy  = BlockEnemy(4, AttackType.Physical);
        var combat = new CombatState { Group = MultiEnemyGroup(enemy) };
        combat.ActiveEnemies.Add(enemy);
        var resolver = MakeResolverWithBlock(state,
            BlockDecl(enemy, new BlockContribution(BlockType.Physical, 4)));

        await resolver.ResolveBlockPhase(combat);

        Assert.Empty(combat.DamageAssignments);
    }

    [Fact]
    public async Task BlockPhase_PartialBlock_FullAttackGetsThrough() {
        var state  = EmptyState();
        var enemy  = BlockEnemy(4, AttackType.Physical);
        var combat = new CombatState { Group = MultiEnemyGroup(enemy) };
        combat.ActiveEnemies.Add(enemy);
        var resolver = MakeResolverWithBlock(state,
            BlockDecl(enemy, new BlockContribution(BlockType.Physical, 3)));

        await resolver.ResolveBlockPhase(combat);

        var da = Assert.Single(combat.DamageAssignments);
        Assert.Equal(enemy,               da.Source);
        Assert.Equal(AttackType.Physical, da.DamageType);
        Assert.Equal(4,                   da.RawValue); // all-or-nothing: full value, not 1
    }

    [Fact]
    public async Task BlockPhase_NoBlock_DamageAssignmentRawValue() {
        var state  = EmptyState();
        var enemy  = BlockEnemy(5, AttackType.Fire);
        var combat = new CombatState { Group = MultiEnemyGroup(enemy) };
        combat.ActiveEnemies.Add(enemy);
        var resolver = MakeResolverWithBlock(state); // no declarations

        await resolver.ResolveBlockPhase(combat);

        var da = Assert.Single(combat.DamageAssignments);
        Assert.Equal(5, da.RawValue);
    }

    [Fact]
    public async Task BlockPhase_InefficientBlock_NeedsDouble() {
        // Fire attack 4; Physical block is 2:1 vs Fire.
        var state  = EmptyState();
        var enemy  = BlockEnemy(4, AttackType.Fire);
        var combat = new CombatState { Group = MultiEnemyGroup(enemy) };
        combat.ActiveEnemies.Add(enemy);

        // Physical block 6 -> effective 3 < 4 -> unblocked.
        var under = MakeResolverWithBlock(state,
            BlockDecl(enemy, new BlockContribution(BlockType.Physical, 6)));
        await under.ResolveBlockPhase(combat);
        Assert.Single(combat.DamageAssignments);

        // Physical block 8 -> effective 4 >= 4 -> blocked.
        var combat2 = new CombatState { Group = MultiEnemyGroup(enemy) };
        combat2.ActiveEnemies.Add(enemy);
        var over = MakeResolverWithBlock(state,
            BlockDecl(enemy, new BlockContribution(BlockType.Physical, 8)));
        await over.ResolveBlockPhase(combat2);
        Assert.Empty(combat2.DamageAssignments);
    }

    [Fact]
    public async Task BlockPhase_EfficientElementalBlock() {
        // Fire attack 4; Ice block is 1:1 vs Fire (elements oppose).
        var state  = EmptyState();
        var enemy  = BlockEnemy(4, AttackType.Fire);
        var combat = new CombatState { Group = MultiEnemyGroup(enemy) };
        combat.ActiveEnemies.Add(enemy);
        var resolver = MakeResolverWithBlock(state,
            BlockDecl(enemy, new BlockContribution(BlockType.Ice, 4)));

        await resolver.ResolveBlockPhase(combat);

        Assert.Empty(combat.DamageAssignments);
    }

    [Fact]
    public async Task BlockPhase_SwiftDoublesThreshold() {
        // Swift Physical attack 4 -> threshold 8.
        var state  = EmptyState();
        var enemy  = BlockEnemy(4, AttackType.Physical, EnemyAbility.Swift);
        var combat = new CombatState { Group = MultiEnemyGroup(enemy) };
        combat.ActiveEnemies.Add(enemy);

        // Physical block 4 -> effective 4 < 8 -> unblocked; RawValue is printed 4, not 8.
        var under = MakeResolverWithBlock(state,
            BlockDecl(enemy, new BlockContribution(BlockType.Physical, 4)));
        await under.ResolveBlockPhase(combat);
        var da = Assert.Single(combat.DamageAssignments);
        Assert.Equal(4, da.RawValue);

        // Physical block 8 -> effective 8 >= 8 -> blocked.
        var combat2 = new CombatState { Group = MultiEnemyGroup(enemy) };
        combat2.ActiveEnemies.Add(enemy);
        var over = MakeResolverWithBlock(state,
            BlockDecl(enemy, new BlockContribution(BlockType.Physical, 8)));
        await over.ResolveBlockPhase(combat2);
        Assert.Empty(combat2.DamageAssignments);
    }

    [Fact]
    public async Task BlockPhase_MixedBlockTypes_Sum() {
        // Fire attack 4. effect-lld example: 1 Ice (eff 1) + 2 Physical (2:1 -> eff 1) = 2 < 4.
        var state  = EmptyState();
        var enemy  = BlockEnemy(4, AttackType.Fire);
        var combat = new CombatState { Group = MultiEnemyGroup(enemy) };
        combat.ActiveEnemies.Add(enemy);
        var under = MakeResolverWithBlock(state,
            BlockDecl(enemy, new BlockContribution(BlockType.Ice, 1),
                             new BlockContribution(BlockType.Physical, 2)));
        await under.ResolveBlockPhase(combat);
        Assert.Single(combat.DamageAssignments);

        // Ice 2 (eff 2) + Physical 4 (2:1 -> eff 2) = 4 >= 4 -> blocked.
        var combat2 = new CombatState { Group = MultiEnemyGroup(enemy) };
        combat2.ActiveEnemies.Add(enemy);
        var over = MakeResolverWithBlock(state,
            BlockDecl(enemy, new BlockContribution(BlockType.Ice, 2),
                             new BlockContribution(BlockType.Physical, 4)));
        await over.ResolveBlockPhase(combat2);
        Assert.Empty(combat2.DamageAssignments);
    }

    [Fact]
    public async Task BlockPhase_BlockForOneEnemy_DoesNotCoverAnother() {
        var state  = EmptyState();
        var eA     = BlockEnemy(4, AttackType.Physical);
        var eB     = BlockEnemy(4, AttackType.Physical);
        var combat = new CombatState { Group = MultiEnemyGroup(eA, eB) };
        combat.ActiveEnemies.Add(eA);
        combat.ActiveEnemies.Add(eB);
        var resolver = MakeResolverWithBlock(state,
            BlockDecl(eA, new BlockContribution(BlockType.Physical, 4)));

        await resolver.ResolveBlockPhase(combat);

        var da = Assert.Single(combat.DamageAssignments);
        Assert.Equal(eB, da.Source); // A blocked; B got through
    }

    [Fact]
    public async Task BlockPhase_AttackCancelledEnemy_NoAssignment() {
        var state  = EmptyState();
        var enemy  = BlockEnemy(4, AttackType.Physical);
        enemy.AttackCancelled = true;
        var combat = new CombatState { Group = MultiEnemyGroup(enemy) };
        combat.ActiveEnemies.Add(enemy);
        var resolver = MakeResolverWithBlock(state); // no block needed

        await resolver.ResolveBlockPhase(combat);

        Assert.Empty(combat.DamageAssignments);
    }

    [Fact]
    public async Task BlockPhase_SummonOnlyAttack_NoAssignment() {
        var state  = EmptyState();
        var enemy  = BlockEnemy(0, AttackType.None);
        var combat = new CombatState { Group = MultiEnemyGroup(enemy) };
        combat.ActiveEnemies.Add(enemy);
        var resolver = MakeResolverWithBlock(state);

        await resolver.ResolveBlockPhase(combat);

        Assert.Empty(combat.DamageAssignments);
    }

    [Fact]
    public async Task BlockPhase_SetsPhaseToBlock() {
        var state  = EmptyState();
        var enemy  = BlockEnemy(4, AttackType.Physical);
        var combat = new CombatState { Group = MultiEnemyGroup(enemy) };
        combat.ActiveEnemies.Add(enemy);
        var resolver = MakeResolverWithBlock(state);

        await resolver.ResolveBlockPhase(combat);

        Assert.Equal(GamePhase.CombatBlock, combat.CurrentPhase);
    }

    [Fact]
    public async Task ResolveCombat_RunsBlockPhase_WhenEnemiesSurvive() {
        var state  = EmptyState();
        var enemy  = BlockEnemy(4, AttackType.Physical);
        var combat = new CombatState { Group = MultiEnemyGroup(enemy) };
        var broker = new TestBroker(new List<RangedAttackDeclaration>()); // ranged defeats nothing
        var resolver = new CombatResolver(state, broker, new EffectScheduler(), new EffectHookRegistry());

        await resolver.ResolveCombat(combat);

        Assert.True(broker.BlockPrompted);
    }

    [Fact]
    public async Task ResolveCombat_SkipsBlockPhase_WhenSkipBlockAndDamagePending() {
        var state  = EmptyState();
        var enemy  = BlockEnemy(4, AttackType.Physical);
        var combat = new CombatState { Group = MultiEnemyGroup(enemy), SkipBlockAndDamagePending = true };
        var broker = new TestBroker(new List<RangedAttackDeclaration>());
        var resolver = new CombatResolver(state, broker, new EffectScheduler(), new EffectHookRegistry());

        await resolver.ResolveCombat(combat);

        Assert.False(broker.BlockPrompted);
    }

    [Fact]
    public async Task ResolveCombat_SkipsBlockPhase_WhenAllEnemiesDefeated() {
        var state  = EmptyState();
        var enemy  = BlockEnemy(4, AttackType.Physical); // Armor 4
        var combat = new CombatState { Group = MultiEnemyGroup(enemy) };
        var rangedDecl = new RangedAttackDeclaration(
            new List<AttackContribution> {
                new(AttackType.Physical, AttackDelivery.Ranged, 4) },
            new List<EnemyTokenInstance> { enemy });
        var broker = new TestBroker(new List<RangedAttackDeclaration> { rangedDecl });
        var resolver = new CombatResolver(state, broker, new EffectScheduler(), new EffectHookRegistry());

        await resolver.ResolveCombat(combat);

        Assert.False(broker.BlockPrompted);
    }

    // --- Story 3-3b: per-attack block allocation (multi-attack enemies) ---

    private static EnemyTokenInstance TwoAttackEnemy(AttackType type, int value) =>
        new(new EnemyTokenDefinition("multi", "Multi Enemy", TokenColor.Brown,
            Armor: 4,
            Attacks: new List<EnemyAttack> { new(value, type), new(value, type) },
            FameValue: 0,
            Abilities: new List<EnemyAbility>(),
            IsRampaging: false, Summon: null));

    [Fact]
    public async Task BlockPhase_MultiAttack_BlockConsumedPerAttack() {
        // Synthetic two-attack enemy: Physical 4 + Physical 4.
        // Declare Physical block 4 against attack index 0 ONLY.
        // Under the 3-3 resolver, allocated was per-enemy (ignoring AttackIndex), so block
        // covered BOTH attacks and this test would pass with zero DamageAssignments — wrong.
        // The per-attack resolver must emit exactly one DamageAssignment for the unblocked attack 1.
        var state  = EmptyState();
        var enemy  = TwoAttackEnemy(AttackType.Physical, 4);
        var combat = new CombatState { Group = MultiEnemyGroup(enemy) };
        combat.ActiveEnemies.Add(enemy);
        var resolver = MakeResolverWithBlock(state,
            BlockDecl(enemy, 0, new BlockContribution(BlockType.Physical, 4)));

        await resolver.ResolveBlockPhase(combat);

        // Attack 0: blocked — no damage.
        // Attack 1: not blocked — damage gets through.
        var da = Assert.Single(combat.DamageAssignments);
        Assert.Equal(enemy,               da.Source);
        Assert.Equal(AttackType.Physical, da.DamageType);
        Assert.Equal(4,                   da.RawValue);
    }

    [Fact]
    public async Task BlockPhase_MultiAttack_SeparateDeclarationsBlockBoth() {
        // Same two-attack enemy; declare Physical block 4 vs index 0 AND Physical block 4 vs index 1.
        // Both attacks individually satisfied → no DamageAssignments.
        var state  = EmptyState();
        var enemy  = TwoAttackEnemy(AttackType.Physical, 4);
        var combat = new CombatState { Group = MultiEnemyGroup(enemy) };
        combat.ActiveEnemies.Add(enemy);
        var resolver = MakeResolverWithBlock(state,
            BlockDecl(enemy, 0, new BlockContribution(BlockType.Physical, 4)),
            BlockDecl(enemy, 1, new BlockContribution(BlockType.Physical, 4)));

        await resolver.ResolveBlockPhase(combat);

        Assert.Empty(combat.DamageAssignments);
    }

    // --- Story 3-4: ResolveAssignDamagePhase ---

    private static UnitInstance TestUnit(int armor, params AttackType[] resistances) =>
        new(armor, resistances);

    private static CombatResolver MakeResolverWithDamageChoices(GameState state,
            params UnitInstance?[] damageChoices) =>
        new(state, new TestBroker(new List<RangedAttackDeclaration>(), null, damageChoices),
            new EffectScheduler(), new EffectHookRegistry());

    [Fact]
    public async Task AssignDamage_NoUnits_HeroDrawsCeilWounds() {
        var state  = EmptyState();
        var combat = new CombatState { Group = new CombatGroup() };
        var enemy  = BlockEnemy(0, AttackType.Physical);
        combat.DamageAssignments.Add(new DamageAssignment(enemy, AttackType.Physical, 5));
        var resolver = MakeResolverWithDamageChoices(state);

        await resolver.ResolveAssignDamagePhase(combat);

        Assert.Equal(3, state.Hero.Hand.Count); // ceil(5/2)
    }

    [Fact]
    public async Task AssignDamage_Brutal_DoublesBeforeHero() {
        var state  = EmptyState();
        var combat = new CombatState { Group = new CombatGroup() };
        var enemy  = BlockEnemy(0, AttackType.Physical, EnemyAbility.Brutal);
        combat.DamageAssignments.Add(new DamageAssignment(enemy, AttackType.Physical, 3));
        var resolver = MakeResolverWithDamageChoices(state);

        await resolver.ResolveAssignDamagePhase(combat);

        Assert.Equal(3, state.Hero.Hand.Count); // ceil(6/2)
    }

    [Fact]
    public async Task AssignDamage_UnitAbsorbs_NoHeroWound() {
        var state  = EmptyState();
        var combat = new CombatState { Group = new CombatGroup() };
        var unit   = TestUnit(3);
        state.Hero.Units.Add(unit);
        var enemy  = BlockEnemy(0, AttackType.Physical);
        combat.DamageAssignments.Add(new DamageAssignment(enemy, AttackType.Physical, 3));
        var resolver = MakeResolverWithDamageChoices(state, unit);

        await resolver.ResolveAssignDamagePhase(combat);

        Assert.Equal(1, unit.WoundCount);
        Assert.Empty(state.Hero.Hand);
    }

    [Fact]
    public async Task AssignDamage_ResistantUnit_AbsorbsAll_NoWound() {
        var state  = EmptyState();
        var combat = new CombatState { Group = new CombatGroup() };
        var unit   = TestUnit(3, AttackType.Fire);
        state.Hero.Units.Add(unit);
        var enemy  = BlockEnemy(0, AttackType.Fire);
        combat.DamageAssignments.Add(new DamageAssignment(enemy, AttackType.Fire, 3));
        var resolver = MakeResolverWithDamageChoices(state, unit);

        await resolver.ResolveAssignDamagePhase(combat);

        Assert.Equal(0, unit.WoundCount);
        Assert.False(unit.IsDestroyed);
    }

    [Fact]
    public async Task AssignDamage_ResistantUnit_ThenSpillsToHero() {
        var state  = EmptyState();
        var combat = new CombatState { Group = new CombatGroup() };
        var unit   = TestUnit(3, AttackType.Fire);
        state.Hero.Units.Add(unit);
        var enemy  = BlockEnemy(0, AttackType.Fire);
        combat.DamageAssignments.Add(new DamageAssignment(enemy, AttackType.Fire, 8));
        var resolver = MakeResolverWithDamageChoices(state, unit);

        await resolver.ResolveAssignDamagePhase(combat);

        Assert.Equal(1, unit.WoundCount);         // d: 8 -> 5 (resist) -> 2 (base armor) -> wound
        Assert.Single(state.Hero.Hand);            // remaining 2 -> ceil(2/2) = 1
    }

    [Fact]
    public async Task AssignDamage_Paralyze_DestroysUnit_NoWound() {
        var state  = EmptyState();
        var combat = new CombatState { Group = new CombatGroup() };
        var unit   = TestUnit(3);
        state.Hero.Units.Add(unit);
        var enemy  = BlockEnemy(0, AttackType.Physical, EnemyAbility.Paralyze);
        combat.DamageAssignments.Add(new DamageAssignment(enemy, AttackType.Physical, 3));
        var resolver = MakeResolverWithDamageChoices(state, unit);

        await resolver.ResolveAssignDamagePhase(combat);

        Assert.True(unit.IsDestroyed);
        Assert.Equal(0, unit.WoundCount);
    }

    [Fact]
    public async Task AssignDamage_Poison_UnitTakesTwoWounds() {
        var state  = EmptyState();
        var combat = new CombatState { Group = new CombatGroup() };
        var unit   = TestUnit(3);
        state.Hero.Units.Add(unit);
        var enemy  = BlockEnemy(0, AttackType.Physical, EnemyAbility.Poison);
        combat.DamageAssignments.Add(new DamageAssignment(enemy, AttackType.Physical, 3));
        var resolver = MakeResolverWithDamageChoices(state, unit);

        await resolver.ResolveAssignDamagePhase(combat);

        Assert.Equal(2, unit.WoundCount);
        Assert.Empty(state.Hero.Hand);
    }

    [Fact]
    public async Task AssignDamage_Poison_HeroExtraWoundsToDiscard() {
        var state  = EmptyState();
        var combat = new CombatState { Group = new CombatGroup() };
        var enemy  = BlockEnemy(0, AttackType.Physical, EnemyAbility.Poison);
        combat.DamageAssignments.Add(new DamageAssignment(enemy, AttackType.Physical, 4));
        var resolver = MakeResolverWithDamageChoices(state);

        await resolver.ResolveAssignDamagePhase(combat);

        Assert.Equal(2, state.Hero.Hand.Count);
        Assert.Equal(2, state.Hero.DiscardPile.Count);
    }

    [Fact]
    public async Task AssignDamage_WoundedUnit_Ineligible_HeroAbsorbs() {
        var state  = EmptyState();
        var combat = new CombatState { Group = new CombatGroup() };
        var unit   = TestUnit(3);
        unit.TakeWound(); // pre-wounded, cross-combat inert rule
        state.Hero.Units.Add(unit);
        var enemy  = BlockEnemy(0, AttackType.Physical);
        combat.DamageAssignments.Add(new DamageAssignment(enemy, AttackType.Physical, 4));
        var resolver = MakeResolverWithDamageChoices(state, unit); // scripted, but should never be offered

        await resolver.ResolveAssignDamagePhase(combat);

        Assert.Equal(1, unit.WoundCount);        // unchanged
        Assert.Equal(2, state.Hero.Hand.Count);   // ceil(4/2)
    }

    [Fact]
    public async Task AssignDamage_UnitUsedOncePerCombat() {
        var state  = EmptyState();
        var combat = new CombatState { Group = new CombatGroup() };
        // Resists Fire so the first assignment fully absorbs with NO wound — proves the
        // alreadyAssigned exclusion is independent of CanAbsorbDamage (unit stays woundless).
        var unit   = TestUnit(3, AttackType.Fire);
        state.Hero.Units.Add(unit);
        var enemy  = BlockEnemy(0, AttackType.Fire);
        combat.DamageAssignments.Add(new DamageAssignment(enemy, AttackType.Fire, 3));
        combat.DamageAssignments.Add(new DamageAssignment(enemy, AttackType.Physical, 4));
        var resolver = MakeResolverWithDamageChoices(state, unit, unit);

        await resolver.ResolveAssignDamagePhase(combat);

        Assert.Equal(0, unit.WoundCount);
        Assert.False(unit.IsDestroyed);
        Assert.Equal(2, state.Hero.Hand.Count); // 2nd assignment fell to hero: ceil(4/2)
    }

    [Fact]
    public async Task AssignDamage_UnitDamageLocked_AllToHero() {
        var state  = EmptyState();
        var combat = new CombatState { Group = new CombatGroup(), UnitDamageLocked = true };
        var unit   = TestUnit(3);
        state.Hero.Units.Add(unit);
        var enemy  = BlockEnemy(0, AttackType.Physical);
        combat.DamageAssignments.Add(new DamageAssignment(enemy, AttackType.Physical, 4));
        var resolver = MakeResolverWithDamageChoices(state, unit);

        await resolver.ResolveAssignDamagePhase(combat);

        Assert.Equal(0, unit.WoundCount);
        Assert.Equal(2, state.Hero.Hand.Count);
    }

    [Fact]
    public async Task ResolveCombat_SkipsAssignDamagePhase_WhenAllEnemiesDefeated() {
        var state  = EmptyState();
        var enemy  = BlockEnemy(4, AttackType.Physical); // Armor 4
        var combat = new CombatState { Group = MultiEnemyGroup(enemy) };
        bool assignDamageEntered = false;
        combat.PhaseCallbacks.Add(
            new TestCallback(GamePhase.CombatAssignDamage, () => assignDamageEntered = true));
        var rangedDecl = new RangedAttackDeclaration(
            new List<AttackContribution> {
                new(AttackType.Physical, AttackDelivery.Ranged, 4) },
            new List<EnemyTokenInstance> { enemy });
        var broker = new TestBroker(new List<RangedAttackDeclaration> { rangedDecl });
        var resolver = new CombatResolver(state, broker, new EffectScheduler(), new EffectHookRegistry());

        await resolver.ResolveCombat(combat);

        Assert.False(assignDamageEntered);
        Assert.Empty(state.Hero.Hand);
    }

    // Review P2: the (armor, 2×armor] resistance band — resistance subtracts armor ONCE,
    // damage survives (no early break), the second armor subtraction absorbs the rest, and the
    // unit takes a wound with NOTHING spilling to the hero. Spec-correct per combat-flow-lld §10.2
    // ("if damage reaches zero before wounding, no wound" — here it does NOT, so a wound is given).
    [Fact]
    public async Task AssignDamage_ResistantUnit_WoundedButNoHeroSpill() {
        var state  = EmptyState();
        var combat = new CombatState { Group = new CombatGroup() };
        var unit   = TestUnit(3, AttackType.Fire);
        state.Hero.Units.Add(unit);
        var enemy  = BlockEnemy(0, AttackType.Fire);
        combat.DamageAssignments.Add(new DamageAssignment(enemy, AttackType.Fire, 5)); // 5-3=2 (>0), 2-3=-1
        var resolver = MakeResolverWithDamageChoices(state, unit);

        await resolver.ResolveAssignDamagePhase(combat);

        Assert.Equal(1, unit.WoundCount);   // wounded (damage exceeded a single armor subtraction)
        Assert.Empty(state.Hero.Hand);      // second armor subtraction absorbed the rest — no spill
    }

    // Review P3: one assignment large enough (Brutal doubles it) to cascade across TWO units and
    // then the hero — each unit takes a full Poison wound (×2) or would be destroyed, and the
    // remainder still poisons the hero. §10.1-correct but previously had no regression coverage.
    [Fact]
    public async Task AssignDamage_MultiUnitSpill_BrutalPoison_CascadesUnitsThenHero() {
        var state  = EmptyState();
        var combat = new CombatState { Group = new CombatGroup() };
        var unit1  = TestUnit(3);
        var unit2  = TestUnit(3);
        state.Hero.Units.Add(unit1);
        state.Hero.Units.Add(unit2);
        var enemy  = BlockEnemy(0, AttackType.Physical, EnemyAbility.Brutal, EnemyAbility.Poison);
        // RawValue 5 → Brutal → d=10. unit1: 10-3=7, +2 wounds. unit2: 7-3=4, +2 wounds.
        // No units left → hero: ceil(4/2)=2 to hand, Poison → 2 to discard.
        combat.DamageAssignments.Add(new DamageAssignment(enemy, AttackType.Physical, 5));
        var resolver = MakeResolverWithDamageChoices(state, unit1, unit2);

        await resolver.ResolveAssignDamagePhase(combat);

        Assert.Equal(2, unit1.WoundCount);
        Assert.Equal(2, unit2.WoundCount);
        Assert.Equal(2, state.Hero.Hand.Count);
        Assert.Equal(2, state.Hero.DiscardPile.Count);
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

    // Scriptable test double for UIBroker — override PromptHeroRangedAttacks /
    // PromptHeroBlock to supply declarations without touching any live UI.
    private sealed class TestBroker : UIBroker {
        private readonly IReadOnlyList<RangedAttackDeclaration> _declarations;
        private readonly IReadOnlyList<BlockDeclaration>        _blockDeclarations;
        private readonly Queue<UnitInstance?>                   _damageChoices;
        public bool BlockPrompted { get; private set; }

        public TestBroker(IReadOnlyList<RangedAttackDeclaration> declarations,
                          IReadOnlyList<BlockDeclaration>? blockDeclarations = null,
                          IEnumerable<UnitInstance?>? damageChoices = null) {
            _declarations      = declarations;
            _blockDeclarations = blockDeclarations ?? new List<BlockDeclaration>();
            _damageChoices     = new Queue<UnitInstance?>(damageChoices ?? Enumerable.Empty<UnitInstance?>());
        }
        public override Task<IReadOnlyList<RangedAttackDeclaration>> PromptHeroRangedAttacks(CombatState combat) =>
            Task.FromResult(_declarations);
        public override Task<IReadOnlyList<BlockDeclaration>> PromptHeroBlock(CombatState combat) {
            BlockPrompted = true;
            return Task.FromResult(_blockDeclarations);
        }
        public override Task<UnitInstance?> PromptHeroDamageTarget(
                DamageAssignment a, IReadOnlyList<UnitInstance> eligible, CombatState combat) =>
            Task.FromResult(_damageChoices.Count > 0 ? _damageChoices.Dequeue() : null);
    }

    private static CombatResolver MakeResolverWith(GameState state, UIBroker broker) =>
        new(state, broker, new EffectScheduler(), new EffectHookRegistry());
}
