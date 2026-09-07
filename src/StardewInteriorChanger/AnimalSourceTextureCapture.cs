using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using StardewInteriorChanger.Core;
using StardewModdingAPI;
using StardewValley;

namespace StardewInteriorChanger;

internal sealed record AnimalSourceTextureSet(
    TextureSnapshot CoopTiles,
    TextureSnapshot? Window,
    IReadOnlyDictionary<string, byte[]> FingerprintFiles);

internal sealed class AnimalSourceTextureCapture
{
    private const string Recipe = "sic-animal-textures-v1;cp=2.9.1;canonical-xnb-coopTiles;smapi-premultiplied-raw;smapi-overlay-replace;downward-extension-only;green-location=six-managed-animal-tiers;global-object-sprites=source-owned";
    private readonly IModHelper helper;
    private readonly int ownerThreadId;

    // Construct in Entry on the game thread; capture and disposal stay synchronous there.
    public AnimalSourceTextureCapture(IModHelper helper)
    {
        this.helper = helper;
        ownerThreadId = Environment.CurrentManagedThreadId;
    }

    public static AnimalSourceConfiguration ReadConfiguration(IContentPack pack, AnimalSourceKind kind) =>
        AnimalSourceConfiguration.Parse(kind, pack.ReadJsonFile<Dictionary<string, string?>>("config.json"));

    public AnimalSourceTextureSet Capture(IContentPack pack, AnimalSourceConfiguration configuration)
    {
        if (Environment.CurrentManagedThreadId != ownerThreadId)
            throw new InvalidOperationException("Animal-source texture capture must run on the game thread which created it.");

        bool green = configuration.Kind == AnimalSourceKind.Green;
        string id = green ? "vikich3rry.GreenCoopsBarns" : "nykachu.coopbarnfacelift";
        string version = green ? "1.0.4" : "1.2";
        string recipeHash = green
            ? "DDB108D70623C635E67896245404BFA36B13978160F3B1112692924903AA1220"
            : "FB6414CD968347F9FA260DC4525AE3F066B555F21281C2284E5829984DFB107F";
        if (!string.Equals(pack.Manifest.UniqueID, id, StringComparison.OrdinalIgnoreCase)
            || pack.Manifest.Version.IsOlderThan(version) || pack.Manifest.Version.IsNewerThan(version))
            throw new InvalidOperationException($"This texture recipe requires the reviewed {id} {version} source pack.");
        if (green && helper.ModRegistry.IsLoaded("KediDili.VanillaPlusProfessions"))
            throw new InvalidOperationException("Green Coops and Barns integration does not support Vanilla Plus Professions' player-dependent expanded animal-house maps. Disable that combination to use this reviewed adapter.");
        if (helper.ModRegistry.Get("Platonymous.Toolkit") is { } pyTk && pyTk.Manifest.Version.IsOlderThan("1.24.0"))
            throw new InvalidOperationException("Animal-source texture capture does not support legacy PyTK image scaling (versions before 1.24.0).");

        var source = new DirectoryPackFileSystem(pack.DirectoryPath);
        byte[] recipeBytes = source.ReadAllBytes("content.json");
        if (!string.Equals(Convert.ToHexString(SHA256.HashData(recipeBytes)), recipeHash, StringComparison.Ordinal))
            throw new InvalidOperationException($"The {id} content.json recipe has changed; source-owned texture capture is unavailable.");
        RequireUnchangedConfiguration(pack, configuration);

        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["animal-textures/recipe.txt"] = Encoding.UTF8.GetBytes(Recipe + ";source=" + id + ";version=" + version),
            ["animal-textures/config.txt"] = Encoding.UTF8.GetBytes(configuration.Canonical),
            ["animal-textures/source/content.json"] = recipeBytes
        };
        var originalFiles = new Dictionary<string, byte[]>(StringComparer.Ordinal) { ["content.json"] = recipeBytes };
        IRawTextureData ReadImage(string path)
        {
            byte[] bytes = source.ReadAllBytes(path);
            originalFiles[path] = bytes;
            files["animal-textures/source/" + path] = bytes;
            // Content Patcher 2.9.1 uses this same public loader for PNG edits.
            // It supplies SMAPI's premultiplied pixels without allocating a GPU source.
            IRawTextureData data = pack.ModContent.Load<IRawTextureData>(path);
            if (data.Width <= 0 || data.Height <= 0)
                throw new InvalidOperationException($"Source texture '{path}' has invalid dimensions.");
            return data;
        }

