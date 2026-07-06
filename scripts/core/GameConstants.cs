namespace MagusWarrior.Core;

public static class GameConstants {
    public static readonly int MaxHandSize = 8;

    // Standard Mage Knight hero armor. data/heroes.yaml carries no armor field
    // (verified) — Hero(int armor) takes this so the value isn't hard-coded in Hero.
    public static readonly int HeroBaseArmor = 2;
}
