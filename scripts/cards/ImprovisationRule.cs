using System.Collections.Generic;
using MagusWarrior.Core.Types;

namespace MagusWarrior.Cards;

public static class ImprovisationRule {
    public static EffectType? GetPhaseEffect(GamePhase phase) => phase switch {
        GamePhase.Movement    => EffectType.Move,
        GamePhase.CombatMelee => EffectType.AttackMelee,
        GamePhase.CombatBlock => EffectType.Block,
        GamePhase.Interaction => EffectType.Influence,
        _                     => null,
    };

    public static bool CanPlay(IReadOnlyList<CardDefinition> hand, string improvisationId) {
        foreach (var card in hand)
            if (card.Id != improvisationId) return true;
        return false;
    }

    public static int GetAmount(bool powered) => powered ? 5 : 3;
}
