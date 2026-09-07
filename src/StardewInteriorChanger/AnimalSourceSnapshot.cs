using System.Security.Cryptography;
using System.Text;
using Microsoft.Xna.Framework.Graphics;
using StardewInteriorChanger.Core;
using StardewModdingAPI;
using xTile;
using xTile.Tiles;

namespace StardewInteriorChanger;

internal static class AnimalSourceSnapshot
{
    private static readonly string[] SharedTextures =
        { "paths", "spring_outdoorsTileSheet", "spring_outdoorsTileSheet2" };

    public static InstalledSourceSnapshot Capture(IModHelper helper, InstalledSourceBridge bridge,
        InstalledSourceProfile profile, AnimalSourceConfiguration configuration,
        AnimalSourceTextureSet sourceTextures, AnimalHouseTargetContract target)
    {
        IContentPack pack = bridge.GetPack(profile)
            ?? throw new InvalidOperationException("The reviewed animal-house source is unavailable.");
        var sourceFiles = new DirectoryPackFileSystem(pack.DirectoryPath);
        byte[] originalMap = sourceFiles.ReadAllBytes(profile.ExpectedAnimalMap(target.MapAsset));
        helper.GameContent.InvalidateCache(pack.ModContent.GetInternalAssetName(profile.ExpectedAnimalMap(target.MapAsset)).Name);
        bridge.BeginResolve(profile);
        string proxy = profile.ProxyFor(target.MapAsset);
        helper.GameContent.InvalidateCache(proxy);
        Map map = MapSnapshot.Capture(helper.GameContent.Load<Map>(proxy)).CreateMap();
        bridge.RequireResolvedConfiguration(profile, configuration.Canonical, target.MapAsset);
        if (!originalMap.AsSpan().SequenceEqual(sourceFiles.ReadAllBytes(profile.ExpectedAnimalMap(target.MapAsset))))
            throw new InvalidOperationException("The original map changed during capture; retry after it stabilizes.");
        map.Id = $"SIC.{profile.Token}.{target.Target}";
        if (!profile.IsGreen && target.RequiresAutoFeed)
            map.Properties["AutoFeed"] = "T";

        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["snapshot/recipe.txt"] = Encoding.UTF8.GetBytes(
                "sic-animal-originals-v1;cp=2.9.1;source=" + profile.Version
                + ";source-recipe=" + profile.RecipeHash
                + ";target=" + target.Target
                + ";source-owned-textures;target-normalized-location;nykachu-deluxe-autofeed;reversible-greenhouse"),
            ["snapshot/config.txt"] = Encoding.UTF8.GetBytes(configuration.Canonical),
            ["snapshot/source-map.bin"] = originalMap,
        };
        foreach ((string name, byte[] bytes) in sourceTextures.FingerprintFiles)
            files.Add("snapshot/source-textures/" + name, bytes);
        var textures = new Dictionary<string, TextureSnapshot>(StringComparer.OrdinalIgnoreCase);
        foreach (TileSheet sheet in map.TileSheets)
        {
            string source = sheet.ImageSource.Replace('\\', '/');
            if (source.StartsWith("Maps/", StringComparison.OrdinalIgnoreCase)) source = source[5..];
            string name = source;
            if (name.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) name = name[..^4];
            TextureSnapshot texture;
            if (string.Equals(name, "coopTiles", StringComparison.OrdinalIgnoreCase))
                texture = sourceTextures.CoopTiles;
            else if (profile.IsGreen && string.Equals(name, "vikiwindow", StringComparison.OrdinalIgnoreCase))
                texture = sourceTextures.Window ?? throw new InvalidOperationException("Green window pixels are unavailable.");
            else
            {
                string allowed = SharedTextures.FirstOrDefault(candidate => string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidOperationException($"Unsupported {profile.Name} tilesheet '{sheet.ImageSource}'.");
                texture = TextureSnapshot.Capture(helper.GameContent.Load<Texture2D>("Maps/" + allowed));
            }
            byte[] data = texture.GetBytes();
            string key = "Mods/StardewInteriorChanger.Core/InstalledTextures/"
                + Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
            files["snapshot/textures/" + name + ".rgba"] = data;
            textures.TryAdd(key, texture);
            sheet.ImageSource = key;
        }
        if (!MapContractValidator.TryValidate(target.Target, map, out string reason,
                allowReversibleGreenhouseState: profile.IsGreen))
            throw new InvalidOperationException($"{profile.Name} {target.Target} map contract failed: {reason}");
        MapSnapshot snapshot = MapSnapshot.Capture(map);
        files["snapshot/map.tbin"] = snapshot.CopyBytes();
        RegisteredInterior Build(string id)
        {
            RegistryBuildResult result = new InteriorRegistryBuilder().Build(profile.Id, new InteriorPackDocument
            {
                FormatVersion = 1,
                Interiors = new()
                {
                    new InteriorDefinitionDto
                    {
                        Id = id, DisplayName = $"{profile.Name} — {target.BuildingType}",
                        Target = target.Target.ToString(), GameplayRoot = "snapshot", Map = "map.tbin"
                    }
                }
            }, new SnapshotPackFileSystem(files));
            if (result.Registry.Entries.Count != 1)
                throw new InvalidOperationException("Animal source snapshot rejected: "
                    + string.Join("; ", result.Diagnostics.Select(item => item.Message)));
            return result.Registry.Entries.Single();
        }
        RegisteredInterior preliminary = Build("current");
        return new InstalledSourceSnapshot(Build(preliminary.ContentHash.Value), snapshot, textures,
            SupportsReversibleGreenhouseState: profile.IsGreen);
    }
}
