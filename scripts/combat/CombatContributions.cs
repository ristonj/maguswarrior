using System.Collections.Generic;
using System.Threading.Tasks;
using MagusWarrior.Broker;
using MagusWarrior.Core.Types;
using MagusWarrior.Units;

namespace MagusWarrior.Combat;

public record AttackContribution(AttackType Type, AttackDelivery Delivery, int Value);
public record BlockContribution(BlockType Type, int Value);
public record DamageAssignment(EnemyTokenInstance Source, AttackType DamageType, int RawValue);

// The hero's Assign-Damage decision for one (spill) step. Replaces the old UnitInstance? return,
// which conflated "hero absorbs" (null) with "provider returned an ineligible unit" (also null-branch).
// Now the two are distinct types: HeroAbsorbs is an explicit choice, and an AssignToUnit carrying an
// ineligible unit is a caller (panel) bug the resolver asserts on rather than silently eating.
//
// CLOSED hierarchy: the private constructor means only the nested cases below can derive from
// DamageChoice. Without it, a future third case would silently fall into the resolver's hero-absorb
// branch — resurrecting the exact conflation this type exists to kill. Adding a case here is meant
// to break ApplyOneDamageAssignment's exhaustiveness check loudly, not to be absorbed by a catch-all.
public abstract record DamageChoice {
    private DamageChoice() { }

    public sealed record HeroAbsorbs()                    : DamageChoice;
    public sealed record AssignToUnit(UnitInstance Unit)   : DamageChoice;
}

// A single targeting declaration for the ranged phase: a set of attack contributions
// aimed at a specific group of enemies (one or many for a combined attack).
public record RangedAttackDeclaration(
    IReadOnlyList<AttackContribution>  Contributions,
    IReadOnlyList<EnemyTokenInstance>  Targets
);

// A single block declaration: a set of block contributions aimed at ONE specific attack
// of one enemy. Block is per-attack, all-or-nothing (LLD §9.3) — block allocated to one
// attack never covers another, even on the same enemy. AttackIndex indexes into
// Target.Definition.Attacks.
public record BlockDeclaration(
    IReadOnlyList<BlockContribution>  Contributions,
    EnemyTokenInstance                Target,
    int                               AttackIndex
);

public interface ICombatAttackModifier {
    AttackContribution Modify(AttackContribution contrib);
}

public interface ICombatPhaseCallback {
    GamePhase TriggerPhase { get; }
    Task Execute(CombatState combat, UIBroker broker);
}
