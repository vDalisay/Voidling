using System;
using System.Collections.Generic;
using Godot;
using Voidling.Domain.Racing;

namespace Voidling.Presentation.Racing;

/// <summary>
/// One static painted layer of the track. The course never changes during a race, so each layer
/// draws once and is cached by the renderer; only the shader-driven layers animate.
/// </summary>
internal sealed partial class RaceTrackCanvas : Node2D
{
    private readonly Action<CanvasItem> _paint;

    public RaceTrackCanvas()
        : this(_ => { })
    {
    }

    public RaceTrackCanvas(Action<CanvasItem> paint)
    {
        _paint = paint;
        TextureRepeat = TextureRepeatEnum.Enabled;
    }

    public override void _Draw() => _paint(this);
}

/// <summary>
/// Builds the track's layers under the race screen: land, water, the things in and over the
/// water, and the roadside foliage. Z order keeps them all below the racers (z 5 and up).
/// </summary>
internal static class RaceTrackLayers
{
    internal const string ShaderRoot = "res://Resources/Presentation/Racing/";

    private static readonly Shader WaterShader = GD.Load<Shader>(ShaderRoot + "RaceWater.gdshader");
    private static readonly Shader SwayShader = GD.Load<Shader>(ShaderRoot + "RaceFoliageSway.gdshader");
    private static readonly Shader CloudShader = GD.Load<Shader>(ShaderRoot + "RaceCloudShadows.gdshader");

    internal static void Build(Node2D owner, RaceCourse course, RaceTrackLayout layout)
    {
        owner.AddChild(new RaceTrackCanvas(canvas => RaceTrackArt.PaintBack(canvas, course, layout))
        {
            Name = "TrackBack",
            ZIndex = -20
        });

        foreach (var segment in course.Segments)
        {
            if (RaceScreen.VisualFor(segment.Kind).Water)
                owner.AddChild(CreateWater(course, segment, layout));
        }

        owner.AddChild(new RaceTrackCanvas(canvas => RaceTrackArt.PaintFront(canvas, course, layout))
        {
            Name = "TrackFront",
            ZIndex = -10
        });

        var sway = new ShaderMaterial { Shader = SwayShader };
        owner.AddChild(new RaceTrackCanvas(canvas => RaceTrackArt.PaintFoliage(canvas, course, layout, behindTrack: true))
        {
            Name = "FoliageBehind",
            ZIndex = -9,
            Material = sway
        });
        owner.AddChild(new RaceTrackCanvas(canvas => RaceTrackArt.PaintFoliage(canvas, course, layout, behindTrack: false))
        {
            Name = "FoliageFront",
            ZIndex = 30,
            Material = sway
        });
    }

    /// <summary>
    /// Weather that travels with the camera: cloud shadows sliding over the course and petals and
    /// leaves blowing through the frame. Both live in world space, so they drift past as the camera
    /// follows the race rather than being pinned to the screen.
    /// </summary>
    internal static void AttachAmbience(Camera2D camera, RaceTrackLayout layout)
    {
        var halfView = new Vector2(layout.ScreenWidth, layout.ScreenHeight) * 0.5f;
        var overscan = new Vector2(24.0f, 24.0f);
        camera.AddChild(new RaceTrackCanvas(canvas => canvas.DrawRect(new Rect2(-halfView - overscan, (halfView + overscan) * 2.0f), Colors.White))
        {
            Name = "CloudShadows",
            ZIndex = 35,
            Material = new ShaderMaterial { Shader = CloudShader }
        });

        var colors = new Gradient();
        colors.SetColor(0, Color.FromHtml("#F4B6C2"));
        colors.SetColor(1, Color.FromHtml("#9CCB6B"));
        colors.AddPoint(0.5f, Color.FromHtml("#FFF2C4"));
        camera.AddChild(new CpuParticles2D
        {
            Name = "Petals",
            Amount = 18,
            Lifetime = 7.0,
            Preprocess = 7.0,
            LocalCoords = false,
            Position = new Vector2(halfView.X * 0.3f, -halfView.Y - 10.0f),
            EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
            EmissionRectExtents = new Vector2(halfView.X * 1.3f, 4.0f),
            Direction = new Vector2(-1.0f, 0.6f),
            Spread = 20.0f,
            Gravity = new Vector2(-6.0f, 14.0f),
            InitialVelocityMin = 14.0f,
            InitialVelocityMax = 30.0f,
            AngularVelocityMin = -90.0f,
            AngularVelocityMax = 90.0f,
            ScaleAmountMin = 1.0f,
            ScaleAmountMax = 2.0f,
            ColorInitialRamp = colors,
            ZIndex = 36
        });
    }

    /// <summary>
    /// A river crossing the course, or the lake under the glide: the shader-driven surface, then
    /// premium lily pads, reeds and stones kept clear of the lanes, then fish drifting underneath.
    /// </summary>
    private static Node2D CreateWater(RaceCourse course, RaceCourseSegment segment, RaceTrackLayout layout)
    {
        var lake = segment.Kind == RaceSegmentKind.Glide;
        var root = new Node2D { Name = "Water_" + segment.Id, ZIndex = -15 };

        var material = new ShaderMaterial { Shader = WaterShader };
        material.SetShaderParameter("water_tile", RaceTrackTiles.Water[0]);
        material.SetShaderParameter("bank_left", segment.StartX);
        material.SetShaderParameter("bank_right", segment.EndX);
        material.SetShaderParameter("two_banks", 1.0f);
        material.SetShaderParameter("flow", lake ? new Vector2(5.0f, 2.0f) : new Vector2(0.0f, 18.0f));

        var body = new Rect2(segment.StartX, 0.0f, segment.EndX - segment.StartX, layout.ScreenHeight);
        root.AddChild(new RaceTrackCanvas(canvas => canvas.DrawRect(body, Colors.White))
        {
            Name = "Surface",
            Material = material
        });

        root.AddChild(new RaceWaterFish(segment, layout, lake) { Name = "Fish" });
        root.AddChild(new RaceTrackCanvas(canvas => PaintWaterDressing(canvas, segment, layout))
        {
            Name = "Dressing"
        });
        return root;
    }

