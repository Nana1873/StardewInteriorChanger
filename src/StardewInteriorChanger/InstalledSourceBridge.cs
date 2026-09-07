using System.Reflection;
using System.Security.Cryptography;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace StardewInteriorChanger;

/// <summary>Owner-scoped bridge for reviewed CP implementations; never edits source files.</summary>
internal sealed class InstalledSourceBridge
{
    internal const string SharedAsset = "Maps/Greenhouse";
    internal const string CpVersion = "2.9.1";
    internal const string CpHash = "5203AF255C6A826A598470A642E0EB488FD943196354F79FE05FCD08BF6C941F";
    private static InstalledSourceBridge? active;
    private readonly IModHelper helper;
    private readonly IMonitor monitor;
    private readonly string harmonyId;
    private readonly Dictionary<string, SourceState> sources = new(StringComparer.OrdinalIgnoreCase);
    private Harmony? harmony;

    private sealed class SourceState(InstalledSourceProfile profile)
    {
        public InstalledSourceProfile Profile { get; } = profile;
        public IContentPack? Pack { get; set; }
        public string? RejectedRecipe { get; set; }
        public string? ResolvedMapFile { get; set; }
        public HashSet<string> ResolvedMapEditors { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public InstalledSourceBridge(IModHelper helper, IMonitor monitor, string modId)
    {
        this.helper = helper;
        this.monitor = monitor;
        harmonyId = modId + ".InstalledSources";
    }

    public bool Enabled { get; private set; }
    public IEnumerable<InstalledSourceProfile> Profiles => sources.Values.Select(source => source.Profile);
    public IContentPack? GetPack(InstalledSourceProfile profile) => sources[profile.Id].Pack;

    public void BeginResolve(InstalledSourceProfile profile)
    {
        SourceState source = sources[profile.Id];
        source.ResolvedMapFile = null;
        source.ResolvedMapEditors.Clear();
    }

    public void RequireResolvedConfiguration(InstalledSourceProfile profile, string configuration)
    {
        SourceState source = sources[profile.Id];
        string expected = profile.IsOasis ? "assets/Greenhouse.tmx" : $"assets/{configuration} Greenhouse.tmx";
        bool valid = string.Equals(source.ResolvedMapFile?.Replace('\\', '/'), expected, StringComparison.OrdinalIgnoreCase);
        if (profile.IsOasis)
        {
            string[] expectedEditors = configuration == "false" ? new[] { "assets/Greenhouse_NoCellar.tmx" } : Array.Empty<string>();
            valid &= source.ResolvedMapEditors.SetEquals(expectedEditors);
        }
        if (!valid)
            throw new InvalidOperationException($"Content Patcher has not resolved the current {profile.Name} settings yet. Retry after it refreshes.");
    }

    public void Attach()
    {
        foreach (InstalledSourceProfile profile in InstalledSourceProfile.All)
        {
            if (helper.ModRegistry.Get(profile.Id) is not IModInfo info)
                continue;
            try
            {
                RequireVersion(info, profile.Version);
                sources.Add(profile.Id, new SourceState(profile));
            }
            catch (Exception exception)
            {
                monitor.Log($"Installed {profile.Name} integration is unavailable: {exception.Message}", LogLevel.Warn);
            }
        }
        if (sources.Count == 0)
            return;
        try
        {
            RequireVersion(helper.ModRegistry.Get("Pathoschild.ContentPatcher")
                ?? throw new InvalidOperationException("Content Patcher is unavailable."), CpVersion);
            Assembly cp = AppDomain.CurrentDomain.GetAssemblies().Single(assembly => assembly.GetName().Name == "ContentPatcher");
            if (Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(cp.Location))) != CpHash)
                throw new InvalidOperationException("Content Patcher assembly does not match the reviewed build.");
            Type manager = cp.GetType("ContentPatcher.Framework.PatchManager", true)!;
            MethodInfo getPatches = RequireMethod(manager, "GetPatches", typeof(IAssetName), "ContentPatcher.Framework.Patches.IPatch");
            MethodInfo loaders = RequireMethod(manager, "GetCurrentLoaders", typeof(AssetRequestedEventArgs), "ContentPatcher.Framework.Patches.LoadPatch");
            MethodInfo editors = RequireMethod(manager, "GetCurrentEditors", typeof(AssetRequestedEventArgs), "ContentPatcher.Framework.Patches.IPatch");
            harmony = new Harmony(harmonyId);
            active = this;
            harmony.Patch(getPatches, prefix: new HarmonyMethod(typeof(InstalledSourceBridge).GetMethod(nameof(Remap), BindingFlags.NonPublic | BindingFlags.Static)!));
            MethodInfo filter = typeof(InstalledSourceBridge).GetMethod(nameof(Filter), BindingFlags.NonPublic | BindingFlags.Static)!;
            foreach (MethodInfo method in new[] { loaders, editors })
                harmony.Patch(method, postfix: new HarmonyMethod(filter.MakeGenericMethod(method.ReturnType.GetGenericArguments()[0])));
            helper.Events.Content.AssetRequested += OnAssetRequested;
            Enabled = true;
            monitor.Log($"Installed-source integration attached to reviewed Content Patcher 2.9.1: {string.Join(", ", Profiles.Select(profile => profile.Name))}.", LogLevel.Info);
        }
        catch (Exception exception)
        {
            Enabled = false;
            harmony?.UnpatchAll(harmonyId);
            monitor.Log($"Installed-source integration is unavailable: {exception.Message} Native interiors remain usable.", LogLevel.Warn);
        }
    }

