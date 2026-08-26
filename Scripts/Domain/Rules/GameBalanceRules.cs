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

public sealed record StatGrowthRules(int TrainingPointsPerLevel, int MaxLevel, int MaxTrainingPoints);

public sealed record LifecycleRules(float ChildToAdultSeconds);

public sealed record ShopRules(int StoreEggPrice, int TrainingItemPrice);

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
    public static GameBalanceRules DemoDefaults { get; } = new(
        Genetics: new GeneticsRules(
            StatIds: Array.AsReadOnly(new[] { "run", "swim", "fly", "power", "stamina" }),
            GradeWeights: Array.AsReadOnly(new[] { 10, 24, 34, 21, 9, 2 }),
            HigherAlleleExpressionChance: 0.70,
            AbilityRankBreakthroughChance: 0.01,
            ColorAlleleCount: 10,
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
            "#D9D1C6"
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
