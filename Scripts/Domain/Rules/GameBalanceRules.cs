using System;
using System.Collections.Generic;
using Voidling.Domain.Garden;

namespace Voidling.Domain.Rules;

public sealed record GeneticsRules(
    IReadOnlyList<string> StatIds,
    IReadOnlyList<int> GradeWeights,
    double HigherAlleleExpressionChance,
    double AbilityRankBreakthroughChance,
    int ColorAlleleCount,
    double RareFounderTraitChance,
    double RareTraitTransmissionChance,
    IReadOnlyList<string> FounderTraitIds,
    int RelatedAncestorDepth,
    int RareTraitMaxTransmittedGenerations);

public sealed record AppearanceRules(
    IReadOnlyList<string> PaletteHex,
    double PaletteBlendInfluence);

public sealed record BreedingRules(float CooldownSeconds, IReadOnlyList<int> HatchFailurePercentByBurden);
/// <summary>
/// Incubation: a base time plus extra time for every S-rank stat, every rare trait and a special
/// variant. Worked out when an egg is created and then fixed.
/// </summary>
public sealed record HatchingRules(float IncubationSeconds)
{
    public float SecondsPerSRankStat { get; init; } = 45.0f;
    public float SecondsPerRareTrait { get; init; } = 120.0f;
    public float SpecialVariantSeconds { get; init; } = 300.0f;
}
public sealed record GardenRules(int MaxPopulation);
public sealed record DailyLoginRules(IReadOnlyList<int> CoinRewards);

public enum DailyMissionEventKind
{
    PetVoidling,
    UseTrainingTreat,
    BreedEgg,
    HatchEgg,
    CompleteStandardRace,
    PurchaseShopItem
}

public sealed record DailyMissionDefinition(
    string Id,
    DailyMissionEventKind EventKind,
    int Target,
    int CoinReward);

public sealed record DailyMissionRules(
    int MissionsPerDay,
    IReadOnlyList<DailyMissionDefinition> Definitions);

/// <summary>How much one training treat is worth, before any favorite-food bonus.</summary>
public sealed record TrainingItemRules(int MinGain, int MaxGain);

/// <summary>
/// Garden land and biome tiles. <see cref="BiomeTilePrice"/> buys a one-star biome tile; stars come
/// from stacking matching tiles, and every star has its own passive training rate.
/// </summary>
public sealed record GardenModuleRules(
    int BiomeTilePrice,
    IReadOnlyList<float> PointsPerMinuteByLevel)
{
    /// <summary>
    /// How many Voidlings one hex can train at once. A three-hex piece is three tiles once it is
    /// down, so it holds three trainees without touching this number.
    /// </summary>
    public int VoidlingsPerTile { get; init; } = 1;

    /// <summary>Cost of one hex of plain ground; a piece costs this per hex it covers.</summary>
    public int EmptyHexCost { get; init; } = 25;

    /// <summary>
    /// Hex geometry the land snaps to. Sized in sprite proportions: a 5-wide flat top scales to
    /// this tile at x21, so the 16px premium ground tiles read at a comfortable size on a hex big
    /// enough for a Voidling to live on.
    /// </summary>
    public GardenHexLayout Hex { get; init; } = new(
        TopEdgeWidth: 105.0f,
        Height: 180.0f,
        OriginX: 416.0f,
        OriginY: 240.0f);

    /// <summary>The highest star a biome tile can reach: one passive rate per star.</summary>
    public int MaxLevel => Math.Max(1, PointsPerMinuteByLevel.Count);

    public float PointsPerMinuteForLevel(int level)
    {
        if (PointsPerMinuteByLevel.Count == 0)
            return 0.0f;
        var index = Math.Clamp(level, 1, MaxLevel) - 1;
        return Math.Max(0.0f, PointsPerMinuteByLevel[index]);
    }
}

/// <summary>
/// Chao Garden stat growth. Every stat levels 0..<see cref="MaxLevel"/> whatever its rank; training
/// fills <see cref="ProgressPerLevel"/> steps per level, and a level-up adds
/// <c>PointsPerLevelPerRank × rank + PointsPerLevelBase + random(1..PointsPerLevelRandomMax)</c>
/// stat points, capped at <see cref="PointCap"/>. Races read the points.
/// </summary>
public sealed record StatGrowthRules(int ProgressPerLevel, int MaxLevel, int PointCap)
{
    public int PointsPerLevelBase { get; init; } = 11;
    public int PointsPerLevelPerRank { get; init; } = 3;
    public int PointsPerLevelRandomMax { get; init; } = 5;

    /// <summary>The level every stat returns to on reincarnation (Chao Garden: 1, not 0).</summary>
    public int ReincarnationLevel { get; init; } = 1;
}

