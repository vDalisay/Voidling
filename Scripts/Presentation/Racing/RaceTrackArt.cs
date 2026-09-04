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
    float ClimbHeight);

/// <summary>
/// Paints the race course out of the Sprout Lands tilesets.
///
/// The tiles are sliced once into standalone textures so the whole track is a handful of tiled
/// <c>DrawTextureRect</c> calls on one canvas instead of thousands of Sprite2D nodes, and so the
/// track reads as the same world the Garden is drawn in rather than as flat debug rectangles.
///
/// Everything here is a pure function of the course, so two runs of the same course paint the same
/// track and nothing touches the simulation's random stream.
/// </summary>
internal static class RaceTrackArt
{
    private static readonly Dictionary<string, Image> SheetCache = new(StringComparer.Ordinal);

    private const string Basic = "res://Assets/Sprout Lands - Sprites - Basic pack/";
    private const string Premium = "res://Assets/Sprout Lands - Sprites - premium pack/";

    // Ground.
    private static readonly Texture2D Grass = Slice(Premium + "Tilesets/ground tiles/New tiles/Grass_tiles_v2.png", 0, 80, 16, 16);
    private static readonly Texture2D GrassTufts = Slice(Premium + "Tilesets/ground tiles/New tiles/Grass_tiles_v2.png", 0, 96, 16, 16);
    private static readonly Texture2D GrassFlowers = Slice(Premium + "Tilesets/ground tiles/New tiles/Grass_tiles_v2.png", 80, 80, 16, 16);

    // Dirt racing surface. Column 1 of the rounded soil blob is the straight-edged run: (1,0) is the
    // top fringe, (1,1) the body and (1,2) the bottom fringe. The 4x4 block's edges are corner
    // wedges, which is why they printed diagonal scars across the lanes.
    private const string SoilSheet = Premium + "Tilesets/ground tiles/New tiles/Soil_Ground_Tiles.png";
    private static readonly Texture2D DirtTop = Slice(SoilSheet, 16, 0, 16, 16);
    private static readonly Texture2D DirtBottom = Slice(SoilSheet, 16, 32, 16, 16);
    private static readonly Texture2D DirtFill = Slice(SoilSheet, 0, 80, 16, 16);
    private static readonly Texture2D DirtWorn = Slice(SoilSheet, 32, 80, 16, 16);

    // Cliff. Rows 57-61 of the Hills cliff tile are pure vertical rock streaks, so that five pixel
    // slice tiles seamlessly in both axes and gives a cliff of any height instead of a 16px ledge.
    private const string HillSheet = Basic + "Tilesets/Hills.png";
    private static readonly Texture2D CliffCap = Slice(HillSheet, 80, 53, 16, 4);
    private static readonly Texture2D CliffBody = Slice(HillSheet, 80, 57, 16, 5);
    private static readonly Texture2D CliffFoot = Slice(HillSheet, 80, 62, 16, 2);

    private static readonly Texture2D[] Water =
    {
        Load(Premium + "Tilesets/ground tiles/water frames/Water_1.png"),
        Load(Premium + "Tilesets/ground tiles/water frames/Water_2.png"),
        Load(Premium + "Tilesets/ground tiles/water frames/Water_3.png"),
        Load(Premium + "Tilesets/ground tiles/water frames/Water_4.png")
    };

    // Rows 33-46 of the bridge deck: the tile's own top row is transparent, and tiling that left a
    // grass-coloured seam every 16px straight through the ramp.
    private static readonly Texture2D Planks = Slice(Premium + "Tilesets/Building parts/Wooden_Bridge_v2.png", 0, 33, 16, 14);

    // Fences. Column 0 is a vertical run (hurdles across the track), row 0 columns 1-3 are the
    // left/middle/right pieces of a horizontal run (ramp and finish rails).
    private const string FenceSheet = Basic + "Tilesets/Fences.png";
    internal static readonly Texture2D FencePost = Slice(FenceSheet, 0, 0, 16, 16);
    private static readonly Texture2D FenceRail = Slice(FenceSheet, 32, 0, 16, 16);

    private static readonly Texture2D Bush = Slice(Premium + "Objects/Trees, stumps and bushes.png", 0, 48, 16, 16);
    private static readonly Texture2D Tree = Slice(Premium + "Objects/Trees, stumps and bushes.png", 144, 48, 48, 48);
    private static readonly Texture2D Stone = Slice(Premium + "Objects/Mushrooms, Flowers, Stones.png", 0, 64, 16, 16);

