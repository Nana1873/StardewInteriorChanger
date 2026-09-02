using System.Text.Json;

namespace StardewInteriorChanger.Core;

public sealed class InteriorPackDocument
{
    public int FormatVersion { get; set; }

    public List<InteriorDefinitionDto>? Interiors { get; set; } = new();
}

public sealed class InteriorDefinitionDto
{
    public string? Id { get; set; }

    public string? DisplayName { get; set; }

    public string? Target { get; set; }

    public string? GameplayRoot { get; set; }

    public string? Map { get; set; }

    public string? Preview { get; set; }

    public Dictionary<string, List<TilePointDto>?>? Anchors { get; set; }
}

public sealed class TilePointDto
{
    public int X { get; set; }

    public int Y { get; set; }
}

public sealed record InteriorPackParseResult(
    InteriorPackDocument? Document,
    IReadOnlyList<RegistryDiagnostic> Diagnostics)
{
    public bool IsValid => Document is not null
        && Diagnostics.All(diagnostic => diagnostic.Severity != RegistryDiagnosticSeverity.Error);
}

public static class InteriorPackJson
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public static InteriorPackParseResult Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return InvalidJson("The interiors document is empty.");
        }

        try
        {
            InteriorPackDocument? document = JsonSerializer.Deserialize<InteriorPackDocument>(json, SerializerOptions);
            return document is null
                ? InvalidJson("The interiors document contains JSON null.")
                : new InteriorPackParseResult(document, Array.Empty<RegistryDiagnostic>());
        }
        catch (JsonException exception)
        {
            return InvalidJson(exception.Message);
        }
    }

    private static InteriorPackParseResult InvalidJson(string message) => new(
        null,
        new[]
        {
            new RegistryDiagnostic(
                RegistryDiagnosticCode.InvalidJson,
                RegistryDiagnosticSeverity.Error,
                message)
        });
}
