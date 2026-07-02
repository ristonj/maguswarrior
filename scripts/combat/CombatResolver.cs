using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MagusWarrior.Broker;
using MagusWarrior.Cards.Effects;
using MagusWarrior.Core;
using MagusWarrior.Core.Types;

namespace MagusWarrior.Combat;

public class CombatResolver {
    private readonly GameState          _state;
    private readonly UIBroker           _broker;
    private readonly EffectScheduler    _scheduler;
    private readonly EffectHookRegistry _hooks;

    public CombatResolver(GameState state, UIBroker broker,
                          EffectScheduler scheduler, EffectHookRegistry hooks) {
        (_state, _broker, _scheduler, _hooks) = (state, broker, scheduler, hooks);
    }

    public async Task ResolveStartOfCombat(CombatState combat) {
        SetPhase(GamePhase.CombatStart, combat);
        combat.ActiveEnemies.Clear();
        combat.ActiveEnemies.AddRange(combat.Group.Enemies);
        await FirePhaseCallbacks(GamePhase.CombatStart, combat);
        await _broker.ShowStartOfCombatInterstitial(combat);
    }

    public void SetPhase(GamePhase phase, CombatState combat) {
        combat.CurrentPhase = phase;
        _state.SetPhase(phase);
    }

    public async Task FirePhaseCallbacks(GamePhase phase, CombatState combat) {
        var callbacks = combat.PhaseCallbacks
            .Where(cb => cb.TriggerPhase == phase)
            .ToList();
        foreach (var cb in callbacks) {
            combat.PhaseCallbacks.Remove(cb);
            await cb.Execute(combat, _broker);
        }
    }

    public async Task<CombatResult> ResolveCombat(CombatState combat) {
        await ResolveStartOfCombat(combat);
        if (!combat.AllEnemiesDefeated) await ResolveRangedPhase(combat);
        if (!combat.AllEnemiesDefeated && !combat.SkipBlockAndDamagePending)
            await ResolveBlockPhase(combat);
        var result = BuildResult(combat);
        TearDownCombatState(combat);
        return result;
    }

    public async Task ResolveBlockPhase(CombatState combat) {
        SetPhase(GamePhase.CombatBlock, combat);
        await FirePhaseCallbacks(GamePhase.CombatBlock, combat);

        var declarations = await _broker.PromptHeroBlock(combat);

        foreach (var enemy in combat.ActiveEnemies.ToList()) {
            // A nullified or destroyed enemy deals no damage this combat. (IsDestroyed has no
            // callers yet, but Destroy() leaves the token in ActiveEnemies — guard defensively.)
            if (enemy.AttackCancelled || enemy.IsDestroyed) continue;

            var attacks = enemy.Definition.Attacks;
            for (int i = 0; i < attacks.Count; i++) {
                var attack = attacks[i];
                if (attack.Type == AttackType.None) continue;   // summon-only "attack"; no damage in Phase 2

                // Block allocated to THIS attack only (per-attack consumption — block declared
                // against attack i never covers attack j, LLD §9.3).
                var allocated = declarations
                    .Where(d => d.Target == enemy && d.AttackIndex == i)
                    .SelectMany(d => d.Contributions)
                    .ToList();

                // All-or-nothing: block must fully meet the threshold or the FULL printed attack
                // gets through. BlockOutcome.IsFullyBlocked encapsulates the sum-then-efficiency
                // logic and the Swift multiplier — shared with the panel (6c).
                if (!BlockOutcome.IsFullyBlocked(attack, enemy.HasAbility(EnemyAbility.Swift), allocated))
                    combat.DamageAssignments.Add(
                        new DamageAssignment(enemy, attack.Type, attack.Value));
            }
        }
    }

    private void TearDownCombatState(CombatState combat) {
        combat.AttackPool.Clear();
        combat.BlockPool.Clear();
        combat.DamageAssignments.Clear();
        combat.AttackModifiers.Clear();
        combat.PhaseCallbacks.Clear();
        combat.ActiveInfluenceConversion = null;
        combat.ActiveMoveConversion      = null;
        foreach (var e in combat.Group.Enemies)
            e.ClearCombatModifiers();
        _state.ClearAttackAndBlockPools();
        _state.SetPhase(GamePhase.EndOfTurn);
    }

    public async Task ResolveRangedPhase(CombatState combat) {
        SetPhase(GamePhase.CombatRanged, combat);
        await FirePhaseCallbacks(GamePhase.CombatRanged, combat);

        var declarations = await _broker.PromptHeroRangedAttacks(combat);
        foreach (var decl in declarations) {
            foreach (var contrib in decl.Contributions)
                combat.AttackPool.Add(contrib);

            // A declaration with no targets deals damage to no one; skip it
            // (Targets.Max below would otherwise throw on the empty sequence).
            if (decl.Targets.Count == 0) continue;

            int govLevel = decl.Targets.Max(t => FortificationLevel(t, combat));
            var surviving = decl.Contributions
                .Where(c => c.Delivery == AttackDelivery.Siege  ? govLevel <= 1
                          : c.Delivery == AttackDelivery.Ranged ? govLevel == 0
                          : false)
                .ToList();

            var byType = surviving.GroupBy(c => c.Type);
            int effectiveTotal = 0;
            foreach (var group in byType) {
                int typeValue = group.Sum(c => ComputeEffectiveAttack(c, combat));
                if (decl.Targets.Any(t => t.HasResistanceTo(group.Key)))
                    typeValue /= 2;
                effectiveTotal += typeValue;
            }

            int threshold = decl.Targets.Sum(t => t.EffectiveArmor);
            if (effectiveTotal >= threshold) {
                foreach (var target in decl.Targets) {
                    // Only record a defeat if the target was actually still active —
                    // guards against the same enemy appearing across two declarations
                    // (or a stale reference) inflating DefeatedEnemies / Fame.
                    if (combat.ActiveEnemies.Remove(target))
                        combat.DefeatedEnemies.Add(target);
                }
            }
        }
    }

    public int FortificationLevel(EnemyTokenInstance enemy, CombatState combat) =>
        (combat.IsAtFortifiedSite ? 1 : 0) + (enemy.HasAbility(EnemyAbility.Fortified) ? 1 : 0);

    public int ComputeEffectiveAttack(AttackContribution contrib, CombatState combat) {
        foreach (var modifier in combat.AttackModifiers)
            contrib = modifier.Modify(contrib);
        return contrib.Value;
    }

    private CombatResult BuildResult(CombatState combat) =>
        new(
            HeroWon:          combat.ActiveEnemies.Count == 0,
            DefeatedEnemies:  new List<EnemyTokenInstance>(combat.DefeatedEnemies),
            FameEarned:       combat.FameEarned,
            ReputationEarned: combat.ReputationEarned
        );
}
