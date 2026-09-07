namespace StardewInteriorChanger.Core;

public sealed record FarmHouseIdentitySnapshot(
    bool IsExactFarmHouseType,
    bool IsCanonicalMainLocation,
    bool IsMasterPlayerOwner,
    bool IsAssociatedWithFarm);

public sealed record FarmHouseStateSnapshot(
    FarmHouseIdentitySnapshot Identity,
    int UpgradeLevel,
    int DisplayedUpgradeLevel,
    int PreviousUpgradeLevel,
    int DaysUntilHouseUpgrade,
    bool HasNpcSpouseOrRoommate,
    int ChildCount,
    int CribStyle,
    IReadOnlySet<string> ActiveOrPendingBuiltInRenovations,
    string? CellarName);

public readonly record struct FarmHouseTileArea(int X, int Y, int Width, int Height);

public sealed record FarmHouseWarpAnchor(
    TilePoint Source,
    string TargetLocation,
    TilePoint Destination);

public sealed record FarmHouseFridgeAnchor(
    TilePoint Tile,
    string TileSheetId,
    int TileIndex);

public sealed record FarmHouseAnchors(
    TilePoint Entry,
    FarmHouseWarpAnchor FarmExit,
    TilePoint PlayerBed,
    IReadOnlyList<TilePoint> ChildBeds,
    IReadOnlyList<TilePoint> KitchenActions,
    TilePoint? KitchenStanding,
    FarmHouseFridgeAnchor? Fridge,
    FarmHouseTileArea? CribBounds,
    IReadOnlyList<FarmHouseWarpAnchor> CellarReturns);

public sealed record FarmHouseDecorationSnapshot(
    IReadOnlySet<string> DeclaredFloorIds,
    IReadOnlySet<string> DeclaredWallIds,
    IReadOnlySet<string> EffectiveFloorIds,
    IReadOnlySet<string> EffectiveWallIds,
    string EffectiveTopologyFingerprint);

public sealed record FarmHouseMapSnapshot(
    int UpgradeLevel,
    int Width,
    int Height,
    string StructuralTopologyFingerprint,
    string DynamicSurfaceFingerprint,
    FarmHouseAnchors Anchors,
    FarmHouseDecorationSnapshot Decoration,
    bool HasAdditionalRenovations);

public sealed record FarmHouseStateValidation(bool IsValid, string? ErrorCode)
{
    internal static FarmHouseStateValidation Valid { get; } = new(true, null);

    internal static FarmHouseStateValidation Rejected(string errorCode) =>
        new(false, errorCode);
}

public sealed record FarmHouseMapValidation(
    bool IsValid,
    bool IsVisualTopologyEquivalent,
    bool RequiresVacantLayout,
    string? ErrorCode)
{
    internal static FarmHouseMapValidation Accepted(bool visualTopologyEquivalent) =>
        // Visual equivalence is geometric evidence only. A separate runtime
        // occupancy policy must explicitly authorize any retained contents.
        new(true, visualTopologyEquivalent, true, null);

    internal static FarmHouseMapValidation Rejected(string errorCode) =>
        new(false, false, true, errorCode);
}

/// <summary>
/// Validates immutable, game-free facts collected from the canonical main farmhouse.
/// This policy does not register a target or mutate a live location.
/// </summary>
public static class FarmHouseStateContract
{
    public const string InvalidIdentity = "invalid-main-farmhouse-identity";
    public const string InvalidUpgradeLevel = "invalid-upgrade-level";
    public const string TransitionInProgress = "house-transition-in-progress";
    public const string SpouseRoomUnsupported = "spouse-room-unsupported";
    public const string ChildrenUnsupported = "children-unsupported";
    public const string RenovationsUnsupported = "renovations-unsupported";
    public const string CribStyleUnsupported = "crib-style-unsupported";
    public const string CellarUnavailable = "cellar-unavailable";
    public const string BaselineInvalid = "baseline-invalid";
    public const string TierMismatch = "tier-mismatch";
    public const string MapShapeInvalid = "map-shape-invalid";
    public const string FunctionalAnchorsInvalid = "functional-anchors-invalid";
    public const string AdditionalRenovationsUnsupported = "additional-renovations-unsupported";
    public const string DecorationTopologyInvalid = "decoration-topology-invalid";
    public const string SavedDecorationRegionUnavailable = "saved-decoration-region-unavailable";

