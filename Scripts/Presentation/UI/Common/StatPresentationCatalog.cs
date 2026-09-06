using System;
using System.Collections.Generic;
using Godot;

namespace Voidling.Presentation.UI.Common;

/// <summary>
/// Presentation identity for the five stable gameplay stats. Gameplay stat IDs and formulas
/// remain domain-owned; this catalog only maps those IDs to player-facing labels and colors.
/// </summary>
public static class StatPresentationCatalog
{
    private static readonly IReadOnlyDictionary<string, string> DisplayNames =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["run"] = "Run",
            ["swim"] = "Swim",
            ["fly"] = "Fly",
            ["power"] = "Power",
            ["stamina"] = "Stamina"
        };

    private static readonly IReadOnlyDictionary<string, Color> IdentityColors =
        new Dictionary<string, Color>(StringComparer.Ordinal)
        {
            ["run"] = Color.FromHtml("#78C96A"),
            ["swim"] = Color.FromHtml("#F2D45C"),
            ["fly"] = Color.FromHtml("#B47AE5"),
            ["power"] = Color.FromHtml("#E7655A"),
            ["stamina"] = Color.FromHtml("#F7F3E7")
        };

    /// <summary>
    /// Where each stat's treat sits on the premium fruit sheet. The Shop, the inventory and the
    /// world drop all read the fruit from here, so a Run treat is the same berry everywhere.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, int> TreatIcons =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["run"] = 0,
            ["swim"] = 1,
            ["fly"] = 2,
            ["power"] = 3,
            ["stamina"] = 4
        };

    public const string TreatAtlasPath =
        "res://Assets/Sprout Lands - Sprites - premium pack/Objects/Items/fruit-n-berries-items.png";

    /// <summary>The 16x16 cell on the fruit sheet that is this stat's treat.</summary>
    public static Rect2 TreatRegionFor(string statId)
    {
        var index = TreatIcons.TryGetValue(statId, out var found) ? found : 0;
        return new Rect2(index % 4 * 16, index / 4 * 16, 16, 16);
    }

    public static string NameFor(string statId)
        => DisplayNames.TryGetValue(statId, out var name) ? name : statId;

    public static Color ColorFor(string statId)
        => IdentityColors.TryGetValue(statId, out var color) ? color : Colors.White;
}
