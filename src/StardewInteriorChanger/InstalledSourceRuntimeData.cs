using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley.GameData.Minecarts;

namespace StardewInteriorChanger;

internal sealed record InstalledSourceRuntimeData(
    string SourceId,
    string SnapshotKey,
    IReadOnlyDictionary<string, string> Strings,
    string? MinecartNetworkId,
    bool HasCellar,
    string? MinecartNetworkJson)
{
    internal const string QueryId = "StardewInteriorChanger_Core_OasisActive";
    internal const string LegacyQueryId = "StardewInteriorChanger_Core_OasisOriginalReturnSafe";
    internal static string DestinationId(string key) => key + ".Basement";

    public MinecartDestinationData CreateReturnDestination() => new()
    {
        Id = DestinationId(SnapshotKey),
        DisplayName = "Greenhouse Basement",
        TargetLocation = "Greenhouse",
        TargetTile = new Point(26, 111),
        TargetDirection = "right",
        Condition = $"{QueryId} {SnapshotKey}, PLAYER_HAS_MAIL Any ccPantry Received",
    };

    public static void EditAssets(AssetRequestedEventArgs e, IEnumerable<RuntimeInterior> interiors)
    {
        InstalledSourceRuntimeData[] snapshots = interiors.Select(interior => interior.RuntimeData)
            .OfType<InstalledSourceRuntimeData>().ToArray();
        if (snapshots.Length > 0 && e.NameWithoutLocale.IsEquivalentTo("Strings/StringsFromMaps"))
        {
            e.Edit(asset =>
            {
                IDictionary<string, string> strings = asset.AsDictionary<string, string>().Data;
                foreach (InstalledSourceRuntimeData snapshot in snapshots)
                    foreach ((string key, string value) in snapshot.Strings)
                        strings[key] = value;
            });
        }
        else if (e.NameWithoutLocale.IsEquivalentTo("Data/Minecarts"))
        {
            e.Edit(asset =>
            {
                IDictionary<string, MinecartNetworkData> networks = asset.AsDictionary<string, MinecartNetworkData>().Data;
                // Unsupported or changed source recipes keep their original patches. Guard their
                // legacy return too whenever SIC is managing a different greenhouse layout.
                if (networks.TryGetValue("Default", out MinecartNetworkData? original))
                {
                    foreach (MinecartDestinationData destination in original.Destinations.Where(destination =>
                                 destination.Id == "GreenhouseBasement" && destination.TargetLocation == "Greenhouse"))
                    {
                        if (destination.Condition?.Contains(LegacyQueryId, StringComparison.Ordinal) != true)
                            destination.Condition = string.IsNullOrWhiteSpace(destination.Condition)
                                ? LegacyQueryId : LegacyQueryId + ", " + destination.Condition;
                    }
                }
                foreach (InstalledSourceRuntimeData snapshot in snapshots)
                {
                    if (snapshot.MinecartNetworkId is null || snapshot.MinecartNetworkJson is null)
                        continue;
                    // Deserialization creates a fresh mutable game-data graph for each asset load.
                    MinecartNetworkData network = JsonConvert.DeserializeObject<MinecartNetworkData>(snapshot.MinecartNetworkJson)
                        ?? throw new InvalidOperationException("The captured minecart network is unavailable.");
                    if (snapshot.HasCellar)
                    {
                        network.Destinations.Add(snapshot.CreateReturnDestination());
                        if (networks.TryGetValue("Default", out MinecartNetworkData? shared))
                            shared.Destinations.Add(snapshot.CreateReturnDestination());
                    }
                    networks[snapshot.MinecartNetworkId] = network;
                }
            }, AssetEditPriority.Late);
        }
    }
}
