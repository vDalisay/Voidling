using System;
using System.Collections.Generic;
using Voidling.Domain.Racing;

namespace Voidling.Presentation.UI.Common;

/// <summary>
/// Presentation identity for authored race courses and the sections they contain. Courses, segment
/// kinds and terrains stay domain-owned; this catalog only maps them to localization keys.
///
/// Every mapping is an exhaustive switch over a stable ID or enum so a new course or segment kind
/// cannot ship with an unlabelled section. RacePresentationSmokeProbe asserts each key resolves
/// against the shipped translation, which is what makes a missing string a CI failure rather than a
/// raw key on screen.
/// </summary>
public static class RaceCoursePresentationCatalog
{
    public static (string NameKey, string SummaryKey) KeysFor(string courseId)
        => courseId switch
        {
            "demo" => ("UI_RACE_COURSE_DEMO_NAME", "UI_RACE_COURSE_DEMO_SUMMARY"),
            "long-standard" => ("UI_RACE_COURSE_LONG_STANDARD_NAME", "UI_RACE_COURSE_LONG_STANDARD_SUMMARY"),
            _ => throw new InvalidOperationException(
                $"Race course '{courseId}' has no presentation strings. Add them before shipping the course.")
        };

    public static string SectionKeyFor(RaceSegmentKind kind)
        => kind switch
        {
            RaceSegmentKind.Ground => "UI_RACE_SECTION_RUN",
            RaceSegmentKind.Swim => "UI_RACE_SECTION_SWIM",
            RaceSegmentKind.Climb => "UI_RACE_SECTION_CLIMB",
            RaceSegmentKind.Glide => "UI_RACE_SECTION_GLIDE",
            _ => throw new InvalidOperationException($"Race segment kind '{kind}' has no section label.")
        };

    /// <summary>
    /// The live HUD label for the section the racer is in. Climb resolves to its own label rather
    /// than falling through to Run, so a Power stretch is readable while it is being raced.
    /// </summary>
    public static string SectionKeyFor(RaceTerrain terrain)
        => terrain switch
        {
            RaceTerrain.Ground => "UI_RACE_SECTION_RUN",
            RaceTerrain.Swim => "UI_RACE_SECTION_SWIM",
            RaceTerrain.FailedGlideSwim => "UI_RACE_SECTION_SWIM",
            RaceTerrain.Climb => "UI_RACE_SECTION_CLIMB",
            RaceTerrain.Glide => "UI_RACE_SECTION_GLIDE",
            _ => throw new InvalidOperationException($"Race terrain '{terrain}' has no section label.")
        };

    public static IEnumerable<string> AllKeys()
    {
        foreach (var course in RaceCourseCatalog.All)
        {
            var (nameKey, summaryKey) = KeysFor(course.Id);
            yield return nameKey;
            yield return summaryKey;
        }

        foreach (RaceSegmentKind kind in Enum.GetValues<RaceSegmentKind>())
            yield return SectionKeyFor(kind);

        foreach (RaceTerrain terrain in Enum.GetValues<RaceTerrain>())
            yield return SectionKeyFor(terrain);

        yield return "UI_RACE_SECTION_GLIDE_SWIM";
        yield return "UI_RACE_SECTION_TAKEOFF";
        yield return "UI_RACE_PICKER_COURSE";
        yield return "UI_RACE_RETURN";
        yield return "UI_RACE_RESULT_WIN";
        yield return "UI_RACE_RESULT_LAST";
        yield return "UI_RACE_RESULT_COMPLETE";
        yield return "UI_RACE_RESULT_PLACE";
        yield return "UI_RACE_RESULT_EYEBROW";
        yield return "UI_RACE_RESULT_REWARD";
        yield return "UI_RACE_RESULT_NEW_RECORD";
        yield return "UI_RACE_PAUSED";
        yield return "UI_RACE_RESUME";
        yield return "UI_RACE_QUIT";

        // The live HUD groups: banner, standings, course progress and the player's own card.
        yield return "UI_RACE_HUD_LOCAL";
        yield return "UI_RACE_HUD_ONLINE";
        yield return "UI_RACE_HUD_MENU";
        yield return "UI_RACE_HUD_STANDINGS";
        yield return "UI_RACE_HUD_COURSE";
        yield return "UI_RACE_HUD_START";
        yield return "UI_RACE_HUD_FINISH";
        yield return "UI_RACE_HUD_YOU";
        yield return "UI_RACE_HUD_LEAD";
        yield return "UI_RACE_HUD_PROGRESS";
        yield return "UI_RACE_HUD_PLACE";
        yield return "UI_RACE_CHEER";
        yield return "UI_RACE_CHEERING";
        yield return "UI_RACE_STAMINA";
        yield return "UI_RACE_ORDINAL_1";
        yield return "UI_RACE_ORDINAL_2";
        yield return "UI_RACE_ORDINAL_3";
        yield return "UI_RACE_ORDINAL_4";
        yield return "UI_RACE_ORDINAL_N";
    }
}
