using System.Collections.ObjectModel;

namespace StardewInteriorChanger.Core;

public enum AnimalSourceKind
{
    Nykachu,
    Green
}

public sealed record AnimalSourceConfiguration
{
    private static readonly string[] WallColors =
    {
        "vanilla", "red", "orange", "green", "blue", "purple", "cream", "chocolate", "teal", "pink",
        "pastelpink", "tdblue", "tdgreen", "tdchocolate", "tdrosegold", "tdgold", "tdred", "tdwhite", "tdpurple"
    };
    private static readonly string[] Recolors = { "Vanilla", "VPR", "Earthy", "Elegant", "Rustic", "RusticAlt" };
    private static readonly string[] NykachuKeys = { "wallcolor", "woodcolor", "recolor_craftables", "recolor_hay" };
    private static readonly string[] GreenKeys =
    {
        "Map Recolor", "SVE's Premium Buildings", "Jen's Mega Buildings", "Resource Chickens' Giant Coop"
    };

    public AnimalSourceKind Kind { get; }
    public string Canonical { get; }
    public IReadOnlyDictionary<string, string> Values { get; }
    public string WallColor => Values["wallcolor"];
    public string WoodColor => Values["woodcolor"];
    public string MapRecolor => Values["Map Recolor"];

    private AnimalSourceConfiguration(AnimalSourceKind kind, Dictionary<string, string> values, string[] order)
    {
        Kind = kind;
        Values = new ReadOnlyDictionary<string, string>(values);
        Canonical = string.Join("\n", order.Select(key => key + "=" + values[key]));
    }

    public static AnimalSourceConfiguration Parse(
        AnimalSourceKind kind,
        IReadOnlyDictionary<string, string?>? values)
    {
        string[] keys = kind switch
        {
            AnimalSourceKind.Nykachu => NykachuKeys,
            AnimalSourceKind.Green => GreenKeys,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        var supplied = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in values ?? new Dictionary<string, string?>())
        {
            if (!keys.Contains(pair.Key, StringComparer.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Unsupported {kind} configuration option '{pair.Key}'.");
            if (!supplied.TryAdd(pair.Key, pair.Value))
                throw new InvalidOperationException($"Duplicate {kind} configuration option '{pair.Key}' with different casing.");
        }

        string Read(string key, string fallback, IEnumerable<string> allowed)
        {
            if (!supplied.TryGetValue(key, out string? raw)) return fallback;
            return allowed.FirstOrDefault(value => string.Equals(value, raw, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"Unsupported {kind} value '{raw ?? "<null>"}' for '{key}'. Allowed values: {string.Join(", ", allowed)}.");
        }
        string ReadBool(string key, string fallback) => Read(key, fallback, new[] { "true", "false" });

        Dictionary<string, string> normalized = kind == AnimalSourceKind.Nykachu
            ? new(StringComparer.Ordinal)
            {
                ["wallcolor"] = Read("wallcolor", "cream", WallColors),
                ["woodcolor"] = Read("woodcolor", "vanilla", new[] { "vanilla", "TD" }),
                ["recolor_craftables"] = ReadBool("recolor_craftables", "false"),
                ["recolor_hay"] = ReadBool("recolor_hay", "false")
            }
            : new(StringComparer.Ordinal)
            {
                ["Map Recolor"] = Read("Map Recolor", "Vanilla", Recolors),
                ["SVE's Premium Buildings"] = ReadBool("SVE's Premium Buildings", "true"),
                ["Jen's Mega Buildings"] = ReadBool("Jen's Mega Buildings", "true"),
                ["Resource Chickens' Giant Coop"] = ReadBool("Resource Chickens' Giant Coop", "true")
            };
        return new AnimalSourceConfiguration(kind, normalized, keys);
    }

    public IReadOnlyList<string> TextureFiles => Kind == AnimalSourceKind.Nykachu
        ? new[] { $"assets/colors/coopTiles_wall_{WallColor}.png", $"assets/colors/coopTiles_wood_{WoodColor}.png" }
        : new[]
        {
            $"assets/recolors/coopTiles_wood_{MapRecolor}.png", "assets/recolors/vikiwindow.png",
            $"assets/recolors/vikiwindow_{MapRecolor}.png"
        };
}
