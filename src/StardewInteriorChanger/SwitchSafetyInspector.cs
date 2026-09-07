using Microsoft.Xna.Framework;
using StardewInteriorChanger.Core;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Events;
using StardewValley.Menus;

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

        int feedHopperCount = indoors.objects.Values.Count(obj =>
            obj.QualifiedItemId == InteriorFixturePolicy.DeluxeBarnFeedHopperId);
        int incubatorCount = indoors.objects.Values.Count(obj =>
            obj.QualifiedItemId == IncubatorFixturePolicy.ItemId);
        bool isAnimalTarget = AnimalHouseTargetContracts.TryGet(target, out _);
        bool reviewedBuilding = isAnimalTarget && AnimalHouseFixtureData.IsReviewedBuilding(building, indoors, target);
        if (isAnimalTarget && !reviewedBuilding)
            blockers.Add("the animal-house type, capacity, or built-in equipment differs from the supported game contract");
        int placedObjects = indoors.objects.Pairs.Count(pair =>
            !IsBuiltInObjectFixture(target, building, indoors, pair.Key, pair.Value,
                feedHopperCount, incubatorCount, reviewedBuilding));
        AddCount(blockers, placedObjects, "placed object(s)");
        AddCount(blockers, indoors.furniture.Count, "piece(s) of furniture");
        AddCount(blockers, indoors.terrainFeatures.Count(), "terrain feature(s) or crop(s)");
        AddCount(blockers, indoors.resourceClumps.Count, "resource clump(s)");
        AddCount(blockers, indoors.largeTerrainFeatures.Count, "large terrain feature(s)");
        AddCount(blockers, indoors.debris.Count, "debris item(s)");
        AddCount(blockers, indoors.characters.Count, "location character(s)");

        if (isAnimalTarget)
        {
            if (indoors is not AnimalHouse animalHouse)
            {
                blockers.Add("the animal-house interior isn't an AnimalHouse location");
            }
            else
            {
                AddCount(
                    blockers,
                    animalHouse.animalsThatLiveHere.Count,
                    "assigned farm animal(s)");
                AddCount(blockers, animalHouse.animals.Count(), "resident farm animal(s)");
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

    internal static bool IsBuiltInObjectFixture(
        InteriorTarget target,
        Building building,
        GameLocation indoors,
        Vector2 dictionaryTile,
        StardewValley.Object obj,
        int feedHopperCount,
        int incubatorCount,
        bool reviewedBuilding)
    {
        if (!reviewedBuilding || !AnimalHouseTargetContracts.TryGet(target, out AnimalHouseTargetContract contract))
            return false;

        TilePoint? ReadTile(Vector2 value, TilePoint expected) =>
            value.X == expected.X && value.Y == expected.Y ? expected : null;

        if (obj.QualifiedItemId == IncubatorFixturePolicy.ItemId && contract.HasIncubator)
        {
            var context = new IncubatorFixtureContext
            {
                BuildingType = building.buildingType.Value,
                IsAnimalHouse = indoors is AnimalHouse,
                HasReviewedBuildingData = reviewedBuilding,
                IncubatorCount = incubatorCount
            };
            var state = new IncubatorFixtureState
            {
                QualifiedItemId = obj.QualifiedItemId,
                DictionaryTile = ReadTile(dictionaryTile, IncubatorFixturePolicy.FixtureTile),
                ObjectTile = ReadTile(obj.TileLocation, IncubatorFixturePolicy.FixtureTile),
                IsPlainObject = obj.GetType() == typeof(StardewValley.Object),
                IsBigCraftable = obj.bigCraftable.Value,
                IsRecipe = obj.IsRecipe,
                Fragility = obj.Fragility,
                Stack = obj.Stack,
                HasModData = obj.modData.Any(),
                ShowNextIndex = obj.showNextIndex.Value,
                HasLightSource = obj.lightSource is not null,
                HasHeldObject = obj.heldObject.Value is not null,
                MinutesUntilReady = obj.MinutesUntilReady,
                ReadyForHarvest = obj.readyForHarvest.Value,
                HasLastInput = obj.lastInputItem.Value is not null,
                LastOutputRuleId = obj.lastOutputRuleId.Value,
                Machine = AnimalHouseFixtureData.CaptureMachine(obj)
            };
            return IncubatorFixturePolicy.Classify(context, state) == IncubatorFixtureClassification.IdleBuiltIn;
        }

        return InteriorFixturePolicy.IsBuiltInObjectFixture(target, indoors is AnimalHouse,
            feedHopperCount, new ObjectFixtureState
            {
                QualifiedItemId = obj.QualifiedItemId,
                DictionaryTile = ReadTile(dictionaryTile, contract.FeedHopperTile),
                ObjectTile = ReadTile(obj.TileLocation, contract.FeedHopperTile),
                IsPlainObject = obj.GetType() == typeof(StardewValley.Object),
                Fragility = obj.Fragility,
                Stack = obj.Stack,
                HasHeldObject = obj.heldObject.Value is not null,
                MinutesUntilReady = obj.MinutesUntilReady,
                ReadyForHarvest = obj.readyForHarvest.Value,
                HasLastInput = obj.lastInputItem.Value is not null,
                HasLastOutputRule = !string.IsNullOrEmpty(obj.lastOutputRuleId.Value),
                HasModData = obj.modData.Any(),
                HasMachineData = obj.GetMachineData() is not null,
            });
    }

    private static List<string> GetTransientBlockers(
        Building building,
        GameLocation indoors)
    {
        var blockers = new List<string>();

        if (indoors is AnimalHouse)
        {
            if (indoors.currentEvent is not null)
                blockers.Add("an animal-house event is active");
            if (Game1.activeClickableMenu is NamingMenu)
                blockers.Add("an animal naming transaction may still be pending");
            if (Game1.farmEvent is QuestionEvent { animal: not null, forceProceed: false } birth
                && ReferenceEquals(birth.animal.homeInterior, indoors))
                blockers.Add("an animal birth is pending for this building");
        }

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
