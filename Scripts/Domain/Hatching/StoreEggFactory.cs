using System;
using Voidling.Domain.Genetics;
using Voidling.Domain.Rules;
using VoidlingGame;

namespace Voidling.Domain.Hatching;

/// <summary>
/// Creates a fully rolled store egg at inventory-entry time. The returned EggData already owns
/// its seed, genome, semantic appearance/palette phenotype, and founder-trait roll; purchase/hatching
/// only move this persisted object and must never reroll it.
/// </summary>
public sealed class StoreEggFactory
{
    private readonly GameBalanceRules _rules;
    private readonly GenomeFactory _genomes;
    private readonly RareTraitInheritanceService _rareTraits;
    private readonly ColorPhenotypeResolver _colors;

    public StoreEggFactory(GameBalanceRules rules)
    {
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        _genomes = new GenomeFactory(rules.Genetics, rules.Appearance);
        _rareTraits = new RareTraitInheritanceService(rules.Genetics);
        _colors = new ColorPhenotypeResolver(rules.Appearance);
    }

    public EggData Create(string eggId, ulong eggSeed)
    {
        if (string.IsNullOrWhiteSpace(eggId))
            throw new ArgumentException("A store egg requires a stable ID.", nameof(eggId));

        var genome = _genomes.CreateRandom(eggSeed);
        var rareTraits = _rareTraits.RollFounderTraits(eggSeed, eggId);
        return new EggData
        {
            Id = eggId,
            Source = EggSource.Store,
            Seed = eggSeed,
            Genome = genome,
            RequiredIncubationSeconds = IncubationPolicy.RequiredSeconds(genome, rareTraits, isSpecialVariant: false, _rules.Hatching),
            TintHex = _colors.ResolveTint(genome),
            Appearance = new VoidlingAppearanceData
            {
                VisualTypeId = VoidlingAppearanceData.DefaultVisualTypeId,
                PaletteHue = _colors.ResolvePaletteHue(genome)
            },
            RareTraits = rareTraits,
            IsViable = true,
            FailureResolved = true
        };
    }
}
