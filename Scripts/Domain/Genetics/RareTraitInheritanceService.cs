using System;
using System.Collections.Generic;
using System.Linq;
using Voidling.Domain.Rules;
using Voidling.Domain.Shared;
using VoidlingGame;

namespace Voidling.Domain.Genetics;

public sealed class RareTraitInheritanceService
{
    private readonly GeneticsRules _rules;

    public RareTraitInheritanceService(GeneticsRules rules)
    {
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
    }

    public List<RareTraitData> RollFounderTraits(ulong seed, string founderId)
    {
        var result = new List<RareTraitData>();
        var random = StableRandom.Create(seed, "rare:founder");
        if (random.NextDouble() >= _rules.RareFounderTraitChance)
            return result;

        result.Add(new RareTraitData
        {
            TraitId = _rules.FounderTraitIds[random.Next(_rules.FounderTraitIds.Count)],
            FounderCreatureId = founderId,
            GenerationFromFounder = 0,
            CanTransmit = true
        });
        return result;
    }

    public List<RareTraitData> Inherit(VoidlingData parentA, VoidlingData parentB, ulong seed)
    {
        var inherited = new List<RareTraitData>();
        TryInheritFrom(parentA, seed, "a", inherited);
        TryInheritFrom(parentB, seed, "b", inherited);
        return Deduplicate(inherited);
    }

    /// <summary>
    /// A child carries each rare trait once, even when both parents transmit it. The surviving copy
    /// is the one closest to its founder so the child keeps the longest remaining transmission
    /// depth; ties resolve on founder ID so the outcome does not depend on parent order.
    /// </summary>
    private static List<RareTraitData> Deduplicate(List<RareTraitData> traits)
    {
        var best = new Dictionary<string, RareTraitData>(StringComparer.OrdinalIgnoreCase);
        foreach (var trait in traits)
        {
            if (!best.TryGetValue(trait.TraitId, out var current) || IsCloserToFounder(trait, current))
                best[trait.TraitId] = trait;
        }

        return traits
            .Where(trait => ReferenceEquals(best[trait.TraitId], trait))
            .ToList();
    }

    private static bool IsCloserToFounder(RareTraitData candidate, RareTraitData current)
    {
        if (candidate.GenerationFromFounder != current.GenerationFromFounder)
            return candidate.GenerationFromFounder < current.GenerationFromFounder;

        return string.CompareOrdinal(candidate.FounderCreatureId, current.FounderCreatureId) < 0;
    }

    private void TryInheritFrom(VoidlingData parent, ulong seed, string side, ICollection<RareTraitData> result)
    {
        for (var i = 0; i < parent.RareTraits.Count; i++)
        {
            var trait = parent.RareTraits[i];
            if (!trait.CanTransmit)
                continue;

            var random = StableRandom.Create(seed, $"rare:inherit:{side}:{trait.FounderCreatureId}:{trait.TraitId}:{i}");
            if (random.NextDouble() >= _rules.RareTraitTransmissionChance)
                continue;

            var nextGeneration = trait.GenerationFromFounder + 1;
            result.Add(new RareTraitData
            {
                TraitId = trait.TraitId,
                FounderCreatureId = trait.FounderCreatureId,
                GenerationFromFounder = nextGeneration,
                CanTransmit = nextGeneration < _rules.RareTraitMaxTransmittedGenerations
            });
        }
    }
}
