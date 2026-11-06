using System;
using System.Collections.Generic;
using Godot;
using Voidling.Domain.Racing;

namespace Voidling.Presentation.Racing;

/// <summary>
/// Race track geometry the painter needs, so the layout constants stay owned by RaceScreen.
/// </summary>
internal readonly record struct RaceTrackLayout(
    float TrackTop,
    float TrackBottom,
    float ScreenWidth,
    float ScreenHeight,
    float ClimbHeight)
{
    internal const int LaneCount = 4;

    // Shared by racers and lane props, including the course-selection preview.
    internal float LaneY(int index) => TrackTop + 28.0f + 20.0f * Math.Clamp(index, 0, LaneCount - 1);
}

/// <summary>
/// Paints the race course out of the Sprout Lands tiles.
///
/// The course is painted in three passes so the animated water can sit between them: the land and
/// the raised track behind it, the water layers, then everything that stands in or over the water
/// (banks, the cliff under the take-off lip) and the course furniture. Raised surfaces are drawn as
/// thin polygon strips whose texture follows the slope, so cliff walls have straight edges
/// instead of the staircase stepped rectangles leave.
///
/// Everything here is a pure function of the course, so two runs of the same course paint the same
/// track and nothing touches the simulation's random stream.
/// </summary>
internal static class RaceTrackArt
{
    /// <summary>Vertical rise, in world pixels, of the clifftop the climb section leads onto.</summary>
    internal const float ClimbHeight = 64.0f;

    /// <summary>Airborne lift supplied by a flower spring above the surface it stands on.</summary>
    internal const float SpringLaunchHeight = 38.0f;

    /// <summary>
    /// How much X a racer spends scaling a cliff. Short, so the wall is steep: the projection cannot
    /// draw a face that is edge-on to the direction of travel, so the wall leans back a little and
    /// the racers climb it at a steep angle instead of walking up a hill.
    /// </summary>
    internal const float WallSpan = 26.0f;

    private const float StripWidth = 2.0f;

    private static readonly Color Shade = new(0.10f, 0.13f, 0.11f, 0.30f);
    private static readonly Color RimLight = new(1.0f, 0.93f, 0.78f, 0.55f);
    private static readonly Color Vine = Color.FromHtml("#5E8C4A");
    private static readonly Color VineLight = Color.FromHtml("#8DBF62");
    private static readonly Color Hold = Color.FromHtml("#6E5A48");
    private static readonly Color HoldLight = Color.FromHtml("#B7A48A");
    private static readonly Color Timber = Color.FromHtml("#8A6242");
    private static readonly Color TimberDark = Color.FromHtml("#5B3F2B");
    private static readonly Color Cream = Color.FromHtml("#F7EFD9");
    private static readonly Color Ink = Color.FromHtml("#5B4C40");

    private static readonly Color[] BuntingColors =
    {
        Color.FromHtml("#E7655A"),
        Color.FromHtml("#F2D45C"),
        Color.FromHtml("#78C96A"),
        Color.FromHtml("#6BA8D8"),
        Color.FromHtml("#C9A0E6")
    };

    /// <summary>
    /// A stretch of course raised onto a clifftop: from the climb that leads up it to wherever the
    /// ground has to come back down, which is the next water or glide crossing.
    /// </summary>
    private readonly record struct Plateau(float StartX, float EndX, bool DropsAtEnd);

    /// <summary>
    /// Where the course sits above its base band at <paramref name="x"/>: the clifftop a climb leads
    /// onto. Art and racers both read this one function, so a racer
    /// cannot walk through the surface it is standing on.
    /// </summary>
    internal static float SurfaceLift(RaceCourse course, float x)
        => PlateauLift(course, x);

    /// <summary>Spring launch altitude above the base track band, including any clifftop.</summary>
    internal static float LaunchHeight(RaceCourse course)
        => course.HasGlideSegment
            ? PlateauLift(course, course.GlideSegment.StartX - 0.5f) + SpringLaunchHeight
            : SpringLaunchHeight;