    private static readonly FarmHouseTileArea DefaultCribBounds = new(30, 12, 3, 4);
    private static readonly TilePoint FirstCellarReturn = new(19, 35);
    private static readonly TilePoint SecondCellarReturn = new(20, 35);

    public static FarmHouseStateValidation ValidateState(FarmHouseStateSnapshot state)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(state.Identity);
        ArgumentNullException.ThrowIfNull(state.ActiveOrPendingBuiltInRenovations);

        FarmHouseIdentitySnapshot identity = state.Identity;
        if (!identity.IsExactFarmHouseType
            || !identity.IsCanonicalMainLocation
            || !identity.IsMasterPlayerOwner
            || !identity.IsAssociatedWithFarm)
        {
            return FarmHouseStateValidation.Rejected(InvalidIdentity);
        }

        if (state.UpgradeLevel is < 0 or > 3)
            return FarmHouseStateValidation.Rejected(InvalidUpgradeLevel);

        if (state.DisplayedUpgradeLevel != state.UpgradeLevel
            || state.PreviousUpgradeLevel != -1
            || state.DaysUntilHouseUpgrade != -1)
        {
            return FarmHouseStateValidation.Rejected(TransitionInProgress);
        }

        if (state.HasNpcSpouseOrRoommate)
            return FarmHouseStateValidation.Rejected(SpouseRoomUnsupported);

        if (state.ChildCount != 0)
            return FarmHouseStateValidation.Rejected(ChildrenUnsupported);

        if (state.UpgradeLevel >= 2)
        {
            if (state.ActiveOrPendingBuiltInRenovations.Count != 0)
                return FarmHouseStateValidation.Rejected(RenovationsUnsupported);

            if (state.CribStyle != 1)
                return FarmHouseStateValidation.Rejected(CribStyleUnsupported);
        }

        if (state.UpgradeLevel == 3 && string.IsNullOrWhiteSpace(state.CellarName))
            return FarmHouseStateValidation.Rejected(CellarUnavailable);

