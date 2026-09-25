using System;
using System.Collections.Generic;
using Godot;
using Voidling.Domain.Racing;
using Voidling.Presentation.UI.Racing;

namespace Voidling.Presentation.Racing;

/// <summary>
/// The course's furniture and crowd: premium signposts announcing each stretch, premium fence
/// hurdles, and premium chickens watching from the verge like the Chao cheering at the stadium.
/// Everything is placed from the course alone, never from the race's random stream.
/// </summary>
internal static class RaceTrackFurniture
{
    private const string ChickenRoot = RaceTrackTiles.Premium + "Animals/Chicken/";

    private static readonly string[] ChickenSheets =
    {
        "chicken default.png",
        "chicken blue.png",
        "chicken brown.png",
        "chicken green.png",
        "chicken red.png"
    };

    private static readonly Dictionary<string, SpriteFrames> ChickenFrames = new(StringComparer.Ordinal);

    private static readonly Color GlyphInk = Color.FromHtml("#5B4C40");

    internal static void Build(Node2D owner, RaceCourse course, RaceTrackLayout layout)
    {
        owner.AddChild(new RaceTrackCanvas(canvas => PaintSignposts(canvas, course, layout))
        {
            Name = "Signposts",
            ZIndex = -8
        });

        var crowd = new RaceSpectators { Name = "Spectators" };
        owner.AddChild(crowd);
        AddCrowd(crowd, course, layout, course.StartX - 70.0f, 7, seed: 11);
        AddCrowd(crowd, course, layout, course.EndX - 90.0f, 8, seed: 29);
        foreach (var segment in course.Segments)
        {
            if (segment.Kind == RaceSegmentKind.Climb)
                AddCrowd(crowd, course, layout, segment.StartX + RaceTrackArt.WallSpan + 30.0f, 4, seed: (int)segment.StartX);
        }
    }

    /// <summary>
    /// A premium hurdle: a vertical fence run across all four lanes, standing on whatever surface
    /// the course has there.
    /// </summary>
    internal static void AddHurdle(Node2D owner, float x, RaceTrackLayout layout, float lift)
    {
        var top = layout.TrackTop + 6.0f;
        var count = (int)Math.Ceiling((layout.TrackBottom - 6.0f - top) / 16.0f);
        for (var i = 0; i < count; i++)
        {
            var texture = i == 0
                ? RaceTrackTiles.FenceVerticalTop
                : i == count - 1 ? RaceTrackTiles.FenceVerticalBottom : RaceTrackTiles.FenceVerticalMid;
            owner.AddChild(new Sprite2D
            {
                Texture = texture,
                Position = new Vector2(x, top + 8.0f + i * 16.0f - lift),
                ZIndex = 6
            });
        }

        // A soft shadow at its foot so it stands on the track.
        owner.AddChild(new Polygon2D
        {
            Polygon = new[]
            {
                new Vector2(x + 3.0f, top - lift + 4.0f),
                new Vector2(x + 7.0f, top - lift + 4.0f),
                new Vector2(x + 7.0f, layout.TrackBottom - lift - 6.0f),
                new Vector2(x + 3.0f, layout.TrackBottom - lift - 6.0f)
            },
            Color = new Color(0.10f, 0.13f, 0.11f, 0.12f),
            ZIndex = 5
        });
    }

    /// <summary>
    /// A blank premium signpost on the far verge before each stretch, carrying the stretch's glyph.
    /// </summary>
    private static void PaintSignposts(CanvasItem canvas, RaceCourse course, RaceTrackLayout layout)
    {
        foreach (var segment in course.Segments)
        {
            if (segment.Kind == RaceSegmentKind.Ground || segment.StartX <= course.StartX + 1.0f)
                continue;

            var x = segment.StartX - 34.0f;
            if (segment.Kind == RaceSegmentKind.Glide && course.HasGlideSegment)
                x = course.GlideLaunchStartX - 30.0f;
            Signpost(canvas, x, layout.TrackTop - RaceTrackArt.SurfaceLift(course, x), segment.Kind.ToString());
        }
    }

