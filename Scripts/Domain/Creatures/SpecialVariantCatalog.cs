using System;
using System.Collections.Generic;
using System.Linq;
using Voidling.Domain.Evolution;
using Voidling.Domain.Garden;
using VoidlingGame;

namespace Voidling.Domain.Creatures;

/// <summary>
/// A special variant: a unique Voidling with its own look that comes from a special environment.
/// The first time two adults of <see cref="ParentForm"/> breed while the player owns an unused
/// <see cref="Environment"/>, their egg is this variant's egg; it only incubates on an unused hex of
/// that environment, is born with <see cref="ForcedStatId"/> at <see cref="ForcedRank"/> in both
/// alleles, and keeps <see cref="VisualTypeId"/> for life. Its looks do not pass on.
/// </summary>
public sealed record SpecialVariantDefinition(
    string Id,
    EvolutionSpecialization ParentForm,
    string Environment,
    string ForcedStatId,
    int ForcedRank,
    string VisualTypeId,
    string RespawnEggItemId);

public static class SpecialVariantCatalog
{
    public const string SwampGuyId = "swamp-variant";

    /// <summary>The green-ish Water Voidling of the Swamp: guaranteed S/S Swim.</summary>
    public static SpecialVariantDefinition SwampGuy { get; } = new(
        SwampGuyId,
        EvolutionSpecialization.Swim,
        BiomeCatalog.Swamp,
        "swim",
        ForcedRank: 5,
        VisualTypeId: SwampGuyId,
        RespawnEggItemId: "shop.swamp-guy-egg");

    public static IReadOnlyList<SpecialVariantDefinition> All { get; } = Array.AsReadOnly(new[] { SwampGuy });

    public static SpecialVariantDefinition? Find(string? variantId)
        => All.FirstOrDefault(variant => string.Equals(variant.Id, variantId, StringComparison.Ordinal));

    public static SpecialVariantDefinition? FindByRespawnItem(string? itemId)
        => All.FirstOrDefault(variant => string.Equals(variant.RespawnEggItemId, itemId, StringComparison.Ordinal));

    /// <summary>Both parents are adults of the variant's parent form (for the Swamp guy: two Water adults).</summary>
    public static bool ParentsQualify(SpecialVariantDefinition variant, VoidlingData parentA, VoidlingData parentB)
    {
        ArgumentNullException.ThrowIfNull(variant);
        ArgumentNullException.ThrowIfNull(parentA);
        ArgumentNullException.ThrowIfNull(parentB);
        return IsQualifyingParent(variant, parentA) && IsQualifyingParent(variant, parentB);
    }

    /// <summary>Sets the variant's guaranteed stat: both alleles at the forced rank.</summary>
    public static void ForceGenes(SpecialVariantDefinition variant, GenomeData genome)
    {
        ArgumentNullException.ThrowIfNull(variant);
        ArgumentNullException.ThrowIfNull(genome);
        genome.AbilityGenes[variant.ForcedStatId] = new GenePairData
        {
            AlleleA = variant.ForcedRank,
            AlleleB = variant.ForcedRank,
            ExpressedAlleleIndex = 0
        };
    }

    private static bool IsQualifyingParent(SpecialVariantDefinition variant, VoidlingData parent)
        => parent.Stage == LifeStage.Adult && parent.EvolutionSpecialization == variant.ParentForm;
}
