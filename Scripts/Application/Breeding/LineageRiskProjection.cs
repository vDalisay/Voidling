using System;

namespace Voidling.Application.Breeding;

/// <summary>
/// Player-facing lineage risk. The confirmed inbreeding consequence is hatch failure, so its exact
/// percentage is intentionally visible. This is not a general offspring genetics probability
/// calculator: stat/color/appearance outcome probabilities remain undisclosed.
/// </summary>
public enum LineageRiskBand
{
    None,
    Low,
    Moderate,
    High,
    Critical
}

public static class LineageRiskProjection
{
    public static LineageRiskBand FromBurden(int burdenLevel)
        => Math.Max(0, burdenLevel) switch
        {
            0 => LineageRiskBand.None,
            1 => LineageRiskBand.Low,
            2 => LineageRiskBand.Moderate,
            3 => LineageRiskBand.High,
            _ => LineageRiskBand.Critical
        };
}

public sealed record BreedingPairInfoProjection(
    bool CanBreed,
    BreedingFailure Failure,
    bool Related,
    bool IsCleanOutcross,
    int ChildBurden,
    LineageRiskBand LineageRisk,
    int HatchFailurePercent);

public sealed class BreedingPairInfoProjectionService
{
    public BreedingPairInfoProjection Create(BreedingPreview preview)
    {
        ArgumentNullException.ThrowIfNull(preview);
        return new BreedingPairInfoProjection(
            preview.CanBreed,
            preview.Failure,
            preview.Related,
            preview.IsCleanOutcross,
            Math.Max(0, preview.ChildBurden),
            LineageRiskProjection.FromBurden(preview.ChildBurden),
            Math.Clamp(preview.HatchFailurePercent, 0, 100));
    }
}
