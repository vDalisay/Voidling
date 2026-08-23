using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VoidlingGame;

public enum LifeStage
{
    Child,
    Adult
}

public enum EggSource
{
    Store,
    Bred
}

public enum EggState
{
    Incubating,
    Failed
}

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

public sealed class VoidlingData
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "Voidling";
    public GenomeData Genome { get; set; } = new();
    public Dictionary<string, int> TrainingPoints { get; set; } = new();
    public LifeStage Stage { get; set; } = LifeStage.Child;
    public float AgeSeconds { get; set; }
    public float BreedCooldownSeconds { get; set; }
    public string ParentAId { get; set; } = "";
    public string ParentBId { get; set; } = "";
    public int FamilyGeneration { get; set; }
    public int InbreedingBurdenLevel { get; set; }
    public bool InbreedingHistoryFlag { get; set; }
    public string TintHex { get; set; } = "#F6F0C9";
    public List<RareTraitData> RareTraits { get; set; } = new();
}

public sealed class EggData
{
    public string Id { get; set; } = "";
    public EggSource Source { get; set; }
    public ulong Seed { get; set; }
    public GenomeData Genome { get; set; } = new();
    public string ParentAId { get; set; } = "";
    public string ParentBId { get; set; } = "";
    public int FamilyGeneration { get; set; }
    public int InbreedingBurdenLevel { get; set; }
    public bool InbreedingHistoryFlag { get; set; }
    public bool IsViable { get; set; } = true;
    public bool FailureResolved { get; set; }
    public EggState State { get; set; } = EggState.Incubating;
    public float IncubationSeconds { get; set; }
    public float RequiredIncubationSeconds { get; set; }
    public string TintHex { get; set; } = "#F6F0C9";
    public List<RareTraitData> RareTraits { get; set; } = new();
}

public sealed class GameStateData
{
    public int SaveVersion { get; set; } = 1;
    public int Coins { get; set; } = 120;
    public long SeedCounter { get; set; } = 1;
    public List<VoidlingData> Voidlings { get; set; } = new();
    public List<EggData> OwnedEggs { get; set; } = new();
    public List<EggData> StoreEggs { get; set; } = new();
    public Dictionary<string, int> TrainingItems { get; set; } = new();
}
