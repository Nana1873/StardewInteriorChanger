using System.Globalization;
using StardewInteriorChanger.Core;
using xTile;
using xTile.Layers;
using xTile.ObjectModel;
using xTile.Tiles;

namespace StardewInteriorChanger;

internal static class MapContractValidator
{
    private const int DeluxeBarnCapacity = 12;

    private static readonly string[] OneWayLocationProperties =
    {
        "Outdoors",
        "IsFarm",
        "IsGreenhouse",
        "TreatAsOutdoors",
        "forceLoadPathLayerLights",
        "indoorWater",
        "LocationContext",
        "SeasonOverride",
    };

    public static bool TryValidate(
        InteriorTarget target,
        Map map,
        out string reason)
    {
        Layer? back = map.GetLayer("Back");
        Layer? buildings = map.GetLayer("Buildings");
        Layer? front = map.GetLayer("Front");
        if (back is null || buildings is null || front is null)
        {
            reason = "Required layers 'Back', 'Buildings', and 'Front' must exist.";
            return false;
        }

        if (back.LayerWidth <= 0 || back.LayerHeight <= 0
            || buildings.LayerWidth != back.LayerWidth
            || buildings.LayerHeight != back.LayerHeight
            || front.LayerWidth != back.LayerWidth
            || front.LayerHeight != back.LayerHeight)
        {
            reason = "Back, Buildings, and Front layers must have the same positive dimensions.";
            return false;
        }

        if (!TryValidateTileSheetSources(map, out reason))
        {
            return false;
        }

        if (!TryValidateOneWayLocationProperties(target, map, out reason))
        {
            return false;
        }

        if (!TryGetProperty(map.Properties, "Warp", out string warpText)
            || !TryReadWarps(warpText, out IReadOnlyList<WarpEntry> warps))
        {
            reason = "The map-level Warp property must contain complete five-field " +
                "groups: source X/Y, target location, and target X/Y.";
            return false;
        }

        if (!IsUsableFarmExit(warps[0], back, buildings))
        {
            reason = "The map's first warp must be a usable exit to 'Farm'; its player " +
                "entry tile one tile north of the warp source must be in bounds and unblocked.";
            return false;
        }

        if (target == InteriorTarget.DeluxeBarn)
        {
            int troughTiles = CountTilesWithProperty(back, "Trough");
            if (troughTiles < DeluxeBarnCapacity)
            {
                reason = $"A Deluxe Barn needs at least {DeluxeBarnCapacity.ToString(CultureInfo.InvariantCulture)} " +
                    "Back-layer Trough tiles for its animal capacity.";
                return false;
            }

            if (!TryGetProperty(map.Properties, "AutoFeed", out string autoFeed)
                || string.IsNullOrWhiteSpace(autoFeed))
            {
                reason = "A Deluxe Barn needs a non-empty map-level AutoFeed property.";
                return false;
            }

            if (!TryGetProperty(map.Properties, "ProduceArea", out string produceArea)
                || !TryReadRectangle(produceArea, out int x, out int y, out int width, out int height)
                || !IsRectangleInBounds(back, x, y, width, height))
            {
                reason = "A Deluxe Barn needs an in-bounds map-level ProduceArea rectangle.";
                return false;
            }

            long produceTileCount = (long)width * height;
            int usableProduceTiles = CountUsableTiles(back, buildings, x, y, width, height);
            if (produceTileCount < DeluxeBarnCapacity
                || usableProduceTiles < DeluxeBarnCapacity)
            {
                reason = $"A Deluxe Barn ProduceArea needs at least " +
                    $"{DeluxeBarnCapacity.ToString(CultureInfo.InvariantCulture)} usable tiles.";
                return false;
            }
        }

        reason = string.Empty;
        return true;
    }

    private static bool TryReadWarps(
        string value,
        out IReadOnlyList<WarpEntry> warps)
    {
        string[] fields = value.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (fields.Length == 0 || fields.Length % 5 != 0)
        {
            warps = Array.Empty<WarpEntry>();
            return false;
        }

        var parsed = new List<WarpEntry>(fields.Length / 5);
        for (int offset = 0; offset < fields.Length; offset += 5)
        {
            if (!int.TryParse(
                    fields[offset],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int sourceX)
                || !int.TryParse(
                    fields[offset + 1],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int sourceY)
                || string.IsNullOrWhiteSpace(fields[offset + 2])
                || !int.TryParse(
                    fields[offset + 3],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int targetX)
                || !int.TryParse(
                    fields[offset + 4],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int targetY))
            {
                warps = Array.Empty<WarpEntry>();
                return false;
            }

            parsed.Add(
                new WarpEntry(
                    sourceX,
                    sourceY,
                    fields[offset + 2],
                    targetX,
                    targetY));
        }

        warps = parsed;
        return true;
    }

    private static bool IsUsableFarmExit(
        WarpEntry warp,
        Layer back,
        Layer buildings)
    {
        if (!string.Equals(warp.TargetLocation, "Farm", StringComparison.Ordinal)
            || warp.TargetX < 0
            || warp.TargetY < 0
            || warp.SourceY == int.MinValue)
        {
            return false;
        }

        int entryX = warp.SourceX;
        int entryY = warp.SourceY - 1;
        return IsInBounds(back, entryX, entryY)
            && IsTileUsable(back, buildings, entryX, entryY);
    }

