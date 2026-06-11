using MagusWarrior.Core.Types;
using MagusWarrior.Hex;
using Xunit;

namespace MagusWarrior.Tests;

public class TerrainCostsTest {
    [Fact] public void GetCost_Plains_Day_Returns2() =>
        Assert.Equal(2, TerrainCosts.GetCost(TerrainType.Plains, isDay: true));
    [Fact] public void GetCost_Plains_Night_Returns2() =>
        Assert.Equal(2, TerrainCosts.GetCost(TerrainType.Plains, isDay: false));
    [Fact] public void GetCost_Hills_Day_Returns3() =>
        Assert.Equal(3, TerrainCosts.GetCost(TerrainType.Hills, isDay: true));
    [Fact] public void GetCost_Hills_Night_Returns3() =>
        Assert.Equal(3, TerrainCosts.GetCost(TerrainType.Hills, isDay: false));
    [Fact] public void GetCost_Forest_Day_Returns3() =>
        Assert.Equal(3, TerrainCosts.GetCost(TerrainType.Forest, isDay: true));
    [Fact] public void GetCost_Forest_Night_Returns5() =>
        Assert.Equal(5, TerrainCosts.GetCost(TerrainType.Forest, isDay: false));
    [Fact] public void GetCost_Desert_Day_Returns5() =>
        Assert.Equal(5, TerrainCosts.GetCost(TerrainType.Desert, isDay: true));
    [Fact] public void GetCost_Desert_Night_Returns3() =>
        Assert.Equal(3, TerrainCosts.GetCost(TerrainType.Desert, isDay: false));
    [Fact] public void GetCost_Swamp_Day_Returns5() =>
        Assert.Equal(5, TerrainCosts.GetCost(TerrainType.Swamp, isDay: true));
    [Fact] public void GetCost_Swamp_Night_Returns5() =>
        Assert.Equal(5, TerrainCosts.GetCost(TerrainType.Swamp, isDay: false));
    [Fact] public void GetCost_Wasteland_Day_Returns4() =>
        Assert.Equal(4, TerrainCosts.GetCost(TerrainType.Wasteland, isDay: true));
    [Fact] public void GetCost_Wasteland_Night_Returns4() =>
        Assert.Equal(4, TerrainCosts.GetCost(TerrainType.Wasteland, isDay: false));
    [Fact] public void GetCost_Mountain_Day_ReturnsNull() =>
        Assert.Null(TerrainCosts.GetCost(TerrainType.Mountain, isDay: true));
    [Fact] public void GetCost_Mountain_Night_ReturnsNull() =>
        Assert.Null(TerrainCosts.GetCost(TerrainType.Mountain, isDay: false));
    [Fact] public void GetCost_Lake_Day_ReturnsNull() =>
        Assert.Null(TerrainCosts.GetCost(TerrainType.Lake, isDay: true));
    [Fact] public void GetCost_Lake_Night_ReturnsNull() =>
        Assert.Null(TerrainCosts.GetCost(TerrainType.Lake, isDay: false));
    [Fact] public void GetCost_CitySpace_Day_Returns2() =>
        Assert.Equal(2, TerrainCosts.GetCost(TerrainType.CitySpace, isDay: true));
    [Fact] public void GetCost_CitySpace_Night_Returns2() =>
        Assert.Equal(2, TerrainCosts.GetCost(TerrainType.CitySpace, isDay: false));
}
