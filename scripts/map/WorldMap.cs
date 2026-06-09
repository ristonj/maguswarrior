using System;
using System.Collections.Generic;
using MagusWarrior.Hex;

namespace MagusWarrior.Map;

public class WorldMap {
    private readonly List<MapTile> _tiles = new();
    private readonly HexGrid _grid = new();

    public HexGrid Grid => _grid;
    public IReadOnlyList<MapTile> PlacedTiles => _tiles.AsReadOnly();
    public HexCoord HeroPosition { get; private set; }

    public event Action<HexCoord>? HeroMoved;

    public WorldMap(HexCoord initialHeroPosition) {
        HeroPosition = initialHeroPosition;
    }

    public void PlaceTile(MapTile tile) {
        _tiles.Add(tile);
        if (!tile.IsRevealed) return;
        foreach (var (worldCoord, terrain) in tile.WorldHexes())
            _grid.Add(worldCoord, new HexState(terrain));
    }

    public void SetHeroPosition(HexCoord coord) {
        HeroPosition = coord;
        HeroMoved?.Invoke(coord);
    }
}
