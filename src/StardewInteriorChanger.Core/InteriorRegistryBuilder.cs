using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace StardewInteriorChanger.Core;

public sealed class InteriorRegistryBuilder
{
    public const int SupportedFormatVersion = 1;

    private static readonly Regex AnchorNamePattern = new(
        "^[A-Za-z][A-Za-z0-9]{0,63}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public RegistryBuildResult Build(
        string packUniqueId,
        InteriorPackDocument? document,
        IPackFileSystem files)
    {
        ArgumentNullException.ThrowIfNull(files);

        var diagnostics = new List<RegistryDiagnostic>();
        var registered = new List<RegisteredInterior>();

        if (!VariantId.TryCreate(packUniqueId, "probe", out _))
        {
            diagnostics.Add(new RegistryDiagnostic(
                RegistryDiagnosticCode.InvalidPackUniqueId,
                RegistryDiagnosticSeverity.Error,
                "The content-pack unique ID is invalid.",
                Field: "PackUniqueId"));
            return Result(registered, diagnostics);
        }

        if (document is null)
        {
            diagnostics.Add(new RegistryDiagnostic(
                RegistryDiagnosticCode.MissingInteriors,
                RegistryDiagnosticSeverity.Error,
                "The interiors document is missing."));
            return Result(registered, diagnostics);
        }

        if (document.FormatVersion != SupportedFormatVersion)
        {
            diagnostics.Add(new RegistryDiagnostic(
                RegistryDiagnosticCode.UnsupportedFormatVersion,
                RegistryDiagnosticSeverity.Error,
                $"FormatVersion must be {SupportedFormatVersion.ToString(CultureInfo.InvariantCulture)}.",
                Field: nameof(document.FormatVersion)));
            return Result(registered, diagnostics);
        }

        if (document.Interiors is null)
        {
            diagnostics.Add(new RegistryDiagnostic(
                RegistryDiagnosticCode.MissingInteriors,
                RegistryDiagnosticSeverity.Error,
                "Interiors must be an array.",
                Field: nameof(document.Interiors)));
            return Result(registered, diagnostics);
        }

        HashSet<int> duplicateIndices = FindDuplicateIndices(document.Interiors);
        for (int index = 0; index < document.Interiors.Count; index++)
        {
            InteriorDefinitionDto? definition = document.Interiors[index];
            if (definition is null)
            {
                diagnostics.Add(new RegistryDiagnostic(
                    RegistryDiagnosticCode.InvalidVariantId,
                    RegistryDiagnosticSeverity.Error,
                    "An interior entry cannot be null.",
                    index));
                continue;
            }

            if (duplicateIndices.Contains(index))
            {
                diagnostics.Add(new RegistryDiagnostic(
                    RegistryDiagnosticCode.DuplicateVariantId,
                    RegistryDiagnosticSeverity.Error,
                    "The local interior ID is duplicated in this pack.",
                    index,
                    definition.Id,
                    nameof(definition.Id)));
                continue;
            }

            RegisteredInterior? entry = ValidateEntry(
                packUniqueId,
                document.FormatVersion,
                index,
                definition,
                files,
                diagnostics);

            if (entry is not null)
            {
                registered.Add(entry);
            }
        }

        return Result(registered, diagnostics);
    }

