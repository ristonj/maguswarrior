using System;
using System.Collections.Generic;
using MagusWarrior.Cards;

namespace MagusWarrior.Deck;

public class DeckManager {
    public IReadOnlyList<CardDefinition> Hand { get; private set; } = Array.Empty<CardDefinition>();

    public event Action? HandChanged;

    public void SetHand(IReadOnlyList<CardDefinition> hand) {
        Hand = hand;
        HandChanged?.Invoke();
    }
}
