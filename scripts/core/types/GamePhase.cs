namespace MagusWarrior.Core.Types;

public enum GamePhase {
    Movement,
    Interaction,
    CombatStart,
    CombatRanged,
    CombatBlock,
    CombatAssignDamage,
    CombatMelee,
    Rest,
    EndOfTurn,
    Any,
}
