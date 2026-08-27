using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VoidlingGame;

/// <summary>
/// Persisted potential for one ability locus. AlleleA and AlleleB are the two player-visible
/// DNA-profile values (DNA1/DNA2); ExpressedAlleleIndex selects the current phenotype value.
/// The legacy property names are retained for save compatibility.
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
/// Persisted breeding potential. Ability genes contain the two visible DNA profiles; trained
/// performance remains separate on VoidlingData.TrainingPoints and is never written back here.
/// </summary>
public sealed class GenomeData
{
    public Dictionary<string, GenePairData> AbilityGenes { get; set; } = new();
    public int ColorAlleleA { get; set; }
    public int ColorAlleleB { get; set; }
    public int ExpressedColorIndex { get; set; }
}

public sealed class RareTraitData
{
    public string TraitId { get; set; } = "";
    public string FounderCreatureId { get; set; } = "";
    public int GenerationFromFounder { get; set; }
    public bool CanTransmit { get; set; }
}
