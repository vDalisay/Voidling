using System;
using System.Collections.Generic;

namespace Voidling.Domain.Rules;

public sealed record GeneticsRules(
    IReadOnlyList<string> StatIds,
    IReadOnlyList<int> GradeWeights,
    double HigherAlleleExpressionChance,
    int ColorAlleleCount,
    double RareFounderTraitChance,
    double RareTraitTransmissionChance,
    IReadOnlyList<string> FounderTraitIds,
    int RelatedAncestorDepth);

public sealed record BreedingRules(float CooldownSeconds, IReadOnlyList<int> HatchFailurePercentByBurden);

public sealed record HatchingRules(float IncubationSeconds);

public sealed record StatGrowthRules(int TrainingPointsPerLevel, int MaxLevel, int MaxTrainingPoints);

public sealed record LifecycleRules(float ChildToAdultSeconds);

public sealed record ShopRules(int StoreEggPrice, int TrainingItemPrice);

/// <summary>
/// Immutable rules consumed by pure game logic. Godot Resource authoring adapters can
/// validate and convert to this shape later; domain code never reads Resources directly.
/// </summary>
public sealed record GameBalanceRules(
    GeneticsRules Genetics,
    BreedingRules Breeding,
    HatchingRules Hatching,
    StatGrowthRules Stats,
    LifecycleRules Lifecycle,
    ShopRules Shop)
{
    public static GameBalanceRules DemoDefaults { get; } = new(
        Genetics: new GeneticsRules(
            StatIds: Array.AsReadOnly(new[] { "run", "swim", "fly", "power", "stamina" }),
            GradeWeights: Array.AsReadOnly(new[] { 10, 24, 34, 21, 9, 2 }),
            HigherAlleleExpressionChance: 0.70,
            ColorAlleleCount: 10,
            RareFounderTraitChance: 0.0005,
            RareTraitTransmissionChance: 0.10,
            FounderTraitIds: Array.AsReadOnly(new[] { "Lustrous", "Prismatic", "Aurora" }),
            RelatedAncestorDepth: 3),
        Breeding: new BreedingRules(
            CooldownSeconds: 8.0f,
            HatchFailurePercentByBurden: Array.AsReadOnly(new[] { 0, 20, 50, 80, 100 })),
        Hatching: new HatchingRules(IncubationSeconds: 22.0f),
        Stats: new StatGrowthRules(
            TrainingPointsPerLevel: 12,
            MaxLevel: 99,
            MaxTrainingPoints: 120),
        Lifecycle: new LifecycleRules(ChildToAdultSeconds: 45.0f),
        Shop: new ShopRules(StoreEggPrice: 30, TrainingItemPrice: 8));
}
