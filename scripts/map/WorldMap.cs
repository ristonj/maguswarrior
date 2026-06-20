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
    public event Action<MapTile>? TileRevealed;

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

    public void CommitHeroMove(HexCoord coord) {
        SetHeroPosition(coord);
    }

    // The canonical way to reveal a tile. Calling MapTile.Reveal() directly leaves
    // the grid stale — those hexes never appear. Always use this method.
    public void RevealTile(MapTile tile) {
        if (tile.IsRevealed) return;  // idempotent: don't re-add grid hexes or re-fire TileRevealed
        tile.Reveal();
        foreach (var (worldCoord, terrain) in tile.WorldHexes())
            _grid.Add(worldCoord, new HexState(terrain));
        TileRevealed?.Invoke(tile);
    }

    // Returns the first unrevealed tile whose world hexes contain coord, or null.
    public MapTile? FindTileForCoord(HexCoord coord) {
        foreach (var tile in _tiles) {
            if (tile.IsRevealed) continue;
            foreach (var (worldCoord, _) in tile.WorldHexes())
                if (worldCoord == coord) return tile;
        }
        return null;
    }
}
