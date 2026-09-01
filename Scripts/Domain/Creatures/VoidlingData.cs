using System.Collections.Generic;
using Voidling.Domain.Care;
using Voidling.Domain.Evolution;

namespace VoidlingGame;

public enum LifeStage
{
    Child,
    Adult
}

public enum CreatureDepartureReason
{
    None,
    Goodbye,
    Death
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
    public float AdultAgeSeconds { get; set; }
    public int ReincarnationCount { get; set; }
    public CreatureDepartureReason DepartureReason { get; set; }
    public float BreedCooldownSeconds { get; set; }
    public string ParentAId { get; set; } = "";
    public string ParentBId { get; set; } = "";
    public int FamilyGeneration { get; set; }
    public int InbreedingBurdenLevel { get; set; }
    public bool InbreedingHistoryFlag { get; set; }
    public string TintHex { get; set; } = "#F6F0C9";
    public List<RareTraitData> RareTraits { get; set; } = new();

    public CreatureNeedsState Needs { get; set; } = new();

    // Core food preference travels with the Voidling across saves/trades/reincarnation. The ID is
    // intentionally hidden from presentation until FavoriteFoodDiscovered becomes true.
    public string FavoriteFoodId { get; set; } = "";
    public bool FavoriteFoodDiscovered { get; set; }

    // Passive-training stat remains for backward-compatible saves and presentation. New player
    // assignments bind to a semantic Garden module ID; migration refreshes the cached rate from
    // that module's current level/placement so balance tuning and upgrades remain authoritative.
    public string PassiveTrainingStatId { get; set; } = "";
    public string PassiveTrainingModuleId { get; set; } = "";
    public float PassiveTrainingPointsPerMinute { get; set; }
    public double PassiveTrainingPointRemainder { get; set; }

    public float SwimFlyInfluence { get; set; }
    public float RunPowerInfluence { get; set; }
    public EvolutionSpecialization EvolutionSpecialization { get; set; } = EvolutionSpecialization.None;
    public float EvolutionMagnitude { get; set; }

    public float WorldX { get; set; }
    public float WorldY { get; set; }
}
