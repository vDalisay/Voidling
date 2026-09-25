using System;
using System.Collections.Generic;
using Godot;

namespace Voidling.Presentation.Racing;

/// <summary>
/// Per-racer terrain effects: the underwater look and wake of a swimmer, the splash going in and
/// out of the water, pebbles knocked loose on a cliff and the sparkle trail of a glider. All of it
/// reads the racer's already-resolved state and draws from the screen's own VFX stream, so none of
/// it can influence a result.
/// </summary>
public partial class RaceScreen
{
    private static readonly Shader UnderwaterShader =
        GD.Load<Shader>(RaceTrackLayers.ShaderRoot + "RaceUnderwater.gdshader");

    private const float SubmersionHalfWidth = 15.0f;
    private const float SubmersionDepth = 30.0f;
    private const float WakeSpacingPixels = 16.0f;

    private static readonly Color Foam = new(0.95f, 0.99f, 1.0f, 0.85f);
    private static readonly Color Pebble = Color.FromHtml("#8A7460");

    private static Texture2D? _bubbleTexture;
    private static Texture2D? _sparkTexture;
    private static Texture2D? _dotTexture;

    /// <summary>
    /// The water drawn over a swimmer's submerged body, a child of the sprite in frame-local units
    /// so it follows every body's scale. Its shader reads back the body beneath it.
    /// </summary>
    private static Polygon2D CreateSubmersion(int racerIndex)
    {
        var material = new ShaderMaterial { Shader = UnderwaterShader };
        material.SetShaderParameter("half_width", SubmersionHalfWidth);
        material.SetShaderParameter("depth", SubmersionDepth);
        material.SetShaderParameter("phase", racerIndex * 1.7f);

        var submersion = new Polygon2D
        {
            Name = "Submersion",
            Polygon = BuildSubmersionPolygon(),
            Color = Colors.White,
            Material = material,
            ZIndex = 5,
            Visible = false
        };

        // Broken foam where the body breaks the surface: the front arc bunched up by the swimmer's
        // push, the back arc thin and patchy.
        submersion.AddChild(new Line2D
        {
            Name = "Collar",
            Points = BuildArc(11.0f, 2.4f, -0.35f, 0.35f + Mathf.Pi * 0.5f),
            Width = 1.4f,
            DefaultColor = Foam
        });
        submersion.AddChild(new Line2D
        {
            Name = "CollarBack",
            Points = BuildArc(11.0f, 2.4f, Mathf.Pi * 0.75f, Mathf.Pi * 1.25f),
            Width = 1.0f,
            DefaultColor = new Color(1.0f, 1.0f, 1.0f, 0.45f)
        });

        submersion.AddChild(new CpuParticles2D
        {
            Name = "Bubbles",
            Texture = BubbleTexture,
            Amount = 5,
            Lifetime = 0.9,
            LocalCoords = false,
            Emitting = false,
            Position = new Vector2(0.0f, 12.0f),
            EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
            EmissionRectExtents = new Vector2(8.0f, 6.0f),
            Direction = Vector2.Up,
            Spread = 18.0f,
            Gravity = new Vector2(0.0f, -22.0f),
            InitialVelocityMin = 6.0f,
            InitialVelocityMax = 14.0f,
            ScaleAmountMin = 0.6f,
            ScaleAmountMax = 1.0f,
            Color = new Color(0.92f, 1.0f, 1.0f, 0.85f),
            ZIndex = 1
        });

        return submersion;
    }

    /// <summary>Knocked-loose grit that trickles down a cliff under a climber.</summary>
    private static CpuParticles2D CreatePebbles() => new()
    {
        Name = "Pebbles",
        Texture = DotTexture,
        Amount = 6,
        Lifetime = 0.7,
        LocalCoords = false,
        Emitting = false,
        Position = new Vector2(-4.0f, 10.0f),
        Direction = new Vector2(-0.3f, 1.0f),
        Spread = 25.0f,
        Gravity = new Vector2(0.0f, 320.0f),
        InitialVelocityMin = 10.0f,
        InitialVelocityMax = 30.0f,
        ScaleAmountMin = 1.0f,
        ScaleAmountMax = 1.8f,
        Color = Pebble,
        ZIndex = -1
    };

