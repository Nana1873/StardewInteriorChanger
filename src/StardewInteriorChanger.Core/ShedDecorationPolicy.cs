namespace StardewInteriorChanger.Core;

public readonly record struct DecorationSurface(
    string? WallId,
    string? FloorId,
    bool CanDecorate);

public readonly record struct DecorationTilesheet(int Columns, int Rows, int TileWidth, int TileHeight);

public static class ShedDecorationPolicy
{
    public const string WallRegion = "Wall";
    public const string FloorRegion = "Floor";

    public static bool AppliesTo(InteriorTarget target) =>
        target is InteriorTarget.Shed or InteriorTarget.BigShed;

    public static bool CanPreserveSavedRegions(
        IEnumerable<string> wallIds,
        IEnumerable<string> floorIds) =>
        wallIds.All(id => string.Equals(id, WallRegion, StringComparison.Ordinal))
        && floorIds.All(id => string.Equals(id, FloorRegion, StringComparison.Ordinal));

    public static bool TryValidateMap(
        int width,
        int height,
        string? wallIds,
        string? floorIds,
        DecorationTilesheet? canonicalSheet,
        Func<TilePoint, DecorationSurface> readBack,
        out string reason)
    {
        // Vanilla XNB uses 16-pixel tiles; SMAPI's TMX loader scales them by four.
        if (canonicalSheet is not { Columns: 16, Rows: >= 32 }
            || canonicalSheet is not ({ TileWidth: 16, TileHeight: 16 } or { TileWidth: 64, TileHeight: 64 }))
        {
            reason = "Sheds require a 'walls_and_floors' tilesheet with 16 columns, at least 32 rows, and resolved tile sizes of 16x16 (XNB) or 64x64 (SMAPI TMX) for saved vanilla patterns.";
            return false;
        }

        if (width <= 0 || height <= 0
            || !string.Equals(wallIds?.Trim(), WallRegion, StringComparison.Ordinal)
            || !string.Equals(floorIds?.Trim(), FloorRegion, StringComparison.Ordinal))
        {
            reason = "Sheds require exactly WallIDs 'Wall' and FloorIDs 'Floor', without aliases or defaults.";
            return false;
        }

        int walls = 0;
        int floors = 0;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                DecorationSurface tile = readBack(new TilePoint(x, y));
                if (tile.WallId is null && tile.FloorId is null)
                    continue;

                if (!tile.CanDecorate
                    || (tile.WallId is not null && tile.FloorId is not null)
                    || (tile.WallId is not null && tile.WallId != WallRegion)
                    || (tile.FloorId is not null && tile.FloorId != FloorRegion))
                {
                    reason = $"Decoration marker at ({x}, {y}) must name one canonical region on a wallpaper/flooring surface.";
                    return false;
                }

                if (tile.WallId is not null) walls++;
                if (tile.FloorId is not null) floors++;
            }
        }

        if (walls == 0 || floors == 0)
        {
            reason = "Sheds need non-empty Back-layer WallID 'Wall' and FloorID 'Floor' surfaces.";
            return false;
        }

        reason = string.Empty;
        return true;
    }
}
