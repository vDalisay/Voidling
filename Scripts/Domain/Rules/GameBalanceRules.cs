using System;
using System.Collections.Generic;

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
    int RelatedAncestorDepth);

public sealed record AppearanceRules(IReadOnlyList<string> PaletteHex);
public sealed record BreedingRules(float CooldownSeconds, IReadOnlyList<int> HatchFailurePercentByBurden);
public sealed record HatchingRules(float IncubationSeconds);
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

public sealed record GardenModuleRules(
    int SlotCount,
    int PurchaseCost,
    IReadOnlyList<int> UpgradeCosts,
    IReadOnlyList<float> PointsPerMinuteByLevel)
{
    // A level above the available upgrade-cost table would otherwise become a silent free upgrade.
    public int MaxLevel => Math.Max(1, Math.Min(PointsPerMinuteByLevel.Count, UpgradeCosts.Count + 1));

    public float PointsPerMinuteForLevel(int level)
    {
        if (PointsPerMinuteByLevel.Count == 0)
            return 0.0f;

        var index = Math.Clamp(level, 1, MaxLevel) - 1;
        return Math.Max(0.0f, PointsPerMinuteByLevel[index]);
    }

    public int UpgradeCostForLevel(int currentLevel)
    {
        var targetLevel = currentLevel + 1;
        if (targetLevel > MaxLevel)
            return -1;

        var index = targetLevel - 2;
        if (index < 0 || index >= UpgradeCosts.Count)
            return -1;
        return Math.Max(0, UpgradeCosts[index]);
    }
}

public sealed record RankTrainingCaps(int E, int D, int C, int B, int A, int S)
{
    public int ForRank(int rank) => rank switch
    {
        <= 0 => E,
        1 => D,
        2 => C,
        3 => B,
        4 => A,
        _ => S
    };
}

public sealed record StatGrowthRules(int TrainingPointsPerLevel, int MaxLevel, int MaxTrainingPoints)
{
    public RankTrainingCaps RankCaps { get; init; } = new(E: 20, D: 40, C: 60, B: 80, A: 100, S: 120);
}

public sealed record PassiveTrainingRules(float PointsPerMinute);
public sealed record LifecycleRules(float ChildToAdultSeconds);
public sealed record ReincarnationRules(
    float AdultLifespanSeconds,
    float MinimumHappiness,
    float MaximumStress,
    float RetainedTrainingFraction);

public sealed record ShopRules(int StoreEggPrice, int TrainingItemPrice, int EggShellSalePrice)
{
    // Prototype cadence/slot count only. Product explicitly leaves exact shop tuning open.
    public float EggRotationIntervalSeconds { get; init; } = 3600.0f;
    public int StoreEggSlotCount { get; init; } = 3;
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

public sealed record EvolutionRules(float SpecializationThreshold);

public sealed record RaceRules(
    float BaseStamina,
    float StaminaPerPoint,
    float BaseStaminaDrainPerSecond,
    float GroundBaseSpeed,
    float GroundRunSpeedScale,
    float SwimBaseSpeed,
    float SwimSpeedScale,
    float SwimExtraDrain,
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
    // Product requires a hard Garden population cap, but its final number is still tuning data.
    public GardenRules Garden { get; init; } = new(MaxPopulation: 8);

    // Exact Garden geometry/rates/costs remain unresolved. These prototype values exist so the
    // purchase/place/upgrade contract can ship without turning tuning guesses into use-case constants.
    public GardenModuleRules GardenModules { get; init; } = new(
        SlotCount: 4,
        PurchaseCost: 40,
        UpgradeCosts: Array.AsReadOnly(new[] { 25, 50 }),
        PointsPerMinuteByLevel: Array.AsReadOnly(new[] { 1.0f, 1.5f, 2.0f }));

    // Daily-chain values are prototype balance only. The system contract is stable; reward values
    // remain replaceable through the configured balance resource.
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

    // Legacy direct assignments use this base rate. New player assignments bind to placed Garden
    // modules whose level-specific rate comes from GardenModules.
    public PassiveTrainingRules PassiveTraining { get; init; } = new(PointsPerMinute: 1.0f);
    public EvolutionRules Evolution { get; init; } = new(SpecializationThreshold: 0.50f);
    public ReincarnationRules Reincarnation { get; init; } = new(
        AdultLifespanSeconds: 900.0f,
        MinimumHappiness: 10.0f,
        MaximumStress: 70.0f,
        RetainedTrainingFraction: 0.10f);
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
            ColorAlleleCount: 14,
            RareFounderTraitChance: 0.0005,
            RareTraitTransmissionChance: 0.10,
            FounderTraitIds: Array.AsReadOnly(new[] { "Lustrous", "Prismatic", "Aurora" }),
            RelatedAncestorDepth: 3),
        Appearance: new AppearanceRules(Array.AsReadOnly(new[]
        {
            "#F6F0C9", "#E7A6B6", "#A9D5C0", "#B7B2E8", "#F0C778", "#A8C8EC", "#D4A7E8",
            "#E9B690", "#AFCB7A", "#D9D1C6", "#E56B63", "#78CBE8", "#8E6C56", "#343941"
        })),
        Breeding: new BreedingRules(
            CooldownSeconds: 8.0f,
            HatchFailurePercentByBurden: Array.AsReadOnly(new[] { 0, 20, 50, 80, 100 })),
        Hatching: new HatchingRules(IncubationSeconds: 22.0f),
        Stats: new StatGrowthRules(TrainingPointsPerLevel: 12, MaxLevel: 99, MaxTrainingPoints: 120),
        Lifecycle: new LifecycleRules(ChildToAdultSeconds: 45.0f),
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
