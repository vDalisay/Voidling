using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VoidlingGame;

public static class GeneticsService
{
    private static readonly int[] GradeWeights = { 10, 24, 34, 21, 9, 2 };

    public static GenomeData CreateRandomGenome(ulong seed)
    {
        var genome = new GenomeData();

        foreach (var statId in GameRules.StatIds)
        {
            var rng = CreateRandom(seed, $"random:{statId}");
            var a = RollGrade(rng);
            var b = RollGrade(rng);
            genome.AbilityGenes[statId] = CreateExpressedPair(a, b, CreateRandom(seed, $"express:{statId}"));
        }

        var colorRng = CreateRandom(seed, "random:color");
        genome.ColorAlleleA = colorRng.Next(GameRules.PaletteHex.Length);
        genome.ColorAlleleB = colorRng.Next(GameRules.PaletteHex.Length);
        genome.ExpressedColorIndex = colorRng.NextDouble() < 0.5 ? 0 : 1;

        return genome;
    }

    public static GenomeData CreateChildGenome(VoidlingData parentA, VoidlingData parentB, ulong seed)
    {
        var genome = new GenomeData();

        foreach (var statId in GameRules.StatIds)
        {
            var a = PickAllele(GameRules.GetGene(parentA, statId), CreateRandom(seed, $"inherit:{statId}:a"));
            var b = PickAllele(GameRules.GetGene(parentB, statId), CreateRandom(seed, $"inherit:{statId}:b"));
            genome.AbilityGenes[statId] = CreateExpressedPair(a, b, CreateRandom(seed, $"express:{statId}"));
        }

        var colorA = CreateRandom(seed, "inherit:color:a");
        var colorB = CreateRandom(seed, "inherit:color:b");
        genome.ColorAlleleA = colorA.NextDouble() < 0.5 ? parentA.Genome.ColorAlleleA : parentA.Genome.ColorAlleleB;
        genome.ColorAlleleB = colorB.NextDouble() < 0.5 ? parentB.Genome.ColorAlleleA : parentB.Genome.ColorAlleleB;
        genome.ExpressedColorIndex = CreateRandom(seed, "express:color").NextDouble() < 0.5 ? 0 : 1;

        return genome;
    }

    public static string ResolveTint(GenomeData genome)
    {
        var index = genome.ExpressedColorIndex == 0 ? genome.ColorAlleleA : genome.ColorAlleleB;
        index = Math.Clamp(index, 0, GameRules.PaletteHex.Length - 1);
        return GameRules.PaletteHex[index];
    }

    public static List<RareTraitData> RollFounderTraits(ulong seed, string founderId)
    {
        var result = new List<RareTraitData>();
        var rng = CreateRandom(seed, "rare:founder");

        if (rng.NextDouble() >= GameRules.RareFounderTraitChance)
            return result;

        result.Add(new RareTraitData
        {
            TraitId = GameRules.RareTraitIds[rng.Next(GameRules.RareTraitIds.Length)],
            FounderCreatureId = founderId,
            GenerationFromFounder = 0,
            CanTransmit = true
        });

        return result;
    }

    public static List<RareTraitData> InheritRareTraits(VoidlingData parentA, VoidlingData parentB, ulong seed)
    {
        var result = new List<RareTraitData>();
        TryInheritFrom(parentA, seed, "a", result);
        TryInheritFrom(parentB, seed, "b", result);
        return result;
    }

