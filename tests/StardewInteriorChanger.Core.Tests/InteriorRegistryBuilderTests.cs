using System.Text;
using StardewInteriorChanger.Core;

namespace StardewInteriorChanger.Core.Tests;

public sealed class InteriorRegistryBuilderTests
{
    private const string PackId = "Example.Interiors";
    private readonly InteriorRegistryBuilder builder = new();

    [Fact]
    public void Build_ValidGreenhouseAndDeluxeBarn_RegistersDeterministically()
    {
        InteriorPackDocument document = Document(
            Definition("barn", "DeluxeBarn", "assets/barn"),
            Definition("greenhouse", "Greenhouse", "assets/greenhouse"));
        MemoryPackFileSystem files = MemoryPackFileSystem.Create(
            ("assets/greenhouse/interior.tmx", "greenhouse-map"),
            ("assets/barn/interior.tmx", "barn-map"));

        RegistryBuildResult result = builder.Build(PackId, document, files);

        Assert.False(result.HasErrors);
        Assert.Equal(
            new[] { "example.interiors/barn", "example.interiors/greenhouse" },
            result.Registry.Entries.Select(entry => entry.Id.Value));
        Assert.Equal(TargetContracts.DeluxeBarn, result.Registry.Entries[0].TargetContract);
        Assert.Equal(TargetContracts.Greenhouse, result.Registry.Entries[1].TargetContract);
    }

