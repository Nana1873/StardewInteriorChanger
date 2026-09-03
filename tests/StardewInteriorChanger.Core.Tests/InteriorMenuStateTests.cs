using StardewInteriorChanger.Core;

namespace StardewInteriorChanger.Core.Tests;

public sealed class InteriorMenuStateTests
{
    private static readonly ContentHash HashA = ContentHash.Parse(new string('a', 64));
    private static readonly ContentHash HashB = ContentHash.Parse(new string('b', 64));
    private static readonly VariantId GreenhouseA = VariantId.Create("Example.Pack", "greenhouse-a");
    private static readonly VariantId GreenhouseB = VariantId.Create("Example.Pack", "greenhouse-b");
    private static readonly VariantId Barn = VariantId.Create("Example.Pack", "barn");

    [Fact]
    public void Build_FiltersTargetAndPlacesBaseFirstWithDeterministicOrder()
    {
        InteriorMenuView view = InteriorMenuStateBuilder.Build(
            InteriorTarget.Greenhouse,
            new[]
            {
                Variant(Barn, "Barn", InteriorTarget.DeluxeBarn, HashA),
                Variant(GreenhouseB, "Zulu", InteriorTarget.Greenhouse, HashA),
                Variant(GreenhouseA, "Alpha", InteriorTarget.Greenhouse, HashA),
            },
            InteriorMenuStoredChoice.Base());

        Assert.Collection(
            view.Options,
            option =>
            {
                Assert.True(option.IsBase);
                Assert.True(option.IsCurrent);
            },
            option => Assert.Equal(GreenhouseA.Value, option.VariantId),
            option => Assert.Equal(GreenhouseB.Value, option.VariantId));
    }

    [Fact]
    public void Build_MarksOnlyAnExactInstalledChoiceAsCurrent()
    {
        InteriorMenuView view = InteriorMenuStateBuilder.Build(
            InteriorTarget.Greenhouse,
            new[] { Variant(GreenhouseA, "Alpha", InteriorTarget.Greenhouse, HashA) },
            InteriorMenuStoredChoice.Custom(GreenhouseA, HashA));

        Assert.False(view.Options[0].IsCurrent);
        Assert.True(view.Options[1].IsCurrent);
        Assert.Equal(InteriorMenuWarningKind.None, view.Warning.Kind);
    }

    [Fact]
    public void Build_KeepsMissingChoiceAsWarningInsteadOfFallingBackToBase()
    {
        InteriorMenuView view = InteriorMenuStateBuilder.Build(
            InteriorTarget.Greenhouse,
            Array.Empty<InteriorMenuVariant>(),
            InteriorMenuStoredChoice.Custom(GreenhouseA, HashA));

        Assert.DoesNotContain(view.Options, option => option.IsCurrent);
        Assert.Equal(InteriorMenuWarningKind.MissingVariant, view.Warning.Kind);
        Assert.Equal(GreenhouseA.Value, view.Warning.VariantId);
    }

    [Fact]
    public void Build_KeepsHashMismatchAsWarningInsteadOfMarkingInstalledVariant()
    {
        InteriorMenuView view = InteriorMenuStateBuilder.Build(
            InteriorTarget.Greenhouse,
            new[] { Variant(GreenhouseA, "Alpha", InteriorTarget.Greenhouse, HashB) },
            InteriorMenuStoredChoice.Custom(GreenhouseA, HashA));

        Assert.DoesNotContain(view.Options, option => option.IsCurrent);
        Assert.Equal(InteriorMenuWarningKind.ContentHashMismatch, view.Warning.Kind);
    }

    [Fact]
    public void Build_KeepsInvalidStoredDataVisibleAsWarning()
    {
        InteriorMenuView view = InteriorMenuStateBuilder.Build(
            InteriorTarget.Greenhouse,
            Array.Empty<InteriorMenuVariant>(),
            InteriorMenuStoredChoice.Invalid("bad data"));

        Assert.False(view.Options[0].IsCurrent);
        Assert.Equal(InteriorMenuWarningKind.InvalidStoredSelection, view.Warning.Kind);
        Assert.Equal("bad data", view.Warning.Detail);
    }

    [Fact]
    public void RequestTracker_BlocksASecondPendingRequest()
    {
        var tracker = new InteriorMenuRequestTracker();

        Assert.True(tracker.TryBegin("building-a", GreenhouseA.Value, out InteriorMenuRequest first));
        Assert.False(tracker.TryBegin("building-b", GreenhouseB.Value, out InteriorMenuRequest returned));

        Assert.Same(first, returned);
        Assert.Same(first, tracker.Pending);
    }

    [Fact]
    public void RequestTracker_IgnoresNonMatchingAndLateResults()
    {
        var tracker = new InteriorMenuRequestTracker();
        tracker.TryBegin("building-a", GreenhouseA.Value, out _);

        Assert.False(tracker.TryComplete("building-a", GreenhouseB.Value, out _));
        Assert.NotNull(tracker.Pending);
        Assert.True(tracker.TryComplete("building-a", GreenhouseA.Value, out _));
        Assert.True(tracker.TryBegin("building-b", GreenhouseB.Value, out InteriorMenuRequest newer));
        Assert.False(tracker.TryComplete("building-a", GreenhouseA.Value, out _));
        Assert.Same(newer, tracker.Pending);
    }

    private static InteriorMenuVariant Variant(
        VariantId id,
        string name,
        InteriorTarget target,
        ContentHash hash) => new(
            id,
            name,
            target,
            hash,
            "Example.Pack",
            "1.0.0",
            null);
}