public sealed record PassiveTrainingRules(float PointsPerMinute);
public sealed record FavoriteFoodRules(int BonusTrainingPoints);
/// <summary>Baby stage length in seconds of open-game time; hours, not weeks, for an idle game.</summary>
public sealed record LifecycleRules(float ChildToAdultSeconds);
public sealed record ReincarnationRules(
    // Adult lifetime in seconds of open-game time. The simulation only advances while the game is
    // running, so this is playtime rather than wall-clock age.
    float AdultLifespanSeconds,
    // Hidden happiness is the only reincarnation condition: at or above this a Voidling reincarnates.
    float MinimumHappiness,
    // Share of each stat's points a Voidling keeps when it reincarnates.
    float RetainedPointFraction)
{
    /// <summary>
    /// Below this the Garden log warns once that a Voidling needs care. Kept apart from
    /// <see cref="MinimumHappiness"/>, which is high enough that every small dip would warn.
    /// </summary>
    public float CareRiskHappiness { get; init; } = 30.0f;
}

public sealed record ShopRules(int StoreEggPrice, int TrainingItemPrice, int EggShellSalePrice)
{
    public float EggRotationIntervalSeconds { get; init; } = 3600.0f;
    public int StoreEggSlotCount { get; init; } = 3;
    public double RareOfferAppearanceChance { get; init; } = 0.20;
    public int FullIncubationSkipPrice { get; init; } = 45;

    /// <summary>A special variant's respawn egg, sold only after that variant has departed.</summary>
    public int SpecialVariantEggPrice { get; init; } = 250;
}

public sealed record EconomyRules(float GardenCoinsPerMinute);

public sealed record NeedsRules(
    float HungerGainPerMinute,
    float EnergyLossPerMinute,
    float FatigueGainPerMinute,
    float StressRecoveryPerMinute,
    float BoredomGainPerMinute,
    float LonelinessGainPerMinute,
    float NourishmentLossPerMinute,
    float ConditionLossPerMinute,
    float HappinessLossPerMinute,
    float TreatHungerReduction,
    float TreatEnergyGain,
    float TreatNourishmentGain,
    float TreatHappinessGain);

/// <summary>The level the highest stat must reach at adulthood to give a typed form instead of Neutral.</summary>
public sealed record EvolutionRules(int MinimumFormLevel);

/// <summary>
/// Current race constants. Presentation animation never owns authoritative race results.
/// </summary>
public sealed record RaceRules(
    float BaseStamina,
    float StaminaPerPoint,
    float BaseStaminaDrainPerSecond,
    float GroundBaseSpeed,
    float GroundRunSpeedScale,
    float SwimBaseSpeed,
    float SwimSpeedScale,
    float SwimExtraDrain,
    float ClimbBaseSpeed,
    float ClimbPowerSpeedScale,
    float ClimbExtraDrain,
    float GlideBaseSpeed,
    float GlideSpeedScale,
    float GlideExtraDrain,
    float FailedGlideSwimBaseSpeed,
    float FailedGlideSwimSpeedScale,
    float FailedGlideSwimExtraDrain,
    float LowStaminaThreshold,
    float LowStaminaSpeedMultiplier,
    float ExhaustedSpeedMultiplier,
    float CheerDurationSeconds,
    float CheerCost,
    float CheerSpeedMultiplier,
    float GlideBaseDistance,
    float GlideDistancePerFlyPoint,
    float ObstacleAvoidBaseChance,
    float ObstacleAvoidRunScale,
    float ObstacleAvoidMaxChance,
    float ObstacleBaseDelaySeconds,
    float ObstacleLowRunDelaySeconds,
    float ObstacleRollbackDistance,
    IReadOnlyList<int> PlacementRewards);

