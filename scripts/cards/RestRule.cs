using System.Collections.Generic;
using System.Linq;

namespace MagusWarrior.Cards;

public static class RestRule {
    public static bool CanDeclareRest(IReadOnlyList<CardDefinition> hand) =>
        hand.Count > 0;

    public static bool IsStandardRest(IReadOnlyList<CardDefinition> hand) =>
        hand.Any(c => c.Type != CardType.Wound);

    public static bool IsExhaustion(IReadOnlyList<CardDefinition> hand) =>
        hand.Count > 0 && hand.All(c => c.Type == CardType.Wound);
}
