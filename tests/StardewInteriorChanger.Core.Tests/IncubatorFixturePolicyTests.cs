using StardewInteriorChanger.Core;

namespace StardewInteriorChanger.Core.Tests;

public sealed class IncubatorFixturePolicyTests
{
    private static readonly IncubatorFixtureContext Context = new()
    {
        BuildingType = "Big Coop", IsAnimalHouse = true,
        HasReviewedBuildingData = true, IncubatorCount = 1
    };

    private static readonly IncubatorMachineState Machine = new()
    {
        IsIncubator = true, OnlyCompleteOvernight = true,
        OutputRuleCount = 1, OutputRuleId = "Default",
        TriggerCount = 1, IsItemPlacedInMachineTrigger = true,
        OutputItemCount = 1,
        OutputMethod = "StardewValley.Object, Stardew Valley: OutputIncubator",
        MatchesRemainingVanillaConfiguration = true
    };

    private static readonly IncubatorFixtureState Idle = new()
    {
        QualifiedItemId = "(BC)101", DictionaryTile = new(2, 3), ObjectTile = new(2, 3),
        IsPlainObject = true, IsBigCraftable = true, Fragility = 2, Stack = 1,
        Machine = Machine
    };

    [Theory]
    [InlineData("Big Coop")]
    [InlineData("Deluxe Coop")]
    public void FreshCanonicalEquipmentCanRemainInEitherUpgradedCoop(string buildingType)
    {
        Assert.Equal(IncubatorFixtureClassification.IdleBuiltIn,
            IncubatorFixturePolicy.Classify(Context with { BuildingType = buildingType }, Idle));
    }

    [Fact]
    public void ACompletedHatchRetainsInputAndDefaultRuleHistoryWithoutBecomingAnActiveMachine()
    {
        IncubatorFixtureState completed = Idle with { HasLastInput = true, LastOutputRuleId = "Default" };
        Assert.Equal(IncubatorFixtureClassification.IdleBuiltIn, Classify(completed));
        Assert.Equal(IncubatorFixtureClassification.Processing,
            Classify(completed with { HasHeldObject = true, MinutesUntilReady = 4500 }));
        Assert.Equal(IncubatorFixtureClassification.ReadyToHatch,
            Classify(completed with { HasHeldObject = true, MinutesUntilReady = 0 }));
        Assert.True(completed.HasLastInput);
        Assert.Equal("Default", completed.LastOutputRuleId);
    }

