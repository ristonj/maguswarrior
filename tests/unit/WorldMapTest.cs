using System.Linq;
using MagusWarrior.Core.Types;
using MagusWarrior.Hex;
using MagusWarrior.Map;
using Xunit;

namespace MagusWarrior.Tests;

public class WorldMapTest {
    private static MapTile RevealedTile(HexCoord origin) {
        var hexes = new[] {
            (new HexCoord(0, 0), TerrainType.Plains),
            (new HexCoord( 1,  0), TerrainType.Plains),
            (new HexCoord(-1,  0), TerrainType.Plains),
            (new HexCoord( 0,  1), TerrainType.Plains),
            (new HexCoord( 0, -1), TerrainType.Plains),
            (new HexCoord( 1, -1), TerrainType.Plains),
            (new HexCoord(-1,  1), TerrainType.Plains),
        };
        return new MapTile("t", TileType.Starting, isRevealed: true, origin, hexes);
    }

    private static MapTile UnrevealedTile(HexCoord origin) {
        var hexes = new[] {
            (new HexCoord(0, 0), TerrainType.Plains),
            (new HexCoord( 1,  0), TerrainType.Plains),
            (new HexCoord(-1,  0), TerrainType.Plains),
            (new HexCoord( 0,  1), TerrainType.Plains),
            (new HexCoord( 0, -1), TerrainType.Plains),
            (new HexCoord( 1, -1), TerrainType.Plains),
            (new HexCoord(-1,  1), TerrainType.Plains),
        };
        return new MapTile("t2", TileType.Countryside, isRevealed: false, origin, hexes);
    }

    [Fact]
    public void InitialHeroPosition_MatchesConstructor() {
        var start = new HexCoord(2, -1);
        var map = new WorldMap(start);
        Assert.Equal(start, map.HeroPosition);
    }

    [Fact]
    public void PlaceTile_RevealedTile_AddsHexesToGrid() {
        var map = new WorldMap(new HexCoord(0, 0));
        map.PlaceTile(RevealedTile(new HexCoord(0, 0)));
        Assert.True(map.Grid.Contains(new HexCoord(0, 0)));
        Assert.True(map.Grid.Contains(new HexCoord(1, 0)));
    }

    [Fact]
    public void PlaceTile_RevealedTile_AddsExactlySevenHexes() {
        var map = new WorldMap(new HexCoord(0, 0));
        map.PlaceTile(RevealedTile(new HexCoord(0, 0)));
        Assert.Equal(7, map.Grid.AllHexes().Count());
    }

    [Fact]
    public void PlaceTile_UnrevealedTile_DoesNotAddToGrid() {
        var map = new WorldMap(new HexCoord(0, 0));
        map.PlaceTile(UnrevealedTile(new HexCoord(5, 5)));
        Assert.False(map.Grid.Contains(new HexCoord(5, 5)));
        Assert.Empty(map.Grid.AllHexes());
    }

    [Fact]
    public void SetHeroPosition_UpdatesProperty() {
        var map = new WorldMap(new HexCoord(0, 0));
        map.SetHeroPosition(new HexCoord(1, 0));
        Assert.Equal(new HexCoord(1, 0), map.HeroPosition);
    }

    [Fact]
    public void SetHeroPosition_FiresHeroMoved() {
        var map = new WorldMap(new HexCoord(0, 0));
        bool fired = false;
        map.HeroMoved += _ => fired = true;
        map.SetHeroPosition(new HexCoord(1, 0));
        Assert.True(fired);
    }

    [Fact]
    public void HeroMoved_PassesNewCoord() {
        var map = new WorldMap(new HexCoord(0, 0));
        HexCoord received = default;
        map.HeroMoved += coord => received = coord;
        map.SetHeroPosition(new HexCoord(1, -1));
        Assert.Equal(new HexCoord(1, -1), received);
    }
}
