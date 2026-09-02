using System.Xml;
using System.Xml.Linq;
using StardewInteriorChanger.Core;

namespace StardewInteriorChanger;

internal static class TmxDependencyValidator
{
    public static bool TryValidate(
        string packDirectory,
        RegisteredInterior definition,
        out string reason)
    {
        if (!string.Equals(
                Path.GetExtension(definition.MapPath),
                ".tmx",
                StringComparison.OrdinalIgnoreCase))
        {
            reason = string.Empty;
            return true;
        }

        string packRoot = EnsureTrailingSeparator(Path.GetFullPath(packDirectory));
        string gameplayRoot = EnsureTrailingSeparator(Path.GetFullPath(
            Path.Combine(packDirectory, ToPlatformPath(definition.GameplayRoot))));
        string mapPath = Path.GetFullPath(
            Path.Combine(packDirectory, ToPlatformPath(definition.MapPath)));
        var visited = new HashSet<string>(PhysicalPathSemantics.Comparer);
        return TryValidateXmlFile(
            mapPath,
            packRoot,
            gameplayRoot,
            allowExternalTilesets: true,
            visited,
            out reason);
    }

    private static bool TryValidateXmlFile(
        string xmlPath,
        string packRoot,
        string gameplayRoot,
        bool allowExternalTilesets,
        HashSet<string> visited,
        out string reason)
    {
        if (!visited.Add(xmlPath))
        {
            reason = string.Empty;
            return true;
        }

        XDocument document;
        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
            };
            using XmlReader reader = XmlReader.Create(xmlPath, settings);
            document = XDocument.Load(reader, LoadOptions.None);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or XmlException)
        {
            reason = $"Couldn't inspect TMX dependency file '{xmlPath}'. {exception.Message}";
            return false;
        }

        if (allowExternalTilesets)
        {
            foreach (XElement tileSet in document.Descendants()
                         .Where(element => element.Name.LocalName == "tileset"))
            {
                string? source = tileSet.Attribute("source")?.Value;
                if (string.IsNullOrWhiteSpace(source))
                {
                    continue;
                }

                if (!TryResolveInsideGameplayRoot(
                        xmlPath,
                        source,
                        packRoot,
                        gameplayRoot,
                        out string dependency,
                        out reason))
                {
                    return false;
                }

                if (!File.Exists(dependency))
                {
                    reason = $"External tileset '{source}' doesn't exist inside GameplayRoot.";
                    return false;
                }

                if (!TryValidateXmlFile(
                        dependency,
                        packRoot,
                        gameplayRoot,
                        allowExternalTilesets: false,
                        visited,
                        out reason))
                {
                    return false;
                }
            }
        }

        foreach (XElement image in document.Descendants()
                     .Where(element => element.Name.LocalName == "image"))
        {
            string? source = image.Attribute("source")?.Value;
            if (string.IsNullOrWhiteSpace(source))
            {
                reason = $"TMX dependency file '{xmlPath}' contains an image without a source.";
                return false;
            }

            if (!TryResolveInsideGameplayRoot(
                    xmlPath,
                    source,
                    packRoot,
                    gameplayRoot,
                    out string dependency,
                    out reason))
            {
                return false;
            }

            if (HasExplicitFileExtension(source) && !File.Exists(dependency))
            {
                reason = $"Tilesheet image '{source}' doesn't exist inside GameplayRoot.";
                return false;
            }
        }

        reason = string.Empty;
        return true;
    }

    private static bool TryResolveInsideGameplayRoot(
        string ownerFile,
        string source,
        string packRoot,
        string gameplayRoot,
        out string resolved,
        out string reason)
    {
        resolved = string.Empty;
        if (source.IndexOf('\0') >= 0
            || Path.IsPathRooted(source)
            || Uri.TryCreate(source, UriKind.Absolute, out _))
        {
            reason = $"TMX dependency '{source}' must be a relative pack path.";
            return false;
        }

        try
        {
            resolved = Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(ownerFile) ?? string.Empty,
                ToPlatformPath(source)));
        }
        catch (Exception exception) when (exception is ArgumentException
            or NotSupportedException
            or PathTooLongException)
        {
            reason = $"TMX dependency '{source}' has an invalid path. {exception.Message}";
            return false;
        }

        if (!IsInside(resolved, packRoot) || !IsInside(resolved, gameplayRoot))
        {
            reason = $"TMX dependency '{source}' resolves outside GameplayRoot.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool IsInside(string path, string directoryWithSeparator) =>
        PhysicalPathSemantics.StartsWith(path, directoryWithSeparator);

    private static bool HasExplicitFileExtension(string source) =>
        !string.IsNullOrEmpty(Path.GetExtension(ToPlatformPath(source)));

    private static string EnsureTrailingSeparator(string path) =>
        path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        + Path.DirectorySeparatorChar;

    private static string ToPlatformPath(string path) =>
        path.Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
}