    private static readonly Color Shade = new(0.10f, 0.13f, 0.11f, 0.30f);
    private static readonly Color Foam = new(0.92f, 0.98f, 1.00f, 0.75f);
    private static readonly Color RockWash = new(0.42f, 0.32f, 0.25f, 0.30f);
    private static readonly Color Strata = new(0.28f, 0.20f, 0.15f, 0.40f);
    private static readonly Color StrataLight = new(0.86f, 0.74f, 0.58f, 0.18f);

    /// <summary>Vertical rise, in world pixels, of the clifftop the climb section leads onto.</summary>
    internal const float ClimbHeight = 56.0f;

    /// <summary>Vertical rise of the glide launch ramp above whatever surface it stands on.</summary>
    internal const float RampHeight = 38.0f;

    /// <summary>
    /// How much X a racer spends scaling a cliff. Short, so the wall is steep: the projection cannot
    /// draw a face that is edge-on to the direction of travel, and stretching the rise over the whole
    /// section turns the cliff into a gentle hill.
    /// </summary>
    private const float WallSpan = 20.0f;

    /// <summary>
    /// A stretch of course raised onto a clifftop: from the climb that leads up it to wherever the
    /// ground has to come back down, which is the next water or glide crossing.
    /// </summary>
    private readonly record struct Plateau(float StartX, float EndX, bool DropsAtEnd);

    /// <summary>
    /// Where the course sits above its base band at <paramref name="x"/>: the clifftop a climb leads
    /// onto plus the launch ramp's own slope. Art and racers both read this one function, so a racer
    /// cannot walk through the surface it is standing on.
    /// </summary>
    internal static float SurfaceLift(RaceCourse course, float x)
    {
        var lift = 0.0f;

        foreach (var plateau in Plateaus(course))
        {
            if (x < plateau.StartX || x > plateau.EndX)
                continue;

            var up = Smooth((x - plateau.StartX) / WallSpan);
            var down = plateau.DropsAtEnd ? Smooth((plateau.EndX - x) / WallSpan) : 1.0f;
            lift += ClimbHeight * Math.Min(up, down);
        }

        if (course.HasGlideSegment && x >= course.GlideLaunchStartX && x < course.GlideSegment.StartX)
        {
            var span = Math.Max(1.0f, course.GlideSegment.StartX - course.GlideLaunchStartX);
            lift += RampHeight * Smooth((x - course.GlideLaunchStartX) / span);
        }

        return lift;
    }

    /// <summary>Height a glider leaves the ramp lip at, measured from the base track band.</summary>
    internal static float LaunchHeight(RaceCourse course)
        => course.HasGlideSegment ? SurfaceLift(course, course.GlideSegment.StartX - 0.5f) : RampHeight;

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

    internal static void Paint(CanvasItem canvas, RaceCourse course, RaceTrackLayout layout, float waterPhase)
    {
        var left = -layout.ScreenWidth;
        var width = course.EndX + layout.ScreenWidth * 2.0f;

        Tile(canvas, Grass, new Rect2(left, 0, width, layout.ScreenHeight));
        Tile(canvas, GrassTufts, new Rect2(left, 0, width, 48.0f));
        Scatter(canvas, course, layout, behindTrack: true);

        PaintSurface(canvas, course, layout);

        foreach (var segment in course.Segments)
        {
            if (RaceScreen.VisualFor(segment.Kind).Water)
                PaintStream(canvas, segment, layout, waterPhase);
        }

        PaintFootholds(canvas, course, layout);
        if (course.HasGlideSegment)
            PaintRampDressing(canvas, course, layout);

        PaintStartGate(canvas, course, layout);
        PaintFinishLine(canvas, course, layout);
        Scatter(canvas, course, layout, behindTrack: false);
    }

    /// <summary>
    /// Walks the course and lays the running surface at whatever height <see cref="SurfaceLift"/>
    /// puts it, dropping a cliff wall wherever that is above the base band. Runs of equal height
    /// merge into one draw, so only the wall and the ramp slope cost a strip each.
    /// </summary>
    private static void PaintSurface(CanvasItem canvas, RaceCourse course, RaceTrackLayout layout)
    {
        const float step = 4.0f;
        var left = -layout.ScreenWidth;
        var right = course.EndX + layout.ScreenWidth;

        var runStart = left;
        var runLift = SurfaceLift(course, left);
        var runPlanked = OnLaunchRamp(course, left);

        for (var x = left + step; x < right; x += step)
        {
            var lift = SurfaceLift(course, x);
            var planked = OnLaunchRamp(course, x);
            if (Mathf.IsEqualApprox(lift, runLift) && planked == runPlanked)
                continue;

            PaintBandRun(canvas, layout, runStart, x, runLift, runPlanked);
            runStart = x;
            runLift = lift;
            runPlanked = planked;
        }

        PaintBandRun(canvas, layout, runStart, right, runLift, runPlanked);
    }

