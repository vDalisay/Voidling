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

    public bool AreRelated(VoidlingData first, VoidlingData second, IReadOnlyList<VoidlingData> population)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        ArgumentNullException.ThrowIfNull(population);

        if (first.Id == second.Id)
            return true;

        var byId = population.ToDictionary(v => v.Id, StringComparer.Ordinal);
        var firstAncestors = GetAncestors(first, byId);
        var secondAncestors = GetAncestors(second, byId);

        if (firstAncestors.Contains(second.Id) || secondAncestors.Contains(first.Id))
            return true;

        return firstAncestors.Overlaps(secondAncestors);
    }

    private HashSet<string> GetAncestors(VoidlingData creature, IReadOnlyDictionary<string, VoidlingData> byId)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        var frontier = new Queue<(string Id, int Depth)>();

        if (!string.IsNullOrWhiteSpace(creature.ParentAId))
            frontier.Enqueue((creature.ParentAId, 1));
        if (!string.IsNullOrWhiteSpace(creature.ParentBId))
            frontier.Enqueue((creature.ParentBId, 1));

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
