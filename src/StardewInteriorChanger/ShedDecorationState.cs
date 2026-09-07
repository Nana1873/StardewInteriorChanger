using Microsoft.Xna.Framework;
using StardewValley;
using xTile;
using xTile.Layers;
using xTile.Tiles;

namespace StardewInteriorChanger;

internal sealed class ShedDecorationState
{
    private readonly Shed shed;
    private readonly Map originalMap;
    private readonly Dictionary<string, List<Vector3>> walls;
    private readonly Dictionary<string, List<Vector3>> floors;

    public ShedDecorationState(Shed shed)
    {
        this.shed = shed;
        originalMap = shed.Map;
        walls = shed.wallpaperTiles.ToDictionary(pair => pair.Key, pair => new List<Vector3>(pair.Value));
        floors = shed.floorTiles.ToDictionary(pair => pair.Key, pair => new List<Vector3>(pair.Value));
        shed.wallpaperTiles.Clear();
        shed.floorTiles.Clear();
    }

    public void RestoreIfMapUnchanged()
    {
        // A failed reload can leave the original map in place. Its caches are still
        // valid, but must never be attached to a different, possibly invalid map.
        if (!ReferenceEquals(shed.Map, originalMap)) return;
        shed.wallpaperTiles.Clear();
        shed.floorTiles.Clear();
        foreach (var pair in walls) shed.wallpaperTiles.Add(pair.Key, pair.Value);
        foreach (var pair in floors) shed.floorTiles.Add(pair.Key, pair.Value);
    }

    public static void Refresh(Shed shed)
    {
        Layer back = shed.Map.GetLayer("Back");
        for (int y = 0; y < back.LayerHeight; y++)
        {
            for (int x = 0; x < back.LayerWidth; x++)
            {
                Tile? tile = back.Tiles[x, y];
                if (tile is null) continue;
                // Vanilla pattern application changes TileIndex. Keep effective TMX
                // index markers as instance properties so region identity survives it.
                foreach (string property in new[] { "WallID", "FloorID" })
                {
                    if (MapContractValidator.TryGetTileProperty(tile, property, out string value))
                        tile.Properties[property] = value;
                }
            }
        }

        // Once copied, remove index markers from this loaded instance. Otherwise
        // another saved pattern could acquire a marker it did not originally have.
        foreach (TileSheet sheet in shed.Map.TileSheets)
        {
            for (int index = 0; index < sheet.TileCount; index++)
            {
                sheet.TileIndexProperties[index].Remove("WallID");
                sheet.TileIndexProperties[index].Remove("FloorID");
            }
        }

        shed.ReadWallpaperAndFloorTileData();
    }
}
