using System.Collections.Generic;
using System.Linq;
using MagusWarrior.Broker;
using MagusWarrior.Combat;
using MagusWarrior.Core.Types;
using Xunit;

namespace MagusWarrior.Tests;

public class AttackBridgeTest {
    private static Dictionary<(EffectType Distance, AttackElement Element), int> Pool(
        params (EffectType d, AttackElement el, int v)[] entries) {
        var d = new Dictionary<(EffectType, AttackElement), int>();
        foreach (var (dist, el, val) in entries) d[(dist, el)] = val;
        return d;
    }

    [Fact]
    public void Extract_RangedPhysical_MapsCorrectly() {
        var pool   = Pool((EffectType.AttackRanged, AttackElement.Physical, 4));
        var result = AttackBridge.ExtractRangedContributions(pool);
        var c      = Assert.Single(result);
        Assert.Equal(AttackType.Physical,    c.Type);
        Assert.Equal(AttackDelivery.Ranged,  c.Delivery);
        Assert.Equal(4,                      c.Value);
    }

    [Fact]
    public void Extract_SiegeFire_MapsCorrectly() {
        var pool   = Pool((EffectType.AttackSiege, AttackElement.Fire, 3));
        var result = AttackBridge.ExtractRangedContributions(pool);
        var c      = Assert.Single(result);
        Assert.Equal(AttackType.Fire,        c.Type);
        Assert.Equal(AttackDelivery.Siege,   c.Delivery);
        Assert.Equal(3,                      c.Value);
    }

    [Fact]
    public void Extract_IgnoresMeleeEntries() {
        var pool   = Pool((EffectType.AttackMelee, AttackElement.Physical, 5));
        var result = AttackBridge.ExtractRangedContributions(pool);
        Assert.Empty(result);
    }

    [Fact]
    public void Extract_ExcludesZeroValues() {
        var pool   = Pool((EffectType.AttackRanged, AttackElement.Ice, 0));
        var result = AttackBridge.ExtractRangedContributions(pool);
        Assert.Empty(result);
    }

    [Fact]
    public void Extract_EmptyPool_ReturnsEmpty() {
        var result = AttackBridge.ExtractRangedContributions(
            new Dictionary<(EffectType, AttackElement), int>());
        Assert.Empty(result);
    }

    [Fact]
    public void Extract_MultipleEntries_AllMapped() {
        var pool = Pool(
            (EffectType.AttackRanged, AttackElement.Physical,  4),
            (EffectType.AttackSiege,  AttackElement.ColdFire,  2),
            (EffectType.AttackMelee,  AttackElement.Physical,  5),
            (EffectType.AttackRanged, AttackElement.Ice,       0));
        var result = AttackBridge.ExtractRangedContributions(pool).ToList();
        Assert.Equal(2, result.Count);
        Assert.Contains(result, c => c.Type == AttackType.Physical  && c.Delivery == AttackDelivery.Ranged && c.Value == 4);
        Assert.Contains(result, c => c.Type == AttackType.ColdFire  && c.Delivery == AttackDelivery.Siege  && c.Value == 2);
    }
}
