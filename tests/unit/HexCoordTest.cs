using System.Linq;
using MagusWarrior.Hex;
using Xunit;

namespace MagusWarrior.Tests;

public class HexCoordTest {
    [Fact]
    public void Neighbors_ReturnsExactlySix() {
        var coord = new HexCoord(0, 0);
        Assert.Equal(6, coord.Neighbors().Count());
    }

    [Fact]
    public void Neighbors_AllDistinct() {
        var coord = new HexCoord(2, -1);
        var neighbors = coord.Neighbors().ToList();
        Assert.Equal(6, neighbors.Distinct().Count());
    }

    [Fact]
    public void Distance_ToNeighbor_IsOne() {
        var origin = new HexCoord(0, 0);
        var neighbor = new HexCoord(1, 0);
        Assert.Equal(1, origin.Distance(neighbor));
    }

    [Fact]
    public void Distance_TwoSteps_IsTwo() {
        var origin = new HexCoord(0, 0);
        var far = new HexCoord(2, 0);
        Assert.Equal(2, origin.Distance(far));
    }

    [Fact]
    public void Distance_Diagonal_IsTwo() {
        var origin = new HexCoord(0, 0);
        var diagonal = new HexCoord(1, 1);
        Assert.Equal(2, origin.Distance(diagonal));
    }

    [Fact]
    public void Addition_SumsComponents() {
        var a = new HexCoord(3, -1);
        var b = new HexCoord(-1, 2);
        var result = a + b;
        Assert.Equal(new HexCoord(2, 1), result);
    }

    [Fact]
    public void EqualityByValue() {
        var a = new HexCoord(1, 2);
        var b = new HexCoord(1, 2);
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }
}
