namespace StardewInteriorChanger.Core;

public sealed record ObjectFixtureState
{
    public string? QualifiedItemId { get; init; }
    public TilePoint? DictionaryTile { get; init; }
    public TilePoint? ObjectTile { get; init; }
    public bool IsPlainObject { get; init; }
    public int Fragility { get; init; }
    public int Stack { get; init; }
    public bool HasHeldObject { get; init; }
    public int MinutesUntilReady { get; init; }
    public bool ReadyForHarvest { get; init; }
    public bool HasLastInput { get; init; }
    public bool HasLastOutputRule { get; init; }
    public bool HasModData { get; init; }
    public bool HasMachineData { get; init; }
}

public static class InteriorFixturePolicy
{
    public const string DeluxeBarnFeedHopperId = "(BC)99";
    public static readonly TilePoint DeluxeBarnFeedHopperTile = new(6, 3);

    public static bool IsBuiltInObjectFixture(
        InteriorTarget target,
        bool isAnimalHouse,
        int feedHopperCount,
        ObjectFixtureState state) =>
        target == InteriorTarget.DeluxeBarn
        && isAnimalHouse
        && feedHopperCount == 1
        && string.Equals(
            state.QualifiedItemId,
            DeluxeBarnFeedHopperId,
            StringComparison.Ordinal)
        // Data/Buildings creates Default_FeedHopper at this tile with fragility 2.
        // The save has no provenance tag, so only this conservative tuple is exempt.
        && state.DictionaryTile == DeluxeBarnFeedHopperTile
        && state.ObjectTile == DeluxeBarnFeedHopperTile
        && state.IsPlainObject
        && state.Fragility == 2
        && state.Stack == 1
        && !state.HasHeldObject
        && state.MinutesUntilReady == 0
        && !state.ReadyForHarvest
        && !state.HasLastInput
        && !state.HasLastOutputRule
        && !state.HasModData
        && !state.HasMachineData;
}
