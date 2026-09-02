namespace StardewInteriorChanger.Core;

public abstract record InteriorChoice
{
    private InteriorChoice()
    {
    }

    public sealed record VanillaChoice : InteriorChoice;

    public sealed record CustomChoice(
        VariantId VariantId,
        ContentHash ContentHash) : InteriorChoice;

    public static InteriorChoice Vanilla { get; } = new VanillaChoice();

    public static InteriorChoice Custom(VariantId variantId, ContentHash contentHash) =>
        new CustomChoice(variantId, contentHash);
}

public sealed record StoredSelection(
    int DataVersion,
    InteriorInstanceId InstanceId,
    TargetContractId TargetContract,
    InteriorChoice Choice)
{
    public const int CurrentDataVersion = 1;

    public static StoredSelection Create(
        InteriorInstanceId instanceId,
        TargetContractId targetContract,
        InteriorChoice choice) =>
        new(CurrentDataVersion, instanceId, targetContract, choice);
}
