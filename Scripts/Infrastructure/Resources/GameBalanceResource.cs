using System;
using Godot;
using Voidling.Domain.Rules;

namespace Voidling.Infrastructure.Resources;

/// <summary>
/// Godot Inspector authoring surface for balance values consumed by live gameplay.
/// Bootstrap converts this Resource once into immutable plain-C# rules.
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
    [Export(PropertyHint.Range, "0,10000,1")]
    public int RankETrainingCap { get; set; } = 20;
    [Export(PropertyHint.Range, "0,10000,1")]
    public int RankDTrainingCap { get; set; } = 40;
    [Export(PropertyHint.Range, "0,10000,1")]
    public int RankCTrainingCap { get; set; } = 60;
    [Export(PropertyHint.Range, "0,10000,1")]
    public int RankBTrainingCap { get; set; } = 80;
    [Export(PropertyHint.Range, "0,10000,1")]
    public int RankATrainingCap { get; set; } = 100;
    [Export(PropertyHint.Range, "0,10000,1")]
    public int RankSTrainingCap { get; set; } = 120;
    [Export(PropertyHint.Range, "0,60,0.1")]
    public float PassiveTrainingPointsPerMinute { get; set; } = 1.0f;
    [Export(PropertyHint.Range, "1,3600,1")]
    public float ChildToAdultSeconds { get; set; } = 45.0f;

    [ExportGroup("Garden Modules")]
    [Export(PropertyHint.Range, "1,12,1")]
    public int GardenModuleSlotCount { get; set; } = 4;
    [Export(PropertyHint.Range, "0,10000,1")]
    public int GardenModulePurchaseCost { get; set; } = 40;
    [Export(PropertyHint.Range, "0,10000,1")]
    public int GardenModuleLevel2UpgradeCost { get; set; } = 25;
    [Export(PropertyHint.Range, "0,10000,1")]
    public int GardenModuleLevel3UpgradeCost { get; set; } = 50;
    [Export(PropertyHint.Range, "0,60,0.1")]
    public float GardenModuleLevel1PointsPerMinute { get; set; } = 1.0f;
    [Export(PropertyHint.Range, "0,60,0.1")]
    public float GardenModuleLevel2PointsPerMinute { get; set; } = 1.5f;
    [Export(PropertyHint.Range, "0,60,0.1")]
    public float GardenModuleLevel3PointsPerMinute { get; set; } = 2.0f;

    [ExportGroup("Evolution")]
    [Export(PropertyHint.Range, "0,1,0.01")]
    public float EvolutionSpecializationThreshold { get; set; } = 0.50f;

    [ExportGroup("Lifecycle / Reincarnation")]
    [Export(PropertyHint.Range, "30,86400,1")]
    public float AdultLifespanSeconds { get; set; } = 900.0f;
    [Export(PropertyHint.Range, "0,100,0.5")]
    public float ReincarnationMinimumHappiness { get; set; } = 10.0f;
    [Export(PropertyHint.Range, "0,100,0.5")]
    public float ReincarnationMaximumStress { get; set; } = 70.0f;
    [Export(PropertyHint.Range, "0,1,0.01")]
    public float ReincarnationRetainedTrainingFraction { get; set; } = 0.10f;

    [ExportGroup("Care / Needs")]
    [Export(PropertyHint.Range, "0,10,0.05")]
    public float HungerGainPerMinute { get; set; } = 0.75f;
    [Export(PropertyHint.Range, "0,10,0.05")]
    public float EnergyLossPerMinute { get; set; } = 0.45f;
    [Export(PropertyHint.Range, "0,10,0.05")]
    public float FatigueGainPerMinute { get; set; } = 0.35f;
    [Export(PropertyHint.Range, "0,10,0.05")]
    public float StressRecoveryPerMinute { get; set; } = 0.20f;
    [Export(PropertyHint.Range, "0,10,0.05")]
    public float BoredomGainPerMinute { get; set; } = 0.50f;
    [Export(PropertyHint.Range, "0,10,0.05")]
    public float LonelinessGainPerMinute { get; set; } = 0.25f;
    [Export(PropertyHint.Range, "0,10,0.05")]
    public float NourishmentLossPerMinute { get; set; } = 0.40f;
    [Export(PropertyHint.Range, "0,10,0.05")]
    public float ConditionLossPerMinute { get; set; } = 0.05f;
    [Export(PropertyHint.Range, "0,10,0.05")]
    public float HappinessLossPerMinute { get; set; } = 0.10f;
    [Export(PropertyHint.Range, "0,100,0.5")]
    public float TreatHungerReduction { get; set; } = 12.0f;
    [Export(PropertyHint.Range, "0,100,0.5")]
    public float TreatEnergyGain { get; set; } = 2.0f;
    [Export(PropertyHint.Range, "0,100,0.5")]
    public float TreatNourishmentGain { get; set; } = 8.0f;
    [Export(PropertyHint.Range, "0,100,0.5")]
    public float TreatHappinessGain { get; set; } = 2.0f;

    [ExportGroup("Economy")]
    [Export(PropertyHint.Range, "0,100,0.1")]
    public float GardenCoinsPerMinute { get; set; } = 1.0f;

    [ExportGroup("Shop")]
    [Export(PropertyHint.Range, "0,10000,1")]
    public int StoreEggPrice { get; set; } = 30;
    [Export(PropertyHint.Range, "0,10000,1")]
    public int TrainingItemPrice { get; set; } = 8;
    [Export(PropertyHint.Range, "60,86400,60")]
    public float EggRotationIntervalSeconds { get; set; } = 3600.0f;

    public GameBalanceRules ToDomainRules()
    {
        var defaults = GameBalanceRules.DemoDefaults;
        var maxTrainingPoints = Math.Max(1, MaxTrainingPoints);

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
            Breeding = defaults.Breeding with { CooldownSeconds = Math.Max(0.0f, BreedCooldownSeconds) },
            Hatching = defaults.Hatching with { IncubationSeconds = Math.Max(0.1f, EggIncubationSeconds) },
            Stats = defaults.Stats with
            {
                TrainingPointsPerLevel = Math.Max(1, TrainingPointsPerLevel),
                MaxLevel = Math.Max(1, MaxStatLevel),
                MaxTrainingPoints = maxTrainingPoints,
                RankCaps = BuildRankCaps(maxTrainingPoints)
            },
            PassiveTraining = defaults.PassiveTraining with
            {
                PointsPerMinute = NonNegative(PassiveTrainingPointsPerMinute)
            },
            GardenModules = new GardenModuleRules(
                SlotCount: Math.Max(1, GardenModuleSlotCount),
                PurchaseCost: Math.Max(0, GardenModulePurchaseCost),
                UpgradeCosts: Array.AsReadOnly(new[]
                {
                    Math.Max(0, GardenModuleLevel2UpgradeCost),
                    Math.Max(0, GardenModuleLevel3UpgradeCost)
                }),
                PointsPerMinuteByLevel: Array.AsReadOnly(new[]
                {
                    NonNegative(GardenModuleLevel1PointsPerMinute),
                    NonNegative(GardenModuleLevel2PointsPerMinute),
                    NonNegative(GardenModuleLevel3PointsPerMinute)
                })),
            Lifecycle = defaults.Lifecycle with { ChildToAdultSeconds = Math.Max(0.1f, ChildToAdultSeconds) },
            Evolution = defaults.Evolution with
            {
                SpecializationThreshold = Math.Clamp(EvolutionSpecializationThreshold, 0.0f, 1.0f)
            },
            Reincarnation = defaults.Reincarnation with
            {
                AdultLifespanSeconds = Math.Max(1.0f, AdultLifespanSeconds),
                MinimumHappiness = Math.Clamp(ReincarnationMinimumHappiness, 0.0f, 100.0f),
                MaximumStress = Math.Clamp(ReincarnationMaximumStress, 0.0f, 100.0f),
                RetainedTrainingFraction = Math.Clamp(ReincarnationRetainedTrainingFraction, 0.0f, 1.0f)
            },
            Needs = defaults.Needs with
            {
                HungerGainPerMinute = NonNegative(HungerGainPerMinute),
                EnergyLossPerMinute = NonNegative(EnergyLossPerMinute),
                FatigueGainPerMinute = NonNegative(FatigueGainPerMinute),
                StressRecoveryPerMinute = NonNegative(StressRecoveryPerMinute),
                BoredomGainPerMinute = NonNegative(BoredomGainPerMinute),
                LonelinessGainPerMinute = NonNegative(LonelinessGainPerMinute),
                NourishmentLossPerMinute = NonNegative(NourishmentLossPerMinute),
                ConditionLossPerMinute = NonNegative(ConditionLossPerMinute),
                HappinessLossPerMinute = NonNegative(HappinessLossPerMinute),
                TreatHungerReduction = NonNegative(TreatHungerReduction),
                TreatEnergyGain = NonNegative(TreatEnergyGain),
                TreatNourishmentGain = NonNegative(TreatNourishmentGain),
                TreatHappinessGain = NonNegative(TreatHappinessGain)
            },
            Economy = defaults.Economy with { GardenCoinsPerMinute = NonNegative(GardenCoinsPerMinute) },
            Shop = defaults.Shop with
            {
                StoreEggPrice = Math.Max(0, StoreEggPrice),
                TrainingItemPrice = Math.Max(0, TrainingItemPrice),
                EggRotationIntervalSeconds = Math.Max(1.0f, EggRotationIntervalSeconds)
            }
        };
    }

    private RankTrainingCaps BuildRankCaps(int maxTrainingPoints)
    {
        var e = Math.Clamp(RankETrainingCap, 0, maxTrainingPoints);
        var d = Math.Clamp(Math.Max(e, RankDTrainingCap), 0, maxTrainingPoints);
        var c = Math.Clamp(Math.Max(d, RankCTrainingCap), 0, maxTrainingPoints);
        var b = Math.Clamp(Math.Max(c, RankBTrainingCap), 0, maxTrainingPoints);
        var a = Math.Clamp(Math.Max(b, RankATrainingCap), 0, maxTrainingPoints);
        var s = Math.Clamp(Math.Max(a, RankSTrainingCap), 0, maxTrainingPoints);
        return new RankTrainingCaps(e, d, c, b, a, s);
    }

    private static double Probability(float value) => Math.Clamp((double)value, 0.0, 1.0);
    private static float NonNegative(float value) => float.IsFinite(value) ? Math.Max(0.0f, value) : 0.0f;
}
