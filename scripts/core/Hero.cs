using System.Collections.Generic;
using MagusWarrior.Cards;
using MagusWarrior.Units;

namespace MagusWarrior.Core;

// Minimal combat damage-sink introduced in story 3-4. Owns the hero's Armor, deployed Units,
// and a REAL card Hand/DiscardPile (Wounds are WoundCard.Create() cards, not counters) so 3-5
// (knockdown) inherits real hand semantics. This is the first slice of the Hero-owns-hand model
// (see project_hero_model_decision). It deliberately does NOT yet subsume DeckManager — the two
// hands coexist until a later unification refactor. Knockout / Paralyze-vs-hero / hand-size are
// story 3-5 and are intentionally absent here.
public class Hero {
    public int                   Armor       { get; }
    public List<UnitInstance>    Units       { get; } = new();
    public List<CardDefinition>  Hand        { get; } = new();
    public List<CardDefinition>  DiscardPile { get; } = new();

    public Hero(int armor) { Armor = armor; }

    public void DrawWoundsToHand(int n) {
        for (int i = 0; i < n; i++) Hand.Add(WoundCard.Create());
    }

    // Poison: for every Wound drawn to hand, one extra Wound goes straight to the discard pile.
    public void AddWoundsToDiscard(int n) {
        for (int i = 0; i < n; i++) DiscardPile.Add(WoundCard.Create());
    }
}
