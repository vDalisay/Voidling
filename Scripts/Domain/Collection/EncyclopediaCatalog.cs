using System;
using System.Collections.Generic;
using System.Linq;

namespace Voidling.Domain.Collection;

/// <summary>How an entry is unlocked: hatching, growing up into a form, or a special variant hatching.</summary>
public enum EncyclopediaDiscoveryMethod
{
    Hatched,
    Evolved,
    SpecialHatched
}

/// <summary>One journal entry: a form or a special variant, matched by its semantic visual type.</summary>
public sealed record EncyclopediaEntryDefinition(string Id, string VisualTypeId, EncyclopediaDiscoveryMethod Method);

/// <summary>
/// The journal's entries, one per form and per special variant, in the order the journal shows them.
/// Entry IDs are persisted in saves; add new entries at the end.
/// </summary>
public static class EncyclopediaCatalog
{
    public static IReadOnlyList<EncyclopediaEntryDefinition> All { get; } = Array.AsReadOnly(new[]
    {
        new EncyclopediaEntryDefinition("baby", "normal", EncyclopediaDiscoveryMethod.Hatched),
        new EncyclopediaEntryDefinition("neutral", "neutral", EncyclopediaDiscoveryMethod.Evolved),
        new EncyclopediaEntryDefinition("run", "run", EncyclopediaDiscoveryMethod.Evolved),
        new EncyclopediaEntryDefinition("swim", "water", EncyclopediaDiscoveryMethod.Evolved),
        new EncyclopediaEntryDefinition("fly", "fly", EncyclopediaDiscoveryMethod.Evolved),
        new EncyclopediaEntryDefinition("power", "power", EncyclopediaDiscoveryMethod.Evolved),
        new EncyclopediaEntryDefinition("swamp-guy", "swamp-variant", EncyclopediaDiscoveryMethod.SpecialHatched),
        new EncyclopediaEntryDefinition("rainbow", "rainbow", EncyclopediaDiscoveryMethod.SpecialHatched)
    });

    public static EncyclopediaEntryDefinition? Find(string? entryId)
        => All.FirstOrDefault(entry => string.Equals(entry.Id, entryId, StringComparison.Ordinal));

    /// <summary>The entry a Voidling with this visual type counts toward, if any.</summary>
    public static EncyclopediaEntryDefinition? ForVisualType(string? visualTypeId)
        => All.FirstOrDefault(entry => string.Equals(entry.VisualTypeId, visualTypeId, StringComparison.OrdinalIgnoreCase));
}
