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

/// <summary>
/// Designer-authored hard training ceilings for the six confirmed E..S ability ranks.
/// Values are prototype balance, not product invariants; ordering is validated by the Resource
/// adapter while the rank identities themselves remain stable domain concepts.
/// </summary>
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

public sealed record LifecycleRules(float ChildToAdultSeconds);

public sealed record ShopRules(int StoreEggPrice, int TrainingItemPrice);

/// <summary>
/// Open-game Garden income. The exact rate is prototype balance and intentionally authorable;
/// the stable product rule is that passive currency accrues while the game is running, with no
/// active-computer-use multiplier or daily cap baked into Domain logic.
/// </summary>
public sealed record EconomyRules(float GardenCoinsPerMinute);

/// <summary>
/// Current-care drift and treat effects. Exact values remain prototype balance knobs. Closing the
/// game supplies no elapsed simulation time, so drift never creates offline neglect punishment.
/// Happiness is intentionally hidden from player-facing projections even though care actions can
/// change it for later lifecycle decisions.
/// </summary>
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

/// <summary>
/// First-evolution tuning. Raising influence is normalized against MaxTrainingPoints, so the
/// threshold remains meaningful when the overall training scale is rebalanced.
/// </summary>
public sealed record EvolutionRules(float SpecializationThreshold);

/// <summary>
/// Current race constants extracted from the MVP controller. Keeping them immutable and
/// domain-owned lets the forthcoming headless simulator reuse exactly the same balancing
/// while Godot presentation remains free to change independently.
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

/// <summary>
/// Immutable rules consumed by pure game logic. Godot Resource authoring adapters can
/// validate and convert to this shape later; domain code never reads Resources directly.
/// </summary>
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
    public EvolutionRules Evolution { get; init; } = new(SpecializationThreshold: 0.50f);
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

    public static GameBalanceRules DemoDefaults { get; } = new(
        Genetics: new GeneticsRules(
            StatIds: Array.AsReadOnly(new[] { "run", "swim", "fly", "power", "stamina" }),
            GradeWeights: Array.AsReadOnly(new[] { 10, 24, 34, 21, 9, 2 }),
            HigherAlleleExpressionChance: 0.70,
            AbilityRankBreakthroughChance: 0.01,
            // Chao-style appearance baseline: 14 stable colour alleles. Existing Voidling palette
            // indices 0-9 are preserved; four new colours are appended so old saves do not shift.
            ColorAlleleCount: 14,
            RareFounderTraitChance: 0.0005,
            RareTraitTransmissionChance: 0.10,
            FounderTraitIds: Array.AsReadOnly(new[] { "Lustrous", "Prismatic", "Aurora" }),
            RelatedAncestorDepth: 3),
        Appearance: new AppearanceRules(Array.AsReadOnly(new[]
        {
            "#F6F0C9",
            "#E7A6B6",
            "#A9D5C0",
            "#B7B2E8",
            "#F0C778",
            "#A8C8EC",
            "#D4A7E8",
            "#E9B690",
            "#AFCB7A",
            "#D9D1C6",
            "#E56B63",
            "#78CBE8",
            "#8E6C56",
            "#343941"
        })),
        Breeding: new BreedingRules(
            CooldownSeconds: 8.0f,
            HatchFailurePercentByBurden: Array.AsReadOnly(new[] { 0, 20, 50, 80, 100 })),
        Hatching: new HatchingRules(IncubationSeconds: 22.0f),
        Stats: new StatGrowthRules(
            TrainingPointsPerLevel: 12,
            MaxLevel: 99,
            MaxTrainingPoints: 120),
        Lifecycle: new LifecycleRules(ChildToAdultSeconds: 45.0f),
        Shop: new ShopRules(StoreEggPrice: 30, TrainingItemPrice: 8),
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