    /// <summary>Star-dust shed by a glider, left hanging in the air behind it.</summary>
    private static CpuParticles2D CreateSparkles()
    {
        var fade = new Gradient();
        fade.SetColor(0, new Color(1.0f, 0.97f, 0.70f, 1.0f));
        fade.SetColor(1, new Color(1.0f, 0.85f, 0.95f, 0.0f));
        return new CpuParticles2D
        {
            Name = "Sparkles",
            Texture = SparkTexture,
            Amount = 16,
            Lifetime = 0.8,
            LocalCoords = false,
            Emitting = false,
            Position = new Vector2(-8.0f, 4.0f),
            EmissionShape = CpuParticles2D.EmissionShapeEnum.Sphere,
            EmissionSphereRadius = 5.0f,
            Direction = Vector2.Left,
            Spread = 40.0f,
            Gravity = new Vector2(0.0f, 18.0f),
            InitialVelocityMin = 4.0f,
            InitialVelocityMax = 16.0f,
            ScaleAmountMin = 0.7f,
            ScaleAmountMax = 1.3f,
            ColorRamp = fade,
            ZIndex = -1
        };
    }

    /// <summary>
    /// Fires the one-off effects on a change of terrain and keeps the continuous ones in step with
    /// what the racer is doing this frame.
    /// </summary>
    private void UpdateTerrainEffects(RacerVisual visual, bool swimming, bool gliding, bool climbing, float drawX)
    {
        if (swimming != visual.WasSwimming)
        {
            SpawnSplash(new Vector2(drawX, WaterlineY(visual)), swimming ? 1.0f : 0.7f);
            visual.WakeDistance = 0.0f;
        }

        if (gliding && !visual.WasGliding)
            SpawnDust(visual, drawX, 1.3f);

        visual.WasSwimming = swimming;
        visual.WasGliding = gliding;

        visual.Pebbles.Emitting = climbing;
        visual.Sparkles.Emitting = gliding;
        if (visual.Submersion.GetNodeOrNull<CpuParticles2D>("Bubbles") is { } bubbles)
            bubbles.Emitting = swimming;
    }

    private static float WaterlineY(RacerVisual visual)
        => visual.Sprite.Position.Y + visual.Submersion.Position.Y * Math.Abs(visual.Sprite.Scale.Y);

    /// <summary>A widening ring left on the surface every few pixels a swimmer travels.</summary>
    private void HandleWake(RacerVisual visual, float drawX, bool swimming)
    {
        if (!swimming)
            return;

        var moved = Math.Abs(drawX - visual.WakeX);
        visual.WakeX = drawX;
        visual.WakeDistance += moved;
        if (visual.WakeDistance < WakeSpacingPixels)
            return;

        visual.WakeDistance = 0.0f;
        var ring = new Line2D
        {
            Points = BuildCollar(9.0f, 2.2f),
            Closed = true,
            Width = 1.0f,
            DefaultColor = new Color(1.0f, 1.0f, 1.0f, 0.30f),
            Position = new Vector2(drawX - 6.0f, WaterlineY(visual) + 1.0f),
            ZIndex = 8
        };
        AddChild(ring);

        // A V of foam trailing off behind, which is what makes the swim read as forward motion.
        var chevron = new Line2D
        {
            Points = new[] { new Vector2(-7.0f, -3.0f), new Vector2(0.0f, 0.0f), new Vector2(-7.0f, 3.0f) },
            Width = 1.0f,
            DefaultColor = new Color(1.0f, 1.0f, 1.0f, 0.32f),
            Position = new Vector2(drawX - 12.0f, WaterlineY(visual) + 1.0f),
            ZIndex = 8
        };
        AddChild(chevron);

        var spread = CreateTween().SetParallel(true);
        spread.TweenProperty(ring, "scale", new Vector2(2.4f, 2.0f), 0.9)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        spread.TweenProperty(ring, "modulate:a", 0.0f, 0.9);
        spread.TweenProperty(chevron, "scale", new Vector2(1.6f, 2.2f), 0.7)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        spread.TweenProperty(chevron, "modulate:a", 0.0f, 0.7);
        spread.Finished += ring.QueueFree;
        spread.Finished += chevron.QueueFree;
    }

