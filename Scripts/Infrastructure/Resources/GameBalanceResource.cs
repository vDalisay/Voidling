using System;
using Godot;
using Voidling.Domain.Rules;

namespace Voidling.Infrastructure.Resources;

/// <summary>
/// Godot Inspector authoring surface for balance values that are already active in the game.
/// The Resource never crosses the infrastructure boundary: Bootstrap converts it once into
/// immutable plain-C# GameBalanceRules consumed by Application and Domain.
///
/// Stable IDs, palettes, inbreeding failure tiers, and reward tables remain in the domain
/// defaults for now because they are product invariants rather than routine designer tuning.
/// Add new exported values only when a real feature needs them.
/// </summary>
[GlobalClass]
public partial class GameBalanceResource : Resource
{
    [ExportGroup("Genetics")]
    [Export(PropertyHint.Range, "0,1,0.01")]
    public float HigherAlleleExpressionChance { get; set; } = 0.70f;

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

    [Export(PropertyHint.Range, "1,3600,1")]
    public float ChildToAdultSeconds { get; set; } = 45.0f;

    [ExportGroup("Shop")]
    [Export(PropertyHint.Range, "0,10000,1")]
    public int StoreEggPrice { get; set; } = 30;

    [Export(PropertyHint.Range, "0,10000,1")]
    public int TrainingItemPrice { get; set; } = 8;

    [ExportGroup("Race - Stamina / Cheer")]
    [Export] public float RaceBaseStamina { get; set; } = 72.0f;
    [Export] public float RaceStaminaPerPoint { get; set; } = 1.05f;
    [Export] public float RaceBaseStaminaDrainPerSecond { get; set; } = 2.1f;
    [Export] public float CheerDurationSeconds { get; set; } = 2.0f;
    [Export] public float CheerCost { get; set; } = 24.0f;
    [Export] public float CheerSpeedMultiplier { get; set; } = 1.22f;

    [ExportGroup("Race - Ground / Water / Glide")]
    [Export] public float GroundBaseSpeed { get; set; } = 31.0f;
    [Export] public float GroundRunSpeedScale { get; set; } = 0.36f;
    [Export] public float SwimBaseSpeed { get; set; } = 24.0f;
    [Export] public float SwimSpeedScale { get; set; } = 0.35f;
    [Export] public float SwimExtraDrain { get; set; } = 1.1f;
    [Export] public float GlideBaseSpeed { get; set; } = 28.0f;
    [Export] public float GlideSpeedScale { get; set; } = 0.40f;
    [Export] public float GlideExtraDrain { get; set; } = 0.85f;
    [Export] public float FailedGlideSwimBaseSpeed { get; set; } = 23.0f;
    [Export] public float FailedGlideSwimSpeedScale { get; set; } = 0.33f;
    [Export] public float FailedGlideSwimExtraDrain { get; set; } = 1.25f;
    [Export] public float GlideBaseDistance { get; set; } = 82.0f;
    [Export] public float GlideDistancePerFlyPoint { get; set; } = 2.55f;

    [ExportGroup("Race - Fatigue / Obstacles")]
    [Export(PropertyHint.Range, "0,1,0.01")]
    public float LowStaminaThreshold { get; set; } = 0.18f;

    [Export] public float LowStaminaSpeedMultiplier { get; set; } = 0.90f;
    [Export] public float ExhaustedSpeedMultiplier { get; set; } = 0.84f;
    [Export(PropertyHint.Range, "0,1,0.01")]
    public float ObstacleAvoidBaseChance { get; set; } = 0.28f;
    [Export] public float ObstacleAvoidRunScale { get; set; } = 0.67f;
    [Export(PropertyHint.Range, "0,1,0.01")]
    public float ObstacleAvoidMaxChance { get; set; } = 0.95f;
    [Export] public float ObstacleBaseDelaySeconds { get; set; } = 0.62f;
    [Export] public float ObstacleLowRunDelaySeconds { get; set; } = 0.55f;
    [Export] public float ObstacleRollbackDistance { get; set; } = 5.0f;

    public GameBalanceRules ToDomainRules()
    {
        var defaults = GameBalanceRules.DemoDefaults;

        return defaults with
        {
            Genetics = defaults.Genetics with
            {
                HigherAlleleExpressionChance = Clamp01(HigherAlleleExpressionChance),
                RareFounderTraitChance = Clamp01(RareFounderTraitChance),
                RareTraitTransmissionChance = Clamp01(RareTraitTransmissionChance),
                RelatedAncestorDepth = Math.Max(1, RelatedAncestorDepth)
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
            },
            Racing = defaults.Racing with
            {
                BaseStamina = NonNegative(RaceBaseStamina),
                StaminaPerPoint = NonNegative(RaceStaminaPerPoint),
                BaseStaminaDrainPerSecond = NonNegative(RaceBaseStaminaDrainPerSecond),
                GroundBaseSpeed = NonNegative(GroundBaseSpeed),
                GroundRunSpeedScale = NonNegative(GroundRunSpeedScale),
                SwimBaseSpeed = NonNegative(SwimBaseSpeed),
                SwimSpeedScale = NonNegative(SwimSpeedScale),
                SwimExtraDrain = NonNegative(SwimExtraDrain),
                GlideBaseSpeed = NonNegative(GlideBaseSpeed),
                GlideSpeedScale = NonNegative(GlideSpeedScale),
                GlideExtraDrain = NonNegative(GlideExtraDrain),
                FailedGlideSwimBaseSpeed = NonNegative(FailedGlideSwimBaseSpeed),
                FailedGlideSwimSpeedScale = NonNegative(FailedGlideSwimSpeedScale),
                FailedGlideSwimExtraDrain = NonNegative(FailedGlideSwimExtraDrain),
                LowStaminaThreshold = Clamp01(LowStaminaThreshold),
                LowStaminaSpeedMultiplier = NonNegative(LowStaminaSpeedMultiplier),
                ExhaustedSpeedMultiplier = NonNegative(ExhaustedSpeedMultiplier),
                CheerDurationSeconds = NonNegative(CheerDurationSeconds),
                CheerCost = NonNegative(CheerCost),
                CheerSpeedMultiplier = NonNegative(CheerSpeedMultiplier),
                GlideBaseDistance = NonNegative(GlideBaseDistance),
                GlideDistancePerFlyPoint = NonNegative(GlideDistancePerFlyPoint),
                ObstacleAvoidBaseChance = Clamp01(ObstacleAvoidBaseChance),
                ObstacleAvoidRunScale = NonNegative(ObstacleAvoidRunScale),
                ObstacleAvoidMaxChance = Clamp01(ObstacleAvoidMaxChance),
                ObstacleBaseDelaySeconds = NonNegative(ObstacleBaseDelaySeconds),
                ObstacleLowRunDelaySeconds = NonNegative(ObstacleLowRunDelaySeconds),
                ObstacleRollbackDistance = NonNegative(ObstacleRollbackDistance)
            }
        };
    }

    private static float NonNegative(float value) => Math.Max(0.0f, value);
    private static double Clamp01(float value) => Math.Clamp((double)value, 0.0, 1.0);
}
