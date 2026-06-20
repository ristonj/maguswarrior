using System.Collections.Generic;
using System.Linq;
using MagusWarrior.Deck;
using MagusWarrior.Hex;
using MagusWarrior.Map;

namespace MagusWarrior.Core;

public abstract record UndoEntry;

public record CardPlayGroup(
    string SourceCardId,
    string? CostCardId,
    GameStateSnapshot StateBefore) : UndoEntry;

public record HeroMoveEntry(
    HexCoord Previous,
    int CostRefund) : UndoEntry;

public class UndoController {
    private readonly Stack<UndoEntry> _stack = new();

    public bool CanUndo => _stack.Count > 0;
    public bool CanUndoHeroMove => _stack.Count > 0 && _stack.Peek() is HeroMoveEntry;

    public void PushCardPlay(string sourceCardId, string? costCardId, GameStateSnapshot stateBefore) =>
        _stack.Push(new CardPlayGroup(sourceCardId, costCardId, stateBefore));

    public void PushHeroMove(HexCoord previous, int costRefund) =>
        _stack.Push(new HeroMoveEntry(previous, costRefund));

    // Returns the HeroMoveEntry if it is on top of the stack and pops it; null otherwise (stack unchanged).
    public HeroMoveEntry? PopHeroMove() {
        if (_stack.Count == 0 || _stack.Peek() is not HeroMoveEntry) return null;
        return (HeroMoveEntry)_stack.Pop();
    }

    // Pops and executes whatever is on top. Hero-move undo only uses state+map (deck is unused).
    public Result<string> ExecuteUndo(GameState state, DeckManager deck, WorldMap map) {
        if (_stack.Count == 0) return Result<string>.Fail("Nothing to undo");
        var entry = _stack.Pop();
        return entry switch {
            CardPlayGroup g => ExecuteCardPlayUndo(g, state, deck),
            HeroMoveEntry m => ExecuteHeroMoveUndo(m, state, map),
            _ => Result<string>.Fail($"Unknown undo entry type: {entry.GetType().Name}")
        };
    }

    public void Clear() => _stack.Clear();

    private Result<string> ExecuteCardPlayUndo(CardPlayGroup group, GameState state, DeckManager deck) {
        // Recall cost card first — it is the only step that can fail.
        // If it fails, abort before mutating anything to prevent partial rollback.
        if (group.CostCardId != null) {
            var recall = deck.RecallFromDiscard(group.CostCardId);
            if (!recall.IsSuccess) {
                _stack.Push(group); // put back — nothing mutated
                return Result<string>.Fail(
                    $"Undo aborted: cost card '{group.CostCardId}' not in discard — {recall.Error}");
            }
        }
        var card = state.Cards.FirstOrDefault(c => c.Id == group.SourceCardId);
        if (card != null) deck.ReturnCard(card);
        state.RestoreSnapshot(group.StateBefore);
        return Result<string>.Ok($"Undo play: {group.SourceCardId}");
    }

    private static Result<string> ExecuteHeroMoveUndo(HeroMoveEntry move, GameState state, WorldMap map) {
        map.SetHeroPosition(move.Previous); // fires HeroMoved → HexMapView marker updates automatically
        state.AddMovePoints(move.CostRefund);
        return Result<string>.Ok($"Undo hero move: refund={move.CostRefund}");
    }
}