    /// <summary>
    /// How far up a cliff wall a racer at <paramref name="x"/> is, 0 at the foot and 1 at the top,
    /// or null when it is not on a wall. Descending walls report their own progress downwards.
    /// </summary>
    internal static float? WallProgress(RaceCourse course, float x, out bool ascending)
    {
        ascending = true;
        foreach (var plateau in Plateaus(course))
        {
            if (x >= plateau.StartX && x < plateau.StartX + WallSpan)
                return (x - plateau.StartX) / WallSpan;

            if (plateau.DropsAtEnd && x > plateau.EndX - WallSpan && x <= plateau.EndX)
            {
                ascending = false;
                return (x - (plateau.EndX - WallSpan)) / WallSpan;
            }
        }

        return null;
    }

    /// <summary>Share of a climb stretch's distance a racer spends on the wall itself.</summary>
    private const float WallShareOfClimb = 0.45f;

    /// <summary>
    /// Where a racer at simulated <paramref name="x"/> is drawn. On a climb stretch the first part
    /// of the distance is spent hauling up the short wall and the rest crossing the clifftop, so the
    /// climb reads as a climb instead of a two-frame hop. Continuous and monotonic, and equal to
    /// <paramref name="x"/> everywhere else, so nothing snaps at either end of the stretch.
    /// </summary>
    internal static float PresentationX(RaceCourse course, float x)
    {
        foreach (var segment in course.Segments)
        {
            if (segment.Kind != RaceSegmentKind.Climb || x < segment.StartX || x >= segment.EndX)
                continue;

            var length = segment.EndX - segment.StartX;
            var wall = Math.Min(WallSpan, length * 0.5f);
            var hauling = length * WallShareOfClimb;
            var along = x - segment.StartX;
            return along < hauling
                ? segment.StartX + wall * (along / hauling)
                : segment.StartX + wall + (length - wall) * ((along - hauling) / (length - hauling));
        }

        return x;
    }

    private static float PlateauLift(RaceCourse course, float x)
    {
        var lift = 0.0f;
        foreach (var plateau in Plateaus(course))
        {
            if (x < plateau.StartX || x > plateau.EndX)
                continue;

            var up = Mathf.Clamp((x - plateau.StartX) / WallSpan, 0.0f, 1.0f);
            var down = plateau.DropsAtEnd ? Mathf.Clamp((plateau.EndX - x) / WallSpan, 0.0f, 1.0f) : 1.0f;
            lift += ClimbHeight * Math.Min(up, down);
        }

        return lift;
    }

    /// <summary>
    /// Each climb raises the course until the next crossing it cannot stay raised over. A clifftop
    /// that ran straight into the glide launch does not drop: the racers leap off it.
    /// </summary>
    private static IEnumerable<Plateau> Plateaus(RaceCourse course)
    {
        foreach (var climb in course.Segments)
        {
            if (climb.Kind != RaceSegmentKind.Climb)
                continue;

            var endX = course.EndX;
            foreach (var next in course.Segments)
            {
                if (next.StartX >= climb.EndX && next.Kind != RaceSegmentKind.Ground)
                {
                    endX = next.StartX;
                    break;
                }
            }

            var launchesFromTop = course.HasGlideSegment &&
                                  course.GlideLaunchStartX >= climb.StartX &&
                                  course.GlideLaunchStartX < endX;
            yield return new Plateau(climb.StartX, endX, !launchesFromTop);
        }
    }

    // ---- Pass 1: land and the raised track --------------------------------------------------

    internal static void PaintBack(CanvasItem canvas, RaceCourse course, RaceTrackLayout layout)
    {
        var left = -layout.ScreenWidth;
        var width = course.EndX + layout.ScreenWidth * 2.0f;

        Tile(canvas, RaceTrackTiles.Grass, new Rect2(left, 0, width, layout.ScreenHeight));
        Tile(canvas, RaceTrackTiles.GrassTufts, new Rect2(left, 0, width, 40.0f));
        PaintMeadowPatches(canvas, course, layout);

        PaintSurface(canvas, course, layout);
        PaintWallDressing(canvas, course, layout);
    }

