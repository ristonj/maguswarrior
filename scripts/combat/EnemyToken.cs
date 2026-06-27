using System.Collections.Generic;
using System.Linq;
using MagusWarrior.Core.Types;

namespace MagusWarrior.Combat;

public record EnemyAttack(int Value, AttackType Type);

public record EnemyTokenDefinition(
    string                      Id,
    string                      Name,
    TokenColor                  Color,
    int                         Armor,
    IReadOnlyList<EnemyAttack>  Attacks,
    int                         FameValue,
    IReadOnlyList<EnemyAbility> Abilities,
    bool                        IsRampaging,
    SummonBehavior?             Summon
);

public record SummonBehavior(TokenColor Color, int Count);

public class EnemyTokenInstance {
    public EnemyTokenDefinition Definition   { get; }
    public int  BaseArmor       => Definition.Armor;
    public int  ArmorModifier   { get; set; } = 0;
    public int  EffectiveArmor  => System.Math.Max(1, Definition.Armor + ArmorModifier);
    public int  AttackModifier  { get; set; } = 0;
    public bool AttackCancelled { get; set; } = false;
    public int  WoundCount      { get; private set; } = 0;
    public bool IsDestroyed     { get; private set; } = false;

    private readonly List<EnemyAbility> _strippedAbilities = new();

    public EnemyTokenInstance(EnemyTokenDefinition definition) {
        Definition = definition;
    }

    public bool HasAbility(EnemyAbility ability) {
        if (_strippedAbilities.Contains(ability)) return false;
        return Definition.Abilities.Contains(ability);
    }

    // Called per-attack during block resolution (enemies may have multiple attacks of different types).
    public bool HasResistanceTo(AttackType type) => type switch {
        AttackType.Physical => HasAbility(EnemyAbility.PhysicalResistance),
        AttackType.Fire     => HasAbility(EnemyAbility.FireResistance),
        AttackType.Ice      => HasAbility(EnemyAbility.IceResistance),
        // Cold fire resistance is derived: must resist both Fire and Ice
        AttackType.ColdFire => HasAbility(EnemyAbility.FireResistance) && HasAbility(EnemyAbility.IceResistance),
        _                   => false,
    };

    public void TakeWound()  { WoundCount++; }
    public void Destroy()    { IsDestroyed = true; }
    public void StripAbility(EnemyAbility ability) => _strippedAbilities.Add(ability);

    public void ClearCombatModifiers() {
        ArmorModifier   = 0;
        AttackModifier  = 0;
        AttackCancelled = false;
        _strippedAbilities.Clear();
    }
}
