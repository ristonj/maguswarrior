using System.Collections.Generic;
using System.Threading.Tasks;
using MagusWarrior.Broker;
using MagusWarrior.Core.Types;

namespace MagusWarrior.Combat;

public record AttackContribution(AttackType Type, AttackDelivery Delivery, int Value);
public record BlockContribution(BlockType Type, int Value);
public record DamageAssignment(EnemyTokenInstance Source, AttackType DamageType, int RawValue);

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
