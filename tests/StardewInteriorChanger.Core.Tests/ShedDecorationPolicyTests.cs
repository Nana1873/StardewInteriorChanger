using StardewInteriorChanger.Core;

namespace StardewInteriorChanger.Core.Tests;

public sealed class ShedDecorationPolicyTests
{
    private static readonly DecorationTilesheet CanonicalSheet = new(16, 32, 16, 16);

    [Theory]
    [InlineData(0, 32, 16, 16)]
    [InlineData(8, 32, 16, 16)]
    [InlineData(16, 0, 16, 16)]
    [InlineData(16, 32, 32, 16)]
    [InlineData(16, 32, 16, 64)]
    [InlineData(16, 32, 64, 16)]
    [InlineData(16, 32, 32, 32)]
    [InlineData(16, 32, 128, 128)]
    [InlineData(8, 32, 64, 64)]
    [InlineData(16, 31, 64, 64)]
    public void InvalidCanonicalSheetCannotRenderSavedVanillaPatterns(int columns, int rows, int tileWidth, int tileHeight)
    {
        Assert.False(ShedDecorationPolicy.TryValidateMap(6, 6, "Wall", "Floor",
            new DecorationTilesheet(columns, rows, tileWidth, tileHeight),
            _ => throw new InvalidOperationException("Invalid sheet must be rejected before reading surfaces."), out _));
    }

    [Theory]
    [InlineData(16)]
    [InlineData(64)]
    public void VanillaXnbAndSmapiScaledTmx_PreserveCanonicalDecorationLayout(int resolvedTileSize)
    {
        Dictionary<TilePoint, DecorationSurface> grid = Grid(new(1, 1), new(1, 3));
        Assert.True(ShedDecorationPolicy.TryValidateMap(6, 6, "Wall", "Floor",
            new DecorationTilesheet(16, 32, resolvedTileSize, resolvedTileSize),
            tile => grid.GetValueOrDefault(tile), out _));
    }

    [Fact]
    public void MissingCanonicalSheet_IsRejectedEvenWhenOtherDecorativeSheetsExist()
    {
        Dictionary<TilePoint, DecorationSurface> grid = Grid(new(1, 1), new(1, 3));
        Assert.False(ShedDecorationPolicy.TryValidateMap(6, 6, "Wall", "Floor", null,
            tile => grid.GetValueOrDefault(tile), out _));
    }
    [Fact]
    public void DifferentRegionPositions_AcceptTheSameSavedRegionIdentity()
    {
        Assert.True(ShedDecorationPolicy.CanPreserveSavedRegions(new[] { "Wall" }, new[] { "Floor" }));

        Assert.True(Validate(Grid(new(1, 1), new(1, 3)), out _));
        Assert.True(Validate(Grid(new(4, 2), new(4, 4)), out _));
    }

    [Theory]
    [InlineData("", "Floor")]
    [InlineData("Wall 11", "Floor")]
    [InlineData("Wall, Other", "Floor")]
    [InlineData("Wall", "Floor Wall")]
    [InlineData("Wall", "floor")]
    public void MapDeclarationsCannotAddRegionsOrMigrateSavedPatterns(string walls, string floors)
    {
        Dictionary<TilePoint, DecorationSurface> grid = Grid(new(1, 1), new(1, 3));
        Assert.False(ShedDecorationPolicy.TryValidateMap(6, 6, walls, floors, CanonicalSheet,
            tile => grid.GetValueOrDefault(tile), out _));
    }

    [Theory]
    [InlineData("Other", null, true)]
    [InlineData(null, "Floor_0", true)]
    [InlineData("Wall", "Floor", true)]
    [InlineData("Wall", null, false)]
    [InlineData(null, "Floor", false)]
    public void HiddenExtraOrUnsupportedMarkedSurface_IsRejected(string? wall, string? floor, bool canDecorate)
    {
        Dictionary<TilePoint, DecorationSurface> grid = Grid(new(1, 1), new(1, 3));
        grid[new(4, 4)] = new(wall, floor, canDecorate);
        Assert.False(Validate(grid, out _));
    }

    [Fact]
    public void DeclaredRegionWithoutActualSurface_IsRejected()
    {
        var grid = new Dictionary<TilePoint, DecorationSurface>
        {
            [new(1, 1)] = new("Wall", null, true)
        };
        Assert.False(Validate(grid, out _));
    }

    [Fact]
    public void ValidationReadsOnlyWithinMapBounds()
    {
        Assert.True(ShedDecorationPolicy.TryValidateMap(2, 1, "Wall", "Floor", CanonicalSheet, tile =>
        {
            Assert.InRange(tile.X, 0, 1);
            Assert.Equal(0, tile.Y);
            return tile.X == 0 ? new("Wall", null, true) : new(null, "Floor", true);
        }, out _));
        Assert.False(ShedDecorationPolicy.TryValidateMap(0, 1, "Wall", "Floor", CanonicalSheet,
            _ => throw new InvalidOperationException("An empty map must not be read."), out _));
    }

    [Theory]
    [InlineData("Wall_0", "Floor")]
    [InlineData("Wall", "Kitchen")]
    [InlineData("wall", "Floor")]
    public void SavedRegionOutsideCanonicalContract_RequiresExplicitMigration(string wall, string floor)
    {
        Assert.False(ShedDecorationPolicy.CanPreserveSavedRegions(new[] { wall }, new[] { floor }));
    }

    [Fact]
    public void NewUninitializedShed_CanUseVanillaDefaultInitialization()
    {
        Assert.True(ShedDecorationPolicy.CanPreserveSavedRegions(Array.Empty<string>(), Array.Empty<string>()));
    }

    private static Dictionary<TilePoint, DecorationSurface> Grid(TilePoint wall, TilePoint floor) => new()
    {
        [wall] = new("Wall", null, true),
        [floor] = new(null, "Floor", true)
    };

    private static bool Validate(Dictionary<TilePoint, DecorationSurface> grid, out string reason) =>
        ShedDecorationPolicy.TryValidateMap(6, 6, "Wall", "Floor", CanonicalSheet,
            tile => grid.GetValueOrDefault(tile), out reason);
}
