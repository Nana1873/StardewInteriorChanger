namespace StardewInteriorChanger;

internal static class MultiplayerMessageTypes
{
    public const string RegistryHello = "RegistryHello";
    public const string SelectionRequest = "SelectionRequest";
    public const string SelectionResult = "SelectionResult";
    public const string SelectionCommitted = "SelectionCommitted";
}

internal sealed class RegistryHelloMessage
{
    public ushort ProtocolMajor { get; set; }

    public ushort ProtocolMinor { get; set; }

    public string ModVersion { get; set; } = string.Empty;

    public List<VariantFingerprintMessage> Variants { get; set; } = new();
}

internal sealed class VariantFingerprintMessage
{
    public string Id { get; set; } = string.Empty;

    public string Target { get; set; } = string.Empty;

    public string ContentHash { get; set; } = string.Empty;
}

internal sealed class SelectionRequestMessage
{
    public string BuildingId { get; set; } = string.Empty;

    public string? VariantId { get; set; }
}

internal sealed class SelectionResultMessage
{
    public bool Success { get; set; }

    public string BuildingId { get; set; } = string.Empty;

    public string? VariantId { get; set; }

    public string Message { get; set; } = string.Empty;
}

internal sealed class SelectionCommittedMessage
{
    public string BuildingId { get; set; } = string.Empty;

    public string? VariantId { get; set; }

    public string? ContentHash { get; set; }
}
