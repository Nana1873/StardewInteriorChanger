namespace StardewInteriorChanger;

internal sealed record InstalledSourceProfile(string Id, string Version, string RecipeHash, string Name, string Token)
{
    public static readonly InstalledSourceProfile Ellie = new(
        "Ellibehr.ElliesIdealGreenhouse", "1.5.0",
        "621A3C8F70C5B4199011F7AE5785CC36CE202EB461D6B1E4BC62D607C44DAFEE", "Ellie", "Ellie");
    public static readonly InstalledSourceProfile Oasis = new(
        "DaisyNiko.OasisGreenhouse", "1.9.4",
        "6C836B131C6228AA1AA209D470B587DBA0D32E195410470777DDEBFE50FBF6B7", "Oasis", "Oasis");
    public static readonly InstalledSourceProfile Nykachu = new(
        "nykachu.coopbarnfacelift", "1.2.0",
        "FB6414CD968347F9FA260DC4525AE3F066B555F21281C2284E5829984DFB107F", "Coop and Barn Facelift", "Nykachu");
    public static readonly InstalledSourceProfile Green = new(
        "vikich3rry.GreenCoopsBarns", "1.0.4",
        "DDB108D70623C635E67896245404BFA36B13978160F3B1112692924903AA1220", "Green Coops and Barns", "Green");
    public static readonly InstalledSourceProfile[] All = { Ellie, Oasis, Nykachu, Green };
    public static readonly string[] AnimalAssets = { "Maps/Barn", "Maps/Barn2", "Maps/Barn3", "Maps/Coop", "Maps/Coop2", "Maps/Coop3" };
    public bool IsOasis => Id == Oasis.Id;
    public bool IsGreen => Id == Green.Id;
    public bool IsAnimalHouse => Id == Nykachu.Id || IsGreen;
    public string MapProxy => $"Mods/StardewInteriorChanger.Core/InstalledSources/{Token}";
    public string StringsProxy => MapProxy + "Strings";
    public string ProxyFor(string sharedAsset) => IsAnimalHouse ? MapProxy + "/" + sharedAsset.Split('/').Last() : MapProxy;
    public IEnumerable<string> MapAssets => IsAnimalHouse ? AnimalAssets : new[] { "Maps/Greenhouse" };
    public string ExpectedAnimalMap(string sharedAsset) => IsGreen
        ? "assets/Green_" + sharedAsset.Split('/').Last() + ".tmx"
        : "assets/" + sharedAsset.Split('/').Last() + "Custom.tbin";
    public IEnumerable<string> SharedAssets => IsAnimalHouse ? AnimalAssets : IsOasis
        ? new[] { "Maps/Greenhouse", "Strings/StringsFromMaps", "Data/Minecarts" }
        : new[] { "Maps/Greenhouse" };
}
