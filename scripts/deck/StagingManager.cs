using System;
using System.Collections.Generic;
using MagusWarrior.Cards;
using MagusWarrior.Core.Types;

namespace MagusWarrior.Deck;

public record StagingTotals(int Move, int Attack, int Block, int Influence);

public class StagingManager {
    public record StagedEntry(CardDefinition Card, EffectType EffectType);

    public event Action? StagingChanged;

    private readonly List<StagedEntry> _staged = new();

    public IReadOnlyList<StagedEntry> StagedCards => _staged;

    public void Stage(CardDefinition card, EffectType effectType) {
        _staged.Add(new StagedEntry(card, effectType));
        StagingChanged?.Invoke();
    }

    public StagedEntry? Unstage() {
        if (_staged.Count == 0) return null;
        var entry = _staged[_staged.Count - 1];
        _staged.RemoveAt(_staged.Count - 1);
        StagingChanged?.Invoke();
        return entry;
    }

    public void Clear() {
        _staged.Clear();
        StagingChanged?.Invoke();
    }

    public StagingTotals GetTotals() {
        int move = 0, attack = 0, block = 0, influence = 0;
        foreach (var entry in _staged) {
            var spec = entry.Card.Unpowered;
            if (spec is null) continue;
            move      += spec.Move;
            attack    += spec.Attack;
            block     += spec.Block;
            influence += spec.Influence;
        }
        return new StagingTotals(move, attack, block, influence);
    }
}
