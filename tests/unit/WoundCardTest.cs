using MagusWarrior.Cards;
using Xunit;

namespace MagusWarrior.Tests;

public class WoundCardTest {
    [Fact]
    public void Create_ProducesWoundType() {
        Assert.Equal(CardType.Wound, WoundCard.Create().Type);
    }

    [Fact]
    public void Create_HasNoUnpoweredSpec() {
        Assert.Null(WoundCard.Create().Unpowered);
    }

    [Fact]
    public void Create_HasNoManaCost() {
        Assert.Null(WoundCard.Create().ManaCost);
    }
}
