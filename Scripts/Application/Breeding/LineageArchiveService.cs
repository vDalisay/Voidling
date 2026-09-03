using System;
using System.Collections.Generic;
using System.Linq;
using Voidling.Domain.Breeding;
using VoidlingGame;

namespace Voidling.Application.Breeding;

/// <summary>
/// Owns the persistent minimal ancestry graph used by breeding and multiplayer transfer.
/// Full owned/departed creature objects remain the strongest local source; imported archive-only
/// ancestors are retained so relatedness and historical appearance survive ownership transfer.
/// </summary>
public sealed class LineageArchiveService
{
    private const int MaxAppearanceLayers = 16;
    private const int MaxAppearanceLayerIdLength = 128;

    public void EnsureCurrentEntries(GameStateData state)
    {
        ArgumentNullException.ThrowIfNull(state);
        state.LineageArchive ??= new List<LineageArchiveEntry>();

        var byId = new Dictionary<string, LineageArchiveEntry>(StringComparer.Ordinal);
        foreach (var entry in state.LineageArchive)
        {
            if (!IsValid(entry) || byId.ContainsKey(entry.CreatureId))
                continue;
            byId.Add(entry.CreatureId, NormalizeAppearance(entry));
        }

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

    public bool CanMerge(
        GameStateData state,
        IEnumerable<LineageArchiveEntry> incomingEntries,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(incomingEntries);
        EnsureCurrentEntries(state);
        return TryBuildMerged(state.LineageArchive, incomingEntries, out _, out error);
    }

    public bool TryMerge(
        GameStateData state,
        IEnumerable<LineageArchiveEntry> incomingEntries,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(incomingEntries);
        EnsureCurrentEntries(state);

        if (!TryBuildMerged(state.LineageArchive, incomingEntries, out var merged, out error))
            return false;

        state.LineageArchive = merged!;
        return true;
    }

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

    private static bool TryBuildMerged(
        IEnumerable<LineageArchiveEntry> existingEntries,
        IEnumerable<LineageArchiveEntry> incomingEntries,
        out List<LineageArchiveEntry>? merged,
        out string? error)
    {
        merged = null;
        error = null;

        var staged = new Dictionary<string, LineageArchiveEntry>(StringComparer.Ordinal);
        foreach (var existing in existingEntries)
        {
            if (IsValid(existing) && !staged.ContainsKey(existing.CreatureId))
                staged.Add(existing.CreatureId, NormalizeAppearance(existing));
        }

        foreach (var incomingRaw in incomingEntries)
        {
            if (!IsValid(incomingRaw))
            {
                error = "Incoming lineage contains an invalid entry.";
                return false;
            }

            var incoming = NormalizeAppearance(incomingRaw);
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

        merged = staged.Values
            .OrderBy(entry => entry.CreatureId, StringComparer.Ordinal)
            .ToList();
        return true;
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
        var visualTypeId = string.IsNullOrWhiteSpace(incoming.VisualTypeId)
            ? existing.VisualTypeId
            : incoming.VisualTypeId;
        var paletteHue = VoidlingAppearanceData.IsValidHue(incoming.PaletteHue)
            ? incoming.PaletteHue
            : existing.PaletteHue;
        var layerIdsKey = string.IsNullOrWhiteSpace(incoming.LayerIdsKey)
            ? existing.LayerIdsKey
            : incoming.LayerIdsKey;

        return existing with
        {
            DisplayName = displayName,
            TintHex = tint,
            InbreedingHistoryFlag = existing.InbreedingHistoryFlag || incoming.InbreedingHistoryFlag,
            VisualTypeId = visualTypeId,
            PaletteHue = paletteHue,
            LayerIdsKey = layerIdsKey
        };
    }

    private static LineageArchiveEntry NormalizeAppearance(LineageArchiveEntry entry)
    {
        var visualTypeId = string.IsNullOrWhiteSpace(entry.VisualTypeId)
            ? VoidlingAppearanceData.DefaultVisualTypeId
            : entry.VisualTypeId.Trim().ToLowerInvariant();
        var paletteHue = VoidlingAppearanceData.IsValidHue(entry.PaletteHue)
            ? VoidlingAppearanceData.NormalizeHue(entry.PaletteHue)
            : VoidlingAppearanceData.LegacyUninitializedPaletteHue;
        return entry with
        {
            VisualTypeId = visualTypeId,
            PaletteHue = paletteHue,
            LayerIdsKey = VoidlingAppearanceData.BuildLayerIdsKey(entry.LayerIds)
        };
    }

    private static bool IsValid(LineageArchiveEntry? entry)
    {
        if (entry == null ||
            string.IsNullOrWhiteSpace(entry.CreatureId) ||
            entry.CreatureId.Length > 128 ||
            entry.FamilyGeneration < 0 ||
            (!string.IsNullOrEmpty(entry.ParentAId) && entry.ParentAId.Length > 128) ||
            (!string.IsNullOrEmpty(entry.ParentBId) && entry.ParentBId.Length > 128) ||
            (!string.IsNullOrEmpty(entry.DisplayName) && entry.DisplayName.Length > 64) ||
            (!string.IsNullOrEmpty(entry.TintHex) && entry.TintHex.Length > 16) ||
            (!string.IsNullOrEmpty(entry.VisualTypeId) && entry.VisualTypeId.Length > 64) ||
            (!string.IsNullOrEmpty(entry.LayerIdsKey) && entry.LayerIdsKey.Length > 1024) ||
            !VoidlingAppearanceData.IsValidStoredHue(entry.PaletteHue))
        {
            return false;
        }

        var layerIds = entry.LayerIds;
        return layerIds.Length <= MaxAppearanceLayers &&
               layerIds.All(id => !string.IsNullOrWhiteSpace(id) && id.Length <= MaxAppearanceLayerIdLength);
    }
}
