using Godot;

namespace Voidling.Presentation.UI.Common;

/// <summary>
/// Names a Voidling's displayed tint so players can search the roster by colour. Purely a
/// presentation label over the hue that colour DNA already resolved; it feeds no gameplay rule.
/// </summary>
public static class VoidlingColorNameCatalog
{
    public static string NameFor(Color tint)
    {
        var hue = tint.H;
        if (tint.S < 0.12f)
            return tint.V < 0.5f ? "Grey" : "Cream";

        return hue switch
        {
            < 0.042f => "Red",
            < 0.100f => "Orange",
            < 0.180f => "Yellow",
            < 0.400f => "Green",
            < 0.520f => "Teal",
            < 0.680f => "Blue",
            < 0.800f => "Purple",
            < 0.920f => "Pink",
            _ => "Red"
        };
    }
}
