namespace StardewInteriorChanger.Core;

public static class RetainedFixtureReachabilityPolicy
{
    public static bool CanPreserve(
        int width,
        int height,
        TilePoint entry,
        IReadOnlyCollection<TilePoint> fixtureTiles,
        Func<TilePoint, bool> hasFloor,
        Func<TilePoint, bool> isUsable)
    {
        ArgumentNullException.ThrowIfNull(fixtureTiles);
        ArgumentNullException.ThrowIfNull(hasFloor);
        ArgumentNullException.ThrowIfNull(isUsable);
        if (fixtureTiles.Count == 0)
            return true;

        bool CanOccupy(TilePoint tile) => tile.X >= 0 && tile.Y >= 0
            && tile.X < width && tile.Y < height && hasFloor(tile) && isUsable(tile);

        var fixtures = fixtureTiles.ToHashSet();
        if (!CanOccupy(entry) || fixtures.Contains(entry) || fixtures.Any(tile => !CanOccupy(tile)))
            return false;

        var reachable = new HashSet<TilePoint> { entry };
        var pending = new Queue<TilePoint>();
        pending.Enqueue(entry);
        while (pending.TryDequeue(out TilePoint tile))
        {
            foreach (TilePoint neighbor in Neighbors(tile))
            {
                if (!fixtures.Contains(neighbor) && CanOccupy(neighbor) && reachable.Add(neighbor))
                    pending.Enqueue(neighbor);
            }
        }

        return fixtures.All(tile => Neighbors(tile).Any(reachable.Contains));
    }

    private static IEnumerable<TilePoint> Neighbors(TilePoint tile)
    {
        yield return new TilePoint(tile.X - 1, tile.Y);
        yield return new TilePoint(tile.X + 1, tile.Y);
        yield return new TilePoint(tile.X, tile.Y - 1);
        yield return new TilePoint(tile.X, tile.Y + 1);
    }
}
