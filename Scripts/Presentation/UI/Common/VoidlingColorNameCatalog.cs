using Godot;

namespace Voidling.Presentation.UI.Common;

/// <summary>
/// Names a Voidling's displayed tint so players can search the roster by colour. Purely a
/// presentation label over the hue that colour DNA already resolved; it feeds no gameplay rule.
/// </summary>
public static class VoidlingColorNameCatalog
{
    // The first ten allele IDs are saved data. IDs 10–29 add two shades of each existing group.
    public static string NameForAllele(int allele)
    {
        if (allele is >= 10 and < 30) allele = (allele - 10) / 2;
        if (allele is >= 30 and < 34) return "Red";
        if (allele is >= 34 and < 38) return "Black";
        return allele switch
        {
            1 => "Pink",
            2 or 8 => "Green",
            3 or 6 => "Purple",
            4 => "Yellow",
            5 => "Blue",
            7 => "Orange",
            _ => "Neutral"
        };
    }

    public static string NameFor(Color tint)
    {
        var hue = tint.H;
        if (tint.V < 0.22f)
            return "Black";
        if (tint.S < 0.16f)
            return tint.V < 0.7f ? "Grey" : "Cream";
        if (tint.S < 0.28f && tint.V > 0.9f && hue is > 0.10f and < 0.20f)
            return "Cream";

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
