using System.Security.Cryptography;
using System.Text;
using StardewInteriorChanger.Core;
using StardewModdingAPI;
using xTile;
using xTile.Tiles;

namespace StardewInteriorChanger;

internal sealed record RuntimeInterior(
    RegisteredInterior Definition,
    string MapAssetKey,
    MapSnapshot Map,
    string? PreviewAssetKey,
    string SourcePackId,
    string SourcePackVersion,
    string? SourceFamilyId = null,
    bool IsCurrentSourceConfiguration = true)
{
    public VariantFingerprint Fingerprint => VariantFingerprint.From(Definition);

    public xTile.Map CreateMap() => Map.CreateMap();
}

internal interface IInteriorCatalog
{
    IReadOnlyList<RuntimeInterior> Entries { get; }

    IReadOnlyList<VariantFingerprint> Fingerprints { get; }

    void Reload();

    void AttachInstalledSources();

    bool RefreshInstalledSources();

    void InvalidateInstalledSources(IEnumerable<string> assetNames);

    bool TryGetTexture(string assetName, out TextureSnapshot texture);

    bool TryGet(string value, out RuntimeInterior interior);

    bool TryGet(VariantId id, out RuntimeInterior interior);

    bool TryGetByMapAssetKey(string value, out RuntimeInterior interior);

    bool TryGetManagedMapTarget(string value, out InteriorTarget target);
}

internal sealed class ContentPackInteriorCatalog : IInteriorCatalog
{
    private readonly IModHelper helper;
    private readonly IMonitor monitor;
    private readonly string managedMapPrefix;
    private readonly InteriorRegistryBuilder registryBuilder = new();
    private readonly InstalledSourceBridge installedBridge;
    private readonly Dictionary<string, TextureSnapshot> textures = new(StringComparer.OrdinalIgnoreCase);
    private bool sourceDirty = true;
    private bool refreshingSource;
    private string? lastConfiguration;
    private string? lastSourceFailure;
    private readonly Dictionary<VariantId, RuntimeInterior> byId = new();
    private readonly Dictionary<string, RuntimeInterior> byMapAssetKey =
        new(StringComparer.OrdinalIgnoreCase);

    public ContentPackInteriorCatalog(
        IModHelper helper,
        IMonitor monitor,
        string coreModId)
    {
        this.helper = helper;
        this.monitor = monitor;
        managedMapPrefix = $"Mods/{coreModId}/InteriorMaps";
        installedBridge = new InstalledSourceBridge(helper, monitor, coreModId);
    }

    public IReadOnlyList<RuntimeInterior> Entries { get; private set; } =
        Array.Empty<RuntimeInterior>();

    public IReadOnlyList<VariantFingerprint> Fingerprints { get; private set; } =
        Array.Empty<VariantFingerprint>();

    public void AttachInstalledSources() => installedBridge.Attach();

    public bool TryGetTexture(string assetName, out TextureSnapshot texture) =>
        textures.TryGetValue(NormalizeAssetName(assetName), out texture!);

    public void InvalidateInstalledSources(IEnumerable<string> assetNames)
    {
        if (!refreshingSource && assetNames.Any(name =>
                string.Equals(name, InstalledSourceBridge.SharedAsset, StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, InstalledSourceBridge.ProxyAsset, StringComparison.OrdinalIgnoreCase)
                || InstalledSourceSnapshot.AllowedTextureNames.Any(texture =>
                    string.Equals(name, "Maps/" + texture, StringComparison.OrdinalIgnoreCase))))
            sourceDirty = true;
    }

