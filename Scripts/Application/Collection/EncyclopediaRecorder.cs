using System;
using System.Collections.Generic;
using System.Linq;
using Voidling.Domain.Collection;
using VoidlingGame;

namespace Voidling.Application.Collection;

public sealed record EncyclopediaEntryProjection(
    string EntryId,
    string VisualTypeId,
    EncyclopediaDiscoveryMethod Method,
    bool Discovered,
    int Order,
    string FirstDiscoveredBy,
    float PaletteHue);

public sealed record EncyclopediaProjection(IReadOnlyList<EncyclopediaEntryProjection> Entries)
{
    public int DiscoveredCount => Entries.Count(entry => entry.Discovered);
    public int Total => Entries.Count;
}

/// <summary>
/// Fills the journal. Hatching, growing up and a special variant hatching unlock the entry for the
/// Voidling's visual type the first time it happens; trades and Voidlings a save already had do not.
/// </summary>
public static class EncyclopediaRecorder
{
    /// <summary>Records the entry for this Voidling's current look if it is new; returns the new entry's ID.</summary>
    public static string? Discover(GameStateData state, VoidlingData creature)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(creature);
        var entry = EncyclopediaCatalog.ForVisualType(creature.Appearance?.VisualTypeId);
        if (entry == null || state.Encyclopedia.Any(record => string.Equals(record.EntryId, entry.Id, StringComparison.Ordinal)))
            return null;

        state.Encyclopedia.Add(new EncyclopediaDiscoveryData
        {
            EntryId = entry.Id,
            Order = state.Encyclopedia.Count + 1,
            CreatureName = creature.Name ?? string.Empty,
            PaletteHue = creature.Appearance?.PaletteHue ?? -1.0f
        });
        return entry.Id;
    }

    public static EncyclopediaProjection Project(GameStateData state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var records = state.Encyclopedia
            .GroupBy(record => record.EntryId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        return new EncyclopediaProjection(EncyclopediaCatalog.All
            .Select(entry => records.TryGetValue(entry.Id, out var record)
                ? new EncyclopediaEntryProjection(entry.Id, entry.VisualTypeId, entry.Method, true, record.Order, record.CreatureName, record.PaletteHue)
                : new EncyclopediaEntryProjection(entry.Id, entry.VisualTypeId, entry.Method, false, 0, string.Empty, -1.0f))
            .ToArray());
    }
}
