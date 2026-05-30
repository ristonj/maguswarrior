using System;
using System.Collections.Generic;
using MagusWarrior.Cards;
using MagusWarrior.Core.Types;

namespace MagusWarrior.Core;

public class GameState {
    public GamePhase CurrentPhase { get; private set; }
    public int MovePointsThisTurn { get; private set; }
    public IReadOnlyList<CardDefinition> Cards { get; private set; }
    public GameEventLog EventLog { get; } = new();

    public GameState() : this(LoadCardsOrThrow()) { }

    public GameState(IReadOnlyList<CardDefinition> cards) {
        Cards = cards;
    }

    public void AddMovePoints(int n) {
        MovePointsThisTurn += n;
    }

    public GameStateSnapshot TakeSnapshot() => new(CurrentPhase, MovePointsThisTurn);

    public void RestoreSnapshot(GameStateSnapshot snapshot) {
        CurrentPhase = snapshot.CurrentPhase;
        MovePointsThisTurn = snapshot.MovePointsThisTurn;
    }

    private static IReadOnlyList<CardDefinition> LoadCardsOrThrow() {
        try {
            return CardLoader.LoadAll("data/cards.yaml");
        } catch (InvalidOperationException) {
            throw;
        } catch (Exception ex) {
            throw new InvalidOperationException(
                "Failed to load cards.yaml — startup failure", ex);
        }
    }
}
