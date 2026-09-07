using StardewInteriorChanger.Core;

namespace StardewInteriorChanger.Core.Tests;

public sealed class InteriorFixturePolicyTests
{
    private static readonly ObjectFixtureState BuiltInHopper = new()
    {
        QualifiedItemId = "(BC)99",
        DictionaryTile = new TilePoint(6, 3),
        ObjectTile = new TilePoint(6, 3),
        IsPlainObject = true,
        Fragility = 2,
        Stack = 1,
    };

    [Fact]
    public void SingleInertIndestructibleHopperAtBuildingEquipmentTileIsExempt()
    {
        Assert.True(IsFixture(BuiltInHopper));
    }

    [Theory]
    [InlineData(InteriorTarget.Greenhouse, true, 1)]
    [InlineData(InteriorTarget.DeluxeBarn, false, 1)]
    [InlineData(InteriorTarget.DeluxeBarn, true, 0)]
    [InlineData(InteriorTarget.DeluxeBarn, true, 2)]
    public void WrongLocationOrAmbiguousHopperCountBlocks(
        InteriorTarget target, bool isAnimalHouse, int count)
    {
        Assert.False(InteriorFixturePolicy.IsBuiltInObjectFixture(
            target, isAnimalHouse, count, BuiltInHopper));
    }

    [Theory]
    [InlineData("(BC)12")]
    [InlineData("(BC)163")]
    [InlineData("(BC)164")]
    [InlineData("(O)388")]
    [InlineData(null)]
    public void OrdinaryMachinesCasksAndOtherObjectsNeverUseFixtureException(string? itemId)
    {
        Assert.False(IsFixture(BuiltInHopper with { QualifiedItemId = itemId }));
    }

    [Fact]
    public void PlayerPlacedOrRelocatedHopperIsNotRecognizedAsEquipment()
    {
        Assert.False(IsFixture(BuiltInHopper with { Fragility = 0 }));
        Assert.False(IsFixture(BuiltInHopper with { DictionaryTile = new TilePoint(7, 3) }));
        Assert.False(IsFixture(BuiltInHopper with { ObjectTile = new TilePoint(7, 3) }));
        Assert.False(IsFixture(BuiltInHopper with { DictionaryTile = null }));
        Assert.False(IsFixture(BuiltInHopper with { ObjectTile = null }));
        Assert.False(IsFixture(BuiltInHopper with { Stack = 2 }));
    }

    [Fact]
    public void HopperWithProcessingOrStoredItemsMustBlockTheSwitch()
    {
        Assert.False(IsFixture(BuiltInHopper with { HasHeldObject = true }));
        Assert.False(IsFixture(BuiltInHopper with { MinutesUntilReady = 10 }));
        Assert.False(IsFixture(BuiltInHopper with { MinutesUntilReady = -1 }));
        Assert.False(IsFixture(BuiltInHopper with { ReadyForHarvest = true }));
        Assert.False(IsFixture(BuiltInHopper with { HasLastInput = true }));
        Assert.False(IsFixture(BuiltInHopper with { HasLastOutputRule = true }));
    }

    [Fact]
    public void ModdedEquipmentMustBlockEvenWhenItsCurrentOutputIsEmpty()
    {
        Assert.False(IsFixture(BuiltInHopper with { IsPlainObject = false }));
        Assert.False(IsFixture(BuiltInHopper with { HasModData = true }));
        Assert.False(IsFixture(BuiltInHopper with { HasMachineData = true }));
    }

    [Fact]
    public void MissingObservationDoesNotQualifyAsBuiltInEquipment()
    {
        Assert.False(IsFixture(new ObjectFixtureState { QualifiedItemId = "(BC)99" }));
    }

    private static bool IsFixture(ObjectFixtureState state) =>
        InteriorFixturePolicy.IsBuiltInObjectFixture(InteriorTarget.DeluxeBarn, true, 1, state);
}
