using System;
using System.Collections.Generic;
using MagusWarrior.Cards;
using MagusWarrior.Core.Types;
#if GODOT
using Godot;
#endif

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
#if GODOT
            using var f = FileAccess.Open("res://data/cards.yaml", FileAccess.ModeFlags.Read);
            if (f == null)
                throw new System.IO.IOException(
                    $"res://data/cards.yaml not found (Godot error {FileAccess.GetOpenError()})");
            return CardLoader.ParseAll(f.GetAsText());
#else
            return CardLoader.LoadAll("data/cards.yaml");
#endif
        } catch (InvalidOperationException) {
            throw;
        } catch (Exception ex) {
            throw new InvalidOperationException(
                "Failed to load cards.yaml — startup failure", ex);
        }
    }
}