    /// <summary>
    /// Moss and flower patches from the premium grass sheet, so the meadow either side of the
    /// track is not one repeated tile.
    /// </summary>
    private static void PaintMeadowPatches(CanvasItem canvas, RaceCourse course, RaceTrackLayout layout)
    {
        for (var x = -layout.ScreenWidth; x < course.EndX + layout.ScreenWidth; x += 16.0f)
        {
            for (var y = 0.0f; y < layout.ScreenHeight; y += 16.0f)
            {
                if (y > layout.TrackTop - 20.0f && y < layout.TrackBottom + 4.0f)
                    continue;

                var hash = Hash(x, y);
                if (hash % 11u == 0u)
                    canvas.DrawTexture(RaceTrackTiles.GrassMoss, new Vector2(x, y));
                else if (hash % 17u == 0u)
                    canvas.DrawTexture(RaceTrackTiles.GrassFlowers, new Vector2(x, y));
            }
        }
    }

    /// <summary>
    /// Walks the course in thin strips. Flat runs merge into one band draw; wall strips are
    /// drawn as quads whose texture is sheared by the lift, so cliff edges stay straight.
    /// </summary>
    private static void PaintSurface(CanvasItem canvas, RaceCourse course, RaceTrackLayout layout)
    {
        var left = -layout.ScreenWidth;
        var right = course.EndX + layout.ScreenWidth;

        var runStart = left;
        var runLift = PlateauLift(course, left);

        for (var x = left; x < right; x += StripWidth)
        {
            var x1 = x + StripWidth;
            var p0 = PlateauLift(course, x);
            var p1 = PlateauLift(course, x1);
            var flat = Mathf.IsEqualApprox(p0, p1);

            if (flat && Mathf.IsEqualApprox(p0, runLift))
                continue;

            PaintBandRun(canvas, layout, runStart, x, runLift);
            if (flat)
            {
                runStart = x;
                runLift = p0;
                continue;
            }

            PaintWallStrip(canvas, layout, x, x1, p0, p1);

            runStart = x1;
            runLift = p1;
        }

        PaintBandRun(canvas, layout, runStart, right, runLift);
    }

    private static void PaintBandRun(CanvasItem canvas, RaceTrackLayout layout, float x0, float x1, float lift)
    {
        var width = x1 - x0;
        if (width <= 0.0f)
            return;

        var band = new Rect2(x0, layout.TrackTop - lift, width, layout.TrackBottom - layout.TrackTop);
        if (lift > 0.5f)
        {
            Face(canvas, new Rect2(x0, band.End.Y, width, lift));
            canvas.DrawRect(new Rect2(x0, layout.TrackBottom, width, 8.0f), Shade);
            canvas.DrawRect(new Rect2(x0, layout.TrackBottom, width, 3.0f), Shade);
        }

        PaintDirtBand(canvas, band);
    }

    private static void PaintDirtBand(CanvasItem canvas, Rect2 band)
    {
        Tile(canvas, RaceTrackTiles.DirtFill, band);
        // A worn racing line down the middle of the lanes.
        Tile(canvas, RaceTrackTiles.DirtWorn, new Rect2(band.Position.X, band.Position.Y + 44.0f, band.Size.X, 32.0f));
        Tile(canvas, RaceTrackTiles.DirtTop, new Rect2(band.Position.X, band.Position.Y, band.Size.X, 16.0f));
        Tile(canvas, RaceTrackTiles.DirtBottom, new Rect2(band.Position.X, band.End.Y - 16.0f, band.Size.X, 16.0f));

        // Kicked-up grit and faint lane lines, so a long straight is not a flat slab of one colour.
        var start = Mathf.Floor(band.Position.X / 23.0f) * 23.0f;
        for (var x = start; x < band.End.X; x += 23.0f)
        {
            if (x < band.Position.X)
                continue;
            var hash = unchecked((uint)(int)Math.Floor(x / 23.0f) * 2246822519u);
            if (hash % 3u != 0u)
                continue;

            var y = band.Position.Y + 20.0f + hash % (uint)Math.Max(1.0f, band.Size.Y - 40.0f);
            var width = Math.Min(3.0f + hash % 5u, band.End.X - x);
            canvas.DrawRect(new Rect2(x, y, width, 2.0f), new Color(0.62f, 0.49f, 0.36f, 0.45f));
        }

        foreach (var laneY in new[] { 39.0f, 59.0f, 79.0f })
        {
            for (var x = Mathf.Ceil(band.Position.X / 12.0f) * 12.0f; x < band.End.X; x += 12.0f)
            {
                canvas.DrawRect(
                    new Rect2(x, band.Position.Y + laneY, Math.Min(5.0f, band.End.X - x), 1.0f),
                    new Color(1.0f, 0.97f, 0.88f, 0.22f));
            }
        }
    }