    [Fact]
    public void Build_InvalidSibling_DoesNotDiscardValidEntry()
    {
        InteriorPackDocument document = Document(
            Definition("valid", "Greenhouse", "assets/valid"),
            Definition("Invalid Uppercase", "Greenhouse", "assets/invalid"));
        MemoryPackFileSystem files = MemoryPackFileSystem.Create(
            ("assets/valid/interior.tmx", "valid-map"),
            ("assets/invalid/interior.tmx", "invalid-map"));

        RegistryBuildResult result = builder.Build(PackId, document, files);

        Assert.Single(result.Registry.Entries);
        Assert.Equal("example.interiors/valid", result.Registry.Entries[0].Id.Value);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == RegistryDiagnosticCode.InvalidVariantId && diagnostic.EntryIndex == 1);
    }

    [Fact]
    public void Build_DuplicateLocalId_RejectsEveryCollidingEntry()
    {
        InteriorPackDocument document = Document(
            Definition("same", "Greenhouse", "assets/one"),
            Definition("same", "DeluxeBarn", "assets/two"));
        MemoryPackFileSystem files = MemoryPackFileSystem.Create(
            ("assets/one/interior.tmx", "one"),
            ("assets/two/interior.tmx", "two"));

        RegistryBuildResult result = builder.Build(PackId, document, files);

        Assert.Empty(result.Registry.Entries);
        Assert.Equal(2, result.Diagnostics.Count(diagnostic =>
            diagnostic.Code == RegistryDiagnosticCode.DuplicateVariantId));
    }

    [Theory]
    [InlineData("../assets")]
    [InlineData("/assets/interior")]
    [InlineData("C:\\assets\\interior")]
    [InlineData("assets//interior")]
    [InlineData(".")]
    public void Build_UnsafeGameplayRoot_IsRejected(string gameplayRoot)
    {
        RegistryBuildResult result = builder.Build(
            PackId,
            Document(Definition("test", "Greenhouse", gameplayRoot)),
            MemoryPackFileSystem.Create());

        Assert.Empty(result.Registry.Entries);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == RegistryDiagnosticCode.InvalidGameplayRoot);
    }

    [Fact]
    public void Build_MapTraversal_IsRejectedEvenWhenSourceClaimsItExists()
    {
        InteriorDefinitionDto definition = Definition("test", "Greenhouse", "assets/test");
        definition.Map = "../outside.tmx";

        RegistryBuildResult result = builder.Build(
            PackId,
            Document(definition),
            MemoryPackFileSystem.Create(("assets/outside.tmx", "map")));

        Assert.Empty(result.Registry.Entries);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == RegistryDiagnosticCode.InvalidMapPath);
    }

    [Theory]
    [InlineData("greenhouse")]
    [InlineData("Barn")]
    [InlineData("BigBarn")]
    [InlineData("Deluxe Barn")]
    public void Build_UnsupportedOrNonCanonicalTarget_IsRejected(string target)
    {
        RegistryBuildResult result = builder.Build(
            PackId,
            Document(Definition("test", target, "assets/test")),
            MemoryPackFileSystem.Create(("assets/test/interior.tmx", "map")));

        Assert.Empty(result.Registry.Entries);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == RegistryDiagnosticCode.InvalidTarget);
    }

    [Fact]
    public void Build_HashIgnoresDisplayNamePreviewOutsideGameplayRootAndFileEnumerationOrder()
    {
        InteriorDefinitionDto firstDefinition = Definition("test", "Greenhouse", "assets/test");
        firstDefinition.DisplayName = "First Name";
        firstDefinition.Preview = "previews/test.png";

        InteriorDefinitionDto secondDefinition = Definition("test", "Greenhouse", "assets/test");
        secondDefinition.DisplayName = "Completely Different Name";
        secondDefinition.Preview = "previews/test.png";

        var firstFiles = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["assets/test/interior.tmx"] = Encoding.UTF8.GetBytes("same-map"),
            ["assets/test/tiles.png"] = Encoding.UTF8.GetBytes("same-tiles"),
            ["previews/test.png"] = Encoding.UTF8.GetBytes("preview-one")
        };
        var secondFiles = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["previews/test.png"] = Encoding.UTF8.GetBytes("preview-two"),
            ["assets/test/tiles.png"] = Encoding.UTF8.GetBytes("same-tiles"),
            ["assets/test/interior.tmx"] = Encoding.UTF8.GetBytes("same-map")
        };

        RegisteredInterior first = Assert.Single(builder.Build(
            PackId,
            Document(firstDefinition),
            new MemoryPackFileSystem(firstFiles)).Registry.Entries);
        RegisteredInterior second = Assert.Single(builder.Build(
            PackId,
            Document(secondDefinition),
            new MemoryPackFileSystem(secondFiles, reverseEnumeration: true)).Registry.Entries);

        Assert.Equal(first.ContentHash, second.ContentHash);
    }

    [Fact]
    public void Build_HashIncludesPreviewWhenPreviewIsBeneathGameplayRoot()
    {
        InteriorDefinitionDto definition = Definition("test", "Greenhouse", "assets/test");
        definition.Preview = "assets/test/preview.png";

        RegisteredInterior first = Assert.Single(builder.Build(
            PackId,
            Document(definition),
            MemoryPackFileSystem.Create(
                ("assets/test/interior.tmx", "same-map"),
                ("assets/test/preview.png", "preview-one")))
            .Registry.Entries);
        RegisteredInterior second = Assert.Single(builder.Build(
            PackId,
            Document(definition),
            MemoryPackFileSystem.Create(
                ("assets/test/interior.tmx", "same-map"),
                ("assets/test/preview.png", "preview-two")))
            .Registry.Entries);

        Assert.NotEqual(first.ContentHash, second.ContentHash);
    }

    [Fact]
    public void Build_HashChangesWhenGameplayFileChanges()
    {
        InteriorPackDocument document = Document(Definition("test", "Greenhouse", "assets/test"));

        RegisteredInterior first = Assert.Single(builder.Build(
            PackId,
            document,
            MemoryPackFileSystem.Create(("assets/test/interior.tmx", "map-one")))
            .Registry.Entries);
        RegisteredInterior second = Assert.Single(builder.Build(
            PackId,
            document,
            MemoryPackFileSystem.Create(("assets/test/interior.tmx", "map-two")))
            .Registry.Entries);

        Assert.NotEqual(first.ContentHash, second.ContentHash);
    }

    [Fact]
    public void Build_HashCanonicalizesAnchorAndPointOrder()
    {
        InteriorDefinitionDto first = Definition("test", "Greenhouse", "assets/test");
        first.Anchors = new Dictionary<string, List<TilePointDto>?>
        {
            ["WaterSource"] = new() { Point(5, 4), Point(1, 2) },
            ["AnimalDoor"] = new() { Point(9, 8) }
        };

        InteriorDefinitionDto second = Definition("test", "Greenhouse", "assets/test");
        second.Anchors = new Dictionary<string, List<TilePointDto>?>
        {
            ["AnimalDoor"] = new() { Point(9, 8) },
            ["WaterSource"] = new() { Point(1, 2), Point(5, 4) }
        };

        MemoryPackFileSystem files = MemoryPackFileSystem.Create(
            ("assets/test/interior.tmx", "same-map"));

        ContentHash firstHash = Assert.Single(builder.Build(PackId, Document(first), files)
            .Registry.Entries).ContentHash;
        ContentHash secondHash = Assert.Single(builder.Build(PackId, Document(second), files)
            .Registry.Entries).ContentHash;

        Assert.Equal(firstHash, secondHash);
    }

    private static InteriorPackDocument Document(params InteriorDefinitionDto[] definitions) => new()
    {
        FormatVersion = 1,
        Interiors = definitions.ToList()
    };

    private static InteriorDefinitionDto Definition(
        string id,
        string target,
        string gameplayRoot) => new()
        {
            Id = id,
            DisplayName = $"Display {id}",
            Target = target,
            GameplayRoot = gameplayRoot,
            Map = "interior.tmx"
        };

    private static TilePointDto Point(int x, int y) => new() { X = x, Y = y };
}
