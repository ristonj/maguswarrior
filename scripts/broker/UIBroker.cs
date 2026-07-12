using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MagusWarrior.Combat;
using MagusWarrior.Units;

namespace MagusWarrior.Broker;

// All methods must remain pure C# (no Godot types) so the test project can compile this file.
// Godot panels wire themselves in via the Provider delegates at startup.
public class UIBroker {
    public Func<CombatState, Task>?                                           InterstitialProvider  { get; set; }
    public Func<CombatState, Task<IReadOnlyList<RangedAttackDeclaration>>>?   RangedAttackProvider  { get; set; }
    public Func<CombatState, Task<IReadOnlyList<BlockDeclaration>>>?          BlockProvider         { get; set; }
    public Func<DamageAssignment, int, IReadOnlyList<UnitInstance>, CombatState, Task<DamageChoice>>?
        DamageTargetProvider { get; set; }

    public Task ShowStartOfCombatInterstitial(CombatState combat) =>
        InterstitialProvider?.Invoke(combat) ?? Task.CompletedTask;

    public virtual Task<IReadOnlyList<RangedAttackDeclaration>> PromptHeroRangedAttacks(CombatState combat) =>
        RangedAttackProvider?.Invoke(combat)
        ?? Task.FromResult<IReadOnlyList<RangedAttackDeclaration>>(new List<RangedAttackDeclaration>());

    public virtual Task<IReadOnlyList<BlockDeclaration>> PromptHeroBlock(CombatState combat) =>
        BlockProvider?.Invoke(combat)
        ?? Task.FromResult<IReadOnlyList<BlockDeclaration>>(new List<BlockDeclaration>());

    // Returns the hero's choice for THIS decision step. `remainingDamage` is the running, post-armor
    // damage still to assign (NOT the printed RawValue) so the panel can show "N still to assign" without
    // re-deriving the resolver's math. Default (no provider): HeroAbsorbs — the correct fallback when the
    // panel is not registered (all unblocked damage lands on the hero).
    public virtual Task<DamageChoice> PromptHeroDamageTarget(
            DamageAssignment assignment, int remainingDamage,
            IReadOnlyList<UnitInstance> eligibleUnits, CombatState combat) =>
        DamageTargetProvider?.Invoke(assignment, remainingDamage, eligibleUnits, combat)
        ?? Task.FromResult<DamageChoice>(new DamageChoice.HeroAbsorbs());

    public Task ResolveEnemyAttackVsHero(EnemyTokenInstance enemy, CombatState combat) => Task.CompletedTask;
    public Task PromptHeroMeleeAttacks(CombatState combat) => Task.CompletedTask;
}