        // A plain MonoGame manager reads the installed game's XNB directly. Do not
        // use Game1's factory or shared GameContent, which route through CP editors.
        using var vanillaContent = new ContentManager(Game1.content.ServiceProvider, Game1.content.RootDirectory);
        TextureSnapshot baseline = TextureSnapshot.Capture(vanillaContent.Load<Texture2D>("Maps/coopTiles"));
        files["animal-textures/baseline/coopTiles.rgba"] = baseline.GetBytes();
        var owned = new HashSet<Texture2D>();
        try
        {
            Texture2D coopTexture = baseline.CreateTexture();
            owned.Add(coopTexture);
            IAssetDataForImage coopEditor = helper.GameContent.GetPatchHelper(coopTexture).AsImage();
            IReadOnlyList<string> paths = configuration.TextureFiles;
            Patch(coopEditor, ReadImage(paths[0]), green ? PatchMode.Replace : PatchMode.Overlay, owned);
            TextureSnapshot? window = null;
            if (green)
            {
                IRawTextureData windowBase = ReadImage(paths[1]);
                var windowTexture = new Texture2D(Game1.graphics.GraphicsDevice, windowBase.Width, windowBase.Height);
                owned.Add(windowTexture);
                windowTexture.SetData(windowBase.Data);
                IAssetDataForImage windowEditor = helper.GameContent.GetPatchHelper(windowTexture).AsImage();
                Patch(windowEditor, ReadImage(paths[2]), PatchMode.Replace, owned);
                window = TextureSnapshot.Capture(windowEditor.Data);
                files["animal-textures/result/vikiwindow.rgba"] = window.GetBytes();
            }
            else
                Patch(coopEditor, ReadImage(paths[1]), PatchMode.Overlay, owned);

            TextureSnapshot coop = TextureSnapshot.Capture(coopEditor.Data);
            files["animal-textures/result/coopTiles.rgba"] = coop.GetBytes();
            foreach (var pair in originalFiles)
            {
                if (!pair.Value.AsSpan().SequenceEqual(source.ReadAllBytes(pair.Key)))
                    throw new InvalidOperationException($"Source file '{pair.Key}' changed during texture capture; retry after it stabilizes.");
            }
            RequireUnchangedConfiguration(pack, configuration);
            return new AnimalSourceTextureSet(coop, window, new ReadOnlyDictionary<string, byte[]>(files));
        }
        finally
        {
            foreach (Texture2D texture in owned) texture.Dispose();
        }
    }

    private static void RequireUnchangedConfiguration(IContentPack pack, AnimalSourceConfiguration expected)
    {
        if (ReadConfiguration(pack, expected.Kind).Canonical != expected.Canonical)
            throw new InvalidOperationException("Source configuration changed during animal-house texture capture; retry the updated configuration.");
    }

    private static void Patch(IAssetDataForImage editor, IRawTextureData source, PatchMode mode, HashSet<Texture2D> owned)
    {
        // Mirror CP 2.9.1's default areas: full source at (0,0), width may not
        // expand, height may expand. Public SMAPI owns the actual pixel semantics.
        if (source.Width > editor.Data.Width)
            throw new InvalidOperationException("The reviewed image patch extends beyond its destination width.");
        try
        {
            editor.ExtendImage(editor.Data.Width, source.Height);
        }
        finally
        {
            owned.Add(editor.Data);
        }
        editor.PatchImage(source, patchMode: mode);
    }
}
