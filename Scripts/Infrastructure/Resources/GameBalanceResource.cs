using System;
using Godot;
using Voidling.Domain.Rules;

namespace Voidling.Infrastructure.Resources;

/// <summary>
/// Godot Inspector authoring surface for balance values that are already consumed by live
/// gameplay. The Resource never crosses the infrastructure boundary: Bootstrap converts it once
/// into immutable plain-C# GameBalanceRules consumed by Application and Domain.
/// </summary>
[GlobalClass]
public partial class GameBalanceResource : Resource
{
    [ExportGroup("Genetics")]
    [Export(PropertyHint.Range, "0,1,0.01")]
    public float HigherAlleleExpressionChance { get; set; } = 0.70f;

    [Export(PropertyHint.Range, "0,0.1,0.001")]
    public float AbilityRankBreakthroughChance { get; set; } = 0.01f;

    [Export(PropertyHint.Range, "0,0.1,0.0001")]
    public float RareFounderTraitChance { get; set; } = 0.0005f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float RareTraitTransmissionChance { get; set; } = 0.10f;

    [Export(PropertyHint.Range, "1,8,1")]
    public int RelatedAncestorDepth { get; set; } = 3;

    [ExportGroup("Appearance Genetics")]
    [Export(PropertyHint.Range, "0,0.49,0.01")]
    public float PaletteBlendInfluence { get; set; } = 0.18f;

    [ExportGroup("Breeding / Hatching")]
    [Export(PropertyHint.Range, "0,300,0.5")]
    public float BreedCooldownSeconds { get; set; } = 8.0f;

    [Export(PropertyHint.Range, "1,600,0.5")]
    public float EggIncubationSeconds { get; set; } = 22.0f;

    [ExportGroup("Growth")]
    [Export(PropertyHint.Range, "1,100,1")]
    public int TrainingPointsPerLevel { get; set; } = 12;

    [Export(PropertyHint.Range, "1,999,1")]
    public int MaxStatLevel { get; set; } = 99;

    [Export(PropertyHint.Range, "1,10000,1")]
    public int MaxTrainingPoints { get; set; } = 120;

    [Export(PropertyHint.Range, "1,3600,1")]
    public float ChildToAdultSeconds { get; set; } = 45.0f;

    [ExportGroup("Shop")]
    [Export(PropertyHint.Range, "0,10000,1")]
    public int StoreEggPrice { get; set; } = 30;

    [Export(PropertyHint.Range, "0,10000,1")]
    public int TrainingItemPrice { get; set; } = 8;

    public GameBalanceRules ToDomainRules()
    {
        var defaults = GameBalanceRules.DemoDefaults;

        return defaults with
        {
            Genetics = defaults.Genetics with
            {
                HigherAlleleExpressionChance = Probability(HigherAlleleExpressionChance),
                AbilityRankBreakthroughChance = Probability(AbilityRankBreakthroughChance),
                RareFounderTraitChance = Probability(RareFounderTraitChance),
                RareTraitTransmissionChance = Probability(RareTraitTransmissionChance),
                RelatedAncestorDepth = Math.Max(1, RelatedAncestorDepth)
            },
            Appearance = defaults.Appearance with
            {
                PaletteBlendInfluence = Math.Clamp((double)PaletteBlendInfluence, 0.0, 0.49)
            },
            Breeding = defaults.Breeding with
            {
                CooldownSeconds = Math.Max(0.0f, BreedCooldownSeconds)
            },
            Hatching = defaults.Hatching with
            {
                IncubationSeconds = Math.Max(0.1f, EggIncubationSeconds)
            },
            Stats = defaults.Stats with
            {
                TrainingPointsPerLevel = Math.Max(1, TrainingPointsPerLevel),
                MaxLevel = Math.Max(1, MaxStatLevel),
                MaxTrainingPoints = Math.Max(1, MaxTrainingPoints)
            },
            Lifecycle = defaults.Lifecycle with
            {
                ChildToAdultSeconds = Math.Max(0.1f, ChildToAdultSeconds)
            },
            Shop = defaults.Shop with
            {
                StoreEggPrice = Math.Max(0, StoreEggPrice),
                TrainingItemPrice = Math.Max(0, TrainingItemPrice)
            }
        };
    }

    private static double Probability(float value) => Math.Clamp((double)value, 0.0, 1.0);
}
