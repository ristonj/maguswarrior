using System.Collections.Generic;
using MagusWarrior.Combat;
using MagusWarrior.Core.Types;

namespace MagusWarrior.Broker;

public static class BlockBridge {
    public static IReadOnlyList<BlockContribution> ExtractBlockContributions(
        IReadOnlyDictionary<AttackElement, int> pool) {

        var result = new List<BlockContribution>();
        foreach (var (element, value) in pool) {
            if (value <= 0) continue;
            // AttackElement and BlockType share identical member names; direct cast is valid.
            var type = (BlockType)(int)element;
            result.Add(new BlockContribution(type, value));
        }
        return result;
    }
}
