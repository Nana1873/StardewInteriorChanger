using Microsoft.Xna.Framework.Content;
using Newtonsoft.Json.Linq;
using StardewInteriorChanger.Core;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.Buildings;
using StardewValley.GameData.Machines;

namespace StardewInteriorChanger;

internal static class AnimalHouseFixtureData
{
    private static readonly string[] BuildingFields =
    {
        "IndoorMap", "IndoorMapType", "NonInstancedIndoorLocation", "MaxOccupants",
        "ValidOccupantTypes", "AllowAnimalPregnancy", "IndoorItems", "IndoorItemMoves"
    };

    private static readonly Lazy<(JToken Machine, Dictionary<string, JObject> Buildings)> Vanilla = new(() =>
    {
        // Read the installed game's original XNB data, outside the shared CP asset
        // pipeline. Comparing the whole machine prevents unmodeled custom callbacks
        // or nested output rules from receiving the idle-equipment exception.
        using var content = new ContentManager(Game1.content.ServiceProvider, Game1.content.RootDirectory);
        var machines = content.Load<Dictionary<string, MachineData>>("Data/Machines");
        var buildings = content.Load<Dictionary<string, BuildingData>>("Data/Buildings");
        return (JToken.FromObject(machines[IncubatorFixturePolicy.ItemId]),
            AnimalHouseTargetContracts.All.ToDictionary(contract => contract.BuildingType,
                contract => JObject.FromObject(buildings[contract.BuildingType]), StringComparer.Ordinal));
    });

    public static bool IsReviewedBuilding(Building building, GameLocation indoors, InteriorTarget target)
    {
        if (!AnimalHouseTargetContracts.TryGet(target, out AnimalHouseTargetContract contract)
            || building.buildingType.Value != contract.BuildingType
            || indoors is not AnimalHouse house || !house.isStructure.Value
            || house.animalLimit.Value != contract.Capacity
            || building.GetData() is not { } data)
            return false;

        try
        {
            JObject current = JObject.FromObject(data);
            JObject original = Vanilla.Value.Buildings[contract.BuildingType];
            return BuildingFields.All(field => JToken.DeepEquals(current[field], original[field]));
        }
        catch (Exception)
        {
            // An unreadable original contract is not evidence that equipment is safe.
            return false;
        }
    }

    public static IncubatorMachineState? CaptureMachine(StardewValley.Object obj)
    {
        MachineData? data = obj.GetMachineData();
        if (data is null) return null;
        MachineOutputRule? rule = data.OutputRules?.Count == 1 ? data.OutputRules[0] : null;
        bool matches;
        try
        {
            matches = JToken.DeepEquals(JToken.FromObject(data), Vanilla.Value.Machine);
        }
        catch (Exception)
        {
            matches = false;
        }

        return new IncubatorMachineState
        {
            IsIncubator = data.IsIncubator,
            OnlyCompleteOvernight = data.OnlyCompleteOvernight,
            AllowLoadWhenFull = data.AllowLoadWhenFull,
            HasInteractMethod = !string.IsNullOrEmpty(data.InteractMethod),
            OutputRuleCount = data.OutputRules?.Count ?? 0,
            OutputRuleId = rule?.Id,
            TriggerCount = rule?.Triggers?.Count ?? 0,
            IsItemPlacedInMachineTrigger = rule?.Triggers?.Count == 1
                && rule.Triggers[0].Trigger == MachineOutputTrigger.ItemPlacedInMachine,
            OutputItemCount = rule?.OutputItem?.Count ?? 0,
            OutputMethod = rule?.OutputItem?.Count == 1 ? rule.OutputItem[0].OutputMethod : null,
            RecalculateOnCollect = rule?.RecalculateOnCollect ?? false,
            MatchesRemainingVanillaConfiguration = matches
        };
    }
}
