namespace MagusWarrior.Core.Types;

public enum EnemyAbility {
    Fortified,
    PhysicalResistance,
    FireResistance,
    IceResistance,
    // Cold fire resistance is derived: IceResistance && FireResistance; no enum value
    Swift,
    Brutal,
    Poison,
    Paralyze,
    Summon,
    ArcaneImmunity,  // no base game tokens; stable hook point for expansion
}
