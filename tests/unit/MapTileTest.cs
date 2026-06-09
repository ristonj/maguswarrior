using System;
using System.Linq;
using MagusWarrior.Core.Types;
using MagusWarrior.Hex;
using MagusWarrior.Map;
using Xunit;

namespace MagusWarrior.Tests;

public class MapTileTest {
    private static (HexCoord, TerrainType)[] SevenHexes(HexCoord origin) =>
        new[] {
            (origin, TerrainType.Plains),
            (origin + new HexCoord( 1,  0), TerrainType.Plains),
            (origin + new HexCoord(-1,  0), TerrainType.Plains),
            (origin + new HexCoord( 0,  1), TerrainType.Plains),
            (origin + new HexCoord( 0, -1), TerrainType.Plains),
            (origin + new HexCoord( 1, -1), TerrainType.Plains),
            (origin + new HexCoord(-1,  1), TerrainType.Plains),
        };

    [Fact]
    public void WorldHexes_AddsOriginToRelative() {
        var origin = new HexCoord(3, 3);
        var tile = new MapTile("t", TileType.Starting, true, origin, SevenHexes(new HexCoord(0, 0)));
        var worldCoords = tile.WorldHexes().Select(h => h.WorldCoord).ToList();
        Assert.Contains(new HexCoord(4, 3), worldCoords);  // relative (1,0) + origin (3,3)
    }

    [Fact]
    public void WorldHexes_CountIsSeven() {
        var tile = new MapTile("t", TileType.Countryside, true, new HexCoord(0, 0), SevenHexes(new HexCoord(0, 0)));
        Assert.Equal(7, tile.WorldHexes().Count());
    }

    [Fact]
    public void Reveal_SetsIsRevealedTrue() {
        var tile = new MapTile("t", TileType.Core, false, new HexCoord(0, 0), SevenHexes(new HexCoord(0, 0)));
        Assert.False(tile.IsRevealed);
        tile.Reveal();
        Assert.True(tile.IsRevealed);
    }

    [Fact]
    public void Constructor_WrongHexCount_ThrowsArgumentException() {
        var sixHexes = SevenHexes(new HexCoord(0, 0)).Take(6);
        Assert.Throws<ArgumentException>(() =>
            new MapTile("t", TileType.Starting, true, new HexCoord(0, 0), sixHexes));
    }
}
