namespace StardewInteriorChanger.Core;

public sealed record IncubatorFixtureContext
{
    public string? BuildingType { get; init; }
    public bool IsAnimalHouse { get; init; }
    public bool HasReviewedBuildingData { get; init; }
    public int IncubatorCount { get; init; }
}

public sealed record IncubatorMachineState
{
    public bool IsIncubator { get; init; }
    public bool OnlyCompleteOvernight { get; init; }
    public bool AllowLoadWhenFull { get; init; }
    public bool HasInteractMethod { get; init; }
    public int OutputRuleCount { get; init; }
    public string? OutputRuleId { get; init; }
    public int TriggerCount { get; init; }
    public bool IsItemPlacedInMachineTrigger { get; init; }
    public int OutputItemCount { get; init; }
    public string? OutputMethod { get; init; }
    public bool RecalculateOnCollect { get; init; }

    // The runtime must compare every remaining MachineData field with the reviewed
    // vanilla (BC)101 configuration, including nested conditions and modifiers.
    // Unknown or additional behavior must not be reduced to IsIncubator alone.
    public bool MatchesRemainingVanillaConfiguration { get; init; }
}

public sealed record IncubatorFixtureState
{
    public string? QualifiedItemId { get; init; }
    // A missing tile also represents an unobserved or fractional runtime position.
    public TilePoint? DictionaryTile { get; init; }
    public TilePoint? ObjectTile { get; init; }
    public bool IsPlainObject { get; init; }
    public bool IsBigCraftable { get; init; }
    public bool IsRecipe { get; init; }
    public int Fragility { get; init; }
    public int Stack { get; init; }
    public bool HasModData { get; init; }
    public bool ShowNextIndex { get; init; }
    public bool HasLightSource { get; init; }
    public bool HasHeldObject { get; init; }
    public int MinutesUntilReady { get; init; }
    public bool ReadyForHarvest { get; init; }
    public bool HasLastInput { get; init; }
    public string? LastOutputRuleId { get; init; }
    public IncubatorMachineState? Machine { get; init; }
}

public enum IncubatorFixtureClassification
{
    UnsupportedFixture,
    Processing,
    ReadyToHatch,
    InconsistentState,
    IdleBuiltIn
}

public static class IncubatorFixturePolicy
{
    public const string ItemId = "(BC)101";
    public const string DefaultOutputRuleId = "Default";
    public const string VanillaOutputMethod = "StardewValley.Object, Stardew Valley: OutputIncubator";
    public static readonly TilePoint FixtureTile = new(2, 3);

    // This classifier is for a new layout selection. Exact saved-map restoration
    // must preserve active incubation and use its separate transient safety gate.
    public static IncubatorFixtureClassification Classify(
        IncubatorFixtureContext context,
        IncubatorFixtureState state)
    {
        // Data/Buildings installs one plain, indestructible object at this tile.
        // No saved provenance tag exists; this conservative tuple is not proof of origin.
        if (context.BuildingType is not ("Big Coop" or "Deluxe Coop")
            || !context.IsAnimalHouse || !context.HasReviewedBuildingData
            || context.IncubatorCount != 1
            || state.QualifiedItemId != ItemId
            || state.DictionaryTile != FixtureTile || state.ObjectTile != FixtureTile
            || !state.IsPlainObject || !state.IsBigCraftable || state.IsRecipe
            || state.Fragility != 2 || state.Stack != 1
            || state.HasModData || state.ShowNextIndex || state.HasLightSource
            || !IsReviewedMachine(state.Machine))
            return IncubatorFixtureClassification.UnsupportedFixture;

        // AnimalHouse.resetSharedState checks the held egg and timer, not the
        // readyForHarvest flag. A full house can retain a ready egg indefinitely.
        if (state.HasHeldObject)
            return state.MinutesUntilReady > 0
                ? IncubatorFixtureClassification.Processing
                : IncubatorFixtureClassification.ReadyToHatch;

        if (state.MinutesUntilReady != 0 || state.ReadyForHarvest)
            return IncubatorFixtureClassification.InconsistentState;

        bool isUnused = !state.HasLastInput && string.IsNullOrEmpty(state.LastOutputRuleId);
        // AnimalHouse.addNewHatchedAnimal clears only heldObject. OutputMachine's
        // Default rule and last input survive a completed hatch; they are inert
        // history under this reviewed machine configuration and must be retained.
        bool hasCompletedHistory = state.HasLastInput && state.LastOutputRuleId == DefaultOutputRuleId;
        return isUnused || hasCompletedHistory
            ? IncubatorFixtureClassification.IdleBuiltIn
            : IncubatorFixtureClassification.InconsistentState;
    }

    private static bool IsReviewedMachine(IncubatorMachineState? machine) =>
        machine is
        {
            IsIncubator: true,
            OnlyCompleteOvernight: true,
            AllowLoadWhenFull: false,
            HasInteractMethod: false,
            OutputRuleCount: 1,
            OutputRuleId: DefaultOutputRuleId,
            TriggerCount: 1,
            IsItemPlacedInMachineTrigger: true,
            OutputItemCount: 1,
            OutputMethod: VanillaOutputMethod,
            RecalculateOnCollect: false,
            MatchesRemainingVanillaConfiguration: true
        };
}
