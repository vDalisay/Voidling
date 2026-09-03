using System;
using System.Linq;
using Godot;
using Voidling.Domain.Racing;
using Voidling.Presentation.UI.Common;
using Voidling.Presentation.Voidlings;

namespace Voidling.Presentation.Racing;

/// <summary>
/// Headless CI probe for race presentation contracts that only hold once Godot is running.
///
/// It guards three regressions that shipped as visible bugs: authored Climb sections that rendered
/// as bare ground, a HUD that labelled Climb as "Run", and course picker entries that showed raw
/// localization keys because the strings were never added.
/// </summary>
public partial class RacePresentationSmokeProbe : Node
{
    public override void _Ready()
    {
        try
        {
            ValidateLocalizedSectionAndCourseStrings();
            ValidateEverySegmentKindIsDrawn();
            ValidateGroundPivotMatchesGardenAtRaceScale();

            GD.Print(
                "[race-presentation-smoke] RACE_PRESENTATION_SMOKE_SUCCESS " +
                $"courses={string.Join(',', RaceCourseCatalog.All.Select(course => course.Id))}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PrintErr($"[race-presentation-smoke] RACE_PRESENTATION_SMOKE_FAILED: {exception.Message}");
            GetTree().Quit(6);
        }
    }

    // A key that translates to itself is a string that was never authored, which is exactly how the
    // course picker ended up showing UI_RACE_COURSE_LONG_NAME to players.
    private void ValidateLocalizedSectionAndCourseStrings()
    {
        foreach (var key in RaceCoursePresentationCatalog.AllKeys().Distinct(StringComparer.Ordinal))
        {
            var translated = Tr(key);
            if (string.IsNullOrWhiteSpace(translated) || string.Equals(translated, key, StringComparison.Ordinal))
                throw new InvalidOperationException($"Localization key '{key}' has no translated string.");
        }
    }

    // Every segment kind an authored course actually contains must produce world geometry. Climb
    // was authored on both courses but drawn by nothing, so players never saw the Power stretch.
    private void ValidateEverySegmentKindIsDrawn()
    {
        foreach (var definition in RaceCourseCatalog.All)
        {
            foreach (var kind in definition.Course.Segments.Select(segment => segment.Kind).Distinct())
            {
                // Throws for any kind the renderer has no declaration for.
                var visual = RaceScreen.VisualFor(kind);
                if (kind != RaceSegmentKind.Ground && !visual.Water && !visual.Climb && !visual.Ramp)
                {
                    throw new InvalidOperationException(
                        $"Course '{definition.Id}' contains {kind} segments that race presentation never draws.");
                }
            }
        }
    }

    // The Garden and the race share one shadow offset, so both must place the sprite on the same
    // ground pivot. Measured in sprite-local pixels the foot-to-shadow distance has to match, or the
    // race sprite floats above its own shadow the way it did when the race offset stopped scaling.
    private static void ValidateGroundPivotMatchesGardenAtRaceScale()
    {
        foreach (var visualTypeId in VoidlingVisualFactory.VisualTypeIds)
        {
            var adultScale = VoidlingVisualFactory.WorldScale(adult: true, visualTypeId);
            var childScale = VoidlingVisualFactory.WorldScale(adult: false, visualTypeId);
            var raceScale = VoidlingVisualFactory.RaceScaleFor(visualTypeId);

            var adultFootGap = FootGapInSpritePixels(
                adultScale,
                VoidlingVisualFactory.WorldSpriteCenterYOffset(adultScale, visualTypeId),
                visualTypeId);
            var childFootGap = FootGapInSpritePixels(
                childScale,
                VoidlingVisualFactory.WorldSpriteCenterYOffset(childScale, visualTypeId),
                visualTypeId);
            var raceFootGap = FootGapInSpritePixels(
                raceScale,
                VoidlingVisualFactory.RaceSpriteCenterYOffset(visualTypeId),
                visualTypeId);

            if (Mathf.Abs(adultFootGap - raceFootGap) > 0.001f ||
                Mathf.Abs(adultFootGap - childFootGap) > 0.001f)
            {
                throw new InvalidOperationException(
                    $"Voidling '{visualTypeId}' does not share one ground pivot across contexts " +
                    $"(adult {adultFootGap:0.###}, child {childFootGap:0.###}, race {raceFootGap:0.###} " +
                    "px in sprite space).");
            }
        }
    }

    private static float FootGapInSpritePixels(float spriteScale, float spriteCenterY, string visualTypeId)
        => (VoidlingVisualFactory.ShadowCenterYOffset(spriteScale, visualTypeId) - spriteCenterY) / spriteScale;
}
