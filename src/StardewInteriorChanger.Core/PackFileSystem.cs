namespace StardewInteriorChanger.Core;

public interface IPackFileSystem
{
    bool FileExists(string normalizedRelativePath);

    IEnumerable<string> EnumerateFiles(string normalizedRelativeDirectory);

    byte[] ReadAllBytes(string normalizedRelativePath);
}

public sealed class DirectoryPackFileSystem : IPackFileSystem
{
    private readonly string rootPath;
    private readonly string rootPrefix;
    private readonly StringComparison pathComparison;

    public DirectoryPackFileSystem(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new ArgumentException("The pack root path must not be empty.", nameof(rootPath));
        }
        this.rootPath = Path.GetFullPath(rootPath);
        rootPrefix = this.rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
    }

    public bool FileExists(string normalizedRelativePath) =>
        File.Exists(Resolve(normalizedRelativePath));

    public IEnumerable<string> EnumerateFiles(string normalizedRelativeDirectory)
    {
        string directory = Resolve(normalizedRelativeDirectory);
        if (!Directory.Exists(directory))
        {
            return Array.Empty<string>();
        }

        var files = new List<string>();
        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(directory);

        while (pendingDirectories.Count > 0)
        {
            string currentDirectory = pendingDirectories.Pop();
            foreach (string path in Directory.EnumerateFileSystemEntries(currentDirectory))
            {
                FileAttributes attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidOperationException(
                        $"Content-pack paths must not traverse symbolic links or junctions: '{ToRelativePath(path)}'.");
                }

                if ((attributes & FileAttributes.Directory) != 0)
                {
                    pendingDirectories.Push(path);
                }
                else
                {
                    files.Add(ToRelativePath(path));
                }
            }
        }

        return files.ToArray();
    }

    public byte[] ReadAllBytes(string normalizedRelativePath) =>
        File.ReadAllBytes(Resolve(normalizedRelativePath));

    private string Resolve(string normalizedRelativePath)
    {
        if (!ContentPath.TryNormalize(normalizedRelativePath, out string normalized))
        {
            throw new ArgumentException("The path must be a safe relative content path.", nameof(normalizedRelativePath));
        }

        string candidate = Path.GetFullPath(Path.Combine(rootPath, normalized.Replace('/', Path.DirectorySeparatorChar)));
        if (!candidate.StartsWith(rootPrefix, pathComparison))
        {
            throw new ArgumentException("The path escapes the content-pack root.", nameof(normalizedRelativePath));
        }

        RejectReparsePoints(candidate);
        return candidate;
    }

    private void RejectReparsePoints(string candidate)
    {
        string relativePath = Path.GetRelativePath(rootPath, candidate);
        string currentPath = rootPath;
        foreach (string segment in relativePath.Split(
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries))
        {
            currentPath = Path.Combine(currentPath, segment);

            FileAttributes attributes;
            try
            {
                attributes = File.GetAttributes(currentPath);
            }
            catch (FileNotFoundException)
            {
                break;
            }
            catch (DirectoryNotFoundException)
            {
                break;
            }

            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException(
                    $"Content-pack paths must not traverse symbolic links or junctions: '{ToRelativePath(currentPath)}'.");
            }
        }
    }

    private string ToRelativePath(string path) =>
        Path.GetRelativePath(rootPath, path).Replace('\\', '/');
}

internal static class ContentPath
{
    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > 512
            || value.IndexOf('\0') >= 0
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            return false;
        }

        string candidate = value.Replace('\\', '/');
        if (candidate.StartsWith("/", StringComparison.Ordinal)
            || candidate.Contains(':'))
        {
            return false;
        }

        string[] segments = candidate.Split('/');
        if (segments.Length == 0
            || segments.Any(segment => segment.Length == 0 || segment is "." or ".."))
        {
            return false;
        }

        normalized = string.Join('/', segments);
        return true;
    }

    public static string Combine(string parent, string child) => $"{parent}/{child}";

    public static bool IsWithin(string path, string directory) =>
        path.StartsWith(directory + "/", StringComparison.Ordinal);
}
