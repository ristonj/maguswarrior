using System.Collections.Generic;
using System.Linq;
using MagusWarrior.Core.Types;

namespace MagusWarrior.Combat;

// Pure-C# shared block-outcome helper (no Godot types).
// Centralises the "is this attack fully blocked?" rule so the resolver
// and the panel use a single definition, not two diverging copies.
public static class BlockOutcome {
    // Returns true when the declared block contributions are sufficient to fully
    // block the given attack (all-or-nothing, LLD §9.3).
    //
    //   effectiveBlock = sum per block-type of BlockEfficiency.Effective(type, attack.Type, sum)
    //   threshold      = attack.Value * (swift ? 2 : 1)
    //
    // Same arithmetic as ResolveBlockPhase: group by BlockType, sum each group,
    // apply efficiency once per type (avoids per-contribution floor loss when
    // same-type block is split across multiple contributions).
    public static bool IsFullyBlocked(EnemyAttack attack, bool swift,
            IEnumerable<BlockContribution> allocated) {
        int effectiveBlock = allocated
            .GroupBy(b => b.Type)
            .Sum(g => BlockEfficiency.Effective(g.Key, attack.Type, g.Sum(b => b.Value)));

        int threshold = attack.Value * (swift ? 2 : 1);
        return effectiveBlock >= threshold;
    }
}
