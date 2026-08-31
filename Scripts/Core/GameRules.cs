using System;
using System.Collections.Generic;
using Godot;
using Voidling.Domain.Genetics;
using Voidling.Domain.Rules;
using Voidling.Domain.Stats;

namespace VoidlingGame;

/// <summary>
/// Legacy compatibility facade. Gameplay formulas are moving into typed Domain rules/services.
/// Bootstrap configures this facade with the same immutable rules used by Application so legacy
/// callers cannot drift from designer-authored balance during the incremental migration.
/// Presentation labels/colors have moved to Presentation catalogs and must not be added here.
/// </summary>
public static class GameRules
{
    public const string AngelMutationId = MutationIds.Angel;

    private static GameBalanceRules _balance = GameBalanceRules.DemoDefaults;
    private static StatCalculator _stats = new(_balance.Stats);

    public static int StoreEggPrice => _balance.Shop.StoreEggPrice;
    public static int TrainingItemPrice => _balance.Shop.TrainingItemPrice;
    public static int EggShellSalePrice => _balance.Shop.EggShellSalePrice;
    public static float ShopEggRotationIntervalSeconds => Math.Max(1.0f, _balance.Shop.EggRotationIntervalSeconds);
    public static float EggIncubationSeconds => _balance.Hatching.IncubationSeconds;
    public static float ChildToAdultSeconds => _balance.Lifecycle.ChildToAdultSeconds;
    public static float BreedCooldownSeconds => _balance.Breeding.CooldownSeconds;
    public static double HigherAlleleExpressionChance => _balance.Genetics.HigherAlleleExpressionChance;
    public static double RareFounderTraitChance => _balance.Genetics.RareFounderTraitChance;
    public static double RareTraitTransmissionChance => _balance.Genetics.RareTraitTransmissionChance;
    public static int RelatedAncestorDepth => _balance.Genetics.RelatedAncestorDepth;
    public static int TrainingPointsPerLevel => _balance.Stats.TrainingPointsPerLevel;
    public static int MaxStatLevel => _balance.Stats.MaxLevel;
    public static int GardenMaxPopulation => Math.Max(1, _balance.Garden.MaxPopulation);
    public static GardenModuleRules GardenModuleRules => _balance.GardenModules;
    public static IReadOnlyList<int> DailyLoginCoinRewards => _balance.DailyLogin.CoinRewards;
    public static DailyMissionRules DailyMissionRules => _balance.DailyMissions;

    public static readonly string[] StatIds = { "run", "swim", "fly", "power", "stamina" };

    public static readonly string[] PaletteHex =
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
    };

    public static readonly string[] RareTraitIds = { "Lustrous", "Prismatic", "Aurora" };

    public static void Configure(GameBalanceRules balance)
    {
        _balance = balance ?? throw new ArgumentNullException(nameof(balance));
        _stats = new StatCalculator(_balance.Stats);
    }

    public static string GradeName(int grade) => Math.Clamp(grade, 0, 5) switch
    {
        0 => "E",
        1 => "D",
        2 => "C",
        3 => "B",
        4 => "A",
        _ => "S"
    };

    public static int HatchFailurePercent(int burdenLevel)
    {
        var values = _balance.Breeding.HatchFailurePercentByBurden;
        if (values.Count == 0)
            return 0;
        return values[Math.Clamp(burdenLevel, 0, values.Count - 1)];
    }

    public static int GetTrainingPoints(VoidlingData data, string statId)
        => _stats.GetTrainingPoints(data, statId);

    public static int StatLevel(VoidlingData data, string statId)
        => _stats.GetLevel(data, statId);

    public static float StatLevelProgress(VoidlingData data, string statId)
        => _stats.GetLevelProgress(data, statId);

    public static GenePairData GetGene(VoidlingData data, string statId)
        => StatCalculator.GetGene(data, statId);

    public static float EffectiveStat(VoidlingData data, string statId)
        => _stats.GetEffectiveStat(data, statId);

    public static bool HasMutation(VoidlingData data, string mutationId)
        => data.RareTraits.Exists(t => string.Equals(t.TraitId, mutationId, StringComparison.OrdinalIgnoreCase));

    public static Color TintColor(string html)
        => string.IsNullOrWhiteSpace(html) ? Colors.White : Color.FromHtml(html);
}
