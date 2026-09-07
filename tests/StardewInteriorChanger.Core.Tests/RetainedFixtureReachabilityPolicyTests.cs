using StardewInteriorChanger.Core;

namespace StardewInteriorChanger.Core.Tests;

public sealed class RetainedFixtureReachabilityPolicyTests
{
    private static readonly TilePoint Entry = new(0, 2);
    private static readonly TilePoint Hopper = new(3, 2);

    [Fact]
    public void OpenFloorPreservesTheExistingFixturePosition()
    {
        Assert.True(Check());
    }

    [Theory]
    [InlineData(-1, 2)]
    [InlineData(5, 2)]
    [InlineData(3, -1)]
    [InlineData(3, 5)]
    public void FixtureOutsideDestinationBoundsIsRejected(int x, int y)
    {
        Assert.False(Check(fixture: new TilePoint(x, y)));
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(3, 2)]
    public void MissingFloorAtEntryOrFixtureIsRejected(int x, int y)
    {
        Assert.False(Check(missingFloor: new TilePoint(x, y)));
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(3, 2)]
    public void BlockedEntryOrFixtureIsRejected(int x, int y)
    {
        Assert.False(Check(blocked: new TilePoint(x, y)));
    }

    [Fact]
    public void UsableFixtureWithOnlyDisconnectedNeighborsIsRejected()
    {
        Assert.False(RetainedFixtureReachabilityPolicy.CanPreserve(
            5, 5, Entry, new[] { Hopper }, _ => true, tile => tile.X != 2));
    }

    [Fact]
    public void MissingFloorCannotFormAPathToTheFixture()
    {
        Assert.False(RetainedFixtureReachabilityPolicy.CanPreserve(
            5, 5, Entry, new[] { Hopper }, tile => tile.X != 2, _ => true));
    }

    [Fact]
    public void SingleTileCorridorToAnAdjacentTileIsEnough()
    {
        Assert.True(RetainedFixtureReachabilityPolicy.CanPreserve(
            5, 5, Entry, new[] { Hopper }, tile => tile.Y == 2, _ => true));
    }

    [Fact]
    public void FixtureCannotBeTraversedToReachAnotherFixture()
    {
        Assert.False(RetainedFixtureReachabilityPolicy.CanPreserve(
            6, 1, new TilePoint(0, 0), new[] { new TilePoint(2, 0), new TilePoint(4, 0) },
            _ => true, _ => true));
    }

    [Fact]
    public void EntryCannotOverlapTheRetainedFixture()
    {
        Assert.False(Check(fixture: Entry));
    }

    [Fact]
    public void DiagonalContactDoesNotProvideAccess()
    {
        Assert.False(RetainedFixtureReachabilityPolicy.CanPreserve(
            2, 2, new TilePoint(0, 0), new[] { new TilePoint(1, 1) },
            tile => tile.X == tile.Y, _ => true));
    }

    private static bool Check(TilePoint? fixture = null, TilePoint? missingFloor = null, TilePoint? blocked = null) =>
        RetainedFixtureReachabilityPolicy.CanPreserve(
            5, 5, Entry, new[] { fixture ?? Hopper }, tile => tile != missingFloor, tile => tile != blocked);
}
