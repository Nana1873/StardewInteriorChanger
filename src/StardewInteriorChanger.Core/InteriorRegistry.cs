using System.Collections.ObjectModel;

namespace StardewInteriorChanger.Core;

public readonly record struct TilePoint(int X, int Y);

public sealed record RegisteredInterior(
    VariantId Id,
    string DisplayName,
    InteriorTarget Target,
    TargetContractId TargetContract,
    string GameplayRoot,
    string MapPath,
    string? PreviewPath,
    IReadOnlyDictionary<string, IReadOnlyList<TilePoint>> Anchors,
    ContentHash ContentHash);

public sealed class InteriorRegistry
{
    private readonly IReadOnlyDictionary<VariantId, RegisteredInterior> byId;

    internal InteriorRegistry(IEnumerable<RegisteredInterior> entries)
    {
        RegisteredInterior[] ordered = entries
            .OrderBy(entry => entry.Id.Value, StringComparer.Ordinal)
            .ToArray();

        Entries = Array.AsReadOnly(ordered);
        byId = new ReadOnlyDictionary<VariantId, RegisteredInterior>(
            ordered.ToDictionary(entry => entry.Id));
    }

    public IReadOnlyList<RegisteredInterior> Entries { get; }

    public bool TryGet(VariantId id, out RegisteredInterior interior) =>
        byId.TryGetValue(id, out interior!);
}

public sealed record RegistryBuildResult(
    InteriorRegistry Registry,
    IReadOnlyList<RegistryDiagnostic> Diagnostics)
{
    public bool HasErrors => Diagnostics.Any(
        diagnostic => diagnostic.Severity == RegistryDiagnosticSeverity.Error);
}