    /// <summary>
    /// One strip of a cliff wall. The part of the track band that is rising is the face the racers
    /// climb; below it the plateau's front face drops to the ground.
    /// </summary>
    private static void PaintWallStrip(CanvasItem canvas, RaceTrackLayout layout, float x0, float x1, float lift0, float lift1)
    {
        var ascending = lift1 >= lift0;
        var face = RaceTrackTiles.CliffFace;
        var size = face.GetSize();

        // The climbing face: courses of earth run along the wall at equal height.
        var faceTint = ascending ? new Color(1.0f, 0.97f, 0.92f) : new Color(0.74f, 0.70f, 0.68f);
        TexturedQuad(
            canvas,
            face,
            new Vector2(x0, layout.TrackTop - lift0),
            new Vector2(x1, layout.TrackTop - lift1),
            new Vector2(x1, layout.TrackBottom - lift1),
            new Vector2(x0, layout.TrackBottom - lift0),
            new Vector2(layout.TrackTop / size.X, lift0 / size.Y),
            new Vector2(layout.TrackTop / size.X, lift1 / size.Y),
            new Vector2(layout.TrackBottom / size.X, lift1 / size.Y),
            new Vector2(layout.TrackBottom / size.X, lift0 / size.Y),
            faceTint);

        // Darker towards the foot, where less light reaches.
        var depth = 1.0f - Math.Max(lift0, lift1) / ClimbHeight;
        canvas.DrawPolygon(
            new[]
            {
                new Vector2(x0, layout.TrackTop - lift0),
                new Vector2(x1, layout.TrackTop - lift1),
                new Vector2(x1, layout.TrackBottom - lift1),
                new Vector2(x0, layout.TrackBottom - lift0)
            },
            new[] { new Color(0.16f, 0.11f, 0.08f, 0.10f + depth * 0.22f) });

        // The plateau's own front face under the rising strip.
        var bottom = layout.TrackBottom;
        if (Math.Max(lift0, lift1) > 0.5f)
        {
            TexturedQuad(
                canvas,
                face,
                new Vector2(x0, bottom - lift0),
                new Vector2(x1, bottom - lift1),
                new Vector2(x1, bottom),
                new Vector2(x0, bottom),
                new Vector2(x0 / size.X, (bottom - lift0) / size.Y),
                new Vector2(x1 / size.X, (bottom - lift1) / size.Y),
                new Vector2(x1 / size.X, bottom / size.Y),
                new Vector2(x0 / size.X, bottom / size.Y),
                new Color(0.80f, 0.76f, 0.72f));
            canvas.DrawRect(new Rect2(x0, bottom, x1 - x0, 6.0f), Shade);
        }
    }

    /// <summary>
    /// Cliff rock of any height under a raised band: the premium earth courses, deepening to shadow
    /// at the foot, with the ledge's tufted base where it meets the grass.
    /// </summary>
    private static void Face(CanvasItem canvas, Rect2 rect)
    {
        if (rect.Size.X <= 0.0f || rect.Size.Y <= 0.0f)
            return;

        Tile(canvas, RaceTrackTiles.CliffFace, rect);

        const int steps = 4;
        var height = rect.Size.Y / steps;
        for (var step = 0; step < steps; step++)
        {
            canvas.DrawRect(
                new Rect2(rect.Position.X, rect.Position.Y + step * height, rect.Size.X, height),
                new Color(0.16f, 0.11f, 0.08f, 0.04f + step * 0.06f));
        }

        // Light catching the lip right under the track edge.
        canvas.DrawRect(new Rect2(rect.Position.X, rect.Position.Y, rect.Size.X, 1.0f), RimLight);

        if (rect.Size.Y > 6.0f)
            Tile(canvas, RaceTrackTiles.CliffFoot, new Rect2(rect.Position.X, rect.End.Y - 4.0f, rect.Size.X, 4.0f));
    }

