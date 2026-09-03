using StardewModdingAPI.Utilities;

namespace StardewInteriorChanger;

internal sealed class ModConfig
{
    public KeybindList OpenMenu { get; set; } = KeybindList.Parse("F8");
}