    public bool RefreshInstalledSources()
    {
        if (!installedBridge.Enabled || refreshingSource)
            return false;
        refreshingSource = true;
        try
        {
            if (installedBridge.SourcePack is null)
                helper.GameContent.Load<Map>(InstalledSourceBridge.ProxyAsset);
            IContentPack pack = installedBridge.SourcePack
                ?? throw new InvalidOperationException("Content Patcher has not exposed the reviewed Ellie source patch.");
            if (!installedBridge.ValidateRecipe(pack))
                throw new InvalidOperationException("Ellie's source recipe is unavailable or changed.");
            string configuration = InstalledSourceSnapshot.ReadConfiguration(pack);
            if (!sourceDirty && configuration == lastConfiguration)
                return false;
            helper.GameContent.InvalidateCache(InstalledSourceBridge.ProxyAsset);
            InstalledSourceSnapshot resolved = InstalledSourceSnapshot.Capture(helper, installedBridge, configuration);
            lastConfiguration = configuration;
            sourceDirty = false;
            lastSourceFailure = null;
            bool changed = SetCurrentSource(resolved.Definition.Id);
            if (!byId.ContainsKey(resolved.Definition.Id))
            {
                string key = CreateManagedMapAssetKey(resolved.Definition);
                var runtime = new RuntimeInterior(resolved.Definition, key, resolved.Map, null,
                    InstalledSourceBridge.SourceId, InstalledSourceBridge.SourceVersion,
                    InstalledSourceBridge.SourceId);
                byId.Add(runtime.Definition.Id, runtime);
                byMapAssetKey.Add(key, runtime);
                foreach ((string textureKey, TextureSnapshot texture) in resolved.Textures)
                    textures.TryAdd(textureKey, texture);
                changed = true;
                monitor.Log($"Registered installed Ellie configuration '{configuration}' as '{runtime.Definition.Id}' ({runtime.Definition.ContentHash}).", LogLevel.Info);
            }
            if (changed)
                PublishEntries();
            return changed;
        }
        catch (Exception exception)
        {
            sourceDirty = true;
            if (lastSourceFailure != exception.Message)
            {
                lastSourceFailure = exception.Message;
                monitor.Log($"Installed Ellie snapshot is unavailable: {exception.Message} Existing snapshots are retained; no building was changed.", LogLevel.Warn);
            }
            bool changed = SetCurrentSource(null);
            if (changed)
                PublishEntries();
            return changed;
        }
        finally
        {
            refreshingSource = false;
        }
    }

    private bool SetCurrentSource(VariantId? current)
    {
        bool changed = false;
        foreach (RuntimeInterior entry in byId.Values.Where(entry => entry.SourceFamilyId is not null).ToArray())
        {
            bool isCurrent = entry.Definition.Id == current;
            if (entry.IsCurrentSourceConfiguration == isCurrent)
                continue;
            RuntimeInterior updated = entry with { IsCurrentSourceConfiguration = isCurrent };
            byId[entry.Definition.Id] = updated;
            byMapAssetKey[entry.MapAssetKey] = updated;
            changed = true;
        }
        return changed;
    }

    private void PublishEntries()
    {
        Entries = byId.Values.OrderBy(entry => entry.Definition.Id.Value, StringComparer.Ordinal).ToArray();
        Fingerprints = Entries.Select(entry => entry.Fingerprint).ToArray();
    }

    public bool TryGet(string value, out RuntimeInterior interior)
    {
        interior = null!;
        return VariantId.TryParse(value, out VariantId id) && TryGet(id, out interior);
    }

    public bool TryGet(VariantId id, out RuntimeInterior interior) =>
        byId.TryGetValue(id, out interior!);

