using System;
using Voidling.Domain.Genetics;
using Voidling.Domain.Rules;
using VoidlingGame;

namespace Voidling.Domain.Hatching;

/// <summary>
/// Creates a fully rolled store egg at inventory-entry time. The returned EggData already owns
/// its seed, genome, phenotype-facing tint, and founder-trait roll; purchase/hatching only move
/// this persisted object and must never reroll it.
///
/// Rare appearance bloodlines are introduced through an explicit FounderAppearanceTemplate rather
/// than an invented mutation rate. Shop/catalog code can therefore author a shiny/glow/glisten/
/// patterned egg when desired while the normal genetics path remains unchanged.
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
        _genomes = new GenomeFactory(rules.Genetics);
        _rareTraits = new RareTraitInheritanceService(rules.Genetics);
        _colors = new ColorPhenotypeResolver(rules.Appearance);
    }

    public EggData Create(string eggId, ulong eggSeed)
        => CreateCore(eggId, eggSeed, appearance: null);

    public EggData Create(
        string eggId,
        ulong eggSeed,
        FounderAppearanceTemplate appearance)
        => CreateCore(
            eggId,
            eggSeed,
            appearance ?? throw new ArgumentNullException(nameof(appearance)));

    private EggData CreateCore(
        string eggId,
        ulong eggSeed,
        FounderAppearanceTemplate? appearance)
    {
        if (string.IsNullOrWhiteSpace(eggId))
            throw new ArgumentException("A store egg requires a stable ID.", nameof(eggId));

        var genome = appearance == null
            ? _genomes.CreateRandom(eggSeed)
            : _genomes.CreateRandom(eggSeed, appearance);
        return new EggData
        {
            Id = eggId,
            Source = EggSource.Store,
            Seed = eggSeed,
            Genome = genome,
            RequiredIncubationSeconds = _rules.Hatching.IncubationSeconds,
            TintHex = _colors.ResolveTint(genome),
            RareTraits = _rareTraits.RollFounderTraits(eggSeed, eggId),
            IsViable = true,
            FailureResolved = true
        };
    }
}
