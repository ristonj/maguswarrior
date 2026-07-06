using System.Collections.Generic;
using System.Linq;
using MagusWarrior.Core.Types;

namespace MagusWarrior.Units;

// Minimal pure-C# damage-sink for deployed Units, introduced in story 3-4. Mirrors
// EnemyTokenInstance's wound/destroy conventions. The absorption MATH (Brutal doubling,
// resistance-doubles-armor, Paralyze-destroys, Poison-second-wound) lives in the resolver's
// ApplyOneDamageAssignment, never here — this class only exposes primitives so the same
// minimal shape survives Epic 5's fuller rules unchanged.
public class UnitInstance {
    public int  Armor       { get; }
    public int  WoundCount  { get; private set; } = 0;
    public bool IsDestroyed { get; private set; } = false;

    // Resistances as a SET. Cold Fire resistance is DERIVED (must resist both Fire and Ice),
    // exactly as EnemyTokenInstance.HasResistanceTo derives it — do not store a ColdFire entry.
    private readonly HashSet<AttackType> _resistances;

    public UnitInstance(int armor, IEnumerable<AttackType>? resistances = null) {
        Armor        = armor;
        _resistances = new HashSet<AttackType>(resistances ?? Enumerable.Empty<AttackType>());
    }

    public bool HasResistanceTo(AttackType type) => type switch {
        AttackType.ColdFire => _resistances.Contains(AttackType.Fire) && _resistances.Contains(AttackType.Ice),
        _                   => _resistances.Contains(type),
    };

    // A wounded OR destroyed unit cannot absorb damage. (The "not already assigned this combat"
    // condition is enforced by the resolver's per-combat HashSet, NOT by a field on the unit —
    // so there is no per-combat flag to reset here.)
    public bool CanAbsorbDamage => WoundCount == 0 && !IsDestroyed;

    public void TakeWound() { WoundCount++; }                 // wounds ACCUMULATE and never destroy
    public void Heal()      { if (WoundCount > 0) WoundCount--; } // one wound at a time
    public void Destroy()   { IsDestroyed = true; }           // wound-independent (Paralyze only)
}
