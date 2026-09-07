using System.Reflection;
using System.Security.Cryptography;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace StardewInteriorChanger;

/// <summary>Owner-scoped bridge for the reviewed CP implementation; never edits source files.</summary>
internal sealed class InstalledSourceBridge
{
    internal const string SourceId = "Ellibehr.ElliesIdealGreenhouse";
    internal const string SourceVersion = "1.5.0";
    internal const string SharedAsset = "Maps/Greenhouse";
    internal const string ProxyAsset = "Mods/StardewInteriorChanger.Core/InstalledSources/Ellie";
    internal const string CpVersion = "2.9.1";
    internal const string CpHash = "5203AF255C6A826A598470A642E0EB488FD943196354F79FE05FCD08BF6C941F";
    internal const string RecipeHash = "621A3C8F70C5B4199011F7AE5785CC36CE202EB461D6B1E4BC62D607C44DAFEE";
    private static InstalledSourceBridge? active;
    private readonly IModHelper helper;
    private readonly IMonitor monitor;
    private readonly string harmonyId;
    private IAssetName? sharedName;
    private Harmony? harmony;
    private string? rejectedRecipe;

    public InstalledSourceBridge(IModHelper helper, IMonitor monitor, string modId)
    {
        this.helper = helper;
        this.monitor = monitor;
        harmonyId = modId + ".InstalledSources";
    }

    public bool Enabled { get; private set; }
    public IContentPack? SourcePack { get; private set; }
    public string? ResolvedSourceFile { get; private set; }

    public void BeginResolve() => ResolvedSourceFile = null;

    public void RequireResolvedConfiguration(string configuration)
    {
        string expected = $"assets/{configuration} Greenhouse.tmx";
        if (!string.Equals(ResolvedSourceFile?.Replace('\\', '/'), expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Content Patcher has not resolved the current Ellie settings yet. Retry after it refreshes.");
    }

    public void Attach()
    {
        if (helper.ModRegistry.Get(SourceId) is not IModInfo source)
            return;
        try
        {
            RequireVersion(source, SourceVersion);
            RequireVersion(helper.ModRegistry.Get("Pathoschild.ContentPatcher")
                ?? throw new InvalidOperationException("Content Patcher is unavailable."), CpVersion);
            Assembly cp = AppDomain.CurrentDomain.GetAssemblies().Single(assembly =>
                assembly.GetName().Name == "ContentPatcher");
            if (Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(cp.Location))) != CpHash)
                throw new InvalidOperationException("Content Patcher assembly does not match the reviewed build.");
            Type manager = cp.GetType("ContentPatcher.Framework.PatchManager", true)!;
            MethodInfo getPatches = RequireMethod(manager, "GetPatches", typeof(IAssetName),
                "ContentPatcher.Framework.Patches.IPatch");
            MethodInfo loaders = RequireMethod(manager, "GetCurrentLoaders", typeof(AssetRequestedEventArgs),
                "ContentPatcher.Framework.Patches.LoadPatch");
            MethodInfo editors = RequireMethod(manager, "GetCurrentEditors", typeof(AssetRequestedEventArgs),
                "ContentPatcher.Framework.Patches.IPatch");
            sharedName = helper.GameContent.ParseAssetName(SharedAsset);
            harmony = new Harmony(harmonyId);
            active = this;
            harmony.Patch(getPatches, prefix: new HarmonyMethod(typeof(InstalledSourceBridge)
                .GetMethod(nameof(Remap), BindingFlags.NonPublic | BindingFlags.Static)!));
            MethodInfo filter = typeof(InstalledSourceBridge)
                .GetMethod(nameof(Filter), BindingFlags.NonPublic | BindingFlags.Static)!;
            foreach (MethodInfo method in new[] { loaders, editors })
                harmony.Patch(method, postfix: new HarmonyMethod(filter.MakeGenericMethod(
                    method.ReturnType.GetGenericArguments()[0])));
            Enabled = true;
            monitor.Log("Installed Ellie integration attached to reviewed Content Patcher 2.9.1.", LogLevel.Info);
        }
        catch (Exception exception)
        {
            Enabled = false;
            harmony?.UnpatchAll(harmonyId);
            monitor.Log($"Installed Ellie integration is unavailable: {exception.Message} Native interiors remain usable.", LogLevel.Warn);
        }
    }

    public bool ValidateRecipe(IContentPack pack)
    {
        string hash;
        try
        {
            hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(
                Path.Combine(pack.DirectoryPath, "content.json"))));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            hash = "unreadable: " + exception.Message;
        }
        if (hash == RecipeHash)
        {
            SourcePack = pack;
            return true;
        }
        if (rejectedRecipe != hash)
        {
            rejectedRecipe = hash;
            monitor.Log("Installed Ellie integration rejected an unreviewed content.json. Original patches remain active; existing immutable snapshots are retained.", LogLevel.Warn);
        }
        return false;
    }

    private static void RequireVersion(IModInfo info, string expected)
    {
        if (info.Manifest.Version.ToString() != expected)
            throw new InvalidOperationException($"{info.Manifest.UniqueID} requires reviewed version {expected}; found {info.Manifest.Version}.");
    }

    private static MethodInfo RequireMethod(Type type, string name, Type parameter, string element)
    {
        MethodInfo? method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public,
            null, new[] { parameter }, null);
        Type? returns = method?.ReturnType;
        if (returns is null || !returns.IsGenericType || returns.GetGenericTypeDefinition() != typeof(IEnumerable<>))
            throw new InvalidOperationException($"Unsupported Content Patcher signature: {name}.");
        Type item = returns.GetGenericArguments()[0];
        if (item.FullName != element || item.GetProperty("ContentPack")?.PropertyType != typeof(IContentPack)
            || item.GetProperty("FromAsset")?.PropertyType != typeof(string))
            throw new InvalidOperationException($"Unsupported Content Patcher patch contract: {name}.");
        return method!;
    }

    private static void Remap(ref IAssetName assetName)
    {
        if (active?.Enabled == true && assetName.IsEquivalentTo(ProxyAsset))
            assetName = active.sharedName!;
    }

    private static void Filter<T>(AssetRequestedEventArgs request, ref IEnumerable<T> __result)
    {
        if (active?.Enabled != true)
            return;
        bool proxy = request.NameWithoutLocale.IsEquivalentTo(ProxyAsset);
        if (!proxy && !request.NameWithoutLocale.IsEquivalentTo(SharedAsset))
            return;
        __result = FilterOwned(__result, proxy).ToArray();
    }

    private static IEnumerable<T> FilterOwned<T>(IEnumerable<T> patches, bool proxy)
    {
        foreach (T patch in patches)
        {
            IContentPack pack = (IContentPack)patch!.GetType().GetProperty("ContentPack")!.GetValue(patch)!;
            bool owned = string.Equals(pack.Manifest.UniqueID, SourceId, StringComparison.OrdinalIgnoreCase);
            bool approved = owned && active!.ValidateRecipe(pack);
            if (proxy && approved && patch!.GetType().FullName == "ContentPatcher.Framework.Patches.LoadPatch")
                active!.ResolvedSourceFile = (string?)patch.GetType().GetProperty("FromAsset")!.GetValue(patch);
            if (proxy ? approved : !approved)
                yield return patch;
        }
    }
}