    /// <summary>
    /// The premium splash animation where a racer hits or leaves the water, with a burst of droplets
    /// thrown up around it.
    /// </summary>
    private void SpawnSplash(Vector2 at, float force)
    {
        var frames = new SpriteFrames();
        frames.RemoveAnimation("default");
        frames.AddAnimation("splash");
        frames.SetAnimationLoop("splash", false);
        frames.SetAnimationSpeed("splash", 14.0);
        foreach (var frame in RaceTrackTiles.Splash)
            frames.AddFrame("splash", frame);

        var splash = new AnimatedSprite2D
        {
            SpriteFrames = frames,
            Position = at + new Vector2(0.0f, -4.0f),
            Scale = Vector2.One * (1.4f + force * 0.6f),
            ZIndex = 16
        };
        AddChild(splash);
        splash.Play("splash");
        splash.AnimationFinished += splash.QueueFree;

        var droplets = new CpuParticles2D
        {
            Texture = DotTexture,
            Amount = (int)(10 + force * 8),
            Lifetime = 0.6,
            OneShot = true,
            Explosiveness = 0.95f,
            LocalCoords = false,
            Position = at,
            EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
            EmissionRectExtents = new Vector2(8.0f, 2.0f),
            Direction = Vector2.Up,
            Spread = 55.0f,
            Gravity = new Vector2(0.0f, 300.0f),
            InitialVelocityMin = 40.0f * force,
            InitialVelocityMax = 95.0f * force,
            ScaleAmountMin = 1.0f,
            ScaleAmountMax = 2.0f,
            Color = new Color(0.90f, 0.98f, 1.0f, 0.95f),
            ZIndex = 17,
            Emitting = true
        };
        AddChild(droplets);
        droplets.Finished += droplets.QueueFree;

        // A flat ring on the surface where it went in.
        var ring = new Line2D
        {
            Points = BuildCollar(8.0f, 2.0f),
            Closed = true,
            Width = 1.4f,
            DefaultColor = Foam,
            Position = at,
            ZIndex = 8
        };
        AddChild(ring);
        var spread = CreateTween().SetParallel(true);
        spread.TweenProperty(ring, "scale", new Vector2(3.0f, 2.6f), 0.7)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        spread.TweenProperty(ring, "modulate:a", 0.0f, 0.7);
        spread.Finished += ring.QueueFree;
    }

    /// <summary>
    /// The water a swimmer sits in: a waterline curved over the shoulders, straight sides down. The
    /// shader softens the sides and bottom away, so the outline never shows.
    /// </summary>
    private static Vector2[] BuildSubmersionPolygon()
    {
        var points = new List<Vector2>();
        for (var i = 0; i <= 12; i++)
        {
            var t = i / 12.0f;
            var x = Mathf.Lerp(-SubmersionHalfWidth, SubmersionHalfWidth, t);
            points.Add(new Vector2(x, -Mathf.Sin(t * Mathf.Pi) * 3.0f));
        }
        points.Add(new Vector2(SubmersionHalfWidth, SubmersionDepth));
        points.Add(new Vector2(-SubmersionHalfWidth, SubmersionDepth));
        return points.ToArray();
    }

    private static Vector2[] BuildArc(float radiusX, float radiusY, float from, float to)
    {
        var points = new Vector2[10];
        for (var i = 0; i < points.Length; i++)
        {
            var angle = Mathf.Lerp(from, to, i / (points.Length - 1.0f));
            points[i] = new Vector2(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY);
        }
        return points;
    }

    private static Vector2[] BuildCollar(float radiusX, float radiusY)
    {
        var points = new Vector2[18];
        for (var i = 0; i < points.Length; i++)
        {
            var angle = Mathf.Tau * i / points.Length;
            points[i] = new Vector2(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY);
        }
        return points;
    }

    private static Texture2D BubbleTexture => _bubbleTexture ??= Pixels(new[]
    {
        ".X.",
        "X.X",
        ".X."
    });

    private static Texture2D SparkTexture => _sparkTexture ??= Pixels(new[]
    {
        "..X..",
        "..X..",
        "XXXXX",
        "..X..",
        "..X.."
    });

    private static Texture2D DotTexture => _dotTexture ??= Pixels(new[] { "XX", "XX" });

    private static Texture2D Pixels(IReadOnlyList<string> rows)
    {
        var image = Image.CreateEmpty(rows[0].Length, rows.Count, false, Image.Format.Rgba8);
        for (var y = 0; y < rows.Count; y++)
        {
            for (var x = 0; x < rows[y].Length; x++)
                image.SetPixel(x, y, rows[y][x] == 'X' ? Colors.White : Colors.Transparent);
        }

        return ImageTexture.CreateFromImage(image);
    }
}
