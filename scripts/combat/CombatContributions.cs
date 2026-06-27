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

public interface ICombatAttackModifier {
    AttackContribution Modify(AttackContribution contrib);
}

public interface ICombatPhaseCallback {
    GamePhase TriggerPhase { get; }
    Task Execute(CombatState combat, UIBroker broker);
}
