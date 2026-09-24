using System;
using System.Collections.Generic;
using Godot;

namespace Voidling.Presentation.UI.Common;

/// <summary>
/// Player-facing names for the semantic visual types a Voidling can have: the baby look, the adult
/// forms and special variants. Gameplay decides the type; this only names it.
/// </summary>
public static class VoidlingFormPresentationCatalog
{
    private static readonly IReadOnlyDictionary<string, string> NameKeys =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["normal"] = "FORM_BABY",
            ["neutral"] = "FORM_NEUTRAL",
            ["run"] = "FORM_RUN",
            ["water"] = "FORM_SWIM",
            ["fly"] = "FORM_FLY",
            ["power"] = "FORM_POWER",
            ["swamp-variant"] = "FORM_SWAMP_VARIANT"
        };

    public static string NameKeyFor(string? visualTypeId)
        => visualTypeId != null && NameKeys.TryGetValue(visualTypeId.Trim(), out var key) ? key : "FORM_NEUTRAL";

    public static string NameFor(string? visualTypeId)
        => TranslationServer.Translate(NameKeyFor(visualTypeId));

    /// <summary>A special variant's egg looks the part in the Garden, the inventory and the shop.</summary>
    public static Color SpecialEggTint(string? variantId)
        => string.Equals(variantId, "swamp-variant", StringComparison.OrdinalIgnoreCase)
            ? Color.FromHtml("#7FA35A")
            : Colors.White;
}
