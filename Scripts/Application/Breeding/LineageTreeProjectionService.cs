using System;
using System.Collections.Generic;
using System.Linq;
using Voidling.Domain.Breeding;
using Voidling.Domain.Rules;
using Voidling.Domain.Stats;
using VoidlingGame;

namespace Voidling.Application.Breeding;

public enum LineageMemberPresence
{
    Owned,
    Departed,
    Archived
}

public sealed record LineageStatProjection(
    string StatId,
    int AlleleA,
    int AlleleB,
    int ExpressedAllele,
    int Level);

public sealed record LineageMemberProjection(
    string CreatureId,
    string DisplayName,
    string ParentAId,
    string ParentBId,
    int FamilyGeneration,
    string TintHex,
    bool InbreedingHistoryFlag,
    int? ActiveInbreedingBurden,
    LineageMemberPresence Presence,
    IReadOnlyList<LineageStatProjection> Stats,
    IReadOnlyList<string> RareTraitIds);

public sealed record LineageTreeProjection(
    string SelectedCreatureId,
    IReadOnlyList<LineageMemberProjection> Members);

/// <summary>
/// Builds immutable, presentation-ready lineage snapshots. Presentation never needs to traverse
/// mutable save DTOs to discover parents, departed members or archive-only ancestors.
/// </summary>
public sealed class LineageTreeProjectionService
{
    private readonly IReadOnlyList<string> _statIds;
    private readonly StatCalculator _stats;
    private readonly LineageArchiveService _archive = new();

    public LineageTreeProjectionService(GameBalanceRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        _statIds = rules.Genetics.StatIds;
        _stats = new StatCalculator(rules.Stats);
    }

    public LineageTreeProjection Create(GameStateData state, string selectedCreatureId)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (string.IsNullOrWhiteSpace(selectedCreatureId))
            return new LineageTreeProjection(string.Empty, Array.Empty<LineageMemberProjection>());

        var lineage = _archive.GetEffectiveLineage(state);
        var archiveById = lineage
            .Where(entry => !string.IsNullOrWhiteSpace(entry.CreatureId))
            .GroupBy(entry => entry.CreatureId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        if (!archiveById.ContainsKey(selectedCreatureId))
            return new LineageTreeProjection(selectedCreatureId, Array.Empty<LineageMemberProjection>());

        var activeById = state.Voidlings
            .Where(creature => !string.IsNullOrWhiteSpace(creature.Id))
            .GroupBy(creature => creature.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var departedById = state.DepartedVoidlings
            .Where(creature => !string.IsNullOrWhiteSpace(creature.Id))
            .GroupBy(creature => creature.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var childIdsByParent = BuildChildIndex(archiveById.Values);
        var connectedIds = CollectConnectedFamily(selectedCreatureId, archiveById, childIdsByParent);
        var members = connectedIds
            .Select(id => ProjectMember(id, archiveById[id], activeById, departedById))
            .OrderBy(member => member.FamilyGeneration)
            .ThenBy(member => member.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(member => member.CreatureId, StringComparer.Ordinal)
            .ToArray();

        return new LineageTreeProjection(selectedCreatureId, members);
    }

    private LineageMemberProjection ProjectMember(
        string creatureId,
        LineageArchiveEntry archive,
        IReadOnlyDictionary<string, VoidlingData> activeById,
        IReadOnlyDictionary<string, VoidlingData> departedById)
    {
        if (activeById.TryGetValue(creatureId, out var active))
            return ProjectFull(active, LineageMemberPresence.Owned);
        if (departedById.TryGetValue(creatureId, out var departed))
            return ProjectFull(departed, LineageMemberPresence.Departed);

        return new LineageMemberProjection(
            archive.CreatureId,
            string.IsNullOrWhiteSpace(archive.DisplayName) ? "Unknown" : archive.DisplayName,
            archive.ParentAId,
            archive.ParentBId,
            Math.Max(0, archive.FamilyGeneration),
            archive.TintHex,
            archive.InbreedingHistoryFlag,
            ActiveInbreedingBurden: null,
            LineageMemberPresence.Archived,
            Array.Empty<LineageStatProjection>(),
            Array.Empty<string>());
    }

    private LineageMemberProjection ProjectFull(VoidlingData creature, LineageMemberPresence presence)
    {
        var stats = _statIds.Select(statId =>
        {
            var gene = StatCalculator.GetGene(creature, statId);
            return new LineageStatProjection(
                statId,
                gene.AlleleA,
                gene.AlleleB,
                gene.ExpressedValue,
                _stats.GetLevel(creature, statId));
        }).ToArray();
        var rareTraitIds = creature.RareTraits?
            .Where(trait => !string.IsNullOrWhiteSpace(trait.TraitId))
            .Select(trait => trait.TraitId)
            .ToArray() ?? Array.Empty<string>();

        return new LineageMemberProjection(
            creature.Id,
            creature.Name,
            creature.ParentAId,
            creature.ParentBId,
            Math.Max(0, creature.FamilyGeneration),
            creature.TintHex,
            creature.InbreedingHistoryFlag,
            Math.Max(0, creature.InbreedingBurdenLevel),
            presence,
            stats,
            rareTraitIds);
    }

    private static Dictionary<string, List<string>> BuildChildIndex(IEnumerable<LineageArchiveEntry> lineage)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var member in lineage)
        {
            AddChild(member.ParentAId, member.CreatureId);
            AddChild(member.ParentBId, member.CreatureId);
        }

        return result;

        void AddChild(string parentId, string childId)
        {
            if (string.IsNullOrWhiteSpace(parentId) || string.IsNullOrWhiteSpace(childId))
                return;
            if (!result.TryGetValue(parentId, out var children))
            {
                children = new List<string>();
                result.Add(parentId, children);
            }
            if (!children.Contains(childId, StringComparer.Ordinal))
                children.Add(childId);
        }
    }

    private static HashSet<string> CollectConnectedFamily(
        string selectedId,
        IReadOnlyDictionary<string, LineageArchiveEntry> byId,
        IReadOnlyDictionary<string, List<string>> childIdsByParent)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>();
        queue.Enqueue(selectedId);

        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            if (!result.Add(id) || !byId.TryGetValue(id, out var current))
                continue;

            if (!string.IsNullOrWhiteSpace(current.ParentAId) && byId.ContainsKey(current.ParentAId))
                queue.Enqueue(current.ParentAId);
            if (!string.IsNullOrWhiteSpace(current.ParentBId) && byId.ContainsKey(current.ParentBId))
                queue.Enqueue(current.ParentBId);

            if (!childIdsByParent.TryGetValue(id, out var children))
                continue;
            foreach (var childId in children.OrderBy(value => value, StringComparer.Ordinal))
            {
                if (byId.ContainsKey(childId))
                    queue.Enqueue(childId);
            }
        }

        return result;
    }
}