    private static RegisteredInterior? ValidateEntry(
        string packUniqueId,
        int formatVersion,
        int index,
        InteriorDefinitionDto definition,
        IPackFileSystem files,
        List<RegistryDiagnostic> diagnostics)
    {
        var entryDiagnostics = new List<RegistryDiagnostic>();

        if (!VariantId.TryCreate(packUniqueId, definition.Id, out VariantId variantId)
            || string.Equals(definition.Id, "vanilla", StringComparison.Ordinal))
        {
            entryDiagnostics.Add(Error(
                RegistryDiagnosticCode.InvalidVariantId,
                "Id must be a lowercase ASCII slug and cannot be 'vanilla'.",
                index,
                definition.Id,
                nameof(definition.Id)));
        }

        if (!TryParseTarget(definition.Target, out InteriorTarget target))
        {
            entryDiagnostics.Add(Error(
                RegistryDiagnosticCode.InvalidTarget,
                "Target must be exactly 'Greenhouse' or 'DeluxeBarn'.",
                index,
                definition.Id,
                nameof(definition.Target)));
        }

        if (!ContentPath.TryNormalize(definition.GameplayRoot, out string gameplayRoot))
        {
            entryDiagnostics.Add(Error(
                RegistryDiagnosticCode.InvalidGameplayRoot,
                "GameplayRoot must be a safe relative directory.",
                index,
                definition.Id,
                nameof(definition.GameplayRoot)));
        }

        if (!ContentPath.TryNormalize(definition.Map, out string mapRelativePath))
        {
            entryDiagnostics.Add(Error(
                RegistryDiagnosticCode.InvalidMapPath,
                "Map must be a safe path relative to GameplayRoot.",
                index,
                definition.Id,
                nameof(definition.Map)));
        }

        string? previewPath = null;
        if (!string.IsNullOrWhiteSpace(definition.Preview)
            && !ContentPath.TryNormalize(definition.Preview, out previewPath))
        {
            entryDiagnostics.Add(Error(
                RegistryDiagnosticCode.InvalidPreviewPath,
                "Preview must be a safe path relative to the content-pack root.",
                index,
                definition.Id,
                nameof(definition.Preview)));
        }

        IReadOnlyDictionary<string, IReadOnlyList<TilePoint>> anchors =
            ValidateAnchors(definition.Anchors, index, definition.Id, entryDiagnostics);

        if (entryDiagnostics.Count > 0)
        {
            diagnostics.AddRange(entryDiagnostics);
            return null;
        }

        string mapPath = ContentPath.Combine(gameplayRoot, mapRelativePath);
        bool mapExists;
        try
        {
            mapExists = files.FileExists(mapPath);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or ArgumentException)
        {
            diagnostics.Add(Error(
                RegistryDiagnosticCode.ContentReadFailed,
                exception.Message,
                index,
                variantId.Value,
                nameof(definition.Map)));
            return null;
        }

        if (!mapExists)
        {
            diagnostics.Add(Error(
                RegistryDiagnosticCode.MissingMapFile,
                $"The map file '{mapPath}' does not exist.",
                index,
                variantId.Value,
                nameof(definition.Map)));
            return null;
        }

        string[] contentFiles;
        try
        {
            contentFiles = ValidateAndOrderContentFiles(
                files.EnumerateFiles(gameplayRoot),
                gameplayRoot,
                index,
                variantId.Value,
                diagnostics);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or ArgumentException)
        {
            diagnostics.Add(Error(
                RegistryDiagnosticCode.ContentEnumerationFailed,
                exception.Message,
                index,
                variantId.Value,
                nameof(definition.GameplayRoot)));
            return null;
        }

        if (diagnostics.Any(diagnostic => diagnostic.EntryIndex == index
            && diagnostic.Severity == RegistryDiagnosticSeverity.Error))
        {
            return null;
        }

        if (!contentFiles.Contains(mapPath, StringComparer.Ordinal))
        {
            diagnostics.Add(Error(
                RegistryDiagnosticCode.MissingMapFile,
                "The map must be enumerated beneath GameplayRoot with matching path casing.",
                index,
                variantId.Value,
                nameof(definition.Map)));
            return null;
        }

        ContentHash contentHash;
        try
        {
            contentHash = VariantContentHasher.Compute(
                formatVersion,
                variantId,
                target,
                gameplayRoot,
                mapRelativePath,
                anchors,
                contentFiles,
                files);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or ArgumentException)
        {
            diagnostics.Add(Error(
                RegistryDiagnosticCode.ContentReadFailed,
                exception.Message,
                index,
                variantId.Value,
                nameof(definition.GameplayRoot)));
            return null;
        }

        string displayName = string.IsNullOrWhiteSpace(definition.DisplayName)
            ? definition.Id!
            : definition.DisplayName.Trim();

        return new RegisteredInterior(
            variantId,
            displayName,
            target,
            TargetContracts.For(target),
            gameplayRoot,
            mapPath,
            previewPath,
            anchors,
            contentHash);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<TilePoint>> ValidateAnchors(
        Dictionary<string, List<TilePointDto>?>? source,
        int index,
        string? localId,
        List<RegistryDiagnostic> diagnostics)
    {
        if (source is null || source.Count == 0)
        {
            return new ReadOnlyDictionary<string, IReadOnlyList<TilePoint>>(
                new Dictionary<string, IReadOnlyList<TilePoint>>(StringComparer.OrdinalIgnoreCase));
        }

        var result = new Dictionary<string, IReadOnlyList<TilePoint>>(StringComparer.OrdinalIgnoreCase);
        foreach ((string name, List<TilePointDto>? points) in source.OrderBy(
            pair => pair.Key,
            StringComparer.OrdinalIgnoreCase))
        {
            if (!AnchorNamePattern.IsMatch(name)
                || points is null
                || points.Count == 0
                || result.ContainsKey(name))
            {
                diagnostics.Add(Error(
                    RegistryDiagnosticCode.InvalidAnchor,
                    $"Anchor '{name}' must have a valid unique name and at least one point.",
                    index,
                    localId,
                    nameof(InteriorDefinitionDto.Anchors)));
                continue;
            }

            TilePoint[] normalized = points
                .Select(point => new TilePoint(point.X, point.Y))
                .OrderBy(point => point.X)
                .ThenBy(point => point.Y)
                .ToArray();

            if (normalized.Any(point => point.X < 0 || point.Y < 0)
                || normalized.Distinct().Count() != normalized.Length)
            {
                diagnostics.Add(Error(
                    RegistryDiagnosticCode.InvalidAnchor,
                    $"Anchor '{name}' contains a negative or duplicate tile coordinate.",
                    index,
                    localId,
                    nameof(InteriorDefinitionDto.Anchors)));
                continue;
            }

            result.Add(name, Array.AsReadOnly(normalized));
        }

        return new ReadOnlyDictionary<string, IReadOnlyList<TilePoint>>(result);
    }

    private static string[] ValidateAndOrderContentFiles(
        IEnumerable<string> source,
        string gameplayRoot,
        int index,
        string variantId,
        List<RegistryDiagnostic> diagnostics)
    {
        var paths = new List<string>();
        foreach (string candidate in source)
        {
            if (!ContentPath.TryNormalize(candidate, out string path)
                || !ContentPath.IsWithin(path, gameplayRoot))
            {
                diagnostics.Add(Error(
                    RegistryDiagnosticCode.InvalidContentFilePath,
                    $"Enumerated content path '{candidate}' is invalid or outside GameplayRoot.",
                    index,
                    variantId,
                    nameof(InteriorDefinitionDto.GameplayRoot)));
                continue;
            }

            paths.Add(path);
        }

        foreach (IGrouping<string, string> collision in paths.GroupBy(
            path => path,
            StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1))
        {
            diagnostics.Add(Error(
                RegistryDiagnosticCode.DuplicateContentFilePath,
                $"GameplayRoot contains a case-insensitive path collision for '{collision.Key}'.",
                index,
                variantId,
                nameof(InteriorDefinitionDto.GameplayRoot)));
        }

        return paths.Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal).ToArray();
    }

