using StardewInteriorChanger.Core;

namespace StardewInteriorChanger.Core.Tests;

public sealed class ShedTargetTests
{
    [Fact]
    public void ExistingWireValuesAreStableAndShedTiersHaveSeparateContracts()
    {
        Assert.Equal(0, (int)InteriorTarget.Greenhouse);
        Assert.Equal(1, (int)InteriorTarget.DeluxeBarn);
        Assert.Equal(2, (int)InteriorTarget.Shed);
        Assert.Equal(3, (int)InteriorTarget.BigShed);
        Assert.NotEqual(TargetContracts.For(InteriorTarget.Shed), TargetContracts.For(InteriorTarget.BigShed));
        Assert.Equal(InteriorTarget.Shed, TargetContracts.ForFarmBuildingType("Shed"));
        Assert.Equal(InteriorTarget.BigShed, TargetContracts.ForFarmBuildingType("Big Shed"));
    }

    [Theory]
    [InlineData("shed")]
    [InlineData("BigShed")]
    [InlineData("Cabin")]
    [InlineData("Farmhouse")]
    public void UnreviewedBuildingTypesAreNotNewShedTargets(string buildingType)
    {
        Assert.Null(TargetContracts.ForFarmBuildingType(buildingType));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void LegacyAndShedCapablePeersCannotNegotiateEvenWithoutActiveVariants(bool currentHost, bool active)
    {
        VariantId id = VariantId.Create("Example.Pack", "shed");
        var variant = new VariantFingerprint(id, InteriorTarget.Shed, ContentHash.Parse(new string('a', 64)));
        ushort host = currentHost ? InteriorProtocol.Major : (ushort)1;
        ushort client = currentHost ? (ushort)1 : InteriorProtocol.Major;
        PeerCompatibilityResult result = PeerCompatibilityEvaluator.Evaluate(host, InteriorProtocol.Minor,
            new[] { variant }, active ? new[] { id } : Array.Empty<VariantId>(),
            new[] { new PeerRegistrySnapshot("peer", client, 99, new[] { variant }) });
        Assert.False(result.IsCompatible);
        Assert.Null(result.NegotiatedProtocolMinor);
        Assert.Empty(result.AvailableToAll);
        Assert.Equal(PeerCompatibilityIssueCode.ProtocolMajorMismatch, Assert.Single(result.Issues).Code);
    }

    [Fact]
    public void SameProtocolCannotSubstituteOtherShedTierForRequiredVariant()
    {
        VariantId id = VariantId.Create("Example.Pack", "shed");
        ContentHash hash = ContentHash.Parse(new string('a', 64));
        PeerCompatibilityResult result = PeerCompatibilityEvaluator.Evaluate(InteriorProtocol.Major, InteriorProtocol.Minor,
            new[] { new VariantFingerprint(id, InteriorTarget.Shed, hash) }, new[] { id },
            new[] { new PeerRegistrySnapshot("peer", InteriorProtocol.Major, InteriorProtocol.Minor,
                new[] { new VariantFingerprint(id, InteriorTarget.BigShed, hash) }) });
        Assert.False(result.IsCompatible);
        Assert.Empty(result.AvailableToAll);
        Assert.Equal(PeerCompatibilityIssueCode.TargetMismatch, Assert.Single(result.Issues).Code);
    }
}
