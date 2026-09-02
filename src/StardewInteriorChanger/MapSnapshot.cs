using xTile;
using xTile.Format;

namespace StardewInteriorChanger;

internal sealed class MapSnapshot
{
    private readonly byte[] data;

    private MapSnapshot(byte[] data)
    {
        this.data = data;
    }

    public static MapSnapshot Capture(Map map)
    {
        ArgumentNullException.ThrowIfNull(map);

        using var stream = new MemoryStream();
        FormatManager.Instance.BinaryFormat.Store(map, stream);
        return new MapSnapshot(stream.ToArray());
    }

    public Map CreateMap()
    {
        using var stream = new MemoryStream(data, writable: false);
        return FormatManager.Instance.BinaryFormat.Load(stream);
    }
}
