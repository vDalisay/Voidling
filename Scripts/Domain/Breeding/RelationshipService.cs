using System;
using System.Collections.Generic;
using System.Linq;
using VoidlingGame;

namespace Voidling.Domain.Breeding;

public sealed class RelationshipService
{
    private readonly int _maxAncestorDepth;

    public RelationshipService(int maxAncestorDepth)
    {
        if (maxAncestorDepth < 1)
            throw new ArgumentOutOfRangeException(nameof(maxAncestorDepth));
        _maxAncestorDepth = maxAncestorDepth;
    }

    /// <summary>
    /// Compatibility overload for legacy callers/tests. New application code should pass the
    /// persistent lineage archive so ancestry can survive departure and multiplayer transfers.
    /// </summary>
    public bool AreRelated(VoidlingData first, VoidlingData second, IReadOnlyList<VoidlingData> population)
    {
        ArgumentNullException.ThrowIfNull(population);
        var lineage = population.Select(LineageArchiveEntry.FromVoidling).ToArray();
        return AreRelated(first, second, lineage);
    }

    public bool AreRelated(
        VoidlingData first,
        VoidlingData second,
        IReadOnlyList<LineageArchiveEntry> lineage)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        ArgumentNullException.ThrowIfNull(lineage);

        if (first.Id == second.Id)
            return true;

        var byId = lineage
            .Where(entry => !string.IsNullOrWhiteSpace(entry.CreatureId))
            .GroupBy(entry => entry.CreatureId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var firstAncestors = GetAncestors(first.ParentAId, first.ParentBId, byId);
        var secondAncestors = GetAncestors(second.ParentAId, second.ParentBId, byId);

        if (firstAncestors.Contains(second.Id) || secondAncestors.Contains(first.Id))
            return true;

        return firstAncestors.Overlaps(secondAncestors);
    }

    private HashSet<string> GetAncestors(
        string parentAId,
        string parentBId,
        IReadOnlyDictionary<string, LineageArchiveEntry> byId)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        var frontier = new Queue<(string Id, int Depth)>();

        if (!string.IsNullOrWhiteSpace(parentAId))
            frontier.Enqueue((parentAId, 1));
        if (!string.IsNullOrWhiteSpace(parentBId))
            frontier.Enqueue((parentBId, 1));

        while (frontier.Count > 0)
        {
            var (id, depth) = frontier.Dequeue();
            if (depth > _maxAncestorDepth || string.IsNullOrWhiteSpace(id) || !result.Add(id))
                continue;

            if (!byId.TryGetValue(id, out var ancestor))
                continue;

            if (!string.IsNullOrWhiteSpace(ancestor.ParentAId))
                frontier.Enqueue((ancestor.ParentAId, depth + 1));
            if (!string.IsNullOrWhiteSpace(ancestor.ParentBId))
                frontier.Enqueue((ancestor.ParentBId, depth + 1));
        }

        return result;
    }
}
