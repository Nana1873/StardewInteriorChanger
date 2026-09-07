namespace StardewInteriorChanger.Core;

public sealed record InteriorMenuVariant(
    VariantId Id,
    string DisplayName,
    InteriorTarget Target,
    ContentHash ContentHash,
    string SourcePackId,
    string SourcePackVersion,
    string? PreviewAssetKey);

public sealed record InteriorMenuStoredChoice(
    bool IsValid,
    bool IsBase,
    VariantId? VariantId,
    ContentHash? ContentHash,
    string? Error)
{
    public static InteriorMenuStoredChoice Base() => new(true, true, null, null, null);

    public static InteriorMenuStoredChoice Custom(VariantId id, ContentHash hash) =>
        new(true, false, id, hash, null);

    public static InteriorMenuStoredChoice Invalid(string error) =>
        new(false, false, null, null, error);
}

public enum InteriorMenuWarningKind
{
    None,
    InvalidStoredSelection,
    MissingVariant,
    ContentHashMismatch
}

public sealed record InteriorMenuWarning(
    InteriorMenuWarningKind Kind,
    string? VariantId,
    string? Detail)
{
    public static InteriorMenuWarning None { get; } =
        new(InteriorMenuWarningKind.None, null, null);
}

public sealed record InteriorMenuOption(
    string? VariantId,
    string DisplayName,
    string SourcePackId,
    string SourcePackVersion,
    string? PreviewAssetKey,
    bool IsBase,
    bool IsCurrent);

public sealed record InteriorMenuView(
    IReadOnlyList<InteriorMenuOption> Options,
    InteriorMenuWarning Warning);

public static class InteriorMenuStateBuilder
{
    public static InteriorMenuView Build(
        InteriorTarget target,
        IEnumerable<InteriorMenuVariant> variants,
        InteriorMenuStoredChoice storedChoice)
    {
        InteriorMenuVariant[] compatible = variants
            .Where(variant => variant.Target == target)
            .OrderBy(variant => variant.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(variant => variant.DisplayName, StringComparer.Ordinal)
            .ThenBy(variant => variant.Id.Value, StringComparer.Ordinal)
            .ToArray();

        bool baseIsCurrent = storedChoice.IsValid && storedChoice.IsBase;
        var options = new List<InteriorMenuOption>(compatible.Length + 1)
        {
            new(
                null,
                string.Empty,
                string.Empty,
                string.Empty,
                null,
                IsBase: true,
                IsCurrent: baseIsCurrent),
        };

        foreach (InteriorMenuVariant variant in compatible)
        {
            bool isCurrent = storedChoice.IsValid
                && !storedChoice.IsBase
                && storedChoice.VariantId == variant.Id
                && storedChoice.ContentHash == variant.ContentHash;
            options.Add(new InteriorMenuOption(
                variant.Id.Value,
                variant.DisplayName,
                variant.SourcePackId,
                variant.SourcePackVersion,
                variant.PreviewAssetKey,
                IsBase: false,
                IsCurrent: isCurrent));
        }

        return new InteriorMenuView(options, GetWarning(compatible, storedChoice));
    }

    private static InteriorMenuWarning GetWarning(
        IReadOnlyList<InteriorMenuVariant> compatible,
        InteriorMenuStoredChoice storedChoice)
    {
        if (!storedChoice.IsValid)
        {
            return new InteriorMenuWarning(
                InteriorMenuWarningKind.InvalidStoredSelection,
                null,
                storedChoice.Error);
        }

        if (storedChoice.IsBase || storedChoice.VariantId is null)
        {
            return InteriorMenuWarning.None;
        }

        InteriorMenuVariant? installed = compatible.FirstOrDefault(
            variant => variant.Id == storedChoice.VariantId.Value);
        if (installed is null)
        {
            return new InteriorMenuWarning(
                InteriorMenuWarningKind.MissingVariant,
                storedChoice.VariantId.Value.Value,
                null);
        }

        if (installed.ContentHash != storedChoice.ContentHash)
        {
            return new InteriorMenuWarning(
                InteriorMenuWarningKind.ContentHashMismatch,
                storedChoice.VariantId.Value.Value,
                null);
        }

        return InteriorMenuWarning.None;
    }
}

public sealed record InteriorMenuRequest(
    long Sequence,
    string BuildingId,
    string? VariantId);

public sealed class InteriorMenuRequestTracker
{
    private long nextSequence;

    public InteriorMenuRequest? Pending { get; private set; }

    public bool TryBegin(
        string buildingId,
        string? variantId,
        out InteriorMenuRequest request)
    {
        if (Pending is not null)
        {
            request = Pending;
            return false;
        }

        request = new InteriorMenuRequest(++nextSequence, buildingId, variantId);
        Pending = request;
        return true;
    }

    public bool TryComplete(
        string buildingId,
        string? variantId,
        out InteriorMenuRequest? request)
    {
        request = Pending;
        if (request is null
            || !string.Equals(request.BuildingId, buildingId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(request.VariantId, variantId, StringComparison.Ordinal))
        {
            return false;
        }

        Pending = null;
        return true;
    }

    public bool TryCancel(InteriorMenuRequest request)
    {
        if (Pending?.Sequence != request.Sequence)
        {
            return false;
        }

        Pending = null;
        return true;
    }

    public void Reset() => Pending = null;
}