    private static void PaintBandRun(CanvasItem canvas, RaceTrackLayout layout, float x0, float x1, float lift, bool planked)
    {
        var width = x1 - x0;
        if (width <= 0.0f)
            return;

        var band = new Rect2(x0, layout.TrackTop - lift, width, layout.TrackBottom - layout.TrackTop);

        if (lift > 0.5f)
        {
            Face(canvas, new Rect2(x0, band.End.Y, width, lift));
            canvas.DrawRect(new Rect2(x0, layout.TrackBottom, width, 7.0f), Shade);
        }

        if (planked)
        {
            Tile(canvas, Planks, band);
            canvas.DrawRect(new Rect2(band.Position.X, band.Position.Y, width, 2.0f), new Color(0.29f, 0.20f, 0.15f, 0.9f));
            canvas.DrawRect(new Rect2(band.Position.X, band.End.Y - 2.0f, width, 2.0f), new Color(0.29f, 0.20f, 0.15f, 0.9f));
            return;
        }

        PaintDirtBand(canvas, band);
    }

    private static bool OnLaunchRamp(RaceCourse course, float x)
        => course.HasGlideSegment && x >= course.GlideLaunchStartX && x < course.GlideSegment.StartX;

    private static void PaintDirtBand(CanvasItem canvas, Rect2 band)
    {
        Tile(canvas, DirtFill, band);
        // A worn racing line down the middle of the lanes.
        Tile(canvas, DirtWorn, new Rect2(band.Position.X, band.Position.Y + 44.0f, band.Size.X, 32.0f));
        Tile(canvas, DirtTop, new Rect2(band.Position.X, band.Position.Y, band.Size.X, 16.0f));
        Tile(canvas, DirtBottom, new Rect2(band.Position.X, band.End.Y - 16.0f, band.Size.X, 16.0f));

        // Kicked-up grit, so a long straight is not a flat slab of one colour.
        for (var x = band.Position.X; x < band.End.X; x += 23.0f)
        {
            var hash = unchecked((uint)(int)Math.Floor(x / 23.0f) * 2246822519u);
            if (hash % 3u != 0u)
                continue;

            var y = band.Position.Y + 20.0f + hash % (uint)Math.Max(1.0f, band.Size.Y - 40.0f);
            var width = 3.0f + hash % 5u;
            canvas.DrawRect(new Rect2(x, y, width, 2.0f), new Color(0.62f, 0.49f, 0.36f, 0.45f));
        }
    }

    /// <summary>
    /// A swim segment is a river crossing the course. It runs the full height of the view so the
    /// water never ends in mid-air the way a rectangle painted inside the lane markings does.
    /// </summary>
    private static void PaintStream(CanvasItem canvas, RaceCourseSegment segment, RaceTrackLayout layout, float waterPhase)
    {
        var frame = Water[(int)(waterPhase * Water.Length) % Water.Length];
        var body = new Rect2(segment.StartX, 0.0f, segment.EndX - segment.StartX, layout.ScreenHeight);

        Tile(canvas, frame, body);

        // Wet banks: a shaded rim against the dirt, then the current breaking along each shore.
        canvas.DrawRect(new Rect2(body.Position.X - 4.0f, 0.0f, 4.0f, body.Size.Y), Shade);
        canvas.DrawRect(new Rect2(body.End.X, 0.0f, 4.0f, body.Size.Y), Shade);

        // Mid-current ripples, so a wide crossing is not a flat sheet of colour.
        for (var y = 10.0f; y < layout.ScreenHeight; y += 27.0f)
        {
            for (var x = body.Position.X + 22.0f; x < body.End.X - 22.0f; x += 31.0f)
            {
                var bob = Mathf.Sin((x * 0.07f) + (y * 0.05f) + waterPhase * Mathf.Tau) * 3.0f;
                canvas.DrawLine(
                    new Vector2(x, y + bob),
                    new Vector2(x + 9.0f, y + bob),
                    new Color(1.0f, 1.0f, 1.0f, 0.30f),
                    1.0f);
            }
        }

        for (var y = 6.0f; y < layout.ScreenHeight; y += 21.0f)
        {
            var drift = Mathf.Sin((y + waterPhase * 90.0f) * 0.09f) * 7.0f;
            canvas.DrawLine(
                new Vector2(body.Position.X + 6.0f, y),
                new Vector2(body.Position.X + 17.0f + drift, y),
                Foam,
                1.0f);
            canvas.DrawLine(
                new Vector2(body.End.X - 17.0f - drift, y + 10.0f),
                new Vector2(body.End.X - 6.0f, y + 10.0f),
                Foam,
                1.0f);
        }
    }

