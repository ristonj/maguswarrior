using System.Threading.Tasks;
using MagusWarrior.Broker;
using MagusWarrior.Core.Types;

namespace MagusWarrior.Combat;

public record AttackContribution(AttackType Type, AttackDelivery Delivery, int Value);
public record BlockContribution(BlockType Type, int Value);
public record DamageAssignment(EnemyTokenInstance Source, AttackType DamageType, int RawValue);

public interface ICombatAttackModifier {
    AttackContribution Modify(AttackContribution contrib);
}

public interface ICombatPhaseCallback {
    GamePhase TriggerPhase { get; }
    Task Execute(CombatState combat, UIBroker broker);
}
