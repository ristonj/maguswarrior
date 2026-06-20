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
        combat.ActiveEnemies.AddRange(combat.Group.Enemies);
        await FirePhaseCallbacks(GamePhase.CombatStart, combat);
        await _broker.ShowStartOfCombatInterstitial(combat);
        // Phase 0 is setup-only; hand off to Phase 1 (Ranged Attack). Ranged
        // resolution itself is story 3-2 — we enter the phase but resolve nothing here.
        SetPhase(GamePhase.CombatRanged, combat);
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
        return BuildResult(combat);
    }

    private CombatResult BuildResult(CombatState combat) =>
        new(
            HeroWon:          combat.ActiveEnemies.Count == 0,
            DefeatedEnemies:  new List<EnemyTokenInstance>(combat.DefeatedEnemies),
            FameEarned:       combat.FameEarned,
            ReputationEarned: combat.ReputationEarned
        );
}
