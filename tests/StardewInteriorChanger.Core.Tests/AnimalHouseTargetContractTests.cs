using StardewInteriorChanger.Core;

namespace StardewInteriorChanger.Core.Tests;

public sealed class AnimalHouseTargetContractTests
{
    [Theory]
    [InlineData(InteriorTarget.Barn, "Barn", "Maps/Barn", 4, false, 6, false)]
    [InlineData(InteriorTarget.BigBarn, "Big Barn", "Maps/Barn2", 8, false, 6, false)]
    [InlineData(InteriorTarget.DeluxeBarn, "Deluxe Barn", "Maps/Barn3", 12, true, 6, false)]
    [InlineData(InteriorTarget.Coop, "Coop", "Maps/Coop", 4, false, 3, false)]
    [InlineData(InteriorTarget.BigCoop, "Big Coop", "Maps/Coop2", 8, false, 3, true)]
    [InlineData(InteriorTarget.DeluxeCoop, "Deluxe Coop", "Maps/Coop3", 12, true, 3, true)]
    public void ContractsMatchReviewedGameCapacityFeedingAndFixtureDifferences(
        InteriorTarget target, string buildingType, string map, int capacity, bool autoFeed, int hopperX, bool incubator)
    {
        Assert.True(AnimalHouseTargetContracts.TryGet(target, out AnimalHouseTargetContract contract));
        Assert.Equal(target, AnimalHouseTargetContracts.ForBuildingType(buildingType));
        Assert.Equal(map, contract.MapAsset);
        Assert.Equal(capacity, contract.Capacity);
        Assert.Equal(autoFeed, contract.RequiresAutoFeed);
        Assert.Equal(new TilePoint(hopperX, 3), contract.FeedHopperTile);
        Assert.Equal(incubator, contract.HasIncubator);

        var hopper = new ObjectFixtureState
        {
            QualifiedItemId = "(BC)99", DictionaryTile = new(hopperX, 3), ObjectTile = new(hopperX, 3),
            IsPlainObject = true, Fragility = 2, Stack = 1
        };
        Assert.True(InteriorFixturePolicy.IsBuiltInObjectFixture(target, true, 1, hopper));
        Assert.False(InteriorFixturePolicy.IsBuiltInObjectFixture(target, true, 1,
            hopper with { ObjectTile = new(hopperX == 6 ? 3 : 6, 3) }));
        Assert.False(InteriorFixturePolicy.IsBuiltInObjectFixture(target, true, 1,
            hopper with { HasHeldObject = true }));
    }

    [Theory]
    [InlineData("BigBarn")]
    [InlineData("coop")]
    [InlineData("DeluxeCoop")]
    [InlineData("SVE_PremiumBarn")]
    [InlineData("Shed")]
    [InlineData(null)]
    public void UnknownOrNoncanonicalBuildingNamesDoNotBecomeAnimalTargets(string? name)
    {
        Assert.Null(AnimalHouseTargetContracts.ForBuildingType(name));
    }

    [Fact]
    public void NewWireValuesDoNotReuseExistingOrReservedTargets()
    {
        Assert.Equal(0, (int)InteriorTarget.Greenhouse);
        Assert.Equal(1, (int)InteriorTarget.DeluxeBarn);
        Assert.Equal(4, (int)InteriorTarget.Barn);
        Assert.Equal(5, (int)InteriorTarget.BigBarn);
        Assert.Equal(6, (int)InteriorTarget.Coop);
        Assert.Equal(7, (int)InteriorTarget.BigCoop);
        Assert.Equal(8, (int)InteriorTarget.DeluxeCoop);
        Assert.False(AnimalHouseTargetContracts.TryGet((InteriorTarget)2, out _));
        Assert.False(AnimalHouseTargetContracts.TryGet((InteriorTarget)3, out _));
        Assert.False(AnimalHouseTargetContracts.TryGet(InteriorTarget.Greenhouse, out _));
        Assert.Equal("greenhouse/v1", TargetContracts.For(InteriorTarget.Greenhouse).Value);
        Assert.Equal("deluxe-barn/v1", TargetContracts.For(InteriorTarget.DeluxeBarn).Value);
    }

    [Fact]
    public void IdenticalMapBytesCannotSubstituteForAnotherAnimalTier()
    {
        var hashes = new HashSet<ContentHash>();
        var contracts = new HashSet<TargetContractId>();
        foreach (AnimalHouseTargetContract target in AnimalHouseTargetContracts.All)
        {
            var document = new InteriorPackDocument
            {
                FormatVersion = 1,
                Interiors = new() { new InteriorDefinitionDto
                {
                    Id = "interior", DisplayName = "Interior", Target = target.Target.ToString(),
                    GameplayRoot = "assets/interior", Map = "interior.tmx"
                } }
            };
            RegistryBuildResult result = new InteriorRegistryBuilder().Build("Example.Pack", document,
                MemoryPackFileSystem.Create(("assets/interior/interior.tmx", "same map bytes")));
            RegisteredInterior entry = Assert.Single(result.Registry.Entries);
            Assert.Equal(target.Target, entry.Target);
            Assert.True(hashes.Add(entry.ContentHash));
            Assert.True(contracts.Add(entry.TargetContract));
        }
    }

    [Theory]
    [InlineData("4")]
    [InlineData("Big Barn")]
    [InlineData("bigbarn")]
    public void PackTargetsRequireTheExactContractName(string target)
    {
        var document = new InteriorPackDocument
        {
            FormatVersion = 1,
            Interiors = new() { new InteriorDefinitionDto
            {
                Id = "interior", DisplayName = "Interior", Target = target,
                GameplayRoot = "assets/interior", Map = "interior.tmx"
            } }
        };
        RegistryBuildResult result = new InteriorRegistryBuilder().Build("Example.Pack", document,
            MemoryPackFileSystem.Create(("assets/interior/interior.tmx", "map bytes")));
        Assert.Empty(result.Registry.Entries);
        Assert.Contains(result.Diagnostics, item => item.Code == RegistryDiagnosticCode.InvalidTarget);
    }

    [Fact]
    public void OldPeerIsRejectedEvenWhenNoCustomInteriorIsSelected()
    {
        PeerCompatibilityResult result = PeerCompatibilityEvaluator.Evaluate(InteriorProtocol.Major,
            InteriorProtocol.Minor, Array.Empty<VariantFingerprint>(), Array.Empty<VariantId>(),
            new[] { new PeerRegistrySnapshot("old-peer", 1, 99, Array.Empty<VariantFingerprint>()) });
        Assert.False(result.IsCompatible);
        Assert.Null(result.NegotiatedProtocolMinor);
        Assert.Equal(PeerCompatibilityIssueCode.ProtocolMajorMismatch, Assert.Single(result.Issues).Code);
    }

    [Fact]
    public void IncubatorAndHopperMustBothRemainAccessibleWithoutCrossingTheOther()
    {
        var fixtures = new[] { new TilePoint(2, 3), new TilePoint(3, 3) };
        Assert.True(RetainedFixtureReachabilityPolicy.CanPreserve(6, 6, new(2, 5), fixtures, _ => true, _ => true));
        // Only a one-tile horizontal corridor remains; reaching the hopper would
        // require walking through the incubator rather than an adjacent free tile.
        Assert.False(RetainedFixtureReachabilityPolicy.CanPreserve(6, 6, new(0, 3), fixtures,
            point => point.Y == 3, _ => true));
    }
}
