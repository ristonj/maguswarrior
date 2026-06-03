using System;
using System.Collections.Generic;
using MagusWarrior.Core.Types;

namespace MagusWarrior.Core;

public record GameStateSnapshot(
    GamePhase CurrentPhase,
    int MovePointsThisTurn,
    int InfluencePointsThisTurn,
    IReadOnlyDictionary<(EffectType Distance, AttackElement Element), int> AttackPool,
    IReadOnlyDictionary<AttackElement, int> BlockPool);

public record EffectFiredEvent(
    string SourceCardId,
    EffectType EffectType,
    GamePhase Phase,
    bool Powered,
    GameStateSnapshot StateBefore);

public class GameEventLog {
    private readonly List<EffectFiredEvent> _events = new();

    public IReadOnlyList<EffectFiredEvent> Events => _events;

    public void Append(EffectFiredEvent e) => _events.Add(e);

    public EffectFiredEvent? PopLast() {
        if (_events.Count == 0) return null;
        var last = _events[^1];
        _events.RemoveAt(_events.Count - 1);
        return last;
    }
}
