using StardewInteriorChanger.Core;
using StardewValley.Buildings;

namespace StardewInteriorChanger;

internal sealed record SelectionReadResult(
    bool IsValid,
    bool IsExplicit,
    StoredSelection Selection,
    string? Error);

internal sealed record SelectionDataSnapshot(
    IReadOnlyDictionary<string, string?> Values);

internal static class SelectionStorage
{
    private const string RootKey = "StardewInteriorChanger.Core/Selections";
    private const string VanillaValue = "vanilla";
    private const string RequiresEmptyRestoreValue = "1";

    public static SelectionReadResult Read(
        Building building,
        InteriorTarget target)
    {
        TargetContractId contract = TargetContracts.For(target);
        InteriorInstanceId instance = GetInstanceId(building, target);
        string choiceKey = Key(contract, "Choice");

        if (!building.modData.TryGetValue(choiceKey, out string? rawChoice))
        {
            return new SelectionReadResult(
                true,
                false,
                StoredSelection.Create(instance, contract, InteriorChoice.Vanilla),
                null);
        }

        if (!building.modData.TryGetValue(Key(contract, "DataVersion"), out string? rawVersion)
            || !int.TryParse(rawVersion, out int dataVersion)
            || dataVersion != StoredSelection.CurrentDataVersion)
        {
            return Invalid(instance, contract, "the saved selection data version is unsupported");
        }

        if (!building.modData.TryGetValue(Key(contract, "Instance"), out string? rawInstance)
            || !string.Equals(rawInstance, instance.Value, StringComparison.Ordinal))
        {
            return Invalid(instance, contract, "the saved building instance ID doesn't match");
        }

        if (string.Equals(rawChoice, VanillaValue, StringComparison.Ordinal))
        {
            return new SelectionReadResult(
                true,
                true,
                StoredSelection.Create(instance, contract, InteriorChoice.Vanilla),
                null);
        }

        if (!VariantId.TryParse(rawChoice, out VariantId variantId))
        {
            return Invalid(instance, contract, "the saved variant ID is invalid");
        }

        if (!building.modData.TryGetValue(Key(contract, "ContentHash"), out string? rawHash)
            || !ContentHash.TryParse(rawHash, out ContentHash contentHash))
        {
            return Invalid(instance, contract, "the saved gameplay hash is missing or invalid");
        }

        return new SelectionReadResult(
            true,
            true,
            StoredSelection.Create(
                instance,
                contract,
                InteriorChoice.Custom(variantId, contentHash)),
            null);
    }

    public static void Write(Building building, StoredSelection selection)
    {
        string prefix = Prefix(selection.TargetContract);
        building.modData[$"{prefix}/DataVersion"] =
            selection.DataVersion.ToString(System.Globalization.CultureInfo.InvariantCulture);
        building.modData[$"{prefix}/Instance"] = selection.InstanceId.Value;

        if (selection.Choice is InteriorChoice.CustomChoice custom)
        {
            building.modData[$"{prefix}/Choice"] = custom.VariantId.Value;
            building.modData[$"{prefix}/ContentHash"] = custom.ContentHash.Value;
        }
        else
        {
            building.modData[$"{prefix}/Choice"] = VanillaValue;
            building.modData.Remove($"{prefix}/ContentHash");
        }

        building.modData.Remove($"{prefix}/RequiresEmptyRestore");
    }

    public static bool RequiresEmptyRestore(
        Building building,
        TargetContractId contract) =>
        building.modData.ContainsKey(Key(contract, "RequiresEmptyRestore"));

    public static void MarkRequiresEmptyRestore(
        Building building,
        TargetContractId contract) =>
        building.modData[Key(contract, "RequiresEmptyRestore")] = RequiresEmptyRestoreValue;

    public static void ClearRequiresEmptyRestore(
        Building building,
        TargetContractId contract) =>
        building.modData.Remove(Key(contract, "RequiresEmptyRestore"));

    public static SelectionDataSnapshot Capture(
        Building building,
        TargetContractId contract)
    {
        var values = new Dictionary<string, string?>();
        foreach (string suffix in new[]
                 {
                     "DataVersion",
                     "Instance",
                     "Choice",
                     "ContentHash",
                     "RequiresEmptyRestore",
                 })
        {
            string key = Key(contract, suffix);
            values[key] = building.modData.TryGetValue(key, out string? value)
                ? value
                : null;
        }

        return new SelectionDataSnapshot(values);
    }

    public static void Restore(Building building, SelectionDataSnapshot snapshot)
    {
        foreach ((string key, string? value) in snapshot.Values)
        {
            if (value is null)
            {
                building.modData.Remove(key);
            }
            else
            {
                building.modData[key] = value;
            }
        }
    }

    public static InteriorInstanceId GetInstanceId(
        Building building,
        InteriorTarget target) => target == InteriorTarget.Greenhouse
            ? new InteriorInstanceId("location:Greenhouse")
            : new InteriorInstanceId($"building:{building.id.Value:N}");

    private static SelectionReadResult Invalid(
        InteriorInstanceId instance,
        TargetContractId contract,
        string error) => new(
            false,
            true,
            StoredSelection.Create(instance, contract, InteriorChoice.Vanilla),
            error);

    private static string Prefix(TargetContractId contract) =>
        $"{RootKey}/{contract.Value}";

    private static string Key(TargetContractId contract, string suffix) =>
        $"{Prefix(contract)}/{suffix}";
}
