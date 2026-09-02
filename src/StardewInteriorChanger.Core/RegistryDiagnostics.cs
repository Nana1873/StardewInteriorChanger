namespace StardewInteriorChanger.Core;

public enum RegistryDiagnosticSeverity
{
    Warning,
    Error
}

public enum RegistryDiagnosticCode
{
    InvalidJson,
    UnsupportedFormatVersion,
    InvalidPackUniqueId,
    MissingInteriors,
    InvalidVariantId,
    DuplicateVariantId,
    InvalidTarget,
    InvalidGameplayRoot,
    InvalidMapPath,
    MissingMapFile,
    InvalidPreviewPath,
    InvalidAnchor,
    InvalidContentFilePath,
    DuplicateContentFilePath,
    ContentEnumerationFailed,
    ContentReadFailed
}

public sealed record RegistryDiagnostic(
    RegistryDiagnosticCode Code,
    RegistryDiagnosticSeverity Severity,
    string Message,
    int? EntryIndex = null,
    string? VariantId = null,
    string? Field = null);
