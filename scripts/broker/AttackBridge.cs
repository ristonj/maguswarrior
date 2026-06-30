using System;
using System.Collections.Generic;
using MagusWarrior.Combat;
using MagusWarrior.Core.Types;

namespace MagusWarrior.Broker;

public static class AttackBridge {
    public static IReadOnlyList<AttackContribution> ExtractRangedContributions(
        IReadOnlyDictionary<(EffectType Distance, AttackElement Element), int> pool) {

        var result = new List<AttackContribution>();
        foreach (var ((distance, element), value) in pool) {
            if (value <= 0) continue;
            var delivery = distance switch {
                EffectType.AttackRanged => AttackDelivery.Ranged,
                EffectType.AttackSiege  => AttackDelivery.Siege,
                _ => (AttackDelivery?)null,
            };
            if (delivery is null) continue;

            // AttackElement and AttackType share identical member names; direct cast is valid.
            var type = (AttackType)(int)element;
            result.Add(new AttackContribution(type, delivery.Value, value));
        }
        return result;
    }
}
