using System.Collections.Generic;
using MagusWarrior.Core.Types;

namespace MagusWarrior.Combat;

public class CombatState {
    public CombatGroup Group        { get; init; } = new();
    public bool   IsAtFortifiedSite => Group.IsAtFortifiedSite;
    public GamePhase CurrentPhase   { get; set; }

    public List<EnemyTokenInstance>    ActiveEnemies     { get; } = new();
    public List<EnemyTokenInstance>    DefeatedEnemies   { get; } = new();
    public List<AttackContribution>    AttackPool        { get; } = new();
    public List<BlockContribution>     BlockPool         { get; } = new();
    public List<DamageAssignment>      DamageAssignments { get; } = new();
    public List<ICombatAttackModifier> AttackModifiers   { get; } = new();
    public List<ICombatPhaseCallback>  PhaseCallbacks    { get; } = new();

    public bool            UnitDamageLocked          { get; set; }
    public bool            SkipBlockAndDamagePending { get; set; }
    public MoveConversionMode? ActiveMoveConversion  { get; set; }
    public BlockType?      ActiveInfluenceConversion { get; set; }

    public int FameEarned       { get; set; }
    public int ReputationEarned { get; set; }

    public bool AllEnemiesDefeated => ActiveEnemies.Count == 0;
}
