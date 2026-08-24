using System;
using System.Collections.Generic;
using System.Linq;
using Voidling.Domain.Breeding;
using VoidlingGame;

namespace Voidling.Application.Breeding;

/// <summary>
/// Owns the persistent minimal ancestry graph used by breeding and multiplayer transfer.
/// Full owned/departed creature objects remain the strongest local source; imported archive-only
/// ancestors are retained so relatedness survives ownership transfer without retaining runtime data.
/// </summary>
public sealed class LineageArchiveService
{
    public void EnsureCurrentEntries(GameStateData state)
    {
        ArgumentNullException.ThrowIfNull(state);
        state.LineageArchive ??= new List<LineageArchiveEntry>();

        var byId = new Dictionary<string, LineageArchiveEntry>(StringComparer.Ordinal);
        foreach (var entry in state.LineageArchive)
        {
            if (!IsValid(entry) || byId.ContainsKey(entry.CreatureId))
                continue;
            byId.Add(entry.CreatureId, entry);
        }

        // Full local creature records are authoritative for their own lineage identity. This also
        // deterministically repairs a stale archive entry instead of making an old save unloadable.
        foreach (var creature in state.Voidlings.Concat(state.DepartedVoidlings))
        {
            if (string.IsNullOrWhiteSpace(creature.Id))
                continue;
            byId[creature.Id] = LineageArchiveEntry.FromVoidling(creature);
        }

        state.LineageArchive = byId.Values
            .OrderBy(entry => entry.CreatureId, StringComparer.Ordinal)
            .ToList();
    }

    public IReadOnlyList<LineageArchiveEntry> GetEffectiveLineage(GameStateData state)
    {
        EnsureCurrentEntries(state);
        return state.LineageArchive.ToArray();
    }

    /// <summary>
    /// Merges lineage received from another player. Conflicting ancestry identity is rejected;
    /// cosmetic/biographical metadata may refresh when the stable ancestry identity matches.
    /// </summary>
    public bool TryMerge(
        GameStateData state,
        IEnumerable<LineageArchiveEntry> incomingEntries,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(incomingEntries);
        EnsureCurrentEntries(state);
        error = null;

        var current = state.LineageArchive.ToDictionary(
            entry => entry.CreatureId,
            entry => entry,
            StringComparer.Ordinal);

        var staged = new Dictionary<string, LineageArchiveEntry>(current, StringComparer.Ordinal);
        foreach (var incoming in incomingEntries)
        {
            if (!IsValid(incoming))
            {
                error = "Incoming lineage contains an invalid entry.";
                return false;
            }

            if (staged.TryGetValue(incoming.CreatureId, out var existing))
            {
                if (!existing.HasSameLineageIdentity(incoming))
                {
                    error = $"Lineage conflict for creature '{incoming.CreatureId}'.";
                    return false;
                }

                staged[incoming.CreatureId] = MergeEquivalent(existing, incoming);
                continue;
            }

            staged.Add(incoming.CreatureId, incoming);
        }

        state.LineageArchive = staged.Values
            .OrderBy(entry => entry.CreatureId, StringComparer.Ordinal)
            .ToList();
        return true;
    }

    /// <summary>
    /// Returns the requested roots plus their archived ancestors, bounded by the same ancestry
    /// depth used for relatedness. The result is deterministic and suitable for a trade package.
    /// </summary>
    public IReadOnlyList<LineageArchiveEntry> GetAncestryClosure(
        GameStateData state,
        IEnumerable<string> rootCreatureIds,
        int maxAncestorDepth)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(rootCreatureIds);
        if (maxAncestorDepth < 0)
            throw new ArgumentOutOfRangeException(nameof(maxAncestorDepth));

        EnsureCurrentEntries(state);
        var byId = state.LineageArchive.ToDictionary(
            entry => entry.CreatureId,
            entry => entry,
            StringComparer.Ordinal);
        var selected = new Dictionary<string, LineageArchiveEntry>(StringComparer.Ordinal);
        var frontier = new Queue<(string Id, int AncestorDepth)>();

        foreach (var rootId in rootCreatureIds)
        {
            if (!string.IsNullOrWhiteSpace(rootId))
                frontier.Enqueue((rootId, 0));
        }

        while (frontier.Count > 0)
        {
            var (id, depth) = frontier.Dequeue();
            if (string.IsNullOrWhiteSpace(id) || selected.ContainsKey(id) || !byId.TryGetValue(id, out var entry))
                continue;

            selected.Add(id, entry);
            if (depth >= maxAncestorDepth)
                continue;

            if (!string.IsNullOrWhiteSpace(entry.ParentAId))
                frontier.Enqueue((entry.ParentAId, depth + 1));
            if (!string.IsNullOrWhiteSpace(entry.ParentBId))
                frontier.Enqueue((entry.ParentBId, depth + 1));
        }

        return selected.Values
            .OrderBy(entry => entry.CreatureId, StringComparer.Ordinal)
            .ToArray();
    }

    private static LineageArchiveEntry MergeEquivalent(
        LineageArchiveEntry existing,
        LineageArchiveEntry incoming)
    {
        var displayName = string.IsNullOrWhiteSpace(incoming.DisplayName)
            ? existing.DisplayName
            : incoming.DisplayName;
        var tint = string.IsNullOrWhiteSpace(incoming.TintHex)
            ? existing.TintHex
            : incoming.TintHex;

        return existing with
        {
            DisplayName = displayName,
            TintHex = tint,
            InbreedingHistoryFlag = existing.InbreedingHistoryFlag || incoming.InbreedingHistoryFlag
        };
    }

    private static bool IsValid(LineageArchiveEntry? entry)
        => entry != null &&
           !string.IsNullOrWhiteSpace(entry.CreatureId) &&
           entry.CreatureId.Length <= 128 &&
           entry.FamilyGeneration >= 0 &&
           (string.IsNullOrEmpty(entry.ParentAId) || entry.ParentAId.Length <= 128) &&
           (string.IsNullOrEmpty(entry.ParentBId) || entry.ParentBId.Length <= 128) &&
           (string.IsNullOrEmpty(entry.DisplayName) || entry.DisplayName.Length <= 64) &&
           (string.IsNullOrEmpty(entry.TintHex) || entry.TintHex.Length <= 16);
}