    private static void TryInheritFrom(VoidlingData parent, ulong seed, string side, List<RareTraitData> result)
    {
        for (var i = 0; i < parent.RareTraits.Count; i++)
        {
            var trait = parent.RareTraits[i];
            if (!trait.CanTransmit)
                continue;

            var rng = CreateRandom(seed, $"rare:inherit:{side}:{trait.FounderCreatureId}:{trait.TraitId}:{i}");
            if (rng.NextDouble() >= GameRules.RareTraitTransmissionChance)
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

    public static bool AreRelated(VoidlingData a, VoidlingData b, IReadOnlyList<VoidlingData> population)
    {
        if (a.Id == b.Id)
            return true;

        var byId = population.ToDictionary(v => v.Id, StringComparer.Ordinal);
        var ancestorsA = GetAncestors(a, byId, GameRules.RelatedAncestorDepth);
        var ancestorsB = GetAncestors(b, byId, GameRules.RelatedAncestorDepth);

        if (ancestorsA.Contains(b.Id) || ancestorsB.Contains(a.Id))
            return true;

        return ancestorsA.Overlaps(ancestorsB);
    }

    public static int ComputeChildBurden(VoidlingData parentA, VoidlingData parentB, bool related)
    {
        if (related)
            return Math.Clamp(Math.Max(parentA.InbreedingBurdenLevel, parentB.InbreedingBurdenLevel) + 1, 1, 4);

        var a = parentA.InbreedingBurdenLevel;
        var b = parentB.InbreedingBurdenLevel;

        if (a > 0 && b == 0)
            return Math.Max(a - 1, 0);

        if (b > 0 && a == 0)
            return Math.Max(b - 1, 0);

        if (a > 0 && b > 0)
            return Math.Max(a, b);

        return 0;
    }

    public static bool RollViability(ulong seed, int burden)
    {
        var failurePercent = GameRules.HatchFailurePercent(burden);
        if (failurePercent <= 0)
            return true;
        if (failurePercent >= 100)
            return false;

        var rng = CreateRandom(seed, "inbreeding:viability");
        return rng.Next(100) >= failurePercent;
    }

    public static Random CreateRandom(ulong seed, string salt)
    {
        var hash = StableHash(seed, salt);
        return new Random(unchecked((int)(hash ^ (hash >> 32))));
    }

    private static GenePairData CreateExpressedPair(int a, int b, Random rng)
    {
        var expressedIndex = 0;

        if (a == b)
        {
            expressedIndex = 0;
        }
        else
        {
            var higherIndex = a > b ? 0 : 1;
            var lowerIndex = higherIndex == 0 ? 1 : 0;
            expressedIndex = rng.NextDouble() < GameRules.HigherAlleleExpressionChance ? higherIndex : lowerIndex;
        }

        return new GenePairData
        {
            AlleleA = a,
            AlleleB = b,
            ExpressedAlleleIndex = expressedIndex
        };
    }

    private static int PickAllele(GenePairData gene, Random rng)
        => rng.NextDouble() < 0.5 ? gene.AlleleA : gene.AlleleB;

    private static int RollGrade(Random rng)
    {
        var total = GradeWeights.Sum();
        var roll = rng.Next(total);
        var cumulative = 0;

        for (var i = 0; i < GradeWeights.Length; i++)
        {
            cumulative += GradeWeights[i];
            if (roll < cumulative)
                return i;
        }

        return GradeWeights.Length - 1;
    }

    private static HashSet<string> GetAncestors(
        VoidlingData creature,
        IReadOnlyDictionary<string, VoidlingData> byId,
        int maxDepth)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        var frontier = new Queue<(string Id, int Depth)>();

        if (!string.IsNullOrWhiteSpace(creature.ParentAId))
            frontier.Enqueue((creature.ParentAId, 1));
        if (!string.IsNullOrWhiteSpace(creature.ParentBId))
            frontier.Enqueue((creature.ParentBId, 1));

        while (frontier.Count > 0)
        {
            var (id, depth) = frontier.Dequeue();
            if (depth > maxDepth || string.IsNullOrWhiteSpace(id) || !result.Add(id))
                continue;

            if (!byId.TryGetValue(id, out var ancestor))
                continue;

            if (!string.IsNullOrWhiteSpace(ancestor.ParentAId))
                frontier.Enqueue((ancestor.ParentAId, depth + 1));
            if (!string.IsNullOrWhiteSpace(ancestor.ParentBId))
                frontier.Enqueue((ancestor.ParentBId, depth + 1));
        }

        return result;
    }

    private static ulong StableHash(ulong seed, string salt)
    {
        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;

        var hash = offset;

        for (var i = 0; i < sizeof(ulong); i++)
        {
            hash ^= (byte)(seed >> (8 * i));
            hash *= prime;
        }

        foreach (var value in Encoding.UTF8.GetBytes(salt))
        {
            hash ^= value;
            hash *= prime;
        }

        return hash;
    }
}