    private static HashSet<int> FindDuplicateIndices(IReadOnlyList<InteriorDefinitionDto> entries)
    {
        return entries
            .Select((entry, index) => new { entry?.Id, Index = index })
            .Where(item => item.Id is not null)
            .GroupBy(item => item.Id!, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .SelectMany(group => group.Select(item => item.Index))
            .ToHashSet();
    }

    private static bool TryParseTarget(string? value, out InteriorTarget target) =>
        Enum.TryParse(value, ignoreCase: false, out target)
        && Enum.IsDefined(typeof(InteriorTarget), target);

    private static RegistryDiagnostic Error(
        RegistryDiagnosticCode code,
        string message,
        int? index,
        string? variantId,
        string? field) => new(
            code,
            RegistryDiagnosticSeverity.Error,
            message,
            index,
            variantId,
            field);

    private static RegistryBuildResult Result(
        IEnumerable<RegisteredInterior> entries,
        IEnumerable<RegistryDiagnostic> diagnostics) => new(
            new InteriorRegistry(entries),
            diagnostics
                .OrderBy(diagnostic => diagnostic.EntryIndex ?? -1)
                .ThenBy(diagnostic => diagnostic.Code)
                .ThenBy(diagnostic => diagnostic.Field, StringComparer.Ordinal)
                .ToArray());
}

internal static class VariantContentHasher
{
    public static ContentHash Compute(
        int formatVersion,
        VariantId variantId,
        InteriorTarget target,
        string gameplayRoot,
        string mapRelativePath,
        IReadOnlyDictionary<string, IReadOnlyList<TilePoint>> anchors,
        IReadOnlyList<string> contentFiles,
        IPackFileSystem files)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        AppendToken(hash, "StardewInteriorChanger.VariantHash/v1");
        AppendToken(hash, formatVersion.ToString(CultureInfo.InvariantCulture));
        AppendToken(hash, variantId.Value);
        AppendToken(hash, target.ToString());
        AppendToken(hash, gameplayRoot);
        AppendToken(hash, mapRelativePath);

        AppendInt32(hash, anchors.Count);
        foreach ((string name, IReadOnlyList<TilePoint> points) in anchors.OrderBy(
            pair => pair.Key,
            StringComparer.OrdinalIgnoreCase))
        {
            AppendToken(hash, name.ToLowerInvariant());
            AppendInt32(hash, points.Count);
            foreach (TilePoint point in points.OrderBy(point => point.X).ThenBy(point => point.Y))
            {
                AppendInt32(hash, point.X);
                AppendInt32(hash, point.Y);
            }
        }

        AppendInt32(hash, contentFiles.Count);
        foreach (string path in contentFiles.OrderBy(path => path, StringComparer.Ordinal))
        {
            AppendToken(hash, path);
            AppendBytes(hash, files.ReadAllBytes(path));
        }

        return ContentHash.FromDigest(hash.GetHashAndReset());
    }

    private static void AppendToken(IncrementalHash hash, string value) =>
        AppendBytes(hash, Encoding.UTF8.GetBytes(value));

    private static void AppendBytes(IncrementalHash hash, byte[] value)
    {
        AppendInt32(hash, value.Length);
        hash.AppendData(value);
    }

    private static void AppendInt32(IncrementalHash hash, int value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(buffer, value);
        hash.AppendData(buffer);
    }
}
