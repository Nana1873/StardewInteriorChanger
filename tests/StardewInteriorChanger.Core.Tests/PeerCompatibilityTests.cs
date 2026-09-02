using StardewInteriorChanger.Core;

namespace StardewInteriorChanger.Core.Tests;

public sealed class PeerCompatibilityTests
{
    private static readonly VariantId GreenhouseId = VariantId.Create("Example.Interiors", "greenhouse");
    private static readonly VariantId BarnId = VariantId.Create("Example.Interiors", "barn");
    private static readonly ContentHash HashA = ContentHash.Parse(new string('a', 64));
    private static readonly ContentHash HashB = ContentHash.Parse(new string('b', 64));

    [Fact]
    public void Evaluate_ExactActiveVariantAndExtraClientVariant_IsCompatible()
    {
        VariantFingerprint greenhouse = Fingerprint(GreenhouseId, InteriorTarget.Greenhouse, HashA);
        VariantFingerprint extra = Fingerprint(
            VariantId.Create("Other.Pack", "extra"),
            InteriorTarget.Greenhouse,
            HashB);

        PeerCompatibilityResult result = PeerCompatibilityEvaluator.Evaluate(
            1,
            3,
            new[] { greenhouse },
            new[] { GreenhouseId },
            new[] { Peer("farmhand", 1, 2, greenhouse, extra) });

        Assert.True(result.IsCompatible);
        Assert.Equal((ushort)2, result.NegotiatedProtocolMinor);
        Assert.Equal(new[] { GreenhouseId }, result.AvailableToAll);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Evaluate_MissingInactiveHostVariant_AllowsSessionButRemovesAvailability()
    {
        VariantFingerprint greenhouse = Fingerprint(GreenhouseId, InteriorTarget.Greenhouse, HashA);
        VariantFingerprint barn = Fingerprint(BarnId, InteriorTarget.DeluxeBarn, HashB);

        PeerCompatibilityResult result = PeerCompatibilityEvaluator.Evaluate(
            1,
            0,
            new[] { greenhouse, barn },
            new[] { GreenhouseId },
            new[] { Peer("farmhand", 1, 0, greenhouse) });

        Assert.True(result.IsCompatible);
        Assert.Equal(new[] { GreenhouseId }, result.AvailableToAll);
    }

    [Fact]
    public void Evaluate_MissingRequiredVariant_FailsClosed()
    {
        VariantFingerprint greenhouse = Fingerprint(GreenhouseId, InteriorTarget.Greenhouse, HashA);

        PeerCompatibilityResult result = PeerCompatibilityEvaluator.Evaluate(
            1,
            0,
            new[] { greenhouse },
            new[] { GreenhouseId },
            new[] { Peer("farmhand", 1, 0) });

        Assert.False(result.IsCompatible);
        Assert.Empty(result.AvailableToAll);
        Assert.Equal(
            PeerCompatibilityIssueCode.MissingRequiredVariant,
            Assert.Single(result.Issues).Code);
    }

    [Fact]
    public void Evaluate_SameIdWithDifferentHash_FailsClosed()
    {
        VariantFingerprint host = Fingerprint(GreenhouseId, InteriorTarget.Greenhouse, HashA);
        VariantFingerprint client = Fingerprint(GreenhouseId, InteriorTarget.Greenhouse, HashB);

        PeerCompatibilityResult result = PeerCompatibilityEvaluator.Evaluate(
            1,
            0,
            new[] { host },
            new[] { GreenhouseId },
            new[] { Peer("farmhand", 1, 0, client) });

        Assert.False(result.IsCompatible);
        Assert.Equal(
            PeerCompatibilityIssueCode.ContentHashMismatch,
            Assert.Single(result.Issues).Code);
    }

    [Fact]
    public void Evaluate_ProtocolMajorMismatch_HasNoNegotiatedMinor()
    {
        VariantFingerprint greenhouse = Fingerprint(GreenhouseId, InteriorTarget.Greenhouse, HashA);

        PeerCompatibilityResult result = PeerCompatibilityEvaluator.Evaluate(
            1,
            4,
            new[] { greenhouse },
            new[] { GreenhouseId },
            new[] { Peer("farmhand", 2, 1, greenhouse) });

        Assert.False(result.IsCompatible);
        Assert.Null(result.NegotiatedProtocolMinor);
        Assert.Empty(result.AvailableToAll);
        Assert.Equal(
            PeerCompatibilityIssueCode.ProtocolMajorMismatch,
            Assert.Single(result.Issues).Code);
    }

    [Fact]
    public void Evaluate_ReorderedPeersAndVariants_ProducesSameDeterministicResult()
    {
        VariantFingerprint greenhouse = Fingerprint(GreenhouseId, InteriorTarget.Greenhouse, HashA);
        VariantFingerprint wrongBarn = Fingerprint(BarnId, InteriorTarget.Greenhouse, HashB);
        VariantFingerprint hostBarn = Fingerprint(BarnId, InteriorTarget.DeluxeBarn, HashB);

        PeerRegistrySnapshot alpha = Peer("alpha", 1, 2, greenhouse, wrongBarn);
        PeerRegistrySnapshot beta = Peer("beta", 1, 1, greenhouse);

        PeerCompatibilityResult first = PeerCompatibilityEvaluator.Evaluate(
            1,
            3,
            new[] { hostBarn, greenhouse },
            new[] { BarnId },
            new[] { beta, alpha });
        PeerCompatibilityResult second = PeerCompatibilityEvaluator.Evaluate(
            1,
            3,
            new[] { greenhouse, hostBarn },
            new[] { BarnId },
            new[] { alpha, beta });

        Assert.Equal(first.IsCompatible, second.IsCompatible);
        Assert.Equal(first.NegotiatedProtocolMinor, second.NegotiatedProtocolMinor);
        Assert.Equal(first.AvailableToAll, second.AvailableToAll);
        Assert.Equal(first.Issues, second.Issues);
    }

    private static VariantFingerprint Fingerprint(
        VariantId id,
        InteriorTarget target,
        ContentHash hash) => new(id, target, hash);

    private static PeerRegistrySnapshot Peer(
        string peerId,
        ushort major,
        ushort minor,
        params VariantFingerprint[] variants) => new(peerId, major, minor, variants);
}
