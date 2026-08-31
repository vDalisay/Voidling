using System.Collections.Generic;

namespace VoidlingGame;

public enum EggSource
{
    Store,
    Bred
}

public enum EggState
{
    Incubating = 0,
    Failed = 1,
    WaitingForSpace = 2
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

    public float WorldX { get; set; }
    public float WorldY { get; set; }
}
