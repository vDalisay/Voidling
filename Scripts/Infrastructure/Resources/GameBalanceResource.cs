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

    [ExportGroup("Favorite Food")]
    [Export(PropertyHint.Range, "0,20,1")]
    public int FavoriteFoodBonusTrainingPoints { get; set; } = 1;

    [ExportGroup("Garden")]
    [Export(PropertyHint.Range, "1,64,1")]
    public int GardenMaxPopulation { get; set; } = 8;

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

    [Export(PropertyHint.Range, "0,100,0.5")]
    public float PetHappinessGain { get; set; } = 2.0f;

    [Export(PropertyHint.Range, "0,100,0.5")]
    public float PetStressReduction { get; set; } = 4.0f;

    [Export(PropertyHint.Range, "0,100,0.5")]
    public float PetBoredomReduction { get; set; } = 5.0f;

    [Export(PropertyHint.Range, "0,100,0.5")]
    public float PetLonelinessReduction { get; set; } = 8.0f;

    [Export(PropertyHint.Range, "0,100,0.5")]
    public float ThrowHappinessLoss { get; set; } = 3.0f;

    [Export(PropertyHint.Range, "0,100,0.5")]
    public float ThrowStressGain { get; set; } = 12.0f;

    [ExportGroup("Economy")]
    [Export(PropertyHint.Range, "0,100,0.1")]
    public float GardenCoinsPerMinute { get; set; } = 1.0f;

    [ExportGroup("Daily Rewards")]
    [Export(PropertyHint.Range, "0,10000,1")]
    public int DailyLoginDay1Reward { get; set; } = 5;
    [Export(PropertyHint.Range, "0,10000,1")]
    public int DailyLoginDay2Reward { get; set; } = 7;
    [Export(PropertyHint.Range, "0,10000,1")]
    public int DailyLoginDay3Reward { get; set; } = 9;
    [Export(PropertyHint.Range, "0,10000,1")]
    public int DailyLoginDay4Reward { get; set; } = 12;
    [Export(PropertyHint.Range, "0,10000,1")]
    public int DailyLoginDay5Reward { get; set; } = 15;
    [Export(PropertyHint.Range, "0,10000,1")]
    public int DailyLoginDay6Reward { get; set; } = 20;
    [Export(PropertyHint.Range, "0,10000,1")]
    public int DailyLoginDay7Reward { get; set; } = 30;

    [Export(PropertyHint.Range, "1,6,1")]
    public int DailyMissionsPerDay { get; set; } = 3;
    [Export(PropertyHint.Range, "1,100,1")]
    public int DailyPetTarget { get; set; } = 2;
    [Export(PropertyHint.Range, "0,10000,1")]
    public int DailyPetReward { get; set; } = 8;
    [Export(PropertyHint.Range, "1,100,1")]
    public int DailyTrainTarget { get; set; } = 1;
    [Export(PropertyHint.Range, "0,10000,1")]
    public int DailyTrainReward { get; set; } = 10;
    [Export(PropertyHint.Range, "1,100,1")]
    public int DailyBreedTarget { get; set; } = 1;
    [Export(PropertyHint.Range, "0,10000,1")]
    public int DailyBreedReward { get; set; } = 12;
    [Export(PropertyHint.Range, "1,100,1")]
    public int DailyHatchTarget { get; set; } = 1;
    [Export(PropertyHint.Range, "0,10000,1")]
    public int DailyHatchReward { get; set; } = 15;
    [Export(PropertyHint.Range, "1,100,1")]
    public int DailyRaceTarget { get; set; } = 1;
    [Export(PropertyHint.Range, "0,10000,1")]
    public int DailyRaceReward { get; set; } = 12;
    [Export(PropertyHint.Range, "1,100,1")]
    public int DailyShopTarget { get; set; } = 1;
    [Export(PropertyHint.Range, "0,10000,1")]
    public int DailyShopReward { get; set; } = 8;

    [ExportGroup("Shop")]
    [Export(PropertyHint.Range, "1,12,1")]
    public int StoreEggSlotCount { get; set; } = 3;

    [Export(PropertyHint.Range, "0,10000,1")]
    public int StoreEggPrice { get; set; } = 30;

    [Export(PropertyHint.Range, "0,10000,1")]
    public int TrainingItemPrice { get; set; } = 8;

    [Export(PropertyHint.Range, "0,10000,1")]
    public int EggShellSalePrice { get; set; } = 5;

    [Export(PropertyHint.Range, "60,86400,60")]
    public float EggRotationIntervalSeconds { get; set; } = 3600.0f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float RareOfferAppearanceChance { get; set; } = 0.20f;

    [Export(PropertyHint.Range, "0,10000,1")]
    public int FullIncubationSkipPrice { get; set; } = 45;

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
            Appearance = defaults.Appearance with
            {
                PaletteBlendInfluence = ClampFinite(PaletteBlendInfluence, 0.0f, 0.49f, 0.18f)
            },
            Breeding = defaults.Breeding with
            {
                CooldownSeconds = NonNegative(BreedCooldownSeconds)
            },
            Hatching = defaults.Hatching with
            {
                IncubationSeconds = Positive(EggIncubationSeconds, 0.1f)
            },
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
            FavoriteFood = defaults.FavoriteFood with
            {
                BonusTrainingPoints = Math.Max(0, FavoriteFoodBonusTrainingPoints)
            },
            Garden = defaults.Garden with
            {
                MaxPopulation = Math.Clamp(GardenMaxPopulation, 1, 64)
            },
            GardenModules = new GardenModuleRules(
                SlotCount: Math.Clamp(GardenModuleSlotCount, 1, 12),
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
            Lifecycle = defaults.Lifecycle with
            {
                ChildToAdultSeconds = Positive(ChildToAdultSeconds, 0.1f)
            },
            Evolution = defaults.Evolution with
            {
                SpecializationThreshold = ClampFinite(EvolutionSpecializationThreshold, 0.0f, 1.0f, 0.50f)
            },
            Reincarnation = defaults.Reincarnation with
            {
                AdultLifespanSeconds = Positive(AdultLifespanSeconds, 1.0f),
                MinimumHappiness = ClampFinite(ReincarnationMinimumHappiness, 0.0f, 100.0f, 10.0f),
                MaximumStress = ClampFinite(ReincarnationMaximumStress, 0.0f, 100.0f, 70.0f),
                RetainedTrainingFraction = ClampFinite(ReincarnationRetainedTrainingFraction, 0.0f, 1.0f, 0.10f)
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
            CareInteractions = new CareInteractionRules(
                PetHappinessGain: NonNegative(PetHappinessGain),
                PetStressReduction: NonNegative(PetStressReduction),
                PetBoredomReduction: NonNegative(PetBoredomReduction),
                PetLonelinessReduction: NonNegative(PetLonelinessReduction),
                ThrowHappinessLoss: NonNegative(ThrowHappinessLoss),
                ThrowStressGain: NonNegative(ThrowStressGain)),
            Economy = defaults.Economy with
            {
                GardenCoinsPerMinute = NonNegative(GardenCoinsPerMinute)
            },
            DailyLogin = new DailyLoginRules(Array.AsReadOnly(new[]
            {
                Math.Max(0, DailyLoginDay1Reward),
                Math.Max(0, DailyLoginDay2Reward),
                Math.Max(0, DailyLoginDay3Reward),
                Math.Max(0, DailyLoginDay4Reward),
                Math.Max(0, DailyLoginDay5Reward),
                Math.Max(0, DailyLoginDay6Reward),
                Math.Max(0, DailyLoginDay7Reward)
            })),
            DailyMissions = new DailyMissionRules(
                MissionsPerDay: Math.Clamp(DailyMissionsPerDay, 1, 6),
                Definitions: Array.AsReadOnly(new[]
                {
                    new DailyMissionDefinition("pet-2", DailyMissionEventKind.PetVoidling, Math.Max(1, DailyPetTarget), Math.Max(0, DailyPetReward)),
                    new DailyMissionDefinition("train-1", DailyMissionEventKind.UseTrainingTreat, Math.Max(1, DailyTrainTarget), Math.Max(0, DailyTrainReward)),
                    new DailyMissionDefinition("breed-1", DailyMissionEventKind.BreedEgg, Math.Max(1, DailyBreedTarget), Math.Max(0, DailyBreedReward)),
                    new DailyMissionDefinition("hatch-1", DailyMissionEventKind.HatchEgg, Math.Max(1, DailyHatchTarget), Math.Max(0, DailyHatchReward)),
                    new DailyMissionDefinition("race-1", DailyMissionEventKind.CompleteStandardRace, Math.Max(1, DailyRaceTarget), Math.Max(0, DailyRaceReward)),
                    new DailyMissionDefinition("shop-1", DailyMissionEventKind.PurchaseShopItem, Math.Max(1, DailyShopTarget), Math.Max(0, DailyShopReward))
                })),
            Shop = defaults.Shop with
            {
                StoreEggPrice = Math.Max(0, StoreEggPrice),
                TrainingItemPrice = Math.Max(0, TrainingItemPrice),
                EggShellSalePrice = Math.Max(0, EggShellSalePrice),
                StoreEggSlotCount = Math.Clamp(StoreEggSlotCount, 1, 12),
                EggRotationIntervalSeconds = Positive(EggRotationIntervalSeconds, 1.0f),
                RareOfferAppearanceChance = Probability(RareOfferAppearanceChance),
                FullIncubationSkipPrice = Math.Max(0, FullIncubationSkipPrice)
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

    private static double Probability(float value)
        => float.IsFinite(value) ? Math.Clamp((double)value, 0.0, 1.0) : 0.0;

    private static float NonNegative(float value)
        => float.IsFinite(value) ? Math.Max(0.0f, value) : 0.0f;

    private static float Positive(float value, float minimum)
        => float.IsFinite(value) ? Math.Max(minimum, value) : minimum;

    private static float ClampFinite(float value, float minimum, float maximum, float fallback)
        => float.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : fallback;
}
