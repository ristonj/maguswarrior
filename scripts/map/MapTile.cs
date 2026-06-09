using System;
using System.Collections.Generic;
using System.Linq;
using MagusWarrior.Core.Types;
using MagusWarrior.Hex;

namespace MagusWarrior.Map;

public class MapTile {
    private readonly IReadOnlyList<(HexCoord RelativeCoord, TerrainType Terrain)> _hexes;

    public string TileId { get; }
    public TileType TileType { get; }
    public bool IsRevealed { get; private set; }
    public HexCoord Origin { get; }

    public MapTile(string tileId, TileType tileType, bool isRevealed, HexCoord origin,
                   IEnumerable<(HexCoord RelativeCoord, TerrainType Terrain)> hexes) {
        var list = hexes.ToList();
        if (list.Count != 7)
            throw new ArgumentException(
                $"Mage Knight tiles contain exactly 7 hexes, got {list.Count}", nameof(hexes));

        TileId = tileId;
        TileType = tileType;
        IsRevealed = isRevealed;
        Origin = origin;
        _hexes = list.AsReadOnly();
    }

    public void Reveal() { IsRevealed = true; }

    public IEnumerable<(HexCoord WorldCoord, TerrainType Terrain)> WorldHexes() {
        foreach (var (rel, terrain) in _hexes)
            yield return (Origin + rel, terrain);
    }
}
