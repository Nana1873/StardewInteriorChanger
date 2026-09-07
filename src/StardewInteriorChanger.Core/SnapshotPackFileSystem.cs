namespace StardewInteriorChanger.Core;

/// <summary>Provides an immutable, in-memory view of content-pack files.</summary>
public sealed class SnapshotPackFileSystem : IPackFileSystem
{
    private readonly IReadOnlyDictionary<string, byte[]> files;
    private readonly string[] orderedPaths;

    public SnapshotPackFileSystem(IReadOnlyDictionary<string, byte[]> files)
    {
        ArgumentNullException.ThrowIfNull(files);

        var snapshot = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach ((string path, byte[] content) in files)
        {
            if (!ContentPath.TryNormalize(path, out string normalizedPath))
            {
                throw new ArgumentException(
                    $"Snapshot file path '{path}' must be a safe relative content path.",
                    nameof(files));
            }

            if (content is null)
            {
                throw new ArgumentException(
                    $"Snapshot file '{normalizedPath}' must have content.",
                    nameof(files));
            }

            if (!snapshot.TryAdd(normalizedPath, content.ToArray()))
            {
                throw new ArgumentException(
                    $"Snapshot file paths contain a case-insensitive collision for '{normalizedPath}'.",
                    nameof(files));
            }
        }

        this.files = snapshot;
        orderedPaths = snapshot.Keys.OrderBy(path => path, StringComparer.Ordinal).ToArray();
    }

    public bool FileExists(string normalizedRelativePath) =>
        files.ContainsKey(Normalize(normalizedRelativePath, nameof(normalizedRelativePath)));

    public IEnumerable<string> EnumerateFiles(string normalizedRelativeDirectory)
    {
        string directory = Normalize(normalizedRelativeDirectory, nameof(normalizedRelativeDirectory));
        string prefix = directory + "/";
        return orderedPaths
            .Where(path => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    public byte[] ReadAllBytes(string normalizedRelativePath)
    {
        string path = Normalize(normalizedRelativePath, nameof(normalizedRelativePath));
        if (!files.TryGetValue(path, out byte[]? content))
        {
            throw new FileNotFoundException($"Snapshot file '{path}' does not exist.", path);
        }

        return content.ToArray();
    }

    private static string Normalize(string path, string parameterName)
    {
        if (!ContentPath.TryNormalize(path, out string normalizedPath))
        {
            throw new ArgumentException(
                "The path must be a safe relative content path.",
                parameterName);
        }

        return normalizedPath;
    }
}
