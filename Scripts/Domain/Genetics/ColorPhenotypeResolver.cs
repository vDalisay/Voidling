using System;
using Voidling.Domain.Rules;
using VoidlingGame;

namespace Voidling.Domain.Genetics;

/// <summary>
/// Compatibility wrapper retained for callers that only need the resolved primary tint. Full
/// appearance resolution (tone/pattern/shiny/coat) lives in AppearancePhenotypeResolver.
/// </summary>
public sealed class ColorPhenotypeResolver
{
    private readonly AppearancePhenotypeResolver _appearance;

    public ColorPhenotypeResolver(AppearanceRules rules)
    {
        _appearance = new AppearancePhenotypeResolver(
            rules ?? throw new ArgumentNullException(nameof(rules)));
    }

    public string ResolveTint(GenomeData genome)
        => _appearance.ResolveTint(genome);
}
