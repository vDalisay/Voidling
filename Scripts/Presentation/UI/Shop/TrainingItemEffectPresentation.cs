namespace Voidling.Presentation.UI.Shop;

/// <summary>
/// Player-facing description of the currently implemented basic training-treat roll. This is kept
/// beside the Shop presentation rather than inventing a new item/balance model. A regression test
/// executes TrainingUseCase across deterministic seeds and fails if the live gain range drifts from
/// what the Shop advertises.
/// </summary>
public static class TrainingItemEffectPresentation
{
    public const int MinimumBaseGain = 5;
    public const int MaximumBaseGain = 9;

    public static string BaseEffectText => $"+{MinimumBaseGain}-{MaximumBaseGain}";

    public static string Tooltip(string statName)
        => $"Adds {MinimumBaseGain}-{MaximumBaseGain} {statName} training points. DNA rank caps still apply; a discovered favorite food can add its bonus.";
}
