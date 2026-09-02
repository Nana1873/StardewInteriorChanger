using StardewInteriorChanger.Core;
using StardewValley;
using StardewValley.Buildings;

namespace StardewInteriorChanger;

internal sealed record SwitchSafetyResult(IReadOnlyList<string> Blockers)
{
    public bool IsSafe => Blockers.Count == 0;

    public string ToUserMessage() => string.Join("; ", Blockers);
}

internal static class SwitchSafetyInspector
{
    public static SwitchSafetyResult Inspect(
        Building building,
        GameLocation indoors,
        InteriorTarget target)
    {
        List<string> blockers = GetTransientBlockers(building, indoors);

        int placedObjects = indoors.objects.Pairs.Count(pair =>
            !InteriorFixturePolicy.IsBuiltInObjectFixture(
                target,
                pair.Value.QualifiedItemId));
        AddCount(blockers, placedObjects, "placed object(s)");
        AddCount(blockers, indoors.furniture.Count, "piece(s) of furniture");
        AddCount(blockers, indoors.terrainFeatures.Count(), "terrain feature(s) or crop(s)");
        AddCount(blockers, indoors.resourceClumps.Count, "resource clump(s)");
        AddCount(blockers, indoors.largeTerrainFeatures.Count, "large terrain feature(s)");
        AddCount(blockers, indoors.debris.Count, "debris item(s)");
        AddCount(blockers, indoors.characters.Count, "location character(s)");

        if (target == InteriorTarget.DeluxeBarn)
        {
            if (indoors is not AnimalHouse animalHouse)
            {
                blockers.Add("the Deluxe Barn interior isn't an AnimalHouse location");
            }
            else
            {
                AddCount(
                    blockers,
                    animalHouse.animalsThatLiveHere.Count,
                    "assigned farm animal(s)");
            }

            int homeAnimals = Game1.getFarm().Animals.Values.Count(animal =>
                ReferenceEquals(animal.home, building)
                || ReferenceEquals(animal.homeInterior, indoors));
            if (homeAnimals > 0
                && (indoors is not AnimalHouse house
                    || house.animalsThatLiveHere.Count == 0))
            {
                AddCount(blockers, homeAnimals, "assigned farm animal(s)");
            }
        }

        return new SwitchSafetyResult(blockers);
    }

    public static SwitchSafetyResult InspectExactSaveRestore(
        Building building,
        GameLocation indoors) => new(GetTransientBlockers(building, indoors));

    private static List<string> GetTransientBlockers(
        Building building,
        GameLocation indoors)
    {
        var blockers = new List<string>();

        if (building.daysOfConstructionLeft.Value > 0
            || building.daysUntilUpgrade.Value > 0
            || !string.IsNullOrWhiteSpace(building.upgradeName.Value))
        {
            blockers.Add("the building is being constructed or upgraded");
        }

        int playersInside = Game1.getOnlineFarmers().Count(farmer =>
            ReferenceEquals(farmer.currentLocation, indoors)
            || string.Equals(
                farmer.currentLocation?.NameOrUniqueName,
                indoors.NameOrUniqueName,
                StringComparison.Ordinal));
        if (playersInside > 0)
        {
            blockers.Add($"{playersInside} player(s) are inside");
        }

        return blockers;
    }

    private static void AddCount(List<string> blockers, int count, string label)
    {
        if (count > 0)
        {
            blockers.Add($"{count} {label}");
        }
    }
}
