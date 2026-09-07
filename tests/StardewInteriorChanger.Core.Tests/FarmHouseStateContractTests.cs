using StardewInteriorChanger.Core;

namespace StardewInteriorChanger.Core.Tests;

public sealed class FarmHouseStateContractTests
{
    private static readonly IReadOnlySet<string> NoRenovations = Set();

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void CanonicalDefaultStateIsEligibleAtEachUpgradeLevel(int level)
    {
        Assert.True(FarmHouseStateContract.ValidateState(State(level)).IsValid);
    }

    [Theory]
    [InlineData(false, true, true, true)]
    [InlineData(true, false, true, true)]
    [InlineData(true, true, false, true)]
    [InlineData(true, true, true, false)]
    public void EveryMainFarmhouseIdentityFactIsRequired(
        bool exactType,
        bool canonicalLocation,
        bool masterOwner,
        bool farmAssociation)
    {
        FarmHouseIdentitySnapshot identity = new(
            exactType,
            canonicalLocation,
            masterOwner,
            farmAssociation);

        FarmHouseStateValidation result = FarmHouseStateContract.ValidateState(
            State(1) with { Identity = identity });

        Assert.False(result.IsValid);
        Assert.Equal(FarmHouseStateContract.InvalidIdentity, result.ErrorCode);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public void UnknownUpgradeLevelIsRejected(int level)
    {
        FarmHouseStateValidation result = FarmHouseStateContract.ValidateState(State(level));

        Assert.False(result.IsValid);
        Assert.Equal(FarmHouseStateContract.InvalidUpgradeLevel, result.ErrorCode);
    }

    [Fact]
    public void AnyUpgradeTransitionStateIsRejected()
    {
        AssertTransition(State(1) with { DisplayedUpgradeLevel = 0 });
        AssertTransition(State(1) with { PreviousUpgradeLevel = 0 });
        AssertTransition(State(1) with { DaysUntilHouseUpgrade = 3 });
        AssertTransition(State(1) with { DaysUntilHouseUpgrade = 0 });
        AssertTransition(State(1) with { DaysUntilHouseUpgrade = -2 });
        AssertTransition(State(1) with { DaysUntilHouseUpgrade = int.MinValue });
    }

    [Fact]
    public void SpouseRoomAndChildrenAreSeparateUnsupportedStates()
    {
        FarmHouseStateValidation spouse = FarmHouseStateContract.ValidateState(
            State(2) with { HasNpcSpouseOrRoommate = true });
        FarmHouseStateValidation child = FarmHouseStateContract.ValidateState(
            State(2) with { ChildCount = 1 });

        Assert.Equal(FarmHouseStateContract.SpouseRoomUnsupported, spouse.ErrorCode);
        Assert.Equal(FarmHouseStateContract.ChildrenUnsupported, child.ErrorCode);
    }

    [Fact]
    public void DefaultRenovationAndCribStateAreRequiredOnceThoseSystemsExist()
    {
        FarmHouseStateValidation renovation = FarmHouseStateContract.ValidateState(
            State(2) with
            {
                ActiveOrPendingBuiltInRenovations = Set("renovation_bedroom_open"),
            });
        FarmHouseStateValidation crib = FarmHouseStateContract.ValidateState(
            State(3) with { CribStyle = 2 });

        Assert.Equal(FarmHouseStateContract.RenovationsUnsupported, renovation.ErrorCode);
        Assert.Equal(FarmHouseStateContract.CribStyleUnsupported, crib.ErrorCode);
    }

    [Fact]
    public void LowerTiersDoNotRejectInertHigherTierStateFields()
    {
        FarmHouseStateSnapshot state = State(1) with
        {
            CribStyle = 2,
            ActiveOrPendingBuiltInRenovations = Set("renovation_bedroom_open"),
        };

        Assert.True(FarmHouseStateContract.ValidateState(state).IsValid);
    }

    [Fact]
    public void LevelThreeRequiresAnAssignedCellar()
    {
        FarmHouseStateValidation result = FarmHouseStateContract.ValidateState(
            State(3) with { CellarName = null });

        Assert.False(result.IsValid);
        Assert.Equal(FarmHouseStateContract.CellarUnavailable, result.ErrorCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void DerivedVanillaTopologyIsRecognizedWithoutAuthorizingAnOccupiedSwitch(int level)
    {
        FarmHouseStateSnapshot state = State(level);
        FarmHouseMapSnapshot baseline = Map(level);

        FarmHouseMapValidation result = FarmHouseStateContract.ValidateMap(
            state,
            baseline,
            baseline,
            Set("Bedroom"),
            Set("Bedroom"));

        Assert.True(result.IsValid);
        Assert.True(result.IsVisualTopologyEquivalent);
        Assert.True(result.RequiresVacantLayout);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public void ChangedCollisionTopologyIsClassifiedAsLayoutChange()
    {
        FarmHouseStateSnapshot state = State(1);
        FarmHouseMapSnapshot baseline = Map(1);
        FarmHouseMapSnapshot candidate = baseline with
        {
            StructuralTopologyFingerprint = "different-collision",
        };

        FarmHouseMapValidation result = Validate(state, baseline, candidate);

        Assert.True(result.IsValid);
        Assert.False(result.IsVisualTopologyEquivalent);
        Assert.True(result.RequiresVacantLayout);
    }

    [Fact]
    public void ChangedRealizedOverlaySurfaceIsClassifiedAsLayoutChange()
    {
        FarmHouseStateSnapshot state = State(2);
        FarmHouseMapSnapshot baseline = Map(2);
        FarmHouseMapSnapshot candidate = baseline with
        {
            DynamicSurfaceFingerprint = "different-realized-overlays",
        };

        FarmHouseMapValidation result = Validate(state, baseline, candidate);

        Assert.True(result.IsValid);
        Assert.False(result.IsVisualTopologyEquivalent);
        Assert.True(result.RequiresVacantLayout);
    }

    [Fact]
    public void MovingAFunctionalAnchorRequiresVacantLayoutEvenWhenStillValid()
    {
        FarmHouseStateSnapshot state = State(1);
        FarmHouseMapSnapshot baseline = Map(1);
        FarmHouseMapSnapshot candidate = baseline with
        {
            Anchors = baseline.Anchors with { PlayerBed = new TilePoint(22, 3) },
        };

        FarmHouseMapValidation result = Validate(state, baseline, candidate);

        Assert.True(result.IsValid);
        Assert.False(result.IsVisualTopologyEquivalent);
        Assert.True(result.RequiresVacantLayout);
    }

    [Fact]
    public void ChangedEffectiveDecorationCellsRequireVacantLayout()
    {
        FarmHouseStateSnapshot state = State(2);
        FarmHouseMapSnapshot baseline = Map(2);
        FarmHouseMapSnapshot candidate = baseline with
        {
            Decoration = baseline.Decoration with
            {
                EffectiveTopologyFingerprint = "different-wall-and-floor-cells",
            },
        };

        FarmHouseMapValidation result = Validate(state, baseline, candidate);

        Assert.True(result.IsValid);
        Assert.False(result.IsVisualTopologyEquivalent);
        Assert.True(result.RequiresVacantLayout);
    }

    [Fact]
    public void SavedLatentDecorationIdsNeedDeclarationsButNotActiveMarkers()
    {
        FarmHouseStateSnapshot state = State(2);
        FarmHouseMapSnapshot baseline = Map(2);

        FarmHouseMapValidation result = FarmHouseStateContract.ValidateMap(
            state,
            baseline,
            baseline,
            Set("Bedroom", "Southern"),
            Set("Bedroom", "Southern_Left"));

        Assert.True(result.IsValid);
        Assert.DoesNotContain("Southern", baseline.Decoration.EffectiveFloorIds);
        Assert.DoesNotContain("Southern_Left", baseline.Decoration.EffectiveWallIds);
    }

    [Fact]
    public void MissingSavedLatentDecorationIdIsRejected()
    {
        FarmHouseStateSnapshot state = State(2);
        FarmHouseMapSnapshot baseline = Map(2);
        FarmHouseDecorationSnapshot decoration = baseline.Decoration with
        {
            DeclaredFloorIds = baseline.Decoration.DeclaredFloorIds
                .Where(id => id != "Southern")
                .ToHashSet(StringComparer.Ordinal),
        };

        FarmHouseMapValidation result = FarmHouseStateContract.ValidateMap(
            state,
            baseline,
            baseline with { Decoration = decoration },
            Set("Southern"),
            Set("Bedroom"));

        Assert.False(result.IsValid);
        Assert.Equal(FarmHouseStateContract.SavedDecorationRegionUnavailable, result.ErrorCode);
    }

    [Fact]
    public void EffectiveDecorationIdWithoutDeclarationIsRejected()
    {
        FarmHouseStateSnapshot state = State(1);
        FarmHouseMapSnapshot baseline = Map(1);
        FarmHouseDecorationSnapshot decoration = baseline.Decoration with
        {
            EffectiveWallIds = Set("Bedroom", "Undeclared"),
        };

        FarmHouseMapValidation result = Validate(
            state,
            baseline,
            baseline with { Decoration = decoration });

        Assert.False(result.IsValid);
        Assert.Equal(FarmHouseStateContract.DecorationTopologyInvalid, result.ErrorCode);
    }

    [Fact]
    public void AdditionalRenovationsRemainOutsideTheFirstContract()
    {
        FarmHouseStateSnapshot state = State(2);
        FarmHouseMapSnapshot baseline = Map(2);

        FarmHouseMapValidation result = Validate(
            state,
            baseline,
            baseline with { HasAdditionalRenovations = true });

        Assert.False(result.IsValid);
        Assert.Equal(FarmHouseStateContract.AdditionalRenovationsUnsupported, result.ErrorCode);
    }

    [Fact]
    public void CandidateMustMatchTheLiveUpgradeTier()
    {
        FarmHouseStateSnapshot state = State(2);
        FarmHouseMapSnapshot baseline = Map(2);

        FarmHouseMapValidation result = Validate(state, baseline, Map(1));

        Assert.False(result.IsValid);
        Assert.Equal(FarmHouseStateContract.TierMismatch, result.ErrorCode);
    }

    [Fact]
    public void InvalidDerivedBaselineFailsClosed()
    {
        FarmHouseStateSnapshot state = State(1);
        FarmHouseMapSnapshot baseline = Map(1) with
        {
            StructuralTopologyFingerprint = string.Empty,
        };

        FarmHouseMapValidation result = Validate(state, baseline, Map(1));

        Assert.False(result.IsValid);
        Assert.Equal(FarmHouseStateContract.BaselineInvalid, result.ErrorCode);
    }

    [Fact]
    public void LevelZeroRejectsKitchenFridgeAndChildAnchors()
    {
        FarmHouseStateSnapshot state = State(0);
        FarmHouseMapSnapshot baseline = Map(0);
        FarmHouseAnchors anchors = baseline.Anchors with
        {
            KitchenActions = new[] { new TilePoint(2, 4) },
            KitchenStanding = new TilePoint(2, 5),
            Fridge = Fridge(new TilePoint(3, 4)),
        };

        FarmHouseMapValidation result = Validate(
            state,
            baseline,
            baseline with { Anchors = anchors });

        Assert.False(result.IsValid);
        Assert.Equal(FarmHouseStateContract.FunctionalAnchorsInvalid, result.ErrorCode);
    }

    [Theory]
    [InlineData("wrong sheet", 173)]
    [InlineData("untitled tile sheet", 172)]
    public void FridgeRequiresTheRuntimeSpriteIdentity(string tileSheet, int tileIndex)
    {
        FarmHouseStateSnapshot state = State(1);
        FarmHouseMapSnapshot baseline = Map(1);
        FarmHouseAnchors anchors = baseline.Anchors with
        {
            Fridge = baseline.Anchors.Fridge! with
            {
                TileSheetId = tileSheet,
                TileIndex = tileIndex,
            },
        };

        FarmHouseMapValidation result = Validate(
            state,
            baseline,
            baseline with { Anchors = anchors });

        Assert.False(result.IsValid);
        Assert.Equal(FarmHouseStateContract.FunctionalAnchorsInvalid, result.ErrorCode);
    }

    [Fact]
    public void LevelTwoRequiresBothChildBedMarkersAndFixedCribSurface()
    {
        FarmHouseStateSnapshot state = State(2);
        FarmHouseMapSnapshot baseline = Map(2);

        FarmHouseMapValidation oneChildBed = Validate(
            state,
            baseline,
            baseline with
            {
                Anchors = baseline.Anchors with
                {
                    ChildBeds = new[] { new TilePoint(37, 14) },
                },
            });
        FarmHouseMapValidation movedCrib = Validate(
            state,
            baseline,
            baseline with
            {
                Anchors = baseline.Anchors with
                {
                    CribBounds = new FarmHouseTileArea(31, 12, 3, 4),
                },
            });

        Assert.Equal(FarmHouseStateContract.FunctionalAnchorsInvalid, oneChildBed.ErrorCode);
        Assert.Equal(FarmHouseStateContract.FunctionalAnchorsInvalid, movedCrib.ErrorCode);
    }

    [Fact]
    public void LevelTwoRejectsACribSurfaceClippedByMapBounds()
    {
        FarmHouseStateSnapshot state = State(2);
        FarmHouseMapSnapshot baseline = Map(2);
        FarmHouseAnchors compactAnchors = baseline.Anchors with
        {
            Entry = new TilePoint(27, 30),
            FarmExit = FarmExit(new TilePoint(27, 31)),
            PlayerBed = new TilePoint(20, 22),
            ChildBeds = Points((20, 14), (21, 14)),
            KitchenActions = Points((19, 23)),
            KitchenStanding = new TilePoint(20, 24),
            Fridge = Fridge(new TilePoint(24, 23)),
        };
        FarmHouseMapSnapshot clipped = baseline with
        {
            Width = 32,
            Height = 32,
            Anchors = compactAnchors,
        };

        FarmHouseMapValidation result = Validate(state, baseline, clipped);

        Assert.False(result.IsValid);
        Assert.Equal(FarmHouseStateContract.FunctionalAnchorsInvalid, result.ErrorCode);
    }

    [Fact]
    public void LevelThreeRequiresBothRewrittenCellarReturns()
    {
        FarmHouseStateSnapshot state = State(3);
        FarmHouseMapSnapshot baseline = Map(3);
        FarmHouseWarpAnchor wrongTarget = baseline.Anchors.CellarReturns[0] with
        {
            TargetLocation = "Cellar2",
        };
        FarmHouseMapSnapshot candidate = baseline with
        {
            Anchors = baseline.Anchors with
            {
                CellarReturns = new[] { wrongTarget, baseline.Anchors.CellarReturns[1] },
            },
        };

        FarmHouseMapValidation result = Validate(state, baseline, candidate);

        Assert.False(result.IsValid);
        Assert.Equal(FarmHouseStateContract.FunctionalAnchorsInvalid, result.ErrorCode);
    }

    [Fact]
    public void LevelThreeRejectsCellarReturnTilesOutsideATruncatedMap()
    {
        FarmHouseStateSnapshot state = State(3);
        FarmHouseMapSnapshot baseline = Map(3);
        FarmHouseMapSnapshot truncated = baseline with { Height = 35 };

        FarmHouseMapValidation result = Validate(state, baseline, truncated);

        Assert.False(result.IsValid);
        Assert.Equal(FarmHouseStateContract.FunctionalAnchorsInvalid, result.ErrorCode);
    }

    [Fact]
    public void FarmExitMustUseTheMainFarmhouseDoorContract()
    {
        FarmHouseStateSnapshot state = State(1);
        FarmHouseMapSnapshot baseline = Map(1);
        FarmHouseMapSnapshot candidate = baseline with
        {
            Anchors = baseline.Anchors with
            {
                FarmExit = baseline.Anchors.FarmExit with
                {
                    Destination = new TilePoint(63, 15),
                },
            },
        };

        FarmHouseMapValidation result = Validate(state, baseline, candidate);

        Assert.False(result.IsValid);
        Assert.Equal(FarmHouseStateContract.FunctionalAnchorsInvalid, result.ErrorCode);
    }

    private static FarmHouseMapValidation Validate(
        FarmHouseStateSnapshot state,
        FarmHouseMapSnapshot baseline,
        FarmHouseMapSnapshot candidate) =>
        FarmHouseStateContract.ValidateMap(
            state,
            baseline,
            candidate,
            Set("Bedroom"),
            Set("Bedroom"));

    private static void AssertTransition(FarmHouseStateSnapshot state)
    {
        FarmHouseStateValidation result = FarmHouseStateContract.ValidateState(state);
        Assert.False(result.IsValid);
        Assert.Equal(FarmHouseStateContract.TransitionInProgress, result.ErrorCode);
    }

    private static FarmHouseStateSnapshot State(int level) => new(
        new FarmHouseIdentitySnapshot(true, true, true, true),
        level,
        level,
        -1,
        -1,
        false,
        0,
        1,
        NoRenovations,
        level == 3 ? "Cellar" : null);

    private static FarmHouseMapSnapshot Map(int level)
    {
        (int width, int height, FarmHouseAnchors anchors, FarmHouseDecorationSnapshot decoration) =
            level switch
            {
                0 => (12, 12, LevelZeroAnchors(), Decoration(0)),
                1 => (30, 12, LevelOneAnchors(), Decoration(1)),
                2 => (70, 46, LevelTwoAnchors(), Decoration(2)),
                3 => (70, 46, LevelThreeAnchors(), Decoration(3)),
                _ => (12, 12, LevelZeroAnchors(), Decoration(0)),
            };

        return new FarmHouseMapSnapshot(
            level,
            width,
            height,
            $"structure-{level}",
            $"dynamic-{level}",
            anchors,
            decoration,
            false);
    }

    private static FarmHouseAnchors LevelZeroAnchors() => new(
        new TilePoint(3, 11),
        FarmExit(new TilePoint(3, 12)),
        new TilePoint(9, 8),
        Array.Empty<TilePoint>(),
        Array.Empty<TilePoint>(),
        null,
        null,
        null,
        Array.Empty<FarmHouseWarpAnchor>());

    private static FarmHouseAnchors LevelOneAnchors() => new(
        new TilePoint(9, 11),
        FarmExit(new TilePoint(9, 12)),
        new TilePoint(21, 3),
        Array.Empty<TilePoint>(),
        Points((2, 4), (3, 4), (4, 4), (5, 4)),
        new TilePoint(4, 5),
        Fridge(new TilePoint(6, 4)),
        null,
        Array.Empty<FarmHouseWarpAnchor>());

    private static FarmHouseAnchors LevelTwoAnchors() => new(
        new TilePoint(27, 30),
        FarmExit(new TilePoint(27, 31)),
        new TilePoint(42, 22),
        Points((37, 14), (41, 14)),
        Points((19, 23), (20, 23), (21, 23), (22, 23), (23, 23)),
        new TilePoint(22, 24),
        Fridge(new TilePoint(24, 23)),
        new FarmHouseTileArea(30, 12, 3, 4),
        Array.Empty<FarmHouseWarpAnchor>());

    private static FarmHouseAnchors LevelThreeAnchors() =>
        LevelTwoAnchors() with
        {
            CellarReturns = new[]
            {
                new FarmHouseWarpAnchor(new TilePoint(19, 35), "Cellar", new TilePoint(3, 2)),
                new FarmHouseWarpAnchor(new TilePoint(20, 35), "Cellar", new TilePoint(4, 2)),
            },
        };

    private static FarmHouseWarpAnchor FarmExit(TilePoint source) =>
        new(source, "Farm", new TilePoint(64, 15));

    private static FarmHouseFridgeAnchor Fridge(TilePoint tile) =>
        new(tile, "untitled tile sheet", 173);

    private static FarmHouseDecorationSnapshot Decoration(int level)
    {
        IReadOnlySet<string> active = level switch
        {
            0 => Set("Bedroom"),
            1 => Set("Bedroom", "Hallway_Bedroom", "Kitchen", "LivingRoom"),
            _ => Set(
                "Bedroom",
                "Hallway_Bedroom",
                "Hallway_Nursery",
                "Kitchen",
                "LivingRoom",
                "Nursery",
                "UpperRoom"),
        };
        IReadOnlySet<string> floors = level >= 2
            ? active.Concat(new[] { "Southern", "DiningRoom" }).ToHashSet(StringComparer.Ordinal)
            : active;
        IReadOnlySet<string> walls = level >= 2
            ? active.Concat(new[] { "Southern_Left", "Southern_Right" }).ToHashSet(StringComparer.Ordinal)
            : active;

        return new FarmHouseDecorationSnapshot(
            floors,
            walls,
            active,
            active,
            $"decoration-{level}");
    }

    private static TilePoint[] Points(params (int X, int Y)[] points) =>
        points.Select(point => new TilePoint(point.X, point.Y)).ToArray();

    private static IReadOnlySet<string> Set(params string[] values) =>
        values.ToHashSet(StringComparer.Ordinal);
}
