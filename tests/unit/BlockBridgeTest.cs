using System.Collections.Generic;
using System.Linq;
using MagusWarrior.Broker;
using MagusWarrior.Combat;
using MagusWarrior.Core.Types;
using Xunit;

namespace MagusWarrior.Tests;

public class BlockBridgeTest {
    private static Dictionary<AttackElement, int> Pool(
        params (AttackElement el, int v)[] entries) {
        var d = new Dictionary<AttackElement, int>();
        foreach (var (el, val) in entries) d[el] = val;
        return d;
    }

    [Fact]
    public void Extract_Physical_MapsCorrectly() {
        var result = BlockBridge.ExtractBlockContributions(Pool((AttackElement.Physical, 4)));
        var c      = Assert.Single(result);
        Assert.Equal(BlockType.Physical, c.Type);
        Assert.Equal(4,                  c.Value);
    }

    [Fact]
    public void Extract_Ice_MapsCorrectly() {
        var result = BlockBridge.ExtractBlockContributions(Pool((AttackElement.Ice, 3)));
        var c      = Assert.Single(result);
        Assert.Equal(BlockType.Ice, c.Type);
        Assert.Equal(3,             c.Value);
    }

    [Fact]
    public void Extract_ColdFire_MapsCorrectly() {
        var result = BlockBridge.ExtractBlockContributions(Pool((AttackElement.ColdFire, 2)));
        var c      = Assert.Single(result);
        Assert.Equal(BlockType.ColdFire, c.Type);
        Assert.Equal(2,                  c.Value);
    }

    [Fact]
    public void Extract_ExcludesZeroValues() {
        var result = BlockBridge.ExtractBlockContributions(Pool((AttackElement.Fire, 0)));
        Assert.Empty(result);
    }

    [Fact]
    public void Extract_EmptyPool_ReturnsEmpty() {
        var result = BlockBridge.ExtractBlockContributions(new Dictionary<AttackElement, int>());
        Assert.Empty(result);
    }

    [Fact]
    public void Extract_MultipleEntries_AllMapped() {
        var pool   = Pool((AttackElement.Physical, 4), (AttackElement.Ice, 3),
                          (AttackElement.Fire, 0), (AttackElement.ColdFire, 2));
        var result = BlockBridge.ExtractBlockContributions(pool).ToList();
        Assert.Equal(3, result.Count);
        Assert.Contains(result, c => c.Type == BlockType.Physical && c.Value == 4);
        Assert.Contains(result, c => c.Type == BlockType.Ice      && c.Value == 3);
        Assert.Contains(result, c => c.Type == BlockType.ColdFire && c.Value == 2);
    }
}