    private static void PaintWaterDressing(CanvasItem canvas, RaceCourseSegment segment, RaceTrackLayout layout)
    {
        // Reeds hug the shores; pads and stones float further out. None of them sit in the lanes.
        for (var y = 4.0f; y < layout.ScreenHeight - 12.0f; y += 13.0f)
        {
            if (y + 16.0f > layout.TrackTop - 6.0f && y < layout.TrackBottom + 6.0f)
                continue;

            var hash = Hash(segment.StartX, y);
            if (hash % 3u == 0u)
                canvas.DrawTexture(Pick(RaceTrackTiles.Reeds, hash >> 4), new Vector2(segment.StartX + 2.0f + hash % 5u, y));
            if ((hash >> 7) % 3u == 0u)
                canvas.DrawTexture(Pick(RaceTrackTiles.Reeds, hash >> 9), new Vector2(segment.EndX - 18.0f - (hash >> 2) % 5u, y + 5.0f));

            var span = segment.EndX - segment.StartX - 60.0f;
            if (span <= 0.0f)
                continue;

            if ((hash >> 12) % 4u == 0u)
                canvas.DrawTexture(Pick(RaceTrackTiles.LilyPads, hash >> 14), new Vector2(segment.StartX + 30.0f + (hash >> 3) % (uint)span, y));
            else if ((hash >> 12) % 9u == 1u)
                canvas.DrawTexture(Pick(RaceTrackTiles.WaterRocks, hash >> 14), new Vector2(segment.StartX + 30.0f + (hash >> 5) % (uint)span, y));
        }
    }

    private static Texture2D Pick(IReadOnlyList<Texture2D> textures, uint hash)
        => textures[(int)(hash % (uint)textures.Count)];

    private static uint Hash(float x, float y)
    {
        unchecked
        {
            var hash = (uint)(int)Math.Floor(x) * 2654435761u ^ (uint)(int)Math.Floor(y) * 40503u;
            hash ^= hash >> 15;
            hash *= 2246822519u;
            return hash ^ (hash >> 13);
        }
    }
}

/// <summary>
/// Dark fish silhouettes from the premium ocean sheet drifting under the surface. They wander on
/// their own clock, which is presentation-only and never reaches the race.
/// </summary>
internal sealed partial class RaceWaterFish : Node2D
{
    private readonly RaceCourseSegment _segment;
    private readonly RaceTrackLayout _layout;
    private readonly bool _lake;
    private readonly List<(Sprite2D Sprite, float Speed, float Phase, float BaseY)> _fish = new();

    public RaceWaterFish()
        : this(new RaceCourseSegment("none", 0.0f, 1.0f, RaceSegmentKind.Swim), default, lake: false)
    {
    }

    public RaceWaterFish(RaceCourseSegment segment, RaceTrackLayout layout, bool lake)
    {
        _segment = segment;
        _layout = layout;
        _lake = lake;
    }

    public override void _Ready()
    {
        var width = _segment.EndX - _segment.StartX;
        var count = Math.Clamp((int)(width / 70.0f), 2, 6);
        for (var i = 0; i < count; i++)
        {
            var seed = unchecked((uint)(i * 747796405 + (int)_segment.StartX * 2891336453));
            var sprite = new Sprite2D
            {
                Texture = RaceTrackTiles.Fish[(int)(seed % (uint)RaceTrackTiles.Fish.Length)],
                Modulate = new Color(0.10f, 0.22f, 0.26f, 0.28f),
                Scale = Vector2.One * (0.8f + (seed >> 8) % 5u * 0.1f)
            };
            AddChild(sprite);
            var baseY = 20.0f + (seed >> 4) % (uint)Math.Max(1.0f, _layout.ScreenHeight - 40.0f);
            _fish.Add((sprite, 6.0f + (seed >> 12) % 9u, (seed >> 16) % 100u / 100.0f * Mathf.Tau, baseY));
        }
    }

    public override void _Process(double delta)
    {
        var width = Math.Max(1.0f, _segment.EndX - _segment.StartX - 24.0f);
        var time = (float)(Time.GetTicksMsec() / 1000.0);
        foreach (var (sprite, speed, phase, baseY) in _fish)
        {
            // Back and forth across the channel, turning at each end.
            var travel = Mathf.PingPong(time * speed + phase * 40.0f, width);
            var heading = Mathf.Sin((time * speed + phase * 40.0f) / width * Mathf.Pi) >= 0.0f;
            var drift = _lake ? time * 3.0f : time * 10.0f;
            var y = Mathf.PosMod(baseY + drift + Mathf.Sin(time * 0.9f + phase) * 6.0f, _layout.ScreenHeight);
            sprite.Position = new Vector2(Mathf.Round(_segment.StartX + 12.0f + travel), Mathf.Round(y));
            sprite.FlipH = !heading;
        }
    }
}