    private static void Signpost(CanvasItem canvas, float x, float groundY, string kind)
    {
        // Doubled, so the board reads at race zoom.
        var origin = new Vector2(Mathf.Round(x), Mathf.Round(groundY - 30.0f));
        canvas.DrawTextureRect(RaceTrackTiles.BlankSign, new Rect2(origin, new Vector2(32.0f, 32.0f)), false);
        canvas.DrawTextureRect(
            RaceSectionGlyphs.For(kind, GlyphInk),
            new Rect2(origin + new Vector2(7.0f, 4.0f), new Vector2(18.0f, 18.0f)),
            false);
    }

    private static void AddCrowd(RaceSpectators crowd, RaceCourse course, RaceTrackLayout layout, float centerX, int count, int seed)
    {
        for (var i = 0; i < count; i++)
        {
            var hash = unchecked((uint)((seed + 1) * 2654435761L + i * 40503));
            var farSide = i % 2 == 0;
            var x = centerX + i * 17.0f + hash % 7u;
            var lift = RaceTrackArt.SurfaceLift(course, x);
            var y = farSide
                ? layout.TrackTop - lift - 6.0f - (hash >> 5) % 8u
                : layout.TrackBottom + 14.0f + (hash >> 5) % 10u;

            var sprite = new AnimatedSprite2D
            {
                SpriteFrames = ChickenFramesFor(ChickenSheets[(int)((hash >> 9) % (uint)ChickenSheets.Length)]),
                Position = new Vector2(Mathf.Round(x), Mathf.Round(y)),
                FlipH = (hash >> 13) % 2u == 0u,
                ZIndex = farSide ? 4 : 31
            };
            crowd.Add(sprite, (hash >> 3) % 1000u / 1000.0f);
        }
    }

    private static SpriteFrames ChickenFramesFor(string sheet)
    {
        if (ChickenFrames.TryGetValue(sheet, out var frames))
            return frames;

        var texture = RaceTrackTiles.Load(ChickenRoot + sheet);
        frames = new SpriteFrames();
        frames.RemoveAnimation("default");
        frames.AddAnimation("idle");
        frames.SetAnimationSpeed("idle", 5.0);
        for (var i = 0; i < 4; i++)
            frames.AddFrame("idle", new AtlasTexture { Atlas = texture, Region = new Rect2(i * 16, 0, 16, 16) });

        frames.AddAnimation("cheer");
        frames.SetAnimationSpeed("cheer", 10.0);
        for (var i = 0; i < 8; i++)
            frames.AddFrame("cheer", new AtlasTexture { Atlas = texture, Region = new Rect2(i * 16, 416, 16, 16) });

        ChickenFrames[sheet] = frames;
        return frames;
    }
}

/// <summary>
/// The chickens watching the race. They bob on their own and hop and flap when the camera, which
/// follows the player's Voidling, comes past.
/// </summary>
internal sealed partial class RaceSpectators : Node2D
{
    private readonly List<(AnimatedSprite2D Sprite, float Phase, float BaseY)> _members = new();

    public void Add(AnimatedSprite2D sprite, float phase)
    {
        AddChild(sprite);
        sprite.Play("idle");
        sprite.Frame = (int)(phase * 4.0f);
        _members.Add((sprite, phase, sprite.Position.Y));
    }

    public override void _Process(double delta)
    {
        var camera = GetViewport().GetCamera2D();
        var focusX = camera?.GlobalPosition.X ?? 0.0f;
        var time = (float)(Time.GetTicksMsec() / 1000.0);

        foreach (var (sprite, phase, baseY) in _members)
        {
            var near = Math.Abs(sprite.Position.X - focusX) < 150.0f;
            var animation = near ? "cheer" : "idle";
            if (sprite.Animation != animation)
                sprite.Play(animation);

            var hop = 0.0f;
            if (near)
            {
                hop = Math.Abs(Mathf.Sin(time * 7.0f + phase * Mathf.Tau)) * 5.0f;
            }
            else if (Mathf.PosMod(time * 0.35f + phase, 1.0f) < 0.12f)
            {
                hop = Math.Abs(Mathf.Sin(time * 9.0f)) * 3.0f;
            }

            sprite.Position = new Vector2(sprite.Position.X, Mathf.Round(baseY - hop));
        }
    }
}
