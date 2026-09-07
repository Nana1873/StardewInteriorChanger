using System.Security.Cryptography;
using System.Text;
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
    IReadOnlyDictionary<string, TextureSnapshot> Textures)
{
    private const string Recipe = "sic-ellie-v1;cp=2.9.1;source=1.5.0;effective-map-tbin;canonical-map-id;immutable-vanilla-rgba-v1";
    internal static readonly string[] AllowedTextureNames =
        { "paths", "townInterior", "CarolineGreenhouseTiles", "spring_outdoorsTileSheet" };
    private static readonly string[] AllowedConfigurations =
        { "Modest", "Large", "Large Rows", "Expanded", "Expanded Rows", "Spacious", "Spacious Rows", "Enormous", "Enormous Rows" };

    public static string ReadConfiguration(IContentPack pack)
    {
        Dictionary<string, string>? config = pack.ReadJsonFile<Dictionary<string, string>>("config.json");
        string value = config is not null && config.TryGetValue("GreenhouseType", out string? selected)
            ? selected : "Modest";
        return AllowedConfigurations.FirstOrDefault(candidate => string.Equals(candidate, value, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Unsupported Ellie GreenhouseType '{value}'.");
    }

    public static InstalledSourceSnapshot Capture(IModHelper helper, InstalledSourceBridge bridge, string configuration)
    {
        bridge.BeginResolve();
        Map map = MapSnapshot.Capture(helper.GameContent.Load<Map>(InstalledSourceBridge.ProxyAsset)).CreateMap();
        bridge.RequireResolvedConfiguration(configuration);
        map.Id = "SIC.Ellie.Greenhouse";
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["snapshot/recipe.txt"] = Encoding.UTF8.GetBytes(Recipe + ";source-recipe=" + InstalledSourceBridge.RecipeHash),
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
            RegistryBuildResult result = builder.Build(InstalledSourceBridge.SourceId, CreateDocument(id), filesystem);
            if (result.Registry.Entries.Count != 1)
                throw new InvalidOperationException("Ellie snapshot registry rejected: " +
                    string.Join("; ", result.Diagnostics.Select(diagnostic => diagnostic.Message)));
            return result.Registry.Entries.Single();
        }
        // The preliminary fixed ID avoids a circular dependency between the ID and its gameplay hash.
        RegisteredInterior preliminary = Build("current");
        return new InstalledSourceSnapshot(Build(preliminary.ContentHash.Value), snapshot, textures);
    }

    private static string TextureKey(byte[] bytes) => "Mods/StardewInteriorChanger.Core/InstalledTextures/" +
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
