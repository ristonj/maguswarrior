using System;
using System.Collections.Generic;
using MagusWarrior.Hex;

namespace MagusWarrior.Map;

public class WorldMap {
    private readonly List<MapTile> _tiles = new();
    private readonly HexGrid _grid = new();
    private readonly List<(HexCoord Previous, int CostPaid)> _movePath = new();

    public HexGrid Grid => _grid;
    public IReadOnlyList<MapTile> PlacedTiles => _tiles.AsReadOnly();
    public HexCoord HeroPosition { get; private set; }
    public bool CanUndoMove => _movePath.Count > 0;

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

    public void CommitHeroMove(HexCoord coord, int costPaid) {
        _movePath.Add((HeroPosition, costPaid));
        SetHeroPosition(coord);
    }

    public (HexCoord Previous, int CostRefund)? UndoLastMove() {
        if (_movePath.Count == 0) return null;
        var (previous, cost) = _movePath[^1];
        _movePath.RemoveAt(_movePath.Count - 1);
        SetHeroPosition(previous);
        return (previous, cost);
    }

    public void ClearMovePath() => _movePath.Clear();
}
