using System;
using System.Collections.Generic;
using Voidling.Domain.Breeding;
using Voidling.Domain.Genetics;
using Voidling.Domain.Hatching;
using Voidling.Domain.Rules;
using Voidling.Domain.Shared;

namespace VoidlingGame;

/// <summary>
/// Compatibility facade for MVP callers. New domain/application code should depend on the
/// focused services in Scripts/Domain instead of growing this static surface.
/// </summary>
public static class GeneticsService
{
    private static readonly GameBalanceRules Rules = GameBalanceRules.DemoDefaults;
    private static readonly GenomeFactory GenomeFactory = new(Rules.Genetics);
    private static readonly GenomeInheritanceService GenomeInheritance = new(Rules.Genetics);
    private static readonly RareTraitInheritanceService RareTraits = new(Rules.Genetics);
    private static readonly RelationshipService Relationships = new(Rules.Genetics.RelatedAncestorDepth);
    private static readonly InbreedingBurdenService InbreedingBurden = new();
    private static readonly HatchViabilityService HatchViability = new(Rules.Breeding);

    public static GenomeData CreateRandomGenome(ulong seed)
        => GenomeFactory.CreateRandom(seed);

    public static GenomeData CreateChildGenome(VoidlingData parentA, VoidlingData parentB, ulong seed)
        => GenomeInheritance.CreateChild(parentA, parentB, seed);

    public static string ResolveTint(GenomeData genome)
    {
        var index = genome.ExpressedColorIndex == 0 ? genome.ColorAlleleA : genome.ColorAlleleB;
        index = Math.Clamp(index, 0, GameRules.PaletteHex.Length - 1);
        return GameRules.PaletteHex[index];
    }

    public static List<RareTraitData> RollFounderTraits(ulong seed, string founderId)
        => RareTraits.RollFounderTraits(seed, founderId);

    public static List<RareTraitData> InheritRareTraits(VoidlingData parentA, VoidlingData parentB, ulong seed)
        => RareTraits.Inherit(parentA, parentB, seed);

    public static bool AreRelated(VoidlingData first, VoidlingData second, IReadOnlyList<VoidlingData> population)
        => Relationships.AreRelated(first, second, population);

    public static int ComputeChildBurden(VoidlingData parentA, VoidlingData parentB, bool related)
        => InbreedingBurden.ComputeChildBurden(parentA, parentB, related);

    public static bool RollViability(ulong seed, int burden)
        => HatchViability.RollViability(seed, burden);

    public static Random CreateRandom(ulong seed, string salt)
        => StableRandom.Create(seed, salt);
}