    /// <summary>
    /// Vines hanging down each wall and the worn holds racers grab on the way up, laid along the
    /// wall's own lean so they read as being on the face rather than floating in front of it.
    /// </summary>
    private static void PaintWallDressing(CanvasItem canvas, RaceCourse course, RaceTrackLayout layout)
    {
        foreach (var plateau in Plateaus(course))
        {
            PaintWallFace(canvas, layout, plateau.StartX, ascending: true);
            if (plateau.DropsAtEnd)
                PaintWallFace(canvas, layout, plateau.EndX - WallSpan, ascending: false);
        }
    }

    private static void PaintWallFace(CanvasItem canvas, RaceTrackLayout layout, float wallStart, bool ascending)
    {
        // Point on the face at a depth (base-band Y) and a height up the wall.
        Vector2 OnFace(float baseY, float height)
        {
            var t = height / ClimbHeight;
            var x = ascending ? wallStart + WallSpan * t : wallStart + WallSpan * (1.0f - t);
            return new Vector2(x, baseY - height);
        }

        // The rim where the face turns onto the clifftop.
        var rimX = ascending ? wallStart + WallSpan : wallStart;
        canvas.DrawRect(
            new Rect2(rimX - (ascending ? 2.0f : 0.0f), layout.TrackTop - ClimbHeight, 2.0f, layout.TrackBottom - layout.TrackTop),
            ascending ? RimLight : Shade);

        // A cast shadow on the grass behind the foot of the wall.
        var footX = ascending ? wallStart : wallStart + WallSpan;
        canvas.DrawRect(new Rect2(footX - (ascending ? 5.0f : 0.0f), layout.TrackTop, 5.0f, layout.TrackBottom - layout.TrackTop), Shade);

        for (var baseY = layout.TrackTop + 6.0f; baseY < layout.TrackBottom - 4.0f; baseY += 9.0f)
        {
            var hash = Hash(wallStart, baseY);

            // Hanging vines, a few leaves each.
            if (hash % 3u == 0u)
            {
                var length = 18.0f + hash % 26u;
                var from = OnFace(baseY, ClimbHeight);
                var to = OnFace(baseY, ClimbHeight - length);
                canvas.DrawLine(from, to, Vine, 1.0f);
                for (var leaf = 6.0f; leaf < length; leaf += 7.0f)
                {
                    var at = OnFace(baseY, ClimbHeight - leaf);
                    canvas.DrawRect(new Rect2(at.X - 1.0f, at.Y - 1.0f, 2.0f, 2.0f), (hash + (uint)leaf) % 2u == 0u ? VineLight : Vine);
                }
            }

            // Hand holds.
            if (ascending && hash % 2u == 1u)
            {
                for (var height = 10.0f + hash % 9u; height < ClimbHeight - 6.0f; height += 17.0f)
                {
                    var at = OnFace(baseY + 4.0f, height);
                    canvas.DrawRect(new Rect2(at.X - 1.5f, at.Y - 1.0f, 4.0f, 3.0f), Hold);
                    canvas.DrawRect(new Rect2(at.X - 1.5f, at.Y - 1.0f, 2.0f, 1.0f), HoldLight);
                }
            }
        }
    }

    // ---- Pass 3: in and over the water, and the course furniture ---------------------------

    internal static void PaintFront(CanvasItem canvas, RaceCourse course, RaceTrackLayout layout)
    {
        foreach (var segment in course.Segments)
        {
            if (RaceScreen.VisualFor(segment.Kind).Water)
                PaintBanks(canvas, course, segment, layout);
        }

        if (course.HasGlideSegment)
            PaintLakeCliff(canvas, course, layout);

        PaintStartGate(canvas, course, layout);
        PaintFinishLine(canvas, course, layout);
    }

