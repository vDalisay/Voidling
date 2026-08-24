using System;
using System.Collections.Generic;
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
        var result = new List<RareTraitData>();
        TryInheritFrom(parentA, seed, "a", result);
        TryInheritFrom(parentB, seed, "b", result);
        return result;
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
                CanTransmit = nextGeneration < 2
            });
        }
    }
}
