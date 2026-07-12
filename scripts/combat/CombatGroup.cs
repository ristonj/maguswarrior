using System.Collections.Generic;

namespace MagusWarrior.Combat;

public class CombatGroup {
    public IReadOnlyList<EnemyTokenInstance> Enemies           { get; init; } = new List<EnemyTokenInstance>();
    public bool                              IsAtFortifiedSite { get; init; } = false;
}

public record CombatResult(
    bool                     HeroWon,
    List<EnemyTokenInstance> DefeatedEnemies,
    int                      FameEarned,
    int                      ReputationEarned,
    int                      WoundsDrawn,      // Wounds that entered Hero.Hand
    int                      WoundsToDiscard   // Wounds sent straight to Hero.DiscardPile (Poison)
);
