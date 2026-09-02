using StardewInteriorChanger;
using StardewInteriorChanger.Core;

namespace StardewInteriorChanger.Core.Tests;

public sealed class TmxDependencyValidatorTests
{
    [Fact]
    public void PhysicalPathSemantics_MatchesDirectoryPackFileSystemCasing()
    {
        const string DeclaredRoot = "pack/assets/Gameplay/";
        const string CaseVariant = "pack/assets/gameplay/tiles.png";

        Assert.Equal(
            OperatingSystem.IsWindows(),
            PhysicalPathSemantics.StartsWith(CaseVariant, DeclaredRoot));
    }

    [Fact]
    public void TryValidate_MissingTilesheetImage_IsRejected()
    {
        using var fixture = new TemporaryTmxPack("assets/gameplay");
        fixture.WriteMap("""
            <?xml version="1.0" encoding="UTF-8"?>
            <map>
              <tileset>
                <image source="missing.png" />
              </tileset>
            </map>
            """);

        bool valid = TmxDependencyValidator.TryValidate(
            fixture.PackPath,
            fixture.CreateDefinition(),
            out string reason);

        Assert.False(valid);
        Assert.Contains("doesn't exist inside GameplayRoot", reason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("paths", "townInterior")]
    [InlineData("coopTiles", "paths")]
    public void TryValidate_ExtensionlessVanillaTilesheets_AreAccepted(
        string firstSource,
        string secondSource)
    {
        using var fixture = new TemporaryTmxPack("assets/gameplay");
        fixture.WriteMap($"""
            <?xml version="1.0" encoding="UTF-8"?>
            <map>
              <tileset>
                <image source="{firstSource}" />
              </tileset>
              <tileset>
                <image source="{secondSource}" />
              </tileset>
            </map>
            """);

        bool valid = TmxDependencyValidator.TryValidate(
            fixture.PackPath,
            fixture.CreateDefinition(),
            out string reason);

        Assert.True(valid, reason);
    }

    [Fact]
    public void TryValidate_CaseDistinctSiblingOutsideGameplayRoot_IsRejectedOnCaseSensitiveSystems()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = new TemporaryTmxPack("assets/Gameplay");
        fixture.WriteMap("""
            <?xml version="1.0" encoding="UTF-8"?>
            <map>
              <tileset>
                <image source="../gameplay/tiles.png" />
              </tileset>
            </map>
            """);
        string sibling = Path.Combine(fixture.PackPath, "assets", "gameplay");
        Directory.CreateDirectory(sibling);
        File.WriteAllBytes(Path.Combine(sibling, "tiles.png"), new byte[] { 1 });

        bool valid = TmxDependencyValidator.TryValidate(
            fixture.PackPath,
            fixture.CreateDefinition(),
            out string reason);

        Assert.False(valid);
        Assert.Contains("outside GameplayRoot", reason, StringComparison.Ordinal);
    }

    private sealed class TemporaryTmxPack : IDisposable
    {
        private readonly string gameplayRoot;

        public TemporaryTmxPack(string gameplayRoot)
        {
            this.gameplayRoot = gameplayRoot;
            PackPath = Path.Combine(
                Path.GetTempPath(),
                "StardewInteriorChanger.TmxDependencyValidatorTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(PackPath, ToPlatformPath(gameplayRoot)));
        }

        public string PackPath { get; }

        public RegisteredInterior CreateDefinition() => new(
            VariantId.Create("Example.Interiors", "test"),
            "Test",
            InteriorTarget.Greenhouse,
            TargetContracts.Greenhouse,
            gameplayRoot,
            $"{gameplayRoot}/map.tmx",
            null,
            new Dictionary<string, IReadOnlyList<TilePoint>>(),
            ContentHash.Parse(new string('a', 64)));

        public void WriteMap(string contents) =>
            File.WriteAllText(
                Path.Combine(PackPath, ToPlatformPath(gameplayRoot), "map.tmx"),
                contents);

        public void Dispose()
        {
            if (Directory.Exists(PackPath))
            {
                Directory.Delete(PackPath, recursive: true);
            }
        }

        private static string ToPlatformPath(string path) =>
            path.Replace('/', Path.DirectorySeparatorChar);
    }
}
