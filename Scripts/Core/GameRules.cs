using System;
using System.Collections.Generic;
using Godot;

namespace VoidlingGame;

public static class GameRules
{
    public const int StoreEggPrice = 30;
    public const int TrainingItemPrice = 8;
    public const float EggIncubationSeconds = 22.0f;
    public const float ChildToAdultSeconds = 45.0f;
    public const float BreedCooldownSeconds = 8.0f;
    public const double HigherAlleleExpressionChance = 0.70;
    public const double RareFounderTraitChance = 0.0005;
    public const double RareTraitTransmissionChance = 0.50;
    public const int RelatedAncestorDepth = 3;
    public const int TrainingPointsPerLevel = 12;
    public const int MaxStatLevel = 99;
    public const string AngelMutationId = "Angel";

    public static readonly string[] StatIds = { "run", "swim", "fly", "power", "stamina" };

    public static readonly IReadOnlyDictionary<string, string> StatDisplayNames =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["run"] = "Run",
            ["swim"] = "Swim",
            ["fly"] = "Fly",
            ["power"] = "Power",
            ["stamina"] = "Stamina"
        };

    // Chao-inspired stat identity colors used consistently in the farm UI, DNA view and race HUD.
    public static readonly IReadOnlyDictionary<string, Color> StatColors =
        new Dictionary<string, Color>(StringComparer.Ordinal)
        {
            ["run"] = Color.FromHtml("#78C96A"),
            ["swim"] = Color.FromHtml("#F2D45C"),
            ["fly"] = Color.FromHtml("#B47AE5"),
            ["power"] = Color.FromHtml("#E7655A"),
            ["stamina"] = Color.FromHtml("#F7F3E7")
        };

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

    public static string GradeName(int grade) => Math.Clamp(grade, 0, 5) switch
    {
        0 => "E",
        1 => "D",
        2 => "C",
        3 => "B",
        4 => "A",
        _ => "S"
    };

    public static int HatchFailurePercent(int burdenLevel) => Math.Clamp(burdenLevel, 0, 4) switch
    {
        0 => 0,
        1 => 20,
        2 => 50,
        3 => 80,
        _ => 100
    };

    public static int GetTrainingPoints(VoidlingData data, string statId)
        => data.TrainingPoints.TryGetValue(statId, out var points) ? points : 0;

    public static int StatLevel(VoidlingData data, string statId)
        => Math.Clamp(1 + GetTrainingPoints(data, statId) / TrainingPointsPerLevel, 1, MaxStatLevel);

    public static float StatLevelProgress(VoidlingData data, string statId)
    {
        if (StatLevel(data, statId) >= MaxStatLevel)
            return 1.0f;
        return (GetTrainingPoints(data, statId) % TrainingPointsPerLevel) / (float)TrainingPointsPerLevel;
    }

    public static GenePairData GetGene(VoidlingData data, string statId)
        => data.Genome.AbilityGenes.TryGetValue(statId, out var gene) ? gene : new GenePairData();

    public static float EffectiveStat(VoidlingData data, string statId)
    {
        var grade = GetGene(data, statId).ExpressedValue;
        var training = GetTrainingPoints(data, statId);
        return Math.Clamp(12.0f + grade * 13.0f + training * 0.55f, 0.0f, 100.0f);
    }

    public static Color StatColor(string statId)
        => StatColors.TryGetValue(statId, out var color) ? color : Colors.White;

    public static bool HasMutation(VoidlingData data, string mutationId)
        => data.RareTraits.Exists(t => string.Equals(t.TraitId, mutationId, StringComparison.OrdinalIgnoreCase));

    public static Color TintColor(string html)
        => string.IsNullOrWhiteSpace(html) ? Colors.White : Color.FromHtml(html);
}
