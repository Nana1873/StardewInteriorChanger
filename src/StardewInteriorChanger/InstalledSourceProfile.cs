namespace StardewInteriorChanger;

internal sealed record InstalledSourceProfile(string Id, string Version, string RecipeHash, string Name, string Token)
{
    public static readonly InstalledSourceProfile Ellie = new(
        "Ellibehr.ElliesIdealGreenhouse", "1.5.0",
        "621A3C8F70C5B4199011F7AE5785CC36CE202EB461D6B1E4BC62D607C44DAFEE", "Ellie", "Ellie");
    public static readonly InstalledSourceProfile Oasis = new(
        "DaisyNiko.OasisGreenhouse", "1.9.4",
        "6C836B131C6228AA1AA209D470B587DBA0D32E195410470777DDEBFE50FBF6B7", "Oasis", "Oasis");
    public static readonly InstalledSourceProfile[] All = { Ellie, Oasis };
    public bool IsOasis => Id == Oasis.Id;
    public string MapProxy => $"Mods/StardewInteriorChanger.Core/InstalledSources/{Token}";
    public string StringsProxy => MapProxy + "Strings";
    public IEnumerable<string> SharedAssets => IsOasis
        ? new[] { "Maps/Greenhouse", "Strings/StringsFromMaps", "Data/Minecarts" }
        : new[] { "Maps/Greenhouse" };
}