    /// <summary>
    /// The premium grass-hill lip along each shore, and the soil lip where the track itself runs
    /// into the water, so a crossing has banks instead of a hard cut.
    /// </summary>
    private static void PaintBanks(CanvasItem canvas, RaceCourse course, RaceCourseSegment segment, RaceTrackLayout layout)
    {
        // A lake the racers glide over has a cliff at its near shore, not a bank.
        var nearShoreIsCliff = segment.Kind == RaceSegmentKind.Glide && PlateauLift(course, segment.StartX - 1.0f) > 0.5f;

        var leftLift = PlateauLift(course, segment.StartX - 1.0f);
        var shores = new List<(float X, Texture2D Grass, Texture2D Dirt, float Lift)>();
        if (!nearShoreIsCliff)
            shores.Add((segment.StartX - 12.0f, RaceTrackTiles.GrassBankRight, RaceTrackTiles.DirtBankRight, leftLift));
        shores.Add((segment.EndX - 4.0f, RaceTrackTiles.GrassBankLeft, RaceTrackTiles.DirtBankLeft, 0.0f));

        foreach (var (x, grass, dirt, lift) in shores)
        {
            // Grass lip above and below the track, the soil lip exactly across the lanes.
            Tile(canvas, grass, new Rect2(x, 0.0f, 16.0f, layout.TrackTop - lift));
            Tile(canvas, grass, new Rect2(x, layout.TrackBottom, 16.0f, layout.ScreenHeight - layout.TrackBottom));
            Tile(canvas, dirt, new Rect2(x, layout.TrackTop - lift, 16.0f, layout.TrackBottom - layout.TrackTop));
        }
    }

    /// <summary>
    /// The clifftop the glide launches from ends at the lake. The face below the lip leans down into
    /// the water with foam where it meets it, so the take-off is off a real drop.
    /// </summary>
    private static void PaintLakeCliff(CanvasItem canvas, RaceCourse course, RaceTrackLayout layout)
    {
        var edgeX = course.GlideSegment.StartX;
        var height = PlateauLift(course, edgeX - 1.0f);
        if (height <= 0.5f)
            return;

        var face = RaceTrackTiles.CliffFace;
        var size = face.GetSize();
        const float lean = 14.0f;

        for (var x = edgeX; x < edgeX + lean; x += StripWidth)
        {
            var x1 = Math.Min(x + StripWidth, edgeX + lean);
            var h0 = height * (1.0f - (x - edgeX) / lean);
            var h1 = height * (1.0f - (x1 - edgeX) / lean);
            TexturedQuad(
                canvas,
                face,
                new Vector2(x, layout.TrackTop - h0),
                new Vector2(x1, layout.TrackTop - h1),
                new Vector2(x1, layout.TrackBottom),
                new Vector2(x, layout.TrackBottom),
                new Vector2(layout.TrackTop / size.X, h0 / size.Y),
                new Vector2(layout.TrackTop / size.X, h1 / size.Y),
                new Vector2(layout.TrackBottom / size.X, h1 / size.Y),
                new Vector2(layout.TrackBottom / size.X, h0 / size.Y),
                new Color(0.62f, 0.58f, 0.58f));
        }

        // Front face of the cliff under the lip.
        Face(canvas, new Rect2(edgeX - 2.0f, layout.TrackBottom - height, 2.0f, height));

        // Foam where rock meets water.
        for (var y = layout.TrackTop; y < layout.TrackBottom + 6.0f; y += 3.0f)
        {
            var wobble = Hash(edgeX, y) % 3u;
            canvas.DrawRect(new Rect2(edgeX + lean - 1.0f + wobble, y, 3.0f, 2.0f), new Color(0.95f, 0.99f, 1.0f, 0.85f));
        }
    }

    private static void PaintStartGate(CanvasItem canvas, RaceCourse course, RaceTrackLayout layout)
    {
        // The gate stands clear of the starting grid so its posts never sit on top of a racer.
        var x = course.StartX - 30.0f;
        var bannerTop = layout.TrackTop - 34.0f;

        canvas.DrawRect(new Rect2(course.StartX - 12.0f, layout.TrackTop, 3.0f, layout.TrackBottom - layout.TrackTop), Cream);

        foreach (var postX in new[] { x - 24.0f, x + 18.0f })
            Post(canvas, postX, bannerTop, layout.TrackBottom + 10.0f);

        canvas.DrawRect(new Rect2(x - 24.0f, bannerTop, 48.0f, 14.0f), Cream);
        canvas.DrawRect(new Rect2(x - 24.0f, bannerTop + 5.0f, 48.0f, 4.0f), Color.FromHtml("#78C96A"));
        canvas.DrawRect(new Rect2(x - 24.0f, bannerTop + 14.0f, 48.0f, 1.0f), Ink);
        Bunting(canvas, new Vector2(x - 21.0f, bannerTop + 16.0f), new Vector2(x + 21.0f, bannerTop + 16.0f), 6);

        Rail(canvas, x - 34.0f, x + 34.0f, layout.TrackBottom + 2.0f);
    }

