using System;
using Voidling.Domain.Rules;
using VoidlingGame;

namespace Voidling.Domain.Genetics;

public sealed class ColorPhenotypeResolver
{
    private readonly AppearanceRules _rules;

    public ColorPhenotypeResolver(AppearanceRules rules)
    {
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
    }

    public string ResolveTint(GenomeData genome)
    {
        ArgumentNullException.ThrowIfNull(genome);
        var index = genome.ExpressedColorIndex == 0 ? genome.ColorAlleleA : genome.ColorAlleleB;
        index = Math.Clamp(index, 0, _rules.PaletteHex.Count - 1);
        return _rules.PaletteHex[index];
    }
}
