using MagusWarrior.Core.Types;
using MagusWarrior.Map;
using Xunit;

namespace MagusWarrior.Tests;

public class TileStockTest {
    [Fact]
    public void Constructor_SetsInitialCounts() {
        var stock = new TileStock(8, 3);
        Assert.Equal(8, stock.CountrysideRemaining);
        Assert.Equal(3, stock.CoreRemaining);
    }

    [Fact]
    public void RecordReveal_Countryside_DecrementsCountrysideOnly() {
        var stock = new TileStock(8, 3);
        stock.RecordReveal(TileType.Countryside);
        Assert.Equal(7, stock.CountrysideRemaining);
        Assert.Equal(3, stock.CoreRemaining);
    }

    [Fact]
    public void RecordReveal_Core_DecrementsCoreOnly() {
        var stock = new TileStock(8, 3);
        stock.RecordReveal(TileType.Core);
        Assert.Equal(8, stock.CountrysideRemaining);
        Assert.Equal(2, stock.CoreRemaining);
    }

    [Fact]
    public void RecordReveal_Starting_DoesNotDecrement() {
        var stock = new TileStock(8, 3);
        stock.RecordReveal(TileType.Starting);
        Assert.Equal(8, stock.CountrysideRemaining);
        Assert.Equal(3, stock.CoreRemaining);
    }

    [Fact]
    public void RecordReveal_Countryside_FiresChanged() {
        var stock = new TileStock(8, 3);
        bool fired = false;
        stock.Changed += () => fired = true;
        stock.RecordReveal(TileType.Countryside);
        Assert.True(fired);
    }

    [Fact]
    public void RecordReveal_Core_FiresChanged() {
        var stock = new TileStock(8, 3);
        bool fired = false;
        stock.Changed += () => fired = true;
        stock.RecordReveal(TileType.Core);
        Assert.True(fired);
    }

    [Fact]
    public void RecordReveal_AtFloor_StillFiresChanged() {
        var stock = new TileStock(0, 3);  // already at floor
        bool fired = false;
        stock.Changed += () => fired = true;
        stock.RecordReveal(TileType.Countryside);
        Assert.True(fired);                       // AC7: fires even at floor
        Assert.Equal(0, stock.CountrysideRemaining);  // AC6: never goes negative
    }

    [Fact]
    public void RecordReveal_Starting_DoesNotFireChanged() {
        var stock = new TileStock(8, 3);
        bool fired = false;
        stock.Changed += () => fired = true;
        stock.RecordReveal(TileType.Starting);
        Assert.False(fired);
    }

    [Fact]
    public void RecordReveal_Countryside_FloorsAtZero() {
        var stock = new TileStock(1, 3);
        stock.RecordReveal(TileType.Countryside);
        stock.RecordReveal(TileType.Countryside);  // already at 0
        Assert.Equal(0, stock.CountrysideRemaining);
    }
}
