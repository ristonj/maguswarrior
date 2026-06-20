using System.Collections.Generic;
using MagusWarrior.Cards;
using MagusWarrior.Core;
using MagusWarrior.Core.Types;
using MagusWarrior.Deck;
using MagusWarrior.Hex;
using MagusWarrior.Map;
using Xunit;

namespace MagusWarrior.Tests;

public class UndoControllerTest {
    private static CardDefinition MarchCard()         => new() { Id = "march",         Name = "March" };
    private static CardDefinition ImprovisationCard() => new() { Id = "improvisation",  Name = "Improvisation" };

    private static GameState EmptyState() => new(new List<CardDefinition>());
    private static GameState StateWith(params CardDefinition[] cards) => new(cards);
    private static WorldMap EmptyMap() => new(new HexCoord(0, 0));
    private static DeckManager EmptyDeck() => new();

    [Fact]
    public void CanUndo_FalseInitially() {
        var uc = new UndoController();
        Assert.False(uc.CanUndo);
    }

    [Fact]
    public void PushCardPlay_ThenExecuteUndo_RestoresStateAndReturnsCard() {
        var march = MarchCard();
        var state = StateWith(march);       // state.Cards has "march" so ReturnCard is called
        var deck = new DeckManager();
        deck.SetHand(new[] { march });
        var snap = state.TakeSnapshot();
        state.AddMovePoints(3);

        var uc = new UndoController();
        uc.PushCardPlay("march", null, snap);
        deck.PlayCard("march");            // simulates what HandView does before enqueue

        var result = uc.ExecuteUndo(state, deck, EmptyMap());

        Assert.True(result.IsSuccess);
        Assert.Equal(0, state.MovePointsThisTurn);  // snapshot restored (was 0 at snap time)
        Assert.Single(deck.Hand);                   // card returned from state.Cards lookup
        Assert.False(uc.CanUndo);
    }

    [Fact]
    public void PushCardPlay_WithCostCard_ExecuteUndo_RecallsCostCard() {
        var march = MarchCard();
        var state = EmptyState();           // no card lookup needed for cost-card path
        var deck = new DeckManager();
        deck.SetHand(new[] { march });
        deck.DiscardCard("march");          // march is now in discard (simulates Improv discard)
        var snap = state.TakeSnapshot();

        var uc = new UndoController();
        uc.PushCardPlay("improvisation", "march", snap);
        // "improvisation" is not in state.Cards so ReturnCard is skipped — that's correct:
        // ImprovisationView keeps the improv card out of hand separately; only cost card is recalled

        var result = uc.ExecuteUndo(state, deck, EmptyMap());

        Assert.True(result.IsSuccess);
        Assert.Single(deck.Hand);       // march recalled to hand
        Assert.Empty(deck.DiscardPile);
    }

    [Fact]
    public void PushCardPlay_CostCardNotInDiscard_Aborts_NothingMutated() {
        var state = EmptyState();
        state.AddMovePoints(5);
        var snap = state.TakeSnapshot();
        var deck = EmptyDeck();         // discard pile is empty

        var uc = new UndoController();
        uc.PushCardPlay("improvisation", "march", snap);

        var result = uc.ExecuteUndo(state, deck, EmptyMap());

        Assert.False(result.IsSuccess);
        Assert.Equal(5, state.MovePointsThisTurn); // NOT rolled back — snap had 5 pts but we abort
        Assert.True(uc.CanUndo);                   // entry put back on stack
    }

    [Fact]
    public void PushCardPlay_Multiple_UndoesInLifoOrder() {
        var state = EmptyState();
        var deck = EmptyDeck();
        var snap1 = state.TakeSnapshot();     // snap1 = 0 pts
        state.AddMovePoints(1);
        var snap2 = state.TakeSnapshot();     // snap2 = 1 pt
        state.AddMovePoints(1);               // current = 2 pts

        var uc = new UndoController();
        uc.PushCardPlay("march",     null, snap1);
        uc.PushCardPlay("swiftness", null, snap2);

        // First undo pops swiftness (restores snap2 = 1 pt)
        uc.ExecuteUndo(state, deck, EmptyMap());
        Assert.Equal(1, state.MovePointsThisTurn);

        // Second undo pops march (restores snap1 = 0 pts)
        uc.ExecuteUndo(state, deck, EmptyMap());
        Assert.Equal(0, state.MovePointsThisTurn);

        Assert.False(uc.CanUndo);
    }

