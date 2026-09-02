using StardewInteriorChanger.Core;

namespace StardewInteriorChanger.Core.Tests;

public sealed class InteriorPackJsonTests
{
    [Fact]
    public void Parse_ValidDocument_PreservesSchemaFields()
    {
        const string json = """
            {
              "FormatVersion": 1,
              "Interiors": [
                {
                  "Id": "wide-greenhouse",
                  "DisplayName": "Wide Greenhouse",
                  "Target": "Greenhouse",
                  "GameplayRoot": "assets/wide-greenhouse",
                  "Map": "interior.tmx",
                  "Preview": "previews/wide-greenhouse.png"
                }
              ]
            }
            """;

        InteriorPackParseResult result = InteriorPackJson.Parse(json);

        Assert.True(result.IsValid);
        InteriorDefinitionDto definition = Assert.Single(result.Document!.Interiors!);
        Assert.Equal("wide-greenhouse", definition.Id);
        Assert.Equal("Greenhouse", definition.Target);
        Assert.Equal("assets/wide-greenhouse", definition.GameplayRoot);
    }

    [Fact]
    public void Parse_InvalidJson_ReturnsDiagnosticInsteadOfThrowing()
    {
        InteriorPackParseResult result = InteriorPackJson.Parse("{ definitely-not-json }");

        Assert.False(result.IsValid);
        Assert.Null(result.Document);
        Assert.Equal(RegistryDiagnosticCode.InvalidJson, Assert.Single(result.Diagnostics).Code);
    }
}
