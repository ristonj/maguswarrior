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
    public Func<DamageAssignment, IReadOnlyList<UnitInstance>, CombatState, Task<UnitInstance?>>?
        DamageTargetProvider { get; set; }

    public Task ShowStartOfCombatInterstitial(CombatState combat) =>
        InterstitialProvider?.Invoke(combat) ?? Task.CompletedTask;

    public virtual Task<IReadOnlyList<RangedAttackDeclaration>> PromptHeroRangedAttacks(CombatState combat) =>
        RangedAttackProvider?.Invoke(combat)
        ?? Task.FromResult<IReadOnlyList<RangedAttackDeclaration>>(new List<RangedAttackDeclaration>());

    public virtual Task<IReadOnlyList<BlockDeclaration>> PromptHeroBlock(CombatState combat) =>
        BlockProvider?.Invoke(combat)
        ?? Task.FromResult<IReadOnlyList<BlockDeclaration>>(new List<BlockDeclaration>());

    // Returns the unit the hero chose to absorb this assignment, or null to assign to the hero.
    // Default (no provider registered): null ⇒ hero takes the damage. This is the correct fallback
    // for the not-yet-live-wired dev flow — all unblocked damage lands on the hero.
    public virtual Task<UnitInstance?> PromptHeroDamageTarget(
            DamageAssignment assignment, IReadOnlyList<UnitInstance> eligibleUnits, CombatState combat) =>
        DamageTargetProvider?.Invoke(assignment, eligibleUnits, combat)
        ?? Task.FromResult<UnitInstance?>(null);

    public Task ResolveEnemyAttackVsHero(EnemyTokenInstance enemy, CombatState combat) => Task.CompletedTask;
    public Task PromptHeroMeleeAttacks(CombatState combat) => Task.CompletedTask;
}
