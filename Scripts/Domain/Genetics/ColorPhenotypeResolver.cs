using System;
using System.Globalization;
using Voidling.Domain.Rules;
using VoidlingGame;

namespace Voidling.Domain.Genetics;

/// <summary>
/// Resolves semantic color DNA into a palette anchor hue. One DNA profile is selected as the
/// dominant side (ExpressedColorIndex), then nudged a small configurable amount toward the other
/// profile around the shortest direction of the hue wheel. This keeps breeding stochastic while
/// allowing gradual color-family drift instead of selecting one fixed tint swatch forever.
/// </summary>
public sealed class ColorPhenotypeResolver
{
    private readonly AppearanceRules _rules;

    public ColorPhenotypeResolver(AppearanceRules rules)
    {
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
    }

    public float ResolvePaletteHue(GenomeData genome)
    {
        ArgumentNullException.ThrowIfNull(genome);
        EnsurePaletteGenes(genome);

        var first = VoidlingAppearanceData.NormalizeHue(genome.PaletteHueA);
        var second = VoidlingAppearanceData.NormalizeHue(genome.PaletteHueB);
        var winner = genome.ExpressedColorIndex == 0 ? first : second;
        var other = genome.ExpressedColorIndex == 0 ? second : first;
        var influence = (float)Math.Clamp(_rules.PaletteBlendInfluence, 0.0, 0.49);
        return MoveHueToward(winner, other, influence);
    }

    public void EnsurePaletteGenes(GenomeData genome)
    {
        ArgumentNullException.ThrowIfNull(genome);
        if (!VoidlingAppearanceData.IsValidHue(genome.PaletteHueA))
            genome.PaletteHueA = HueForLegacyAllele(genome.ColorAlleleA);
        if (!VoidlingAppearanceData.IsValidHue(genome.PaletteHueB))
            genome.PaletteHueB = HueForLegacyAllele(genome.ColorAlleleB);
        genome.ExpressedColorIndex = genome.ExpressedColorIndex == 0 ? 0 : 1;
    }

    public float AlleleHue(GenomeData genome, int profileIndex)
    {
        EnsurePaletteGenes(genome);
        return profileIndex == 0
            ? VoidlingAppearanceData.NormalizeHue(genome.PaletteHueA)
            : VoidlingAppearanceData.NormalizeHue(genome.PaletteHueB);
    }

    public string ResolveTint(GenomeData genome)
    {
        ArgumentNullException.ThrowIfNull(genome);
        var targetHue = ResolvePaletteHue(genome);
        var legacyIndex = genome.ExpressedColorIndex == 0 ? genome.ColorAlleleA : genome.ColorAlleleB;
        var sourceHex = _rules.PaletteHex.Count == 0
            ? "#F6F0C9"
            : _rules.PaletteHex[Math.Clamp(legacyIndex, 0, _rules.PaletteHex.Count - 1)];
        var (_, saturation, value) = RgbHexToHsv(sourceHex);
        return HsvToHex(targetHue, saturation, value);
    }

    public float HueForLegacyAllele(int allele)
    {
        if (_rules.PaletteHex.Count == 0)
            return 0.0f;
        var hex = _rules.PaletteHex[Math.Clamp(allele, 0, _rules.PaletteHex.Count - 1)];
        return RgbHexToHsv(hex).Hue;
    }

    public static float MoveHueToward(float winnerHue, float otherHue, float influence)
    {
        winnerHue = VoidlingAppearanceData.NormalizeHue(winnerHue);
        otherHue = VoidlingAppearanceData.NormalizeHue(otherHue);
        influence = Math.Clamp(influence, 0.0f, 0.49f);

        var delta = otherHue - winnerHue;
        if (delta > 0.5f)
            delta -= 1.0f;
        else if (delta < -0.5f)
            delta += 1.0f;

        return VoidlingAppearanceData.NormalizeHue(winnerHue + delta * influence);
    }

    private static (float Hue, float Saturation, float Value) RgbHexToHsv(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return (0.0f, 0.0f, 1.0f);

        var value = hex.Trim().TrimStart('#');
        if (value.Length == 3)
            value = string.Concat(value[0], value[0], value[1], value[1], value[2], value[2]);
        if (value.Length < 6 ||
            !byte.TryParse(value[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rb) ||
            !byte.TryParse(value.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var gb) ||
            !byte.TryParse(value.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var bb))
        {
            return (0.0f, 0.0f, 1.0f);
        }

        var r = rb / 255.0f;
        var g = gb / 255.0f;
        var b = bb / 255.0f;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var chroma = max - min;
        var hue = 0.0f;
        if (chroma > 0.00001f)
        {
            if (Math.Abs(max - r) < 0.00001f)
                hue = ((g - b) / chroma) % 6.0f;
            else if (Math.Abs(max - g) < 0.00001f)
                hue = (b - r) / chroma + 2.0f;
            else
                hue = (r - g) / chroma + 4.0f;
            hue /= 6.0f;
        }

        var saturation = max <= 0.00001f ? 0.0f : chroma / max;
        return (VoidlingAppearanceData.NormalizeHue(hue), saturation, max);
    }

    private static string HsvToHex(float hue, float saturation, float value)
    {
        hue = VoidlingAppearanceData.NormalizeHue(hue);
        saturation = Math.Clamp(saturation, 0.0f, 1.0f);
        value = Math.Clamp(value, 0.0f, 1.0f);

        var h = hue * 6.0f;
        var sector = (int)MathF.Floor(h) % 6;
        var fraction = h - MathF.Floor(h);
        var p = value * (1.0f - saturation);
        var q = value * (1.0f - fraction * saturation);
        var t = value * (1.0f - (1.0f - fraction) * saturation);
        var (r, g, b) = sector switch
        {
            0 => (value, t, p),
            1 => (q, value, p),
            2 => (p, value, t),
            3 => (p, q, value),
            4 => (t, p, value),
            _ => (value, p, q)
        };

        return $"#{ToByte(r):X2}{ToByte(g):X2}{ToByte(b):X2}";
    }

    private static byte ToByte(float value)
        => (byte)Math.Clamp((int)MathF.Round(value * 255.0f), 0, 255);
}
