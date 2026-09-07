namespace StardewInteriorChanger.Core;

public static class InstalledSourceRoutePolicy
{
    public static bool CanOfferReturn(
        bool worldReady,
        bool isFarmGreenhouse,
        bool validSelection,
        bool quarantined,
        bool cellarSnapshot,
        bool fingerprintMatches,
        bool loadedMapMatches,
        bool peerCanLoad) =>
        worldReady
        && isFarmGreenhouse
        && validSelection
        && !quarantined
        && cellarSnapshot
        && fingerprintMatches
        && loadedMapMatches
        && peerCanLoad;
}