    [Fact]
    public void PushHeroMove_ThenPopHeroMove_ReturnsEntryAndStackEmpty() {
        var uc = new UndoController();
        uc.PushHeroMove(new HexCoord(0, 0), costRefund: 2);

        var entry = uc.PopHeroMove();

        Assert.NotNull(entry);
        Assert.Equal(new HexCoord(0, 0), entry!.Previous);
        Assert.Equal(2, entry.CostRefund);
        Assert.False(uc.CanUndo);
    }

    [Fact]
    public void PushHeroMove_ThenExecuteUndo_RestoresPositionAndRefundsPoints() {
        var state = EmptyState();
        state.AddMovePoints(5);
        state.SpendMovePoints(2);   // spent 2 to walk → now 3 points remaining
        var map = EmptyMap();
        map.SetHeroPosition(new HexCoord(1, 0));

        var uc = new UndoController();
        uc.PushHeroMove(new HexCoord(0, 0), costRefund: 2);

        uc.ExecuteUndo(state, EmptyDeck(), map);

        Assert.Equal(new HexCoord(0, 0), map.HeroPosition);
        Assert.Equal(5, state.MovePointsThisTurn);  // 3 remaining + 2 refunded = 5 (original)
    }

    [Fact]
    public void Clear_EmptiesStack() {
        var state = EmptyState();
        var uc = new UndoController();
        uc.PushCardPlay("march", null, state.TakeSnapshot());
        uc.PushHeroMove(new HexCoord(0, 0), costRefund: 1);

        uc.Clear();

        Assert.False(uc.CanUndo);
        Assert.False(uc.CanUndoHeroMove);
    }

    [Fact]
    public void CanUndoHeroMove_TrueOnlyWhenTopIsHeroMoveEntry() {
        var state = EmptyState();
        var uc = new UndoController();
        Assert.False(uc.CanUndoHeroMove);

        uc.PushCardPlay("march", null, state.TakeSnapshot());
        Assert.False(uc.CanUndoHeroMove);  // top is CardPlayGroup

        uc.PushHeroMove(new HexCoord(0, 0), 1);
        Assert.True(uc.CanUndoHeroMove);   // top is HeroMoveEntry
    }

    [Fact]
    public void PopHeroMove_WhenTopIsCardPlay_ReturnsNull() {
        var state = EmptyState();
        var uc = new UndoController();
        uc.PushCardPlay("march", null, state.TakeSnapshot());

        Assert.Null(uc.PopHeroMove());
        Assert.True(uc.CanUndo);   // stack NOT modified
    }

    [Fact]
    public void PushCardPlay_PushHeroMove_ExecuteUndo_PopsHeroMoveFirst() {
        var state = EmptyState();
        state.AddMovePoints(3);
        var snap = state.TakeSnapshot();    // snap = 3 pts
        var map = EmptyMap();
        map.SetHeroPosition(new HexCoord(1, 0));

        var uc = new UndoController();
        uc.PushCardPlay("march", null, snap);          // bottom
        uc.PushHeroMove(new HexCoord(0, 0), 2);       // top

        uc.ExecuteUndo(state, EmptyDeck(), map);

        Assert.Equal(new HexCoord(0, 0), map.HeroPosition);  // hero move undone
        Assert.True(uc.CanUndo);                              // card play still on stack
    }

    [Fact]
    public void PushHeroMove_PushCardPlay_ExecuteUndo_PopsCardPlayFirst() {
        var state = EmptyState();
        var snap = state.TakeSnapshot();    // snap = 0 pts
        state.AddMovePoints(3);             // current = 3 pts
        var map = EmptyMap();
        map.SetHeroPosition(new HexCoord(1, 0));

        var uc = new UndoController();
        uc.PushHeroMove(new HexCoord(0, 0), 2);       // bottom
        uc.PushCardPlay("march", null, snap);          // top

        uc.ExecuteUndo(state, EmptyDeck(), map);

        Assert.Equal(0, state.MovePointsThisTurn);    // card play undone (snap restored to 0)
        Assert.True(uc.CanUndo);                      // hero move still on stack
    }
}