    [Theory]
    [InlineData(9000, false, IncubatorFixtureClassification.Processing)]
    [InlineData(10, true, IncubatorFixtureClassification.Processing)]
    [InlineData(0, false, IncubatorFixtureClassification.ReadyToHatch)]
    [InlineData(0, true, IncubatorFixtureClassification.ReadyToHatch)]
    [InlineData(-10, false, IncubatorFixtureClassification.ReadyToHatch)]
    public void HeldEggBlocksIncludingReadyEggWithoutHarvestFlag(
        int minutes, bool ready, IncubatorFixtureClassification expected)
    {
        Assert.Equal(expected, Classify(Idle with
        {
            HasHeldObject = true, MinutesUntilReady = minutes, ReadyForHarvest = ready,
            HasLastInput = true, LastOutputRuleId = "Default"
        }));
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    public void EmptyMachineWithResidualActiveStateMustNotBeNormalizedByTheSelector(int minutes, bool ready)
    {
        Assert.Equal(IncubatorFixtureClassification.InconsistentState,
            Classify(Idle with { MinutesUntilReady = minutes, ReadyForHarvest = ready }));
    }

    [Theory]
    [InlineData(true, null)]
    [InlineData(true, "")]
    [InlineData(true, "Custom")]
    [InlineData(true, "default")]
    [InlineData(false, "Default")]
    [InlineData(false, "Custom")]
    public void IncompleteOrUnknownHistoryIsNotTreatedAsCompletedVanillaIncubation(bool hasInput, string? rule)
    {
        Assert.Equal(IncubatorFixtureClassification.InconsistentState,
            Classify(Idle with { HasLastInput = hasInput, LastOutputRuleId = rule }));
    }

    [Theory]
    [InlineData("Coop")]
    [InlineData("Barn")]
    [InlineData("Deluxe Barn")]
    [InlineData("Shed")]
    [InlineData("BigCoop")]
    [InlineData("big coop")]
    [InlineData(null)]
    public void OtherTargetsCannotBorrowTheIncubatorException(string? buildingType)
    {
        Assert.Equal(IncubatorFixtureClassification.UnsupportedFixture,
            IncubatorFixturePolicy.Classify(Context with { BuildingType = buildingType }, Idle));
    }

    [Theory]
    [InlineData(0, true, true)]
    [InlineData(2, true, true)]
    [InlineData(1, false, true)]
    [InlineData(1, true, false)]
    public void DuplicateEquipmentOrUnreviewedLocationCannotQualify(int count, bool animalHouse, bool reviewed)
    {
        Assert.Equal(IncubatorFixtureClassification.UnsupportedFixture,
            IncubatorFixturePolicy.Classify(Context with
            {
                IncubatorCount = count, IsAnimalHouse = animalHouse, HasReviewedBuildingData = reviewed
            }, Idle));
    }

    public static IEnumerable<object[]> ModifiedEquipment()
    {
        yield return new object[] { Idle with { QualifiedItemId = "(BC)156" } };
        yield return new object[] { Idle with { QualifiedItemId = "(BC)163" } };
        yield return new object[] { Idle with { DictionaryTile = new(3, 3) } };
        yield return new object[] { Idle with { ObjectTile = new(3, 3) } };
        yield return new object[] { Idle with { DictionaryTile = null } };
        yield return new object[] { Idle with { ObjectTile = null } };
        yield return new object[] { Idle with { IsPlainObject = false } };
        yield return new object[] { Idle with { IsBigCraftable = false } };
        yield return new object[] { Idle with { IsRecipe = true } };
        yield return new object[] { Idle with { Fragility = 0 } };
        yield return new object[] { Idle with { Stack = 2 } };
        yield return new object[] { Idle with { HasModData = true } };
        yield return new object[] { Idle with { ShowNextIndex = true } };
        yield return new object[] { Idle with { HasLightSource = true } };
    }

    [Theory]
    [MemberData(nameof(ModifiedEquipment))]
    public void RelocatedStatefulOrModdedEquipmentDoesNotReceiveAnException(IncubatorFixtureState state)
    {
        Assert.Equal(IncubatorFixtureClassification.UnsupportedFixture, Classify(state));
    }

    public static IEnumerable<object[]> UnreviewedMachineConfigurations()
    {
        yield return new object[] { Machine with { IsIncubator = false } };
        yield return new object[] { Machine with { OnlyCompleteOvernight = false } };
        yield return new object[] { Machine with { AllowLoadWhenFull = true } };
        yield return new object[] { Machine with { HasInteractMethod = true } };
        yield return new object[] { Machine with { OutputRuleCount = 2 } };
        yield return new object[] { Machine with { OutputRuleId = "Custom" } };
        yield return new object[] { Machine with { TriggerCount = 2 } };
        yield return new object[] { Machine with { IsItemPlacedInMachineTrigger = false } };
        yield return new object[] { Machine with { OutputItemCount = 2 } };
        yield return new object[] { Machine with { OutputMethod = "AnotherMod: ProduceOutput" } };
        yield return new object[] { Machine with { RecalculateOnCollect = true } };
        yield return new object[] { Machine with { MatchesRemainingVanillaConfiguration = false } };
    }

    [Theory]
    [MemberData(nameof(UnreviewedMachineConfigurations))]
    public void ExtraOrChangedMachineBehaviorCannotUseTheHistoricalInputAllowance(IncubatorMachineState machine)
    {
        Assert.Equal(IncubatorFixtureClassification.UnsupportedFixture, Classify(Idle with
        {
            Machine = machine, HasLastInput = true, LastOutputRuleId = "Default"
        }));
    }

    [Fact]
    public void MissingObservationFailsClosed()
    {
        Assert.Equal(IncubatorFixtureClassification.UnsupportedFixture, Classify(new IncubatorFixtureState()));
        Assert.Equal(IncubatorFixtureClassification.UnsupportedFixture, Classify(Idle with { Machine = null }));
        Assert.Equal(IncubatorFixtureClassification.UnsupportedFixture, Classify(Idle with { Machine = new() }));
        Assert.Equal(IncubatorFixtureClassification.UnsupportedFixture,
            IncubatorFixturePolicy.Classify(new IncubatorFixtureContext(), Idle));
    }

    private static IncubatorFixtureClassification Classify(IncubatorFixtureState state) =>
        IncubatorFixturePolicy.Classify(Context, state);
}
