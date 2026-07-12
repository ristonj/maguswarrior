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
    public int WoundsToHand     { get; set; }   // Wounds drawn to Hero.Hand this combat (for the post-combat readout)
    // Wounds sent straight to Hero.DiscardPile this combat (Poison). Tracked SEPARATELY rather than
    // folded into WoundsToHand: the two are equal per assignment, but NOT per combat — a group with
    // one Poison and one plain enemy draws hand-wounds from both and discard-wounds from only one.
    public int WoundsToDiscard  { get; set; }

    public bool AllEnemiesDefeated => ActiveEnemies.Count == 0;
}
