using StardewInteriorChanger.Core;

namespace StardewInteriorChanger.Core.Tests;

public sealed class InstalledSourceRoutePolicyTests
{
    [Theory]
    [InlineData(false, true, true, false, true, true, true, true)]
    [InlineData(true, false, true, false, true, true, true, true)]
    [InlineData(true, true, false, false, true, true, true, true)]
    [InlineData(true, true, true, true, true, true, true, true)]
    [InlineData(true, true, true, false, false, true, true, true)]
    [InlineData(true, true, true, false, true, false, true, true)]
    [InlineData(true, true, true, false, true, true, false, true)]
    [InlineData(true, true, true, false, true, true, true, false)]
    public void ReturnIsUnavailableWhenAnyRequiredCheckFails(
        bool worldReady,
        bool isFarmGreenhouse,
        bool validSelection,
        bool quarantined,
        bool cellarSnapshot,
        bool fingerprintMatches,
        bool loadedMapMatches,
        bool peerCanLoad)
    {
        Assert.False(InstalledSourceRoutePolicy.CanOfferReturn(
            worldReady,
            isFarmGreenhouse,
            validSelection,
            quarantined,
            cellarSnapshot,
            fingerprintMatches,
            loadedMapMatches,
            peerCanLoad));
    }

    [Fact]
    public void RetainedCellarSnapshotCanOfferReturnWhenEveryRuntimeCheckPasses()
    {
        // Current source settings do not invalidate an exact, still-active snapshot.
        Assert.True(InstalledSourceRoutePolicy.CanOfferReturn(
            worldReady: true,
            isFarmGreenhouse: true,
            validSelection: true,
            quarantined: false,
            cellarSnapshot: true,
            fingerprintMatches: true,
            loadedMapMatches: true,
            peerCanLoad: true));
    }
}
