using System;
using MagusWarrior.Core.Types;

namespace MagusWarrior.Map;

// Tracks how many tiles remain to be revealed, by back type. This is the minimal
// "deck depth" model — it does NOT hold real tile definitions (that is the future
// tile-drawing story that will load data/tiles.yaml). Counts decrement when a tile
// is revealed; the HUD overlay observes Changed.
public class TileStock {
    public int CountrysideRemaining { get; private set; }
    public int CoreRemaining { get; private set; }

    public event Action? Changed;

    public TileStock(int countrysideRemaining, int coreRemaining) {
        CountrysideRemaining = countrysideRemaining;
        CoreRemaining = coreRemaining;
    }

    // Decrement the counter matching the revealed tile's back type and notify observers.
    // Starting tiles are not part of the deck — no-op, no event. Counts floor at 0.
    public void RecordReveal(TileType type) {
        switch (type) {
            case TileType.Countryside:
                CountrysideRemaining = Math.Max(0, CountrysideRemaining - 1);
                Changed?.Invoke();
                break;
            case TileType.Core:
                CoreRemaining = Math.Max(0, CoreRemaining - 1);
                Changed?.Invoke();
                break;
            // TileType.Starting (and any future non-deck type): no-op, no event.
        }
    }
}