        return FarmHouseStateValidation.Valid;
    }

    public static FarmHouseMapValidation ValidateMap(
        FarmHouseStateSnapshot state,
        FarmHouseMapSnapshot vanillaBaseline,
        FarmHouseMapSnapshot candidate,
        IReadOnlySet<string> savedFloorIds,
        IReadOnlySet<string> savedWallIds)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(vanillaBaseline);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(savedFloorIds);
        ArgumentNullException.ThrowIfNull(savedWallIds);

        FarmHouseStateValidation stateValidation = ValidateState(state);
        if (!stateValidation.IsValid)
            return FarmHouseMapValidation.Rejected(stateValidation.ErrorCode!);

        if (vanillaBaseline.UpgradeLevel != state.UpgradeLevel
            || !HasValidMapShape(vanillaBaseline)
            || vanillaBaseline.HasAdditionalRenovations
            || !HasValidDecorationTopology(vanillaBaseline.Decoration)
            || !HasValidFunctionalAnchors(state, vanillaBaseline))
        {
            return FarmHouseMapValidation.Rejected(BaselineInvalid);
        }

        if (candidate.UpgradeLevel != state.UpgradeLevel)
            return FarmHouseMapValidation.Rejected(TierMismatch);

        if (!HasValidMapShape(candidate))
            return FarmHouseMapValidation.Rejected(MapShapeInvalid);

        if (candidate.HasAdditionalRenovations)
            return FarmHouseMapValidation.Rejected(AdditionalRenovationsUnsupported);

        if (!HasValidFunctionalAnchors(state, candidate))
            return FarmHouseMapValidation.Rejected(FunctionalAnchorsInvalid);

        if (!HasValidDecorationTopology(candidate.Decoration))
            return FarmHouseMapValidation.Rejected(DecorationTopologyInvalid);

        if (!IsSubset(savedFloorIds, candidate.Decoration.DeclaredFloorIds)
            || !IsSubset(savedWallIds, candidate.Decoration.DeclaredWallIds))
        {
            return FarmHouseMapValidation.Rejected(SavedDecorationRegionUnavailable);
        }

        bool visualTopologyEquivalent = candidate.Width == vanillaBaseline.Width
            && candidate.Height == vanillaBaseline.Height
            && string.Equals(
                candidate.StructuralTopologyFingerprint,
                vanillaBaseline.StructuralTopologyFingerprint,
                StringComparison.Ordinal)
            && string.Equals(
                candidate.DynamicSurfaceFingerprint,
                vanillaBaseline.DynamicSurfaceFingerprint,
                StringComparison.Ordinal)
            && SameAnchors(candidate.Anchors, vanillaBaseline.Anchors)
            && SameDecorationTopology(candidate.Decoration, vanillaBaseline.Decoration);

        return FarmHouseMapValidation.Accepted(visualTopologyEquivalent);
    }

    private static bool HasValidMapShape(FarmHouseMapSnapshot map) =>
        map.Width > 0
        && map.Height > 0
        && !string.IsNullOrWhiteSpace(map.StructuralTopologyFingerprint)
        && !string.IsNullOrWhiteSpace(map.DynamicSurfaceFingerprint)
        && map.Anchors is not null
        && map.Decoration is not null;

    private static bool HasValidDecorationTopology(FarmHouseDecorationSnapshot decoration) =>
        decoration.DeclaredFloorIds is not null
        && decoration.DeclaredWallIds is not null
        && decoration.EffectiveFloorIds is not null
        && decoration.EffectiveWallIds is not null
        && !string.IsNullOrWhiteSpace(decoration.EffectiveTopologyFingerprint)
        && HasOnlyNamedIds(decoration.DeclaredFloorIds)
        && HasOnlyNamedIds(decoration.DeclaredWallIds)
        && HasOnlyNamedIds(decoration.EffectiveFloorIds)
        && HasOnlyNamedIds(decoration.EffectiveWallIds)
        && IsSubset(decoration.EffectiveFloorIds, decoration.DeclaredFloorIds)
        && IsSubset(decoration.EffectiveWallIds, decoration.DeclaredWallIds);

    private static bool HasOnlyNamedIds(IEnumerable<string> ids) =>
        ids.All(id => !string.IsNullOrWhiteSpace(id));

    private static bool HasValidFunctionalAnchors(
        FarmHouseStateSnapshot state,
        FarmHouseMapSnapshot map)
    {
        FarmHouseAnchors anchors = map.Anchors;
        if (!IsInside(anchors.Entry, map.Width, map.Height)
            || !IsInside(anchors.PlayerBed, map.Width, map.Height)
            || anchors.FarmExit.Source.X < 0
            || anchors.FarmExit.Source.X >= map.Width
            || anchors.FarmExit.Source.Y <= 0
            || anchors.FarmExit.Source.Y > map.Height
            || anchors.FarmExit.Source.X != anchors.Entry.X
            || anchors.FarmExit.Source.Y - 1 != anchors.Entry.Y
            || !string.Equals(anchors.FarmExit.TargetLocation, "Farm", StringComparison.Ordinal)
            || anchors.FarmExit.Destination != new TilePoint(64, 15)
            || anchors.ChildBeds.Any(tile => !IsInside(tile, map.Width, map.Height))
            || anchors.KitchenActions.Any(tile => !IsInside(tile, map.Width, map.Height)))
        {
            return false;
        }

        if (map.UpgradeLevel == 0)
        {
            return anchors.ChildBeds.Count == 0
                && anchors.KitchenActions.Count == 0
                && anchors.KitchenStanding is null
                && anchors.Fridge is null
                && anchors.CribBounds is null
                && anchors.CellarReturns.Count == 0;
        }

        if (anchors.KitchenActions.Count == 0
            || anchors.KitchenStanding is not { } kitchenStanding
            || !IsInside(kitchenStanding, map.Width, map.Height)
            || anchors.Fridge is not { } fridge
            || !IsInside(fridge.Tile, map.Width, map.Height)
            || !string.Equals(fridge.TileSheetId, "untitled tile sheet", StringComparison.Ordinal)
            || fridge.TileIndex != 173)
        {
            return false;
        }

        if (map.UpgradeLevel == 1)
        {
            return anchors.ChildBeds.Count == 0
                && anchors.CribBounds is null
                && anchors.CellarReturns.Count == 0;
        }

        if (anchors.ChildBeds.Count != 2
            || anchors.ChildBeds.Distinct().Count() != 2
            || anchors.CribBounds != DefaultCribBounds
            || !IsInside(DefaultCribBounds, map.Width, map.Height))
        {
            return false;
        }

        if (map.UpgradeLevel == 2)
            return anchors.CellarReturns.Count == 0;

        return anchors.CellarReturns.All(warp => IsInside(warp.Source, map.Width, map.Height))
            && HasValidCellarReturns(state.CellarName!, anchors.CellarReturns);
    }

    private static bool HasValidCellarReturns(
        string cellarName,
        IReadOnlyList<FarmHouseWarpAnchor> returns)
    {
        if (returns.Count != 2)
            return false;

        FarmHouseWarpAnchor[] first = returns.Where(warp => warp.Source == FirstCellarReturn).ToArray();
        FarmHouseWarpAnchor[] second = returns.Where(warp => warp.Source == SecondCellarReturn).ToArray();
        return first.Length == 1
            && second.Length == 1
            && string.Equals(first[0].TargetLocation, cellarName, StringComparison.Ordinal)
            && string.Equals(second[0].TargetLocation, cellarName, StringComparison.Ordinal)
            && first[0].Destination == new TilePoint(3, 2)
            && second[0].Destination == new TilePoint(4, 2);
    }

    private static bool SameAnchors(FarmHouseAnchors left, FarmHouseAnchors right) =>
        left.Entry == right.Entry
        && left.FarmExit == right.FarmExit
        && left.PlayerBed == right.PlayerBed
        && SameSet(left.ChildBeds, right.ChildBeds)
        && SameSet(left.KitchenActions, right.KitchenActions)
        && left.KitchenStanding == right.KitchenStanding
        && left.Fridge == right.Fridge
        && left.CribBounds == right.CribBounds
        && SameSet(left.CellarReturns, right.CellarReturns);

    private static bool SameDecorationTopology(
        FarmHouseDecorationSnapshot left,
        FarmHouseDecorationSnapshot right) =>
        string.Equals(
            left.EffectiveTopologyFingerprint,
            right.EffectiveTopologyFingerprint,
            StringComparison.Ordinal)
        && left.DeclaredFloorIds.SetEquals(right.DeclaredFloorIds)
        && left.DeclaredWallIds.SetEquals(right.DeclaredWallIds)
        && left.EffectiveFloorIds.SetEquals(right.EffectiveFloorIds)
        && left.EffectiveWallIds.SetEquals(right.EffectiveWallIds);

    private static bool SameSet<T>(IEnumerable<T> left, IEnumerable<T> right) =>
        left.ToHashSet().SetEquals(right);

    private static bool IsSubset(IReadOnlySet<string> subset, IReadOnlySet<string> superset) =>
        subset.All(superset.Contains);

    private static bool IsInside(TilePoint tile, int width, int height) =>
        tile.X >= 0 && tile.Y >= 0 && tile.X < width && tile.Y < height;

    private static bool IsInside(FarmHouseTileArea area, int width, int height) =>
        area.X >= 0
        && area.Y >= 0
        && area.Width > 0
        && area.Height > 0
        && area.X <= width - area.Width
        && area.Y <= height - area.Height;
}
