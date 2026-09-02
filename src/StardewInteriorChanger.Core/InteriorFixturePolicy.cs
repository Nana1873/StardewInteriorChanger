namespace StardewInteriorChanger.Core;

public static class InteriorFixturePolicy
{
    public const string DeluxeBarnFeedHopperId = "(BC)99";

    public static bool IsBuiltInObjectFixture(
        InteriorTarget target,
        string? qualifiedItemId) =>
        target == InteriorTarget.DeluxeBarn
        && string.Equals(
            qualifiedItemId,
            DeluxeBarnFeedHopperId,
            StringComparison.Ordinal);
}
