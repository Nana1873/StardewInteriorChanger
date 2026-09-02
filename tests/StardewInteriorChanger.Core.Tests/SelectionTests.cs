using StardewInteriorChanger.Core;

namespace StardewInteriorChanger.Core.Tests;

public sealed class SelectionTests
{
    [Fact]
    public void StoredSelection_VanillaUsesDedicatedChoiceType()
    {
        StoredSelection selection = StoredSelection.Create(
            new InteriorInstanceId("location:Greenhouse"),
            TargetContracts.Greenhouse,
            InteriorChoice.Vanilla);

        Assert.Equal(StoredSelection.CurrentDataVersion, selection.DataVersion);
        Assert.IsType<InteriorChoice.VanillaChoice>(selection.Choice);
    }

    [Fact]
    public void StoredSelection_CustomCarriesExactSelectedHash()
    {
        VariantId id = VariantId.Create("Example.Interiors", "wide");
        ContentHash hash = ContentHash.Parse(new string('a', 64));

        StoredSelection selection = StoredSelection.Create(
            new InteriorInstanceId("building:00000000-0000-0000-0000-000000000001"),
            TargetContracts.DeluxeBarn,
            InteriorChoice.Custom(id, hash));

        var custom = Assert.IsType<InteriorChoice.CustomChoice>(selection.Choice);
        Assert.Equal(id, custom.VariantId);
        Assert.Equal(hash, custom.ContentHash);
    }
}