    private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (Enabled && Profiles.Any(profile => profile.IsOasis && e.NameWithoutLocale.IsEquivalentTo(profile.StringsProxy)))
            e.LoadFrom(() => new Dictionary<string, string>(StringComparer.Ordinal), AssetLoadPriority.Exclusive);
    }

    public bool ValidateRecipe(InstalledSourceProfile profile, IContentPack pack)
    {
        SourceState source = sources[profile.Id];
        string hash;
        try
        {
            hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(pack.DirectoryPath, "content.json"))));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            hash = "unreadable: " + exception.Message;
        }
        if (hash == profile.RecipeHash)
        {
            source.Pack = pack;
            return true;
        }
        if (source.RejectedRecipe != hash)
        {
            source.RejectedRecipe = hash;
            monitor.Log($"Installed {profile.Name} integration rejected an unreviewed content.json. Original patches remain active; existing immutable snapshots are retained.", LogLevel.Warn);
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
        MethodInfo? method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public, null, new[] { parameter }, null);
        Type? returns = method?.ReturnType;
        if (returns is null || !returns.IsGenericType || returns.GetGenericTypeDefinition() != typeof(IEnumerable<>))
            throw new InvalidOperationException($"Unsupported Content Patcher signature: {name}.");
        Type item = returns.GetGenericArguments()[0];
        if (item.FullName != element || item.GetProperty("ContentPack")?.PropertyType != typeof(IContentPack)
            || item.GetProperty("FromAsset")?.PropertyType != typeof(string))
            throw new InvalidOperationException($"Unsupported Content Patcher patch contract: {name}.");
        return method!;
    }

    private SourceState? FindProxy(IAssetName name) => sources.Values.FirstOrDefault(source =>
        name.IsEquivalentTo(source.Profile.MapProxy) || (source.Profile.IsOasis && name.IsEquivalentTo(source.Profile.StringsProxy)));

    private static void Remap(ref IAssetName assetName)
    {
        if (active?.Enabled != true)
            return;
        SourceState? source = active.FindProxy(assetName);
        if (source is not null)
            assetName = active.helper.GameContent.ParseAssetName(assetName.IsEquivalentTo(source.Profile.MapProxy)
                ? SharedAsset : "Strings/StringsFromMaps");
    }

    private static void Filter<T>(AssetRequestedEventArgs request, ref IEnumerable<T> __result)
    {
        if (active?.Enabled != true)
            return;
        SourceState? proxy = active.FindProxy(request.NameWithoutLocale);
        if (proxy is null && !active.Profiles.Any(profile => profile.SharedAssets.Any(asset => request.NameWithoutLocale.IsEquivalentTo(asset))))
            return;
        __result = FilterOwned(__result, request, proxy).ToArray();
    }

    private static IEnumerable<T> FilterOwned<T>(IEnumerable<T> patches, AssetRequestedEventArgs request, SourceState? proxy)
    {
        foreach (T patch in patches)
        {
            IContentPack pack = (IContentPack)patch!.GetType().GetProperty("ContentPack")!.GetValue(patch)!;
            bool owned = active!.sources.TryGetValue(pack.Manifest.UniqueID, out SourceState? source);
            bool targeted = owned && (proxy is not null ? source == proxy : source!.Profile.SharedAssets.Any(asset => request.NameWithoutLocale.IsEquivalentTo(asset)));
            bool approved = targeted && active.ValidateRecipe(source!.Profile, pack);
            if (proxy is not null && approved && request.NameWithoutLocale.IsEquivalentTo(proxy.Profile.MapProxy))
            {
                string? from = ((string?)patch!.GetType().GetProperty("FromAsset")!.GetValue(patch))?.Replace('\\', '/');
                if (patch.GetType().FullName == "ContentPatcher.Framework.Patches.LoadPatch")
                    proxy.ResolvedMapFile = from;
                else if (from is not null)
                    proxy.ResolvedMapEditors.Add(from);
            }
            if (proxy is not null ? approved : !approved)
                yield return patch;
        }
    }
}
