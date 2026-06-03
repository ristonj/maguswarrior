using MagusWarrior.Core.Types;

namespace MagusWarrior.Cards;

public static class SidewaysRule {
    public static (EffectType EffectType, int Amount)? GetEffect(GamePhase phase) =>
        phase switch {
            GamePhase.Movement    => (EffectType.Move, 1),
            GamePhase.Interaction => (EffectType.Influence, 1),
            GamePhase.CombatBlock => (EffectType.Block, 1),
            GamePhase.CombatMelee => (EffectType.AttackMelee, 1),
            _                     => null
        };
}
