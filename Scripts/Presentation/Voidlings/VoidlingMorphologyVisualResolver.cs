using System;
using Voidling.Domain.Evolution;

namespace Voidling.Presentation.Voidlings;

/// <summary>
/// Decision-neutral bridge between an already-resolved evolution specialization and presentation.
/// The mapping is supplied explicitly by the caller/configuration layer. Blank entries preserve the
/// creature's existing semantic visual type so unresolved morphology rules cannot leak into runtime
/// behavior through fallback guesses.
/// </summary>
public static class VoidlingMorphologyVisualResolver
{
    public static string Resolve(
        EvolutionSpecialization specialization,
        string? fallbackVisualTypeId,
        string? defaultVisualTypeId,
        VoidlingMorphologyVisualMapping mapping)
    {
        var configured = specialization switch
        {
            EvolutionSpecialization.Generalist => mapping.GeneralistVisualTypeId,
            EvolutionSpecialization.Run => mapping.RunVisualTypeId,
            EvolutionSpecialization.Swim => mapping.SwimVisualTypeId,
            EvolutionSpecialization.Fly => mapping.FlyVisualTypeId,
            EvolutionSpecialization.Power => mapping.PowerVisualTypeId,
            _ => string.Empty
        };

        if (!string.IsNullOrWhiteSpace(configured))
            return Normalize(configured);

        if (!string.IsNullOrWhiteSpace(fallbackVisualTypeId))
            return Normalize(fallbackVisualTypeId);

        return string.IsNullOrWhiteSpace(defaultVisualTypeId)
            ? "normal"
            : Normalize(defaultVisualTypeId);
    }

    private static string Normalize(string value)
        => value.Trim().ToLowerInvariant();
}

public readonly record struct VoidlingMorphologyVisualMapping(
    string GeneralistVisualTypeId,
    string RunVisualTypeId,
    string SwimVisualTypeId,
    string FlyVisualTypeId,
    string PowerVisualTypeId)
{
    public static VoidlingMorphologyVisualMapping Empty { get; } = new(
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty);
}
