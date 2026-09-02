using System.Text;
using StardewInteriorChanger.Core;

namespace StardewInteriorChanger.Core.Tests;

internal sealed class MemoryPackFileSystem : IPackFileSystem
{
    private readonly IReadOnlyDictionary<string, byte[]> files;
    private readonly bool reverseEnumeration;

    public MemoryPackFileSystem(
        IReadOnlyDictionary<string, byte[]> files,
        bool reverseEnumeration = false)
    {
        this.files = files;
        this.reverseEnumeration = reverseEnumeration;
    }

    public static MemoryPackFileSystem Create(params (string Path, string Content)[] files) =>
        new(files.ToDictionary(
            file => file.Path,
            file => Encoding.UTF8.GetBytes(file.Content),
            StringComparer.Ordinal));

    public bool FileExists(string normalizedRelativePath) => files.ContainsKey(normalizedRelativePath);

    public IEnumerable<string> EnumerateFiles(string normalizedRelativeDirectory)
    {
        IEnumerable<string> result = files.Keys.Where(path =>
            path.StartsWith(normalizedRelativeDirectory + "/", StringComparison.Ordinal));
        return reverseEnumeration ? result.Reverse().ToArray() : result.ToArray();
    }

    public byte[] ReadAllBytes(string normalizedRelativePath) => files[normalizedRelativePath];
}
