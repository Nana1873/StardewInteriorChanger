using StardewInteriorChanger.Core;

namespace StardewInteriorChanger.Core.Tests;

public sealed class SnapshotPackFileSystemTests
{
    private const string PackId = "Example.Interiors";

    [Fact]
    public void ConstructorAndReads_DefensivelyCopyMutableData()
    {
        byte[] originalContent = { 1, 2, 3 };
        var source = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["assets/test/interior.tbin"] = originalContent
        };
        var files = new SnapshotPackFileSystem(source);

        originalContent[0] = 9;
        source["assets/test/late-file.json"] = new byte[] { 4 };
        byte[] firstRead = files.ReadAllBytes("assets/test/interior.tbin");
        firstRead[1] = 9;

        Assert.Equal(new byte[] { 1, 2, 3 }, files.ReadAllBytes("assets/test/interior.tbin"));
        Assert.False(files.FileExists("assets/test/late-file.json"));
    }

    [Fact]
    public void EnumerateFiles_NormalizesAndSortsRecursiveRelativePaths()
    {
        var files = new SnapshotPackFileSystem(new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["outside.json"] = Array.Empty<byte>(),
            ["assets/test/z-last.json"] = Array.Empty<byte>(),
            ["assets\\test\\nested\\middle.json"] = Array.Empty<byte>(),
            ["assets/test/a-first.tbin"] = Array.Empty<byte>()
        });

        Assert.Equal(
            new[]
            {
                "assets/test/a-first.tbin",
                "assets/test/nested/middle.json",
                "assets/test/z-last.json"
            },
            files.EnumerateFiles("ASSETS/test"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" assets/test.tbin")]
    [InlineData("/assets/test.tbin")]
    [InlineData("C:\\assets\\test.tbin")]
    [InlineData("assets//test.tbin")]
    [InlineData("assets/../test.tbin")]
    [InlineData("assets/./test.tbin")]
    public void Constructor_RejectsUnsafePaths(string path)
    {
        var source = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            [path] = Array.Empty<byte>()
        };

        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => new SnapshotPackFileSystem(source));

        Assert.Equal("files", exception.ParamName);
        Assert.Contains("safe relative content path", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_RejectsCaseInsensitiveNormalizedPathCollisions()
    {
        var source = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["Assets/Test/Interior.tbin"] = new byte[] { 1 },
            ["assets\\test\\interior.tbin"] = new byte[] { 2 }
        };

        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => new SnapshotPackFileSystem(source));

        Assert.Equal("files", exception.ParamName);
        Assert.Contains("case-insensitive collision", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadAllBytes_MissingFileFailsClearly()
    {
        var files = new SnapshotPackFileSystem(
            new Dictionary<string, byte[]>(StringComparer.Ordinal));

        FileNotFoundException exception = Assert.Throws<FileNotFoundException>(
            () => files.ReadAllBytes("assets/test/missing.tbin"));

        Assert.Equal("assets/test/missing.tbin", exception.FileName);
        Assert.Contains("does not exist", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RegistryBuild_DictionaryOrderDoesNotAffectContentHash()
    {
        SnapshotPackFileSystem firstFiles = CreateFiles(
            ("assets/test/interior.tbin", "map"),
            ("assets/test/config.json", "config"),
            ("assets/test/recipe.json", "recipe"));
        SnapshotPackFileSystem secondFiles = CreateFiles(
            ("assets/test/recipe.json", "recipe"),
            ("assets/test/config.json", "config"),
            ("assets/test/interior.tbin", "map"));

        ContentHash firstHash = BuildHash(firstFiles);
        ContentHash secondHash = BuildHash(secondFiles);

        Assert.Equal(firstHash, secondHash);
    }

    [Theory]
    [InlineData("assets/test/interior.tbin")]
    [InlineData("assets/test/config.json")]
    [InlineData("assets/test/recipe.json")]
    public void RegistryBuild_ChangingEffectiveSnapshotBytesChangesContentHash(string changedPath)
    {
        var original = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["assets/test/interior.tbin"] = "map"u8.ToArray(),
            ["assets/test/config.json"] = "config"u8.ToArray(),
            ["assets/test/recipe.json"] = "recipe"u8.ToArray()
        };
        var changed = original.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ToArray(),
            StringComparer.Ordinal);
        changed[changedPath] = "changed"u8.ToArray();

        ContentHash originalHash = BuildHash(new SnapshotPackFileSystem(original));
        ContentHash changedHash = BuildHash(new SnapshotPackFileSystem(changed));

        Assert.NotEqual(originalHash, changedHash);
    }

    private static ContentHash BuildHash(SnapshotPackFileSystem files)
    {
        var document = new InteriorPackDocument
        {
            FormatVersion = InteriorRegistryBuilder.SupportedFormatVersion,
            Interiors = new List<InteriorDefinitionDto>
            {
                new()
                {
                    Id = "test",
                    DisplayName = "Test",
                    Target = "Greenhouse",
                    GameplayRoot = "assets/test",
                    Map = "interior.tbin"
                }
            }
        };

        RegistryBuildResult result = new InteriorRegistryBuilder().Build(PackId, document, files);
        Assert.False(result.HasErrors);
        return Assert.Single(result.Registry.Entries).ContentHash;
    }

    private static SnapshotPackFileSystem CreateFiles(params (string Path, string Content)[] files) =>
        new(files.ToDictionary(
            file => file.Path,
            file => System.Text.Encoding.UTF8.GetBytes(file.Content),
            StringComparer.Ordinal));
}