    /// <summary>
    /// A climb segment is a raised plateau: the whole track band is lifted by <see cref="ClimbHeight"/>
    /// and a real cliff face is drawn under it. The height is what makes the section read as a wall
    /// the racers scale rather than another patch of ground with decoration painted on it.
    /// </summary>
    /// <summary>Worn holds up each wall, marking the line a racer takes to the top.</summary>
    private static void PaintFootholds(CanvasItem canvas, RaceCourse course, RaceTrackLayout layout)
    {
        foreach (var plateau in Plateaus(course))
        {
            const int holds = 5;
            for (var i = 0; i < holds; i++)
            {
                var t = (i + 0.5f) / holds;
                canvas.DrawRect(
                    new Rect2(
                        plateau.StartX + WallSpan * t - 5.0f,
                        layout.TrackBottom - 6.0f - Smooth(t) * (ClimbHeight - 8.0f),
                        11.0f,
                        4.0f),
                    new Color(0.30f, 0.22f, 0.16f, 0.80f));
            }
        }
    }

    /// <summary>
    /// Cliff rock of any height: capped top, seamless vertical body, footed base. The tileset only
    /// ships a 16px ledge, so the body is a five pixel strip of pure vertical rock streaks repeated,
    /// then aged with strata and an ambient-occlusion falloff so a tall face reads as stone rather
    /// than as a fence.
    /// </summary>
    private static void Face(CanvasItem canvas, Rect2 rect)
    {
        if (rect.Size.X <= 0.0f || rect.Size.Y <= 0.0f)
            return;

        Tile(canvas, CliffBody, rect);
        canvas.DrawRect(rect, RockWash);

        for (var y = rect.Position.Y + 21.0f; y < rect.End.Y - 6.0f; y += 24.0f)
        {
            canvas.DrawLine(new Vector2(rect.Position.X, y), new Vector2(rect.End.X, y), Strata, 1.0f);
            canvas.DrawLine(new Vector2(rect.Position.X, y + 1.0f), new Vector2(rect.End.X, y + 1.0f), StrataLight, 1.0f);
        }

        // Deeper in shadow the further down the face, so the mass has weight.
        var steps = 4;
        for (var step = 0; step < steps; step++)
        {
            var height = rect.Size.Y / steps;
            canvas.DrawRect(
                new Rect2(rect.Position.X, rect.Position.Y + step * height, rect.Size.X, height),
                new Color(0.16f, 0.12f, 0.10f, 0.05f + step * 0.05f));
        }

        Tile(canvas, CliffCap, new Rect2(rect.Position.X, rect.Position.Y, rect.Size.X, 4.0f));
        Tile(canvas, CliffFoot, new Rect2(rect.Position.X, rect.End.Y - 2.0f, rect.Size.X, 2.0f));
    }

