using System;
using System.Collections.Generic;
using System.Linq;

namespace Voidling.Domain.Racing;

/// <summary>
/// Stable authored identity for one recurring Cup opponent. Visible copy is referenced by
/// localization keys so names/flavor can be authored without changing durable IDs.
/// </summary>
public sealed record CupNpcDefinition(
    string Id,
    string DisplayNameKey,
    string FlavorKey);

/// <summary>
/// Authorable championship definition layered on the existing deterministic race-course catalog.
/// Economy is deliberately absent: entry fees/refunds/prizes remain an unresolved product decision.
/// </summary>
public sealed class CupDefinition
{
    public CupDefinition(
        string id,
        string displayNameKey,
        string summaryKey,
        RaceCourseDefinition course,
        IReadOnlyList<CupNpcDefinition> cast,
        string? prerequisiteCupId = null,
        bool isMajor = false,
        bool isSpecial = false)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Cup ID is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(displayNameKey)) throw new ArgumentException("Cup display-name key is required.", nameof(displayNameKey));
        if (string.IsNullOrWhiteSpace(summaryKey)) throw new ArgumentException("Cup summary key is required.", nameof(summaryKey));
        ArgumentNullException.ThrowIfNull(course);
        ArgumentNullException.ThrowIfNull(cast);
        if (cast.Count == 0) throw new ArgumentException("A Cup requires a stable NPC cast.", nameof(cast));
        if (cast.Any(npc => string.IsNullOrWhiteSpace(npc.Id) || string.IsNullOrWhiteSpace(npc.DisplayNameKey) || string.IsNullOrWhiteSpace(npc.FlavorKey)))
            throw new ArgumentException("Every Cup NPC requires a stable ID and localization keys.", nameof(cast));
        if (cast.Select(npc => npc.Id).Distinct(StringComparer.Ordinal).Count() != cast.Count)
            throw new ArgumentException("Cup NPC IDs must be unique inside a Cup.", nameof(cast));

        Id = id.Trim();
        DisplayNameKey = displayNameKey.Trim();
        SummaryKey = summaryKey.Trim();
        Course = course;
        Cast = Array.AsReadOnly(cast.ToArray());
        PrerequisiteCupId = prerequisiteCupId?.Trim() ?? string.Empty;
        IsMajor = isMajor;
        IsSpecial = isSpecial;
    }

    public string Id { get; }
    public string DisplayNameKey { get; }
    public string SummaryKey { get; }
    public RaceCourseDefinition Course { get; }
    public IReadOnlyList<CupNpcDefinition> Cast { get; }
    public string PrerequisiteCupId { get; }
    public bool IsMajor { get; }
    public bool IsSpecial { get; }
}

/// <summary>
/// Semantic Cup catalog. The first two entries intentionally reuse the already-authored race
/// courses, so championship structure does not fork race simulation or invent new obstacle rules.
/// Localization keys are placeholders for content authoring; stable IDs are the durable contract.
/// </summary>
public static class CupCatalog
{
    public static CupDefinition FirstCup { get; } = new(
        "cup-first",
        "CUP_FIRST_NAME",
        "CUP_FIRST_SUMMARY",
        RaceCourseCatalog.Demo,
        new[]
        {
            new CupNpcDefinition("cup-first-rival-1", "CUP_FIRST_RIVAL_1_NAME", "CUP_FIRST_RIVAL_1_FLAVOR"),
            new CupNpcDefinition("cup-first-rival-2", "CUP_FIRST_RIVAL_2_NAME", "CUP_FIRST_RIVAL_2_FLAVOR"),
            new CupNpcDefinition("cup-first-rival-3", "CUP_FIRST_RIVAL_3_NAME", "CUP_FIRST_RIVAL_3_FLAVOR")
        });

    public static CupDefinition LongCup { get; } = new(
        "cup-long",
        "CUP_LONG_NAME",
        "CUP_LONG_SUMMARY",
        RaceCourseCatalog.LongStandard,
        new[]
        {
            new CupNpcDefinition("cup-long-rival-1", "CUP_LONG_RIVAL_1_NAME", "CUP_LONG_RIVAL_1_FLAVOR"),
            new CupNpcDefinition("cup-long-rival-2", "CUP_LONG_RIVAL_2_NAME", "CUP_LONG_RIVAL_2_FLAVOR"),
            new CupNpcDefinition("cup-long-rival-3", "CUP_LONG_RIVAL_3_NAME", "CUP_LONG_RIVAL_3_FLAVOR")
        },
        prerequisiteCupId: FirstCup.Id,
        isMajor: true);

    private static readonly IReadOnlyList<CupDefinition> Definitions =
        Array.AsReadOnly(new[] { FirstCup, LongCup });

    public static IReadOnlyList<CupDefinition> All => Definitions;

    public static bool TryGet(string cupId, out CupDefinition definition)
    {
        var match = Definitions.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, cupId, StringComparison.Ordinal));
        if (match == null)
        {
            definition = null!;
            return false;
        }

        definition = match;
        return true;
    }
}