    private static void PaintFinishLine(CanvasItem canvas, RaceCourse course, RaceTrackLayout layout)
    {
        const float square = 10.0f;
        var top = layout.TrackTop - 6.0f;
        var height = layout.TrackBottom - layout.TrackTop + 12.0f;

        canvas.DrawRect(new Rect2(course.EndX - 10.0f, top, 20.0f, height), Cream);
        for (var row = 0; row * square < height; row++)
        {
            for (var col = 0; col < 2; col++)
            {
                if ((row + col) % 2 == 0)
                {
                    canvas.DrawRect(
                        new Rect2(course.EndX - 10.0f + col * square, top + row * square, square, Math.Min(square, height - row * square)),
                        Ink);
                }
            }
        }

        // A finish arch: posts planted either side of the lanes with a checked banner strung between
        // them and bunting underneath, so the line is an event marker and not just a painted stripe.
        var bannerTop = top - 34.0f;
        foreach (var postX in new[] { course.EndX - 32.0f, course.EndX + 26.0f })
            Post(canvas, postX, bannerTop, layout.TrackBottom + 10.0f);

        canvas.DrawRect(new Rect2(course.EndX - 32.0f, bannerTop, 64.0f, 14.0f), Cream);
        for (var i = 0; i < 8; i++)
        {
            var y = i % 2 == 0 ? bannerTop : bannerTop + 7.0f;
            canvas.DrawRect(new Rect2(course.EndX - 32.0f + i * 8.0f, y, 8.0f, 7.0f), Ink);
        }
        Bunting(canvas, new Vector2(course.EndX - 29.0f, bannerTop + 16.0f), new Vector2(course.EndX + 29.0f, bannerTop + 16.0f), 8);

        Rail(canvas, course.EndX - 42.0f, course.EndX + 42.0f, layout.TrackBottom + 2.0f);
    }

    /// <summary>A tall timber post with a cap, standing on the verge.</summary>
    private static void Post(CanvasItem canvas, float x, float top, float bottom)
    {
        canvas.DrawRect(new Rect2(x, top, 6.0f, bottom - top), Timber);
        canvas.DrawRect(new Rect2(x, top, 2.0f, bottom - top), TimberDark);
        canvas.DrawRect(new Rect2(x - 1.0f, top - 2.0f, 8.0f, 3.0f), Cream);
        canvas.DrawRect(new Rect2(x - 1.0f, bottom - 2.0f, 8.0f, 2.0f), new Color(0.0f, 0.0f, 0.0f, 0.18f));
    }

    /// <summary>A premium fence run between two X positions, with its end pieces.</summary>
    private static void Rail(CanvasItem canvas, float startX, float endX, float y)
    {
        for (var x = startX; x < endX; x += 16.0f)
        {
            var texture = x <= startX
                ? RaceTrackTiles.FenceRailLeft
                : x + 16.0f >= endX ? RaceTrackTiles.FenceRailRight : RaceTrackTiles.FenceRail;
            canvas.DrawTextureRect(texture, new Rect2(x, y, 16.0f, 16.0f), false);
        }
    }

    /// <summary>Triangle flags on a sagging string, in the festival colours.</summary>
    internal static void Bunting(CanvasItem canvas, Vector2 from, Vector2 to, int flags)
    {
        var points = new Vector2[flags + 1];
        for (var i = 0; i <= flags; i++)
        {
            var t = i / (float)flags;
            var sag = Mathf.Sin(t * Mathf.Pi) * 4.0f;
            points[i] = from.Lerp(to, t) + new Vector2(0.0f, sag);
        }

        canvas.DrawPolyline(points, Ink, 1.0f);
        for (var i = 0; i < flags; i++)
        {
            var a = points[i];
            var b = points[i + 1];
            var tip = (a + b) * 0.5f + new Vector2(0.0f, 6.0f);
            canvas.DrawColoredPolygon(new[] { a, b, tip }, BuntingColors[i % BuntingColors.Length]);
        }
    }