public sealed record GameBalanceRules(
    GeneticsRules Genetics,
    AppearanceRules Appearance,
    BreedingRules Breeding,
    HatchingRules Hatching,
    StatGrowthRules Stats,
    LifecycleRules Lifecycle,
    ShopRules Shop,
    RaceRules Racing)
{
    public GardenRules Garden { get; init; } = new(MaxPopulation: 8);
    public TrainingItemRules TrainingItems { get; init; } = new(MinGain: 5, MaxGain: 9);
    public GardenModuleRules GardenModules { get; init; } = new(
        BiomeTilePrice: 40,
        PointsPerMinuteByLevel: Array.AsReadOnly(new[] { 1.0f, 1.5f, 2.0f, 3.0f }));
    public DailyLoginRules DailyLogin { get; init; } = new(Array.AsReadOnly(new[] { 5, 7, 9, 12, 15, 20, 30 }));
    public DailyMissionRules DailyMissions { get; init; } = new(
        MissionsPerDay: 3,
        Definitions: Array.AsReadOnly(new[]
        {
            new DailyMissionDefinition("pet-2", DailyMissionEventKind.PetVoidling, 2, 8),
            new DailyMissionDefinition("train-1", DailyMissionEventKind.UseTrainingTreat, 1, 10),
            new DailyMissionDefinition("breed-1", DailyMissionEventKind.BreedEgg, 1, 12),
            new DailyMissionDefinition("hatch-1", DailyMissionEventKind.HatchEgg, 1, 15),
            new DailyMissionDefinition("race-1", DailyMissionEventKind.CompleteStandardRace, 1, 12),
            new DailyMissionDefinition("shop-1", DailyMissionEventKind.PurchaseShopItem, 1, 8)
        }));
    public PassiveTrainingRules PassiveTraining { get; init; } = new(PointsPerMinute: 1.0f);
    public FavoriteFoodRules FavoriteFood { get; init; } = new(BonusTrainingPoints: 1);
    public EvolutionRules Evolution { get; init; } = new(MinimumFormLevel: 10);
    public ReincarnationRules Reincarnation { get; init; } = new(
        AdultLifespanSeconds: 28800.0f,
        MinimumHappiness: 70.0f,
        RetainedPointFraction: 0.10f);
    public EconomyRules Economy { get; init; } = new(GardenCoinsPerMinute: 1.0f);
    public NeedsRules Needs { get; init; } = new(
        HungerGainPerMinute: 0.75f,
        EnergyLossPerMinute: 0.45f,
        FatigueGainPerMinute: 0.35f,
        StressRecoveryPerMinute: 0.20f,
        BoredomGainPerMinute: 0.50f,
        LonelinessGainPerMinute: 0.25f,
        NourishmentLossPerMinute: 0.40f,
        ConditionLossPerMinute: 0.05f,
        HappinessLossPerMinute: 0.10f,
        TreatHungerReduction: 12.0f,
        TreatEnergyGain: 2.0f,
        TreatNourishmentGain: 8.0f,
        TreatHappinessGain: 2.0f);
    public CareInteractionRules CareInteractions { get; init; } = CareInteractionRules.DemoDefaults;

    public static GameBalanceRules DemoDefaults { get; } = new(
        Genetics: new GeneticsRules(
            StatIds: Array.AsReadOnly(new[] { "run", "swim", "fly", "power", "stamina" }),
            GradeWeights: Array.AsReadOnly(new[] { 10, 24, 34, 21, 9, 2 }),
            HigherAlleleExpressionChance: 0.70,
            AbilityRankBreakthroughChance: 0.01,
            // Keep the validated legacy palette cardinality. Continuous hue DNA is authoritative.
            ColorAlleleCount: 10,
            RareFounderTraitChance: 0.0005,
            RareTraitTransmissionChance: 0.10,
            FounderTraitIds: Array.AsReadOnly(new[] { "Lustrous", "Prismatic", "Aurora" }),
            RelatedAncestorDepth: 3,
            RareTraitMaxTransmittedGenerations: 2),
        Appearance: new AppearanceRules(
            PaletteHex: Array.AsReadOnly(new[]
            {
                "#F6F0C9", "#E7A6B6", "#A9D5C0", "#B7B2E8", "#F0C778",
                "#A8C8EC", "#D4A7E8", "#E9B690", "#AFCB7A", "#D9D1C6"
            }),
            PaletteBlendInfluence: 0.18),
        Breeding: new BreedingRules(
            CooldownSeconds: 8.0f,
            HatchFailurePercentByBurden: Array.AsReadOnly(new[] { 0, 20, 50, 80, 100 })),
        Hatching: new HatchingRules(IncubationSeconds: 22.0f),
        Stats: new StatGrowthRules(ProgressPerLevel: 10, MaxLevel: 99, PointCap: 3266),
        Lifecycle: new LifecycleRules(ChildToAdultSeconds: 5400.0f),
        Shop: new ShopRules(StoreEggPrice: 30, TrainingItemPrice: 8, EggShellSalePrice: 5),
        Racing: new RaceRules(
            BaseStamina: 72.0f,
            StaminaPerPoint: 1.05f,
            BaseStaminaDrainPerSecond: 2.1f,
            GroundBaseSpeed: 31.0f,
            GroundRunSpeedScale: 0.36f,
            SwimBaseSpeed: 24.0f,
            SwimSpeedScale: 0.35f,
            SwimExtraDrain: 1.1f,
            ClimbBaseSpeed: 15.0f,
            ClimbPowerSpeedScale: 0.34f,
            ClimbExtraDrain: 1.45f,
            GlideBaseSpeed: 28.0f,
            GlideSpeedScale: 0.40f,
            GlideExtraDrain: 0.85f,
            FailedGlideSwimBaseSpeed: 23.0f,
            FailedGlideSwimSpeedScale: 0.33f,
            FailedGlideSwimExtraDrain: 1.25f,
            LowStaminaThreshold: 0.18f,
            LowStaminaSpeedMultiplier: 0.90f,
            ExhaustedSpeedMultiplier: 0.84f,
            CheerDurationSeconds: 2.0f,
            CheerCost: 24.0f,
            CheerSpeedMultiplier: 1.22f,
            GlideBaseDistance: 82.0f,
            GlideDistancePerFlyPoint: 2.55f,
            ObstacleAvoidBaseChance: 0.28f,
            ObstacleAvoidRunScale: 0.67f,
            ObstacleAvoidMaxChance: 0.95f,
            ObstacleBaseDelaySeconds: 0.62f,
            ObstacleLowRunDelaySeconds: 0.55f,
            ObstacleRollbackDistance: 5.0f,
            PlacementRewards: Array.AsReadOnly(new[] { 30, 20, 10, 5 })));
}
