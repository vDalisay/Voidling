using System;

namespace Voidling.Application.Breeding;

/// <summary>
/// Player-facing qualitative lineage risk. The underlying deterministic breeding rules may retain
/// exact percentages for viability resolution, but UI projections deliberately expose only bands so
/// the game does not become an exact offspring-probability calculator.
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
    LineageRiskBand LineageRisk);

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
            LineageRiskProjection.FromBurden(preview.ChildBurden));
    }
}