    // ---- Foliage ------------------------------------------------------------------------------

    /// <summary>
    /// Roadside trees, bushes, flowers and stones from the premium object sheets. Positions come
    /// from a hash of the world X so the same course always grows the same scenery, and nothing is
    /// placed on the track, in the water or against a cliff.
    /// </summary>
    internal static void PaintFoliage(CanvasItem canvas, RaceCourse course, RaceTrackLayout layout, bool behindTrack)
    {
        var from = -layout.ScreenWidth;
        var to = course.EndX + layout.ScreenWidth;
        const float spacing = 30.0f;
        for (var x = from; x < to; x += spacing)
        {
            var slot = (int)Math.Floor(x / spacing);
            var hash = unchecked((uint)slot * 2654435761u) ^ (behindTrack ? 0x9E37u : 0x5BD1u);
            var pick = hash % 20u;
            var px = x + ((hash >> 5) % 21u) - 10.0f;
            var jitterY = (hash >> 11) % 17u;

            Texture2D? texture = pick switch
            {
                < 5u => RaceTrackTiles.Trees[(int)((hash >> 3) % (uint)RaceTrackTiles.Trees.Length)],
                < 9u => RaceTrackTiles.Bushes[(int)((hash >> 3) % (uint)RaceTrackTiles.Bushes.Length)],
                < 11u => RaceTrackTiles.Stones[(int)((hash >> 3) % (uint)RaceTrackTiles.Stones.Length)],
                < 15u => RaceTrackTiles.Flowers[(int)((hash >> 3) % (uint)RaceTrackTiles.Flowers.Length)],
                < 17u => RaceTrackTiles.GrassClumps[(int)((hash >> 3) % (uint)RaceTrackTiles.GrassClumps.Length)],
                < 18u => RaceTrackTiles.Stumps[(int)((hash >> 3) % (uint)RaceTrackTiles.Stumps.Length)],
                _ => null
            };
            if (texture == null)
                continue;

            var size = texture.GetSize();
            if (IsFeature(course, px - 4.0f) || IsFeature(course, px + size.X + 4.0f))
                continue;

            float y;
            if (behindTrack)
            {
                // Trees stand back from the verge; the smaller things come closer to the track.
                var baseline = size.Y > 20.0f ? 44.0f + jitterY : layout.TrackTop - 22.0f - jitterY * 0.6f;
                y = baseline - size.Y;
            }
            else
            {
                var baseline = size.Y > 20.0f ? layout.ScreenHeight - 12.0f - jitterY : layout.TrackBottom + 30.0f + jitterY;
                y = baseline - size.Y;
            }

            canvas.DrawTexture(texture, new Vector2(Mathf.Round(px), Mathf.Round(y)));
        }
    }

    /// <summary>Ground that belongs to the track: water, cliff or clifftop. Nothing grows here.</summary>
    private static bool IsFeature(RaceCourse course, float x)
        => course.SegmentKindAt(x) is RaceSegmentKind.Swim or RaceSegmentKind.Climb or RaceSegmentKind.Glide ||
           SurfaceLift(course, x) > 0.0f;

    // ---- Drawing helpers -----------------------------------------------------------------------

    private static void Tile(CanvasItem canvas, Texture2D texture, Rect2 rect)
        => canvas.DrawTextureRect(texture, rect, tile: true);

    private static void TexturedQuad(
        CanvasItem canvas,
        Texture2D texture,
        Vector2 a, Vector2 b, Vector2 c, Vector2 d,
        Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud,
        Color modulate)
        => canvas.DrawPolygon(new[] { a, b, c, d }, new[] { modulate, modulate, modulate, modulate }, new[] { ua, ub, uc, ud }, texture);

    private static uint Hash(float x, float y)
    {
        unchecked
        {
            var hash = (uint)(int)Math.Floor(x) * 73856093u ^ (uint)(int)Math.Floor(y) * 19349663u;
            hash ^= hash >> 13;
            hash *= 1274126177u;
            return hash ^ (hash >> 16);
        }
    }
}