    /// <summary>
    /// Dressing for the launch ramp. The slope itself comes from <see cref="SurfaceLift"/>, so this
    /// only adds the rails that follow it up, the take-off chevrons and the lip the racer leaves.
    /// </summary>
    private static void PaintRampDressing(CanvasItem canvas, RaceCourse course, RaceTrackLayout layout)
    {
        var startX = course.GlideLaunchStartX;
        var endX = course.GlideSegment.StartX;
        if (endX <= startX)
            return;

        var height = layout.TrackBottom - layout.TrackTop;

        // Rails ride the slope, which is what makes the deck read as rising. One fence tile per
        // tile width: stepping finer just stacks the sprite on itself into a solid hatch.
        for (var x = startX - 8.0f; x < endX + 8.0f; x += 14.0f)
        {
            var top = layout.TrackTop - SurfaceLift(course, Mathf.Clamp(x, startX, endX - 0.5f));
            canvas.DrawTextureRect(FenceRail, new Rect2(x, top - 12.0f, 16.0f, 16.0f), false);
            canvas.DrawTextureRect(FenceRail, new Rect2(x, top + height - 5.0f, 16.0f, 16.0f), false);
        }

        for (var x = startX + 8.0f; x < endX - 10.0f; x += 15.0f)
        {
            var top = layout.TrackTop - SurfaceLift(course, x);
            var tone = Mathf.InverseLerp(startX, endX, x);
            var color = new Color(1.0f, 0.93f, 0.55f, 0.28f + tone * 0.55f);
            for (var y = top + 22.0f; y < top + height - 18.0f; y += 26.0f)
            {
                canvas.DrawLine(new Vector2(x, y - 5.0f), new Vector2(x + 6.0f, y), color, 2.0f);
                canvas.DrawLine(new Vector2(x + 6.0f, y), new Vector2(x, y + 5.0f), color, 2.0f);
            }
        }

        // The lip, and the drop beyond it.
        var lipTop = layout.TrackTop - SurfaceLift(course, endX - 0.5f);
        canvas.DrawRect(new Rect2(endX - 4.0f, lipTop, 4.0f, height), new Color(0.98f, 0.90f, 0.70f, 0.95f));
        canvas.DrawRect(new Rect2(endX, lipTop, 9.0f, height), Shade);
    }

    private static void PaintStartGate(CanvasItem canvas, RaceCourse course, RaceTrackLayout layout)
    {
        // The gate stands clear of the starting grid so its posts never sit on top of a racer.
        var x = course.StartX - 30.0f;
        var cream = Color.FromHtml("#F7EFD9");
        var post = Color.FromHtml("#8A6A4B");
        var bannerTop = layout.TrackTop - 26.0f;

        canvas.DrawRect(new Rect2(course.StartX - 12.0f, layout.TrackTop, 3.0f, layout.TrackBottom - layout.TrackTop), cream);

        foreach (var postX in new[] { x - 22.0f, x + 16.0f })
        {
            canvas.DrawRect(new Rect2(postX, bannerTop, 6.0f, layout.TrackBottom + 10.0f - bannerTop), post);
            canvas.DrawRect(new Rect2(postX, bannerTop, 6.0f, 3.0f), cream);
        }

        canvas.DrawRect(new Rect2(x - 22.0f, bannerTop, 44.0f, 12.0f), cream);
        canvas.DrawRect(new Rect2(x - 22.0f, bannerTop + 4.0f, 44.0f, 4.0f), Color.FromHtml("#78C96A"));

        Rail(canvas, x - 32.0f, x + 32.0f, layout.TrackBottom + 2.0f);
    }

    private static void PaintFinishLine(CanvasItem canvas, RaceCourse course, RaceTrackLayout layout)
    {
        const float square = 10.0f;
        var top = layout.TrackTop - 6.0f;
        var height = layout.TrackBottom - layout.TrackTop + 12.0f;
        var cream = Color.FromHtml("#F7EFD9");
        var ink = Color.FromHtml("#5B4C40");

        canvas.DrawRect(new Rect2(course.EndX - 10.0f, top, 20.0f, height), cream);
        for (var row = 0; row * square < height; row++)
        {
            for (var col = 0; col < 2; col++)
            {
                if ((row + col) % 2 == 0)
                {
                    canvas.DrawRect(
                        new Rect2(course.EndX - 10.0f + col * square, top + row * square, square, Math.Min(square, height - row * square)),
                        ink);
                }
            }
        }

        // A finish arch: two posts planted either side of the lanes with a checked banner strung
        // between them, so the line is an event marker and not just a painted stripe.
        var post = Color.FromHtml("#8A6A4B");
        var bannerTop = top - 26.0f;
        foreach (var postX in new[] { course.EndX - 30.0f, course.EndX + 24.0f })
        {
            canvas.DrawRect(new Rect2(postX, bannerTop, 6.0f, layout.TrackBottom + 10.0f - bannerTop), post);
            canvas.DrawRect(new Rect2(postX, bannerTop, 6.0f, 3.0f), cream);
        }

        canvas.DrawRect(new Rect2(course.EndX - 30.0f, bannerTop, 60.0f, 12.0f), cream);
        for (var i = 0; i < 6; i++)
        {
            if (i % 2 == 0)
                canvas.DrawRect(new Rect2(course.EndX - 30.0f + i * 10.0f, bannerTop, 10.0f, 6.0f), ink);
            else
                canvas.DrawRect(new Rect2(course.EndX - 30.0f + i * 10.0f, bannerTop + 6.0f, 10.0f, 6.0f), ink);
        }

        Rail(canvas, course.EndX - 40.0f, course.EndX + 40.0f, layout.TrackBottom + 2.0f);
    }

