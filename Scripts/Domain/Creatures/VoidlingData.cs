using System.Collections.Generic;
using Voidling.Domain.Evolution;

namespace VoidlingGame;

public enum LifeStage
{
    Child,
    Adult
}

/// <summary>
/// Persisted creature state. The legacy namespace is intentionally retained during the
/// architecture migration so existing scenes/controllers and save serialization remain stable.
/// </summary>
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

    // Hidden raising influence is persisted independently from trained performance. It determines
    // first-evolution specialization but is never read by race simulation as a substitute stat.
    public float SwimFlyInfluence { get; set; }
    public float RunPowerInfluence { get; set; }
    public EvolutionSpecialization EvolutionSpecialization { get; set; } = EvolutionSpecialization.None;
    public float EvolutionMagnitude { get; set; }

    // Initial/world placement. Normal wandering does not continuously write to the save.
    public float WorldX { get; set; }
    public float WorldY { get; set; }
}
