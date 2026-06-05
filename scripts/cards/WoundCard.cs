namespace MagusWarrior.Cards;

// Wound cards are defined in code only (cards.yaml carries no Wound entries — see its
// header note). All Wounds are identical and fungible: no effect, no mana cost, never
// playable through the normal hand flow. See story 1b-5.
public static class WoundCard {
    public static CardDefinition Create() =>
        new() {
            Id = "wound",
            Name = "Wound",
            Type = CardType.Wound,
        };
}
