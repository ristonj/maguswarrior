using MagusWarrior.Core.Types;

namespace MagusWarrior.Combat;

// Block efficiency (canonical: effect-lld.md §Block Efficiency; combat-flow-lld.md §9.4 agrees).
// Elements oppose: Fire block counters Ice, Ice block counters Fire. ColdFire blocks all efficiently.
public static class BlockEfficiency {
    // Effective block points `value` provides against `attack`:
    //   efficient (1:1) -> full value; inefficient (2:1) -> value / 2 (integer floor).
    public static int Effective(BlockType block, AttackType attack, int value) =>
        IsEfficient(block, attack) ? value : value / 2;

    private static bool IsEfficient(BlockType block, AttackType attack) => block switch {
        BlockType.Physical => attack == AttackType.Physical,
        BlockType.Fire     => attack is AttackType.Physical or AttackType.Ice,
        BlockType.Ice      => attack is AttackType.Physical or AttackType.Fire,
        BlockType.ColdFire => true,
        _                  => false,
    };
}