    public bool TryGetByMapAssetKey(string value, out RuntimeInterior interior)
    {
        string normalized = NormalizeAssetName(value);
        if (byMapAssetKey.TryGetValue(normalized, out interior!))
        {
            return true;
        }

        foreach ((string baseKey, RuntimeInterior candidate) in byMapAssetKey)
        {
            string instancePrefix = baseKey + "/Instances/";
            if (!normalized.StartsWith(instancePrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string token = normalized[instancePrefix.Length..];
            bool validInstance = candidate.Definition.Target switch
            {
                InteriorTarget.Greenhouse => string.Equals(
                    token,
                    "greenhouse",
                    StringComparison.OrdinalIgnoreCase),
                InteriorTarget.DeluxeBarn => Guid.TryParseExact(token, "N", out _),
                _ => false,
            };
            if (validInstance)
            {
                interior = candidate;
                return true;
            }
        }

        interior = null!;
        return false;
    }

    public bool TryGetManagedMapTarget(string value, out InteriorTarget target)
    {
        target = default;
        string normalized = NormalizeAssetName(value);
        string prefix = managedMapPrefix + "/";
        if (!normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string remainder = normalized[prefix.Length..];
        int separator = remainder.IndexOf('/');
        string targetValue = separator < 0 ? remainder : remainder[..separator];
        return Enum.TryParse(targetValue, ignoreCase: false, out target)
            && Enum.IsDefined(typeof(InteriorTarget), target);
    }

    public void Reload()
    {
        foreach (RuntimeInterior entry in byId.Values.Where(entry => entry.SourceFamilyId is null).ToArray())
        {
            byId.Remove(entry.Definition.Id);
            byMapAssetKey.Remove(entry.MapAssetKey);
        }

        foreach (IContentPack pack in helper.ContentPacks.GetOwned()
                     .OrderBy(pack => pack.Manifest.UniqueID, StringComparer.OrdinalIgnoreCase))
        {
            LoadPack(pack);
        }

        PublishEntries();

        monitor.Log(
            $"Loaded {Entries.Count} structurally valid interior variant(s) " +
            "from owned content packs.",
            LogLevel.Info);
    }

    private void LoadPack(IContentPack pack)
    {
        string documentPath = Path.Combine(pack.DirectoryPath, "interiors.json");
        if (!File.Exists(documentPath))
        {
            monitor.Log(
                $"Ignored content pack '{pack.Manifest.UniqueID}': interiors.json is missing.",
                LogLevel.Warn);
            return;
        }

        InteriorPackParseResult parsed;
        try
        {
            parsed = InteriorPackJson.Parse(File.ReadAllText(documentPath));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            monitor.Log(
                $"Ignored content pack '{pack.Manifest.UniqueID}': couldn't read " +
                $"interiors.json. {exception.Message}",
                LogLevel.Error);
            return;
        }

        LogDiagnostics(pack, parsed.Diagnostics);
        if (parsed.Document is null)
        {
            return;
        }

        RegistryBuildResult built;
        try
        {
            built = registryBuilder.Build(
                pack.Manifest.UniqueID,
                parsed.Document,
                new DirectoryPackFileSystem(pack.DirectoryPath));
        }
        catch (Exception exception)
        {
            monitor.Log(
                $"Ignored content pack '{pack.Manifest.UniqueID}': registry creation " +
                $"failed unexpectedly. {exception}",
                LogLevel.Error);
            return;
        }

        LogDiagnostics(pack, built.Diagnostics);
        foreach (RegisteredInterior definition in built.Registry.Entries)
        {
            RegisterRuntimeMap(pack, definition);
        }
    }

    private void RegisterRuntimeMap(IContentPack pack, RegisteredInterior definition)
    {
        if (byId.ContainsKey(definition.Id))
        {
            monitor.Log(
                $"Ignored duplicate global interior ID '{definition.Id}'.",
                LogLevel.Error);
            return;
        }

        if (definition.PreviewPath is not null && !pack.HasFile(definition.PreviewPath))
        {
            monitor.Log(
                $"Ignored interior '{definition.Id}': preview file " +
                $"'{definition.PreviewPath}' doesn't exist.",
                LogLevel.Error);
            return;
        }

        if (!TmxDependencyValidator.TryValidate(
                pack.DirectoryPath,
                definition,
                out string dependencyReason))
        {
            monitor.Log(
                $"Ignored interior '{definition.Id}': TMX dependency contract failed. " +
                dependencyReason,
                LogLevel.Error);
            return;
        }

        Map map;
        try
        {
            map = pack.ModContent.Load<Map>(definition.MapPath);
        }
        catch (Exception exception)
        {
            monitor.Log(
                $"Ignored interior '{definition.Id}': map '{definition.MapPath}' " +
                $"couldn't be loaded. {exception.Message}",
                LogLevel.Error);
            return;
        }

        if (!MapContractValidator.TryValidate(definition.Target, map, out string reason))
        {
            monitor.Log(
                $"Ignored interior '{definition.Id}': map contract failed. {reason}",
                LogLevel.Error);
            return;
        }

        if (!TryValidateGameplayDependencies(pack, definition, map, out reason))
        {
            monitor.Log(
                $"Ignored interior '{definition.Id}': gameplay dependency contract failed. " +
                reason,
                LogLevel.Error);
            return;
        }

        MapSnapshot snapshot;
        try
        {
            snapshot = MapSnapshot.Capture(map);
        }
        catch (Exception exception)
        {
            monitor.Log(
                $"Ignored interior '{definition.Id}': map couldn't be snapshotted for " +
                $"safe runtime loading. {exception.Message}",
                LogLevel.Error);
            return;
        }

        string mapAssetKey = CreateManagedMapAssetKey(definition);
        string? previewAssetKey = definition.PreviewPath is null
            ? null
            : pack.ModContent.GetInternalAssetName(definition.PreviewPath).Name;

        var runtime = new RuntimeInterior(
            definition,
            mapAssetKey,
            snapshot,
            previewAssetKey,
            pack.Manifest.UniqueID,
            pack.Manifest.Version.ToString());
        byId.Add(definition.Id, runtime);
        byMapAssetKey.Add(mapAssetKey, runtime);
    }

    private string CreateManagedMapAssetKey(RegisteredInterior definition)
    {
        byte[] idDigest = SHA256.HashData(Encoding.UTF8.GetBytes(definition.Id.Value));
        string idToken = Convert.ToHexString(idDigest).ToLowerInvariant();
        return $"{managedMapPrefix}/{definition.Target}/{idToken}/" +
            definition.ContentHash.Value;
    }

    private static string NormalizeAssetName(string value) =>
        value.Replace('\\', '/').Trim('/');

    private static bool TryValidateGameplayDependencies(
        IContentPack pack,
        RegisteredInterior definition,
        Map map,
        out string reason)
    {
        const string Probe = "__sic_path_probe__";
        string internalRootProbe = NormalizeAssetName(
            pack.ModContent.GetInternalAssetName($"{definition.GameplayRoot}/{Probe}").Name);
        string internalPackProbe = NormalizeAssetName(
            pack.ModContent.GetInternalAssetName(Probe).Name);
        string internalRoot = internalRootProbe[..^Probe.Length].TrimEnd('/');
        string internalPack = internalPackProbe[..^Probe.Length].TrimEnd('/');
        string mapDirectory = NormalizeAssetName(
            Path.GetDirectoryName(definition.MapPath.Replace('/', Path.DirectorySeparatorChar))
                ?.Replace(Path.DirectorySeparatorChar, '/')
            ?? string.Empty);

        foreach (TileSheet tileSheet in map.TileSheets)
        {
            string source = NormalizeAssetName(tileSheet.ImageSource);
            if (IsWithin(source, internalRoot))
            {
                continue;
            }

            if (IsWithin(source, internalPack)
                || source.StartsWith("Mods/", StringComparison.OrdinalIgnoreCase))
            {
                reason = $"Tile sheet '{tileSheet.Id}' resolves to '{source}', outside " +
                    "this variant's GameplayRoot. Pack-local and mod-provided map " +
                    "dependencies must stay inside GameplayRoot so they are hashed.";
                return false;
            }

            string localCandidate = NormalizeAssetName(
                mapDirectory.Length == 0 ? source : $"{mapDirectory}/{source}");
            if (pack.HasFile(localCandidate)
                && !IsWithin(localCandidate, definition.GameplayRoot))
            {
                reason = $"Tile sheet '{tileSheet.Id}' references pack file " +
                    $"'{localCandidate}' outside GameplayRoot.";
                return false;
            }
        }

        reason = string.Empty;
        return true;
    }

    private static bool IsWithin(string path, string directory) =>
        PhysicalPathSemantics.Equals(path, directory)
        || PhysicalPathSemantics.StartsWith(path, directory + "/");

    private void LogDiagnostics(
        IContentPack pack,
        IEnumerable<RegistryDiagnostic> diagnostics)
    {
        foreach (RegistryDiagnostic diagnostic in diagnostics)
        {
            string entry = diagnostic.EntryIndex is null
                ? string.Empty
                : $", entry {diagnostic.EntryIndex.Value}";
            monitor.Log(
                $"Interior pack '{pack.Manifest.UniqueID}'{entry}: " +
                $"{diagnostic.Code}: {diagnostic.Message}",
                diagnostic.Severity == RegistryDiagnosticSeverity.Error
                    ? LogLevel.Error
                    : LogLevel.Warn);
        }
    }
}
