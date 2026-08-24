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

    public static string NameFor(string statId)
        => DisplayNames.TryGetValue(statId, out var name) ? name : statId;

    public static Color ColorFor(string statId)
        => IdentityColors.TryGetValue(statId, out var color) ? color : Colors.White;
}
