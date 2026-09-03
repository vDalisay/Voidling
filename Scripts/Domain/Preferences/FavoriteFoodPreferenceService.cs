using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VoidlingGame;

namespace Voidling.Domain.Preferences;

/// <summary>
/// Stable per-creature food preference. The preference is derived from durable creature identity,
/// so discovering it never consumes a gameplay RNG stream and cannot perturb breeding or racing.
/// Candidate order is authored gameplay data and therefore part of the preference contract.
/// </summary>
public sealed class FavoriteFoodPreferenceService
{
    public string Resolve(string creatureId, IReadOnlyList<string> candidateFoodIds)
    {
        ArgumentNullException.ThrowIfNull(candidateFoodIds);
        if (string.IsNullOrWhiteSpace(creatureId))
            return string.Empty;

        var candidates = candidateFoodIds
            .Where(static id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (candidates.Length == 0)
            return string.Empty;

        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        var hash = offset;
        foreach (var value in Encoding.UTF8.GetBytes(creatureId.Trim()))
        {
            hash ^= value;
            hash *= prime;
        }

        return candidates[(int)(hash % (ulong)candidates.Length)];
    }

    /// <summary>
    /// Repairs missing/invalid persisted preference data without revealing an undiscovered food.
    /// Returns true only when persisted preference state changed.
    /// </summary>
    public bool Normalize(VoidlingData creature, IReadOnlyList<string> candidateFoodIds)
    {
        ArgumentNullException.ThrowIfNull(creature);
        ArgumentNullException.ThrowIfNull(candidateFoodIds);

        var current = creature.FavoriteFoodId ?? string.Empty;
        if (candidateFoodIds.Contains(current, StringComparer.Ordinal))
            return false;

        creature.FavoriteFoodId = Resolve(creature.Id, candidateFoodIds);
        creature.FavoriteFoodDiscovered = false;
        return true;
    }
}
