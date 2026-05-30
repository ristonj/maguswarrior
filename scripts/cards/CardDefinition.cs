using System;
using MagusWarrior.Core.Types;

namespace MagusWarrior.Cards;

public enum CardType {
    BasicAction,
    AdvancedAction,
    Spell,
    Artifact,
    Wound,
}

public class EffectSpec {
    public EffectType EffectType { get; set; }
    public string Text { get; set; } = string.Empty;
    public int Move { get; set; }
    public int Attack { get; set; }
    public int Block { get; set; }
    public int Influence { get; set; }
    public int Heal { get; set; }
}

public class CardDefinition {
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public CardType Type { get; set; }
    public ManaColor? ManaCost { get; set; }
    public EffectSpec? Unpowered { get; set; }
    public EffectSpec? Powered { get; set; }
    public EffectType[] AlternateEffectTypes { get; set; } = Array.Empty<EffectType>();
    public GamePhase[]? LegalPhases { get; set; }
}
