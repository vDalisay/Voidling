using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VoidlingGame;

public sealed class GenePairData
{
    public int AlleleA { get; set; }
    public int AlleleB { get; set; }
    public int ExpressedAlleleIndex { get; set; }

    [JsonIgnore]
    public int ExpressedValue => ExpressedAlleleIndex == 0 ? AlleleA : AlleleB;
}

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
