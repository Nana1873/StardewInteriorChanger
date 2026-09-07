using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StardewValley.GameData.Minecarts;
using xTile.ObjectModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewInteriorChanger.Core;
using StardewModdingAPI;
using StardewValley;
using xTile;

namespace StardewInteriorChanger;

internal sealed class TextureSnapshot
{
    private readonly Color[] pixels;
    public int Width { get; }
    public int Height { get; }

    private TextureSnapshot(int width, int height, Color[] pixels)
    {
        Width = width;
        Height = height;
        this.pixels = pixels;
    }

    public static TextureSnapshot Capture(Texture2D texture)
    {
        var pixels = new Color[checked(texture.Width * texture.Height)];
        texture.GetData(pixels);
        return new TextureSnapshot(texture.Width, texture.Height, pixels);
    }

    public byte[] GetBytes()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(Width);
        writer.Write(Height);
        foreach (Color pixel in pixels)
            writer.Write(pixel.PackedValue);
        return stream.ToArray();
    }

    public Texture2D CreateTexture()
    {
        var texture = new Texture2D(Game1.graphics.GraphicsDevice, Width, Height);
        texture.SetData(pixels);
        return texture;
    }
}

internal sealed record InstalledSourceSnapshot(
    RegisteredInterior Definition,
    MapSnapshot Map,
    IReadOnlyDictionary<string, TextureSnapshot> Textures,
    InstalledSourceRuntimeData? RuntimeData = null,
    bool SupportsReversibleGreenhouseState = false)
{
    private const string Recipe = "sic-ellie-v1;cp=2.9.1;source=1.5.0;effective-map-tbin;canonical-map-id;immutable-vanilla-rgba-v1";
    internal static readonly string[] AllowedTextureNames =
        { "paths", "townInterior", "CarolineGreenhouseTiles", "spring_outdoorsTileSheet" };
    private static readonly string[] AllowedConfigurations =
        { "Modest", "Large", "Large Rows", "Expanded", "Expanded Rows", "Spacious", "Spacious Rows", "Enormous", "Enormous Rows" };

    public static string GetOasisContext()
    {
        if (!Context.IsWorldReady)
            throw new InvalidOperationException("Oasis snapshots become available after a save is loaded.");
        return Game1.player.farmName.Value + "|" + LocalizedContentManager.CurrentLanguageCode;
    }

    public static string ReadConfiguration(IContentPack pack, InstalledSourceProfile profile)
    {
        Dictionary<string, string>? config = pack.ReadJsonFile<Dictionary<string, string>>("config.json");
        string key = profile.IsOasis ? "UseCellar" : "GreenhouseType";
        string value = config?.FirstOrDefault(pair => string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase)).Value
            ?? (profile.IsOasis ? "true" : "Modest");
        if (profile.IsOasis)
            return bool.TryParse(value, out bool cellar) ? (cellar ? "true" : "false")
                : throw new InvalidOperationException($"Unsupported Oasis UseCellar '{value}'.");
        return AllowedConfigurations.FirstOrDefault(candidate => string.Equals(candidate, value, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Unsupported Ellie GreenhouseType '{value}'.");
    }

    public static InstalledSourceSnapshot Capture(IModHelper helper, InstalledSourceBridge bridge, InstalledSourceProfile profile, string configuration)
    {
        if (profile.IsOasis)
            return CaptureOasis(helper, bridge, profile, configuration);
        bridge.BeginResolve(profile);
        Map map = MapSnapshot.Capture(helper.GameContent.Load<Map>(profile.MapProxy)).CreateMap();
        bridge.RequireResolvedConfiguration(profile, configuration);
        map.Id = "SIC.Ellie.Greenhouse";
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["snapshot/recipe.txt"] = Encoding.UTF8.GetBytes(Recipe + ";source-recipe=" + profile.RecipeHash),
            ["snapshot/config.txt"] = Encoding.UTF8.GetBytes("GreenhouseType=" + configuration),
        };
        var textures = new Dictionary<string, TextureSnapshot>(StringComparer.OrdinalIgnoreCase);
        foreach (var sheet in map.TileSheets)
        {
            string source = sheet.ImageSource.Replace('\\', '/');
            if (source.StartsWith("Maps/", StringComparison.OrdinalIgnoreCase))
                source = source[5..];
            if (source.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                source = source[..^4];
            string allowed = AllowedTextureNames.FirstOrDefault(name => string.Equals(name, source, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"Ellie tilesheet '{sheet.Id}' has unsupported dependency '{sheet.ImageSource}'.");
            string fileName = $"snapshot/textures/{allowed}.rgba";
            if (!files.TryGetValue(fileName, out byte[]? data))
            {
                TextureSnapshot texture = TextureSnapshot.Capture(helper.GameContent.Load<Texture2D>("Maps/" + allowed));
                data = texture.GetBytes();
                files.Add(fileName, data);
                string key = TextureKey(data);
                textures[key] = texture;
            }
            sheet.ImageSource = TextureKey(data);
        }
        if (!MapContractValidator.TryValidate(InteriorTarget.Greenhouse, map, out string reason))
            throw new InvalidOperationException($"Ellie map contract failed: {reason}");
        MapSnapshot snapshot = MapSnapshot.Capture(map);
        files["snapshot/map.tbin"] = snapshot.CopyBytes();
        var filesystem = new SnapshotPackFileSystem(files);
        var builder = new InteriorRegistryBuilder();
        InteriorPackDocument CreateDocument(string id) => new()
        {
            FormatVersion = 1,
            Interiors = new()
            {
                new InteriorDefinitionDto
                {
                    Id = id,
                    DisplayName = $"Ellie's Ideal Greenhouse ({configuration})",
                    Target = "Greenhouse",
                    GameplayRoot = "snapshot",
                    Map = "map.tbin",
                },
            },
        };
        RegisteredInterior Build(string id)
        {
            RegistryBuildResult result = builder.Build(profile.Id, CreateDocument(id), filesystem);
            if (result.Registry.Entries.Count != 1)
                throw new InvalidOperationException("Ellie snapshot registry rejected: " +
                    string.Join("; ", result.Diagnostics.Select(diagnostic => diagnostic.Message)));
            return result.Registry.Entries.Single();
        }
        // The preliminary fixed ID avoids a circular dependency between the ID and its gameplay hash.
        RegisteredInterior preliminary = Build("current");
        return new InstalledSourceSnapshot(Build(preliminary.ContentHash.Value), snapshot, textures);
    }

    private static InstalledSourceSnapshot CaptureOasis(IModHelper helper, InstalledSourceBridge bridge,
        InstalledSourceProfile profile, string configuration)
    {
        _ = GetOasisContext();
        IContentPack pack = bridge.GetPack(profile) ?? throw new InvalidOperationException("Oasis source is unavailable.");
        bridge.BeginResolve(profile);
        Map map = MapSnapshot.Capture(helper.GameContent.Load<Map>(profile.MapProxy)).CreateMap();
        bridge.RequireResolvedConfiguration(profile, configuration);
        map.Id = "SIC.Oasis.Greenhouse";
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["snapshot/recipe.txt"] = Encoding.UTF8.GetBytes("sic-oasis-v1;cp=2.9.1;source=1.9.4;effective-map;immutable-textures-strings-network;source-recipe=" + profile.RecipeHash),
            ["snapshot/config.txt"] = Encoding.UTF8.GetBytes("UseCellar=" + configuration),
        };
        var textures = new Dictionary<string, TextureSnapshot>(StringComparer.OrdinalIgnoreCase);
        string NormalizeImage(string value) => value.Replace('\\', '/').EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            ? value.Replace('\\', '/')[..^4] : value.Replace('\\', '/');
        var ownedTextures = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string name in new[] { "greenhouse", "outdoors" })
            ownedTextures[NormalizeImage(pack.ModContent.GetInternalAssetName("assets/" + name + ".png").Name)] = name;
        foreach (var sheet in map.TileSheets)
        {
            string source = NormalizeImage(sheet.ImageSource);
            string texturePath;
            string fileName;
            if (ownedTextures.TryGetValue(source, out string? owned))
            {
                texturePath = pack.ModContent.GetInternalAssetName("assets/" + owned + ".png").Name;
                fileName = "snapshot/textures/source-" + owned + ".rgba";
            }
            else
            {
                string vanilla = source.StartsWith("Maps/", StringComparison.OrdinalIgnoreCase) ? source[5..] : source;
                string allowed = new[] { "paths", "townInterior", "Mines/mine" }.FirstOrDefault(name =>
                    string.Equals(name, vanilla, StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidOperationException($"Oasis tilesheet '{sheet.Id}' has unsupported dependency '{sheet.ImageSource}'.");
                texturePath = "Maps/" + allowed;
                fileName = "snapshot/textures/vanilla-" + allowed.Replace('/', '-') + ".rgba";
            }
            if (!files.TryGetValue(fileName, out byte[]? bytes))
            {
                TextureSnapshot captured = TextureSnapshot.Capture(helper.GameContent.Load<Texture2D>(texturePath));
                bytes = captured.GetBytes();
                files.Add(fileName, bytes);
                textures[TextureKey(bytes)] = captured;
            }
            sheet.ImageSource = TextureKey(bytes);
        }

        Dictionary<string, string> strings = helper.GameContent.Load<Dictionary<string, string>>(profile.StringsProxy);
        string[] expectedKeys = Enumerable.Range(1, 16).Select(number => "dngh." + number).ToArray();
        if (!expectedKeys.All(key => strings.TryGetValue(key, out string? text) && !string.IsNullOrEmpty(text)
                && !text.Contains("{{", StringComparison.Ordinal)))
            throw new InvalidOperationException("Oasis localized messages are not ready in the current game context.");
        var capturedStrings = expectedKeys.ToDictionary(key => key, key => strings[key], StringComparer.Ordinal);
        files["snapshot/strings.json"] = Encoding.UTF8.GetBytes(CanonicalJson(capturedStrings));

        Dictionary<string, MinecartNetworkData> networks = helper.GameContent.Load<Dictionary<string, MinecartNetworkData>>("Data/Minecarts");
        if (!networks.TryGetValue("Default", out MinecartNetworkData? shared))
            throw new InvalidOperationException("The default minecart network is unavailable.");
        MinecartNetworkData network = JsonConvert.DeserializeObject<MinecartNetworkData>(JsonConvert.SerializeObject(shared))!;
        network.Destinations.RemoveAll(destination => destination.Id.StartsWith("StardewInteriorChanger.Core.Oasis.", StringComparison.Ordinal));
        string networkJson = CanonicalJson(network);
        files["snapshot/minecarts.json"] = Encoding.UTF8.GetBytes(networkJson);
        files["snapshot/map.tbin"] = MapSnapshot.Capture(map).CopyBytes();
        RegisteredInterior Build(string id)
        {
            RegistryBuildResult result = new InteriorRegistryBuilder().Build(profile.Id, new InteriorPackDocument
            {
                FormatVersion = 1,
                Interiors = new() { new InteriorDefinitionDto { Id = id,
                    DisplayName = configuration == "true" ? "Oasis Greenhouse (cellar)" : "Oasis Greenhouse (no cellar)",
                    Target = "Greenhouse", GameplayRoot = "snapshot", Map = "map.tbin" } },
            }, new SnapshotPackFileSystem(files));
            if (result.Registry.Entries.Count != 1)
                throw new InvalidOperationException("Oasis snapshot registry rejected: " + string.Join("; ", result.Diagnostics.Select(d => d.Message)));
            return result.Registry.Entries.Single();
        }
        string snapshotKey = "StardewInteriorChanger.Core.Oasis." + Build("current").ContentHash.Value;
        string networkId = snapshotKey + ".Minecarts";
        var scopedStrings = capturedStrings.ToDictionary(pair => snapshotKey + "." + pair.Key, pair => pair.Value, StringComparer.Ordinal);
        void Rewrite(IPropertyCollection properties)
        {
            foreach (string key in new[] { "Action", "TouchAction" })
            {
                if (!properties.TryGetValue(key, out PropertyValue? value))
                    continue;
                string action = value.ToString();
                Match message = Regex.Match(action, "^Message \"(dngh\\.[0-9]+)\"$", RegexOptions.CultureInvariant);
                if (message.Success)
                {
                    if (!capturedStrings.ContainsKey(message.Groups[1].Value))
                        throw new InvalidOperationException("Oasis map refers to an unknown message.");
                    properties[key] = "Message \"" + snapshotKey + "." + message.Groups[1].Value + "\"";
                }
                else if (action == "MinecartTransport Default GreenhouseBasement")
                    properties[key] = "MinecartTransport " + networkId + " " + InstalledSourceRuntimeData.DestinationId(snapshotKey);
            }
        }
        foreach (var sheet in map.TileSheets)
            for (int index = 0; index < sheet.TileCount; index++)
                Rewrite(sheet.TileIndexProperties[index]);
        foreach (var layer in map.Layers)
            for (int y = 0; y < layer.LayerHeight; y++)
                for (int x = 0; x < layer.LayerWidth; x++)
                    if (layer.Tiles[x, y] is { } tile)
                        Rewrite(tile.Properties);
        if (!MapContractValidator.TryValidate(InteriorTarget.Greenhouse, map, out string reason))
            throw new InvalidOperationException("Oasis map contract failed: " + reason);
        MapSnapshot snapshot = MapSnapshot.Capture(map);
        files["snapshot/map.tbin"] = snapshot.CopyBytes();
        RegisteredInterior final = Build(Build("current").ContentHash.Value);
        return new InstalledSourceSnapshot(final, snapshot, textures, new InstalledSourceRuntimeData(profile.Id,
            snapshotKey, scopedStrings, networkId, configuration == "true", networkJson));
    }

    private static string CanonicalJson(object value)
    {
        JToken Order(JToken token) => token switch
        {
            JObject obj => new JObject(obj.Properties().OrderBy(property => property.Name, StringComparer.Ordinal)
                .Select(property => new JProperty(property.Name, Order(property.Value)))),
            JArray array => new JArray(array.Select(Order)),
            _ => token.DeepClone(),
        };
        return Order(JToken.FromObject(value)).ToString(Formatting.None);
    }

    private static string TextureKey(byte[] bytes) => "Mods/StardewInteriorChanger.Core/InstalledTextures/" +
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
