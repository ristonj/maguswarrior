using System.Linq;
using MagusWarrior.Core.Types;
using MagusWarrior.Hex;
using Xunit;

namespace MagusWarrior.Tests;

public class HexGridTest {
    private static HexState Plains => new HexState(TerrainType.Plains);

    [Fact]
    public void Contains_AfterAdd_ReturnsTrue() {
        var grid = new HexGrid();
        var coord = new HexCoord(0, 0);
        grid.Add(coord, Plains);
        Assert.True(grid.Contains(coord));
    }

    [Fact]
    public void Contains_NotAdded_ReturnsFalse() {
        var grid = new HexGrid();
        Assert.False(grid.Contains(new HexCoord(99, 99)));
    }

    [Fact]
    public void GetState_AfterAdd_ReturnsState() {
        var grid = new HexGrid();
        var coord = new HexCoord(1, -1);
        var state = new HexState(TerrainType.Forest);
        grid.Add(coord, state);
        Assert.Equal(state, grid.GetState(coord));
    }

    [Fact]
    public void GetState_Absent_ReturnsNull() {
        var grid = new HexGrid();
        Assert.Null(grid.GetState(new HexCoord(5, 5)));
    }

    [Fact]
    public void GetNeighbors_ReturnsOnlyGridMembers() {
        var grid = new HexGrid();
        var center = new HexCoord(0, 0);
        var inGrid = new HexCoord(1, 0);
        grid.Add(center, Plains);
        grid.Add(inGrid, Plains);
        // center's neighbor (1,0) is in grid; other 5 neighbors are not
        var neighbors = grid.GetNeighbors(center).ToList();
        Assert.Single(neighbors);
        Assert.Equal(inGrid, neighbors[0]);
    }
}