    private static bool TryValidateOneWayLocationProperties(
        InteriorTarget target,
        Map map,
        out string reason)
    {
        // Stardew 1.6.15 applies these map-driven states only when present and
        // doesn't restore the target's prior state when a later map omits them.
        // Vanilla Greenhouse.tmx and Barn3.tmx omit all of them; IsGreenhouse
        // and IsFarm come from the location/building identity instead.
        foreach (string propertyName in OneWayLocationProperties)
        {
            if (map.Properties.ContainsKey(propertyName))
            {
                reason = $"Map property '{propertyName}' isn't allowed for {target}: " +
                    "it can leave stale location state after switching interiors.";
                return false;
            }
        }

        reason = string.Empty;
        return true;
    }

    private static bool TryValidateTileSheetSources(Map map, out string reason)
    {
        foreach (TileSheet tileSheet in map.TileSheets)
        {
            string imageSource = tileSheet.ImageSource;
            if (string.IsNullOrWhiteSpace(imageSource))
            {
                reason = "Every tile sheet needs a non-empty image source.";
                return false;
            }

            if (IsUnsafeReferencedPath(imageSource))
            {
                reason = $"Tile sheet image source '{imageSource}' must not be absolute, " +
                    "URI-based, or traverse a parent directory.";
                return false;
            }
        }

        reason = string.Empty;
        return true;
    }

    private static bool IsUnsafeReferencedPath(string value)
    {
        if (value.IndexOf('\0') >= 0
            || Path.IsPathRooted(value)
            || Uri.TryCreate(value, UriKind.Absolute, out _))
        {
            return true;
        }

        string normalized = value.Replace('\\', '/');
        if (normalized.StartsWith("/", StringComparison.Ordinal)
            || (normalized.Length >= 2
                && char.IsLetter(normalized[0])
                && normalized[1] == ':'))
        {
            return true;
        }

        return normalized.Split('/').Any(segment => segment == "..");
    }

    private static bool TryReadRectangle(
        string value,
        out int x,
        out int y,
        out int width,
        out int height)
    {
        x = y = width = height = default;
        string[] fields = value.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return fields.Length == 4
            && int.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out x)
            && int.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out y)
            && int.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out width)
            && int.TryParse(fields[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out height);
    }

    private static bool IsInBounds(Layer layer, int x, int y) =>
        x >= 0 && y >= 0 && x < layer.LayerWidth && y < layer.LayerHeight;

    private static bool IsRectangleInBounds(
        Layer layer,
        int x,
        int y,
        int width,
        int height) =>
        x >= 0
        && y >= 0
        && width > 0
        && height > 0
        && (long)x + width <= layer.LayerWidth
        && (long)y + height <= layer.LayerHeight;

    private static int CountUsableTiles(
        Layer back,
        Layer buildings,
        int x,
        int y,
        int width,
        int height)
    {
        int count = 0;
        for (int tileY = y; tileY < y + height; tileY++)
        {
            for (int tileX = x; tileX < x + width; tileX++)
            {
                if (IsTileUsable(back, buildings, tileX, tileY))
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static bool IsTileUsable(
        Layer back,
        Layer buildings,
        int x,
        int y)
    {
        Tile? backTile = back.Tiles[x, y];
        if (backTile is not null
            && TryGetTileProperty(backTile, "Passable", out _))
        {
            return false;
        }

        Tile? blocker = buildings.Tiles[x, y];
        return blocker is null
            || TryGetTileProperty(blocker, "Passable", out _)
            || TryGetTileProperty(blocker, "Shadow", out _);
    }

    private static int CountTilesWithProperty(Layer layer, string propertyName) =>
        EnumerateTiles(layer).Count(tile =>
            TryGetTileProperty(tile, propertyName, out _));

    private static IEnumerable<Tile> EnumerateTiles(Layer layer)
    {
        for (int y = 0; y < layer.LayerHeight; y++)
        {
            for (int x = 0; x < layer.LayerWidth; x++)
            {
                Tile? tile = layer.Tiles[x, y];
                if (tile is not null)
                {
                    yield return tile;
                }
            }
        }
    }

    private static bool TryGetTileProperty(
        Tile tile,
        string name,
        out string value)
    {
        if (TryGetProperty(tile.Properties, name, out value))
        {
            return true;
        }

        IPropertyCollection indexProperties =
            tile.TileSheet.TileIndexProperties[tile.TileIndex];
        if (TryGetProperty(indexProperties, name, out value))
        {
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static bool TryGetProperty(
        IPropertyCollection properties,
        string name,
        out string value)
    {
        if (properties.TryGetValue(name, out PropertyValue? property))
        {
            value = property?.ToString() ?? string.Empty;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private readonly record struct WarpEntry(
        int SourceX,
        int SourceY,
        string TargetLocation,
        int TargetX,
        int TargetY);
}
