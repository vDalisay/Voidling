using System;
using VoidlingGame;

namespace Voidling.Domain.Lifecycle;

/// <summary>
/// Requirement set for the long-term immortal/trophy form. The recipe itself is an unresolved
/// product decision, so every requirement is optional and a missing requirement means the gate is
/// not authored yet. Only the multi-lifecycle dimension named in the design context is represented;
/// further dimensions are added when the recipe is locked, not guessed here.
/// </summary>
public readonly record struct TrophyRequirements(int? MinimumReincarnations)
{
    /// <summary>No authored recipe: nothing qualifies. This is the shipped default.</summary>
    public static TrophyRequirements Undecided { get; } = new((int?)null);

    public bool IsAuthored => MinimumReincarnations.HasValue;
}

/// <summary>
/// Decision-neutral gate for the trophy form. Confirmed player-facing effects of the form are
/// permanent/immortal, retains its final appearance, cannot breed, can still race, and gains no
/// hidden race power beyond its actual stats. Those effects stay unwired until the recipe is
/// approved; this type only answers whether an explicitly authored requirement set is met.
/// </summary>
public static class TrophyTransformation
{
    public static bool Qualifies(VoidlingData creature, TrophyRequirements requirements)
    {
        ArgumentNullException.ThrowIfNull(creature);
        if (!requirements.IsAuthored)
            return false;

        return Math.Max(0, creature.ReincarnationCount) >= requirements.MinimumReincarnations!.Value;
    }
}