    /// <summary>A horizontal Sprout Lands fence run between two X positions.</summary>
    private static void Rail(CanvasItem canvas, float startX, float endX, float y)
    {
        for (var x = startX; x < endX; x += 16.0f)
            canvas.DrawTextureRect(FenceRail, new Rect2(x, y, 16.0f, 16.0f), false);
    }

    /// <summary>
    /// Roadside dressing. Positions come from a hash of the world X so the same course always grows
    /// the same trees, and nothing is placed on the track itself.
    /// </summary>
    private static void Scatter(CanvasItem canvas, RaceCourse course, RaceTrackLayout layout, bool behindTrack)
    {
        var from = -layout.ScreenWidth;
        var to = course.EndX + layout.ScreenWidth;
        for (var x = from; x < to; x += 34.0f)
        {
            var slot = (int)Math.Floor(x / 34.0f);
            var hash = unchecked(slot * 2654435761u) ^ (behindTrack ? 0x9E37u : 0x5BD1u);
            var pick = hash % 10u;
            var jitterX = (hash >> 5) % 21u - 10.0f;
            var jitterY = (hash >> 11) % 17u;

            // Water and cliffs own their own ground; leave those stretches clear. A tree is 48px
            // wide, so its far edge has to clear the feature too or it grows out of the river.
            if (IsFeature(course, x + jitterX) || IsFeature(course, x + jitterX + 48.0f))
                continue;

            if (behindTrack)
            {
                var y = 12.0f + jitterY;
                if (pick < 3u)
                    canvas.DrawTexture(Tree, new Vector2(x + jitterX, y));
                else if (pick < 6u)
                    canvas.DrawTexture(Bush, new Vector2(x + jitterX, y + 34.0f));
                else if (pick < 7u)
                    canvas.DrawTexture(Stone, new Vector2(x + jitterX, y + 44.0f));
                else if (pick < 9u)
                    canvas.DrawTextureRect(GrassFlowers, new Rect2(x + jitterX, y + 48.0f, 16.0f, 16.0f), false);
            }
            else
            {
                var y = layout.TrackBottom + 12.0f + jitterY;
                if (pick < 2u)
                    canvas.DrawTexture(Tree, new Vector2(x + jitterX, y + 24.0f));
                else if (pick < 5u)
                    canvas.DrawTexture(Bush, new Vector2(x + jitterX, y));
                else if (pick < 6u)
                    canvas.DrawTexture(Stone, new Vector2(x + jitterX, y + 20.0f));
                else if (pick < 8u)
                    canvas.DrawTextureRect(GrassFlowers, new Rect2(x + jitterX, y + 30.0f, 16.0f, 16.0f), false);
            }
        }
    }

    /// <summary>Ground that belongs to the track: water, cliff, clifftop or ramp. Nothing grows here.</summary>
    private static bool IsFeature(RaceCourse course, float x)
        => course.SegmentKindAt(x) is RaceSegmentKind.Swim or RaceSegmentKind.Climb or RaceSegmentKind.Glide ||
           SurfaceLift(course, x) > 0.0f ||
           OnLaunchRamp(course, x);

    private static void Tile(CanvasItem canvas, Texture2D texture, Rect2 rect)
        => canvas.DrawTextureRect(texture, rect, tile: true);

    private static float Smooth(float t)
    {
        t = Mathf.Clamp(t, 0.0f, 1.0f);
        return t * t * (3.0f - 2.0f * t);
    }

    private static Texture2D Load(string path)
        => GD.Load<Texture2D>(path) ?? throw new InvalidOperationException($"Race track art '{path}' is missing.");

    /// <summary>
    /// Cuts one region out of a tilesheet as a standalone texture. Standalone textures are what make
    /// tiled <c>DrawTextureRect</c> work: an AtlasTexture region repeats its whole atlas.
    /// </summary>
    private static Texture2D Slice(string path, int x, int y, int width, int height)
    {
        if (!SheetCache.TryGetValue(path, out var image))
        {
            image = Load(path).GetImage()
                ?? throw new InvalidOperationException($"Race track art '{path}' has no readable image.");
            if (image.IsCompressed())
                image.Decompress();
            SheetCache[path] = image;
        }

        return ImageTexture.CreateFromImage(image.GetRegion(new Rect2I(x, y, width, height)));
    }
}
