using StardewInteriorChanger.Core;

namespace StardewInteriorChanger.Core.Tests;

public sealed class AnimalSourceConfigurationTests
{
    [Fact]
    public void MissingNykachuConfigUsesExactOriginalDefaults()
    {
        AnimalSourceConfiguration config = AnimalSourceConfiguration.Parse(AnimalSourceKind.Nykachu, null);
        Assert.Equal("wallcolor=cream\nwoodcolor=vanilla\nrecolor_craftables=false\nrecolor_hay=false", config.Canonical);
        Assert.Equal(new[] { "assets/colors/coopTiles_wall_cream.png", "assets/colors/coopTiles_wood_vanilla.png" }, config.TextureFiles);
    }

    [Fact]
    public void MissingGreenConfigKeepsAllOriginalCompatibilityTogglesEnabled()
    {
        AnimalSourceConfiguration config = AnimalSourceConfiguration.Parse(AnimalSourceKind.Green, null);
        Assert.Equal("Map Recolor=Vanilla\nSVE's Premium Buildings=true\nJen's Mega Buildings=true\nResource Chickens' Giant Coop=true", config.Canonical);
        Assert.Contains("assets/recolors/vikiwindow_Vanilla.png", config.TextureFiles);
    }

    [Fact]
    public void KeyAndValueCasingAndOrderDoNotChangeFingerprintInput()
    {
        var first = new Dictionary<string, string?> { ["WALLCOLOR"] = "TDBLUE", ["WOODCOLOR"] = "td", ["RECOLOR_HAY"] = "TRUE" };
        var second = new Dictionary<string, string?> { ["recolor_hay"] = "true", ["woodcolor"] = "TD", ["wallcolor"] = "tdblue" };
        AnimalSourceConfiguration a = AnimalSourceConfiguration.Parse(AnimalSourceKind.Nykachu, first);
        AnimalSourceConfiguration b = AnimalSourceConfiguration.Parse(AnimalSourceKind.Nykachu, second);
        Assert.Equal(a.Canonical, b.Canonical);
        Assert.Equal("assets/colors/coopTiles_wood_TD.png", a.TextureFiles[1]);
    }

    [Theory]
    [InlineData("Vanilla")]
    [InlineData("VPR")]
    [InlineData("Earthy")]
    [InlineData("Elegant")]
    [InlineData("Rustic")]
    [InlineData("RusticAlt")]
    public void EveryDeclaredGreenRecolorResolvesItsOwnCoopAndWindowFiles(string recolor)
    {
        var config = AnimalSourceConfiguration.Parse(AnimalSourceKind.Green,
            new Dictionary<string, string?> { ["map recolor"] = recolor.ToLowerInvariant() });
        Assert.Equal($"assets/recolors/coopTiles_wood_{recolor}.png", config.TextureFiles[0]);
        Assert.Equal($"assets/recolors/vikiwindow_{recolor}.png", config.TextureFiles[2]);
    }

    [Theory]
    [InlineData("Map Recolor", "../../external")]
    [InlineData("Map Recolor", "Vanilla ")]
    [InlineData("Map Recolor", null)]
    [InlineData("SVE's Premium Buildings", "yes")]
    [InlineData("Unknown", "true")]
    public void UnsupportedGreenOptionsFailClosed(string key, string? value)
    {
        var error = Assert.Throws<InvalidOperationException>(() => AnimalSourceConfiguration.Parse(AnimalSourceKind.Green,
            new Dictionary<string, string?> { [key] = value }));
        Assert.Contains(key, error.Message);
    }

    [Fact]
    public void CaseDuplicateOptionsAreAmbiguousAndRejected()
    {
        Assert.Throws<InvalidOperationException>(() => AnimalSourceConfiguration.Parse(AnimalSourceKind.Nykachu,
            new Dictionary<string, string?> { ["wallcolor"] = "cream", ["WallColor"] = "red" }));
    }

    [Theory]
    [InlineData("wallcolor", "ultraviolet")]
    [InlineData("woodcolor", "Earthy")]
    [InlineData("recolor_hay", "1")]
    public void UnsupportedNykachuValuesAreNotUsedAsSourcePaths(string key, string value)
    {
        Assert.Throws<InvalidOperationException>(() => AnimalSourceConfiguration.Parse(AnimalSourceKind.Nykachu,
            new Dictionary<string, string?> { [key] = value }));
    }

    [Fact]
    public void GlobalSpriteOptionsRemainInCanonicalConfigWithoutBecomingPrivateTextureEdits()
    {
        var baseline = AnimalSourceConfiguration.Parse(AnimalSourceKind.Nykachu,
            new Dictionary<string, string?> { ["woodcolor"] = "TD" });
        var changed = AnimalSourceConfiguration.Parse(AnimalSourceKind.Nykachu,
            new Dictionary<string, string?> { ["woodcolor"] = "TD", ["recolor_craftables"] = "true", ["recolor_hay"] = "true" });
        Assert.NotEqual(baseline.Canonical, changed.Canonical);
        Assert.Equal(baseline.TextureFiles, changed.TextureFiles);
        Assert.DoesNotContain(changed.TextureFiles, path => path.Contains("craftables") || path.EndsWith("/hay.png"));
    }
}
