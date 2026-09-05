using System;

namespace Voidling.Presentation.Voidlings;

/// <summary>
/// Presentation-only mapping from current effective stats to subtle Garden roaming flavor.
/// These values never feed authoritative simulation, persistence, training, or racing.
/// </summary>
public readonly record struct VoidlingAmbientBehaviorProfile(
    float WalkSpeedMultiplier,
    float RestSecondsMin,
    float RestSecondsMax,
    float ShorelineTargetChance);

public static class VoidlingAmbientBehaviorResolver
{
    public static VoidlingAmbientBehaviorProfile Resolve(float run, float stamina)
        => Resolve(run, stamina, swim: 0.0f);

    public static VoidlingAmbientBehaviorProfile Resolve(float run, float stamina, float swim)
    {
        var runNormalized = Normalize(run);
        var staminaNormalized = Normalize(stamina);
        var swimNormalized = Normalize(swim);

        // Keep the effects intentionally modest: trained stats should be noticeable over time without
        // making low-stat Voidlings look broken or turning Garden roaming into another optimization layer.
        var speedMultiplier = Lerp(0.90f, 1.15f, runNormalized);
        var restMin = Lerp(0.28f, 0.08f, staminaNormalized);
        var restMax = Lerp(0.90f, 0.28f, staminaNormalized);

        // A high Swim stat only biases an occasional free-roam destination toward the island's edge.
        // It does not change simulation, training, or race outcomes.
        var shorelineTargetChance = Lerp(0.04f, 0.34f, swimNormalized);
        return new VoidlingAmbientBehaviorProfile(speedMultiplier, restMin, restMax, shorelineTargetChance);
    }

    private static float Normalize(float value)
        => float.IsFinite(value) ? Math.Clamp(value / 100.0f, 0.0f, 1.0f) : 0.0f;

    private static float Lerp(float from, float to, float t)
        => from + (to - from) * t;
}
