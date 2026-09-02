using StardewInteriorChanger.Core;

namespace StardewInteriorChanger.Core.Tests;

public sealed class InteriorFixturePolicyTests
{
    [Fact]
    public void DeluxeBarnFeedHopperIsABuiltInFixture()
    {
        Assert.True(InteriorFixturePolicy.IsBuiltInObjectFixture(
            InteriorTarget.DeluxeBarn,
            InteriorFixturePolicy.DeluxeBarnFeedHopperId));
    }

    [Theory]
    [InlineData(InteriorTarget.Greenhouse, "(BC)99")]
    [InlineData(InteriorTarget.DeluxeBarn, "(O)388")]
    [InlineData(InteriorTarget.DeluxeBarn, null)]
    public void OtherObjectsStillBlock(
        InteriorTarget target,
        string? qualifiedItemId)
    {
        Assert.False(InteriorFixturePolicy.IsBuiltInObjectFixture(
            target,
            qualifiedItemId));
    }
}
