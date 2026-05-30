using System.Collections.Generic;
using MagusWarrior.Cards;
using MagusWarrior.Core;
using MagusWarrior.Core.Types;

namespace MagusWarrior.Cards.Effects;

public static class PhaseGate {
    private static readonly HashSet<(EffectType, GamePhase)> _legal = new() {
        (EffectType.Move,         GamePhase.Movement),
        (EffectType.AttackMelee,  GamePhase.CombatMelee),
        (EffectType.AttackRanged, GamePhase.CombatRanged),
        // MK rule: ranged/siege may also be played in the melee step (forfeits first-strike).
        (EffectType.AttackRanged, GamePhase.CombatMelee),
        (EffectType.AttackSiege,  GamePhase.CombatRanged),
        (EffectType.AttackSiege,  GamePhase.CombatMelee),
        (EffectType.Block,        GamePhase.CombatBlock),
        (EffectType.Influence,    GamePhase.Interaction),
        (EffectType.Heal,         GamePhase.Movement),
        (EffectType.Heal,         GamePhase.Interaction),
        (EffectType.Heal,         GamePhase.Rest),
        (EffectType.Heal,         GamePhase.EndOfTurn),
        (EffectType.Mana,         GamePhase.Any),
        (EffectType.Crystal,      GamePhase.Any),
        (EffectType.Special,      GamePhase.Any),
    };

    public static bool IsLegal(EffectType effectType, GamePhase currentPhase) =>
        _legal.Contains((effectType, GamePhase.Any)) ||
        _legal.Contains((effectType, currentPhase));
}

public class PhaseValidator {
    public Result<bool> ValidatePhase(CardDefinition card, EffectType chosenType, GamePhase phase) {
        var isValidChoice = card.Unpowered?.EffectType == chosenType
                         || card.Powered?.EffectType == chosenType;
        if (card.AlternateEffectTypes.Length > 0) {
            bool isAlternate = false;
            foreach (var alt in card.AlternateEffectTypes)
                if (alt == chosenType) { isAlternate = true; break; }
            isValidChoice = isValidChoice || isAlternate;
        }
        if (!isValidChoice)
            return Result<bool>.Fail($"'{chosenType}' is not a valid effect type for card '{card.Id}'");

        if (card.LegalPhases != null) {
            bool found = false;
            foreach (var lp in card.LegalPhases)
                if (lp == phase) { found = true; break; }
            return found
                ? Result<bool>.Ok(true)
                : Result<bool>.Fail($"Card '{card.Id}' restricts play to specific phases");
        }

        return PhaseGate.IsLegal(chosenType, phase)
            ? Result<bool>.Ok(true)
            : Result<bool>.Fail($"{chosenType} is not legal in {phase}");
    }
}
