using System.Text.RegularExpressions;

namespace StardewInteriorChanger.Core;

public readonly record struct VariantId
{
    private static readonly Regex PackIdPattern = new(
        "^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex LocalIdPattern = new(
        "^[a-z0-9][a-z0-9._-]{0,63}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private VariantId(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static bool TryCreate(string? packUniqueId, string? localId, out VariantId value)
    {
        value = default;
        if (packUniqueId is null || localId is null
            || !PackIdPattern.IsMatch(packUniqueId)
            || !LocalIdPattern.IsMatch(localId))
        {
            return false;
        }

        value = new VariantId($"{packUniqueId.ToLowerInvariant()}/{localId}");
        return true;
    }

    public static VariantId Create(string packUniqueId, string localId)
    {
        if (!TryCreate(packUniqueId, localId, out VariantId value))
        {
            throw new ArgumentException("The pack unique ID or local variant ID is invalid.");
        }

        return value;
    }

    public static bool TryParse(string? value, out VariantId variantId)
    {
        variantId = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        int separator = value.LastIndexOf('/');
        return separator > 0
            && separator < value.Length - 1
            && TryCreate(value[..separator], value[(separator + 1)..], out variantId);
    }

    public static VariantId Parse(string value)
    {
        if (!TryParse(value, out VariantId variantId))
        {
            throw new FormatException($"'{value}' is not a valid interior variant ID.");
        }

        return variantId;
    }

    public override string ToString() => Value ?? string.Empty;
}

public readonly record struct ContentHash
{
    private static readonly Regex Sha256Pattern = new(
        "^[0-9a-f]{64}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private ContentHash(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static bool TryParse(string? value, out ContentHash contentHash)
    {
        contentHash = default;
        if (value is null)
        {
            return false;
        }

        string normalized = value.ToLowerInvariant();
        if (!Sha256Pattern.IsMatch(normalized))
        {
            return false;
        }

        contentHash = new ContentHash(normalized);
        return true;
    }

    public static ContentHash Parse(string value)
    {
        if (!TryParse(value, out ContentHash contentHash))
        {
            throw new FormatException("The value is not a SHA-256 content hash.");
        }

        return contentHash;
    }

    internal static ContentHash FromDigest(ReadOnlySpan<byte> digest) =>
        new(Convert.ToHexString(digest).ToLowerInvariant());

    public override string ToString() => Value ?? string.Empty;
}

public readonly record struct InteriorInstanceId(string Value)
{
    public override string ToString() => Value ?? string.Empty;
}

public readonly record struct TargetContractId(string Value)
{
    public override string ToString() => Value ?? string.Empty;
}

public enum InteriorTarget
{
    Greenhouse,
    DeluxeBarn
}

public static class TargetContracts
{
    public static readonly TargetContractId Greenhouse = new("greenhouse/v1");
    public static readonly TargetContractId DeluxeBarn = new("deluxe-barn/v1");

    public static TargetContractId For(InteriorTarget target) => target switch
    {
        InteriorTarget.Greenhouse => Greenhouse,
        InteriorTarget.DeluxeBarn => DeluxeBarn,
        _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Unknown interior target.")
    };
}
