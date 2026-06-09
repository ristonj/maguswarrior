using System.Collections.Generic;

namespace MagusWarrior.Hex;

public class HexGrid {
    private readonly Dictionary<HexCoord, HexState> _hexes = new();

    public void Add(HexCoord coord, HexState state) => _hexes[coord] = state;

    public bool Contains(HexCoord coord) => _hexes.ContainsKey(coord);

    public HexState? GetState(HexCoord coord) =>
        _hexes.TryGetValue(coord, out var state) ? state : null;

    public IEnumerable<HexCoord> GetNeighbors(HexCoord coord) {
        foreach (var neighbor in coord.Neighbors())
            if (_hexes.ContainsKey(neighbor))
                yield return neighbor;
    }

    public IEnumerable<(HexCoord Coord, HexState State)> AllHexes() {
        foreach (var kv in _hexes)
            yield return (kv.Key, kv.Value);
    }
}
