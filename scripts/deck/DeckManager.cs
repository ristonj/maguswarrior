using System;
using System.Collections.Generic;
using System.Linq;
using MagusWarrior.Cards;
using MagusWarrior.Core;

namespace MagusWarrior.Deck;

public class DeckManager {
    public IReadOnlyList<CardDefinition> Hand { get; private set; } = Array.Empty<CardDefinition>();

    public event Action? HandChanged;

    public void SetHand(IReadOnlyList<CardDefinition> hand) {
        Hand = hand;
        HandChanged?.Invoke();
    }

    public void ReturnCard(CardDefinition card) {
        var list = Hand.ToList();
        list.Add(card);
        SetHand(list);
    }

    public Result<CardDefinition> PlayCard(string cardId) {
        var list = Hand.ToList();
        var idx = list.FindIndex(c => c.Id == cardId);
        if (idx < 0)
            return Result<CardDefinition>.Fail($"Card '{cardId}' not in hand");
        var played = list[idx];
        list.RemoveAt(idx);
        SetHand(list);
        return Result<CardDefinition>.Ok(played);
    }
}
