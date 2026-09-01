using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VoidlingGame;

/// <summary>
/// Persisted potential for one ability/personality locus. AlleleA and AlleleB are the two inherited
/// values; ExpressedAlleleIndex selects the currently expressed value.
/// </summary>
public sealed class GenePairData
{
    public int AlleleA { get; set; }
    public int AlleleB { get; set; }
    public int ExpressedAlleleIndex { get; set; }

    [JsonIgnore]
    public int ExpressedValue => ExpressedAlleleIndex == 0 ? AlleleA : AlleleB;
}

/// <summary>
/// Stable appearance-locus conventions. Existing saves deserialize missing fields as zero, so zero
/// is deliberately the vanilla/recessive value for every newly-added appearance locus.
/// </summary>
public static class AppearanceAlleles
{
    public const int NormalColor = 0;

    public const int TwoTone = 0;
    public const int MonoTone = 1;

    public const int DefaultPattern = 0;

    public const int NonShiny = 0;
    public const int Shiny = 1;

    public const int NoSpecialCoat = 0;
    public const int GlowCoat = 1;
    public const int GlistenCoat = 2;
}

/// <summary>
/// Persisted breeding potential. Ability genes contain the two visible DNA profiles; trained
/// performance remains separate on VoidlingData.TrainingPoints and is never written back here.
///
/// Appearance follows a Chao-style diploid model. Each locus carries one allele from each parent.
/// The expressed index is the deterministic birth-time tie-breaker for equally-dominant alleles;
/// dominance itself is resolved by AppearancePhenotypeResolver.
///
/// PersonalityGenes reserve the stable semantic v1 personality vector. They are atmospheric only:
/// racing and stat calculation intentionally do not read these loci.
/// </summary>
public sealed class GenomeData
{
    public Dictionary<string, GenePairData> AbilityGenes { get; set; } = new();
    public Dictionary<string, GenePairData> PersonalityGenes { get; set; } = new();

    // Legacy color fields are retained verbatim for save compatibility.
    public int ColorAlleleA { get; set; }
    public int ColorAlleleB { get; set; }
    public int ExpressedColorIndex { get; set; }

    // Two-tone and mono-tone are equally dominant, matching Chao appearance genetics.
    public int ToneAlleleA { get; set; }
    public int ToneAlleleB { get; set; }
    public int ExpressedToneIndex { get; set; }

    // Pattern is a Voidling extension using the same Chao-style recessive-default/equal-dominance
    // rule as color/coat. Actual pattern art is registered by the presentation visual definition.
    public int PatternAlleleA { get; set; }
    public int PatternAlleleB { get; set; }
    public int ExpressedPatternIndex { get; set; }

    // Shiny is dominant over non-shiny.
    public int ShinyAlleleA { get; set; }
    public int ShinyAlleleB { get; set; }

    // Special coats (for example glow/glisten) are dominant over the normal coat. Different
    // non-normal coats are equally dominant and use ExpressedCoatIndex as their birth-time tie-break.
    public int CoatAlleleA { get; set; }
    public int CoatAlleleB { get; set; }
    public int ExpressedCoatIndex { get; set; }
}

public sealed class RareTraitData
{
    public string TraitId { get; set; } = "";
    public string FounderCreatureId { get; set; } = "";
    public int GenerationFromFounder { get; set; }
    public bool CanTransmit { get; set; }
}
