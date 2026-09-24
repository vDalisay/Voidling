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

    /// <summary>
    /// Chao-style progress per stat ID: level 0-99, bar progress and stat points. Replaced the
    /// rank-capped training points in save version 23; older saves start again at level 0.
    /// </summary>
    public Dictionary<string, StatProgressData> Stats { get; set; } = new();
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

    // Legacy single-tint presentation value. Kept for save/backwards compatibility and fallback UI;
    // production rendering resolves semantic Appearance + continuous palette DNA through the visual catalog.
    public string TintHex { get; set; } = "#F6F0C9";
    public VoidlingAppearanceData Appearance { get; set; } = new();

    public List<RareTraitData> RareTraits { get; set; } = new();

    /// <summary>
    /// Blank for an ordinary Voidling; a special variant's ID (the Swamp guy) otherwise. A special
    /// variant keeps its look for life, cannot be traded, and its looks do not pass on.
    /// </summary>
    public string SpecialVariantId { get; set; } = "";

    public CreatureNeedsState Needs { get; set; } = new();

    // Core food preference travels with the Voidling across saves/trades/reincarnation. The ID is
    // intentionally hidden from presentation until FavoriteFoodDiscovered becomes true.
    public string FavoriteFoodId { get; set; } = "";
    public bool FavoriteFoodDiscovered { get; set; }

    // Passive-training assignments use semantic Garden module/stat IDs. These are gameplay state,
    // never presentation resource identifiers.
    public string PassiveTrainingStatId { get; set; } = "";
    public string PassiveTrainingModuleId { get; set; } = "";
    public float PassiveTrainingPointsPerMinute { get; set; }
    public double PassiveTrainingPointRemainder { get; set; }

    // Retired hidden raising influence from the first evolution rule. Kept so older saves load;
    // adulthood now reads stat levels instead.
    public float SwimFlyInfluence { get; set; }
    public float RunPowerInfluence { get; set; }

    /// <summary>The adult form chosen at adulthood; None while a baby, Generalist for Neutral.</summary>
    public EvolutionSpecialization EvolutionSpecialization { get; set; } = EvolutionSpecialization.None;
    public float EvolutionMagnitude { get; set; }

    // Initial/world placement. Normal wandering does not continuously write to the save.
    public float WorldX { get; set; }
    public float WorldY { get; set; }
}
