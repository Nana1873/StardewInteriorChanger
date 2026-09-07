namespace StardewInteriorChanger.Core;

public sealed record AnimalHouseTargetContract(
    InteriorTarget Target,
    string BuildingType,
    string MapAsset,
    int Capacity,
    bool RequiresAutoFeed,
    TilePoint FeedHopperTile,
    bool HasIncubator,
    string OccupantType);

public static class AnimalHouseTargetContracts
{
    public static IReadOnlyList<AnimalHouseTargetContract> All { get; } = Array.AsReadOnly(new[]
    {
        new AnimalHouseTargetContract(InteriorTarget.Barn, "Barn", "Maps/Barn", 4, false, new(6, 3), false, "Barn"),
        new AnimalHouseTargetContract(InteriorTarget.BigBarn, "Big Barn", "Maps/Barn2", 8, false, new(6, 3), false, "Barn"),
        new AnimalHouseTargetContract(InteriorTarget.DeluxeBarn, "Deluxe Barn", "Maps/Barn3", 12, true, new(6, 3), false, "Barn"),
        new AnimalHouseTargetContract(InteriorTarget.Coop, "Coop", "Maps/Coop", 4, false, new(3, 3), false, "Coop"),
        new AnimalHouseTargetContract(InteriorTarget.BigCoop, "Big Coop", "Maps/Coop2", 8, false, new(3, 3), true, "Coop"),
        new AnimalHouseTargetContract(InteriorTarget.DeluxeCoop, "Deluxe Coop", "Maps/Coop3", 12, true, new(3, 3), true, "Coop")
    });

    public static bool TryGet(InteriorTarget target, out AnimalHouseTargetContract contract)
    {
        contract = All.FirstOrDefault(candidate => candidate.Target == target)!;
        return contract is not null;
    }

    public static InteriorTarget? ForBuildingType(string? buildingType) =>
        All.FirstOrDefault(candidate => candidate.BuildingType == buildingType)?.Target;
}
