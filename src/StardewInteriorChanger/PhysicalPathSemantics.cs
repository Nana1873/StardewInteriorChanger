namespace StardewInteriorChanger;

internal static class PhysicalPathSemantics
{
    public static StringComparison Comparison { get; } = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    public static StringComparer Comparer { get; } = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    public static bool Equals(string left, string right) =>
        string.Equals(left, right, Comparison);

    public static bool StartsWith(string value, string prefix) =>
        value.StartsWith(prefix, Comparison);
}
