using System;
using System.Collections.Generic;
using Godot;

namespace Voidling.Presentation.Garden.Atmosphere;

/// <summary>
/// What the weather looks like on the island: rain streaks in two depths angled by the wind, drops
/// splashing on the grass, puddles that fill while it rains and dry out afterwards, drifting cloud
/// shadows, and lightning in a storm. Rings on the sea itself are drawn by the sea shader.
///
/// The camera-bound parts (rain, cloud shadows) are reparented under the Garden camera so they stay
/// in view without being pinned to the screen: they are sampled in world space.
/// </summary>
public partial class GardenWeather : Node2D
{
    private const float PuddleFillSeconds = 30.0f;
    private const float PuddleDrySeconds = 150.0f;
    private const int MaxSplashes = 90;

    private static readonly Shader PuddleShader = GD.Load<Shader>(GardenAtmosphereAssets.ShaderRoot + "GardenPuddle.gdshader");
    private static readonly Shader CloudShader = GD.Load<Shader>(GardenAtmosphereAssets.ShaderRoot + "GardenCloudShadows.gdshader");

    private static Texture2D[]? _puddleTextures;

    private readonly RandomNumberGenerator _random = new() { Seed = 0x4A1D };
    private readonly List<Splash> _splashes = new();
    private readonly List<Rect2> _puddleRects = new();

    private CpuParticles2D _nearRain = null!;
    private CpuParticles2D _farRain = null!;
    private CpuParticles2D _downpour = null!;
    private Node2D _cloudShadows = null!;
    private ShaderMaterial _cloudMaterial = null!;
    private ShaderMaterial _puddleMaterial = null!;
    private Node2D _puddleRoot = null!;
    private GardenIslandField _field = GardenIslandField.Empty;
    private Camera2D? _camera;
    private float _splashCredit;
    private float _wetness;
    private double _lightningCountdown = 8.0;
    private double _flashSeconds = -1.0;

    private struct Splash
    {
        public Vector2 Position;
        public float Age;
        public bool InPuddle;
    }

    private static Texture2D[] PuddleTextures => _puddleTextures ??= new[]
    {
        GardenAtmosphereAssets.Slice(GardenAtmosphereAssets.Premium + "Objects/Water Objects.png", 0, 16, 16, 16),
        GardenAtmosphereAssets.Slice(GardenAtmosphereAssets.Premium + "Objects/Water Objects.png", 16, 16, 16, 16),
        GardenAtmosphereAssets.Slice(GardenAtmosphereAssets.Premium + "Objects/Water Objects.png", 32, 16, 16, 16),
        GardenAtmosphereAssets.Slice(GardenAtmosphereAssets.Premium + "Objects/Water Objects.png", 80, 16, 16, 16)
    };

    /// <summary>0..1 lightning brightness this frame, read by the lighting.</summary>
    public float Flash { get; private set; }

    /// <summary>How wet the ground is, 0..1: fills with rain, dries slowly after.</summary>
    public float Wetness => _wetness;

    public override void _Ready()
    {
        ZIndex = -2;
        _puddleMaterial = new ShaderMaterial { Shader = PuddleShader };
        _puddleRoot = new Node2D { Name = "Puddles", ZIndex = -1 };
        AddChild(_puddleRoot);

        _nearRain = CreateRain("NearRain", 420, new Vector2(1.0f, 1.6f), 300.0f, new Color(0.84f, 0.91f, 0.98f, 0.78f));
        _farRain = CreateRain("FarRain", 220, new Vector2(0.55f, 0.85f), 210.0f, new Color(0.72f, 0.80f, 0.90f, 0.34f));
        _downpour = CreateRain("Downpour", 300, new Vector2(1.0f, 1.5f), 380.0f, new Color(0.84f, 0.90f, 0.98f, 0.55f));

        _cloudMaterial = new ShaderMaterial { Shader = CloudShader };
        _cloudShadows = new GardenAtmosphereCanvas(canvas => canvas.DrawRect(new Rect2(-560, -340, 1120, 680), Colors.White))
        {
            Name = "CloudShadows",
            ZIndex = 35,
            Material = _cloudMaterial
        };
    }

    public void SetIsland(GardenIslandField field, IReadOnlyList<Vector2> hexCenters, float hexInnerRadius, Func<Vector2, bool> clear)
    {
        _field = field;
        foreach (var child in _puddleRoot.GetChildren())
            child.QueueFree();
        _puddleRects.Clear();

        foreach (var center in hexCenters)
        {
            var seed = unchecked((ulong)((long)(center.X * 131.0f) ^ (long)(center.Y * 977.0f) ^ 0x9DD1E));
            var rng = new RandomNumberGenerator { Seed = seed };
            var count = rng.RandiRange(1, 3);
            for (var i = 0; i < count; i++)
            {
                var spot = (center + Vector2.Right.Rotated(rng.RandfRange(0.0f, Mathf.Tau)) *
                            hexInnerRadius * rng.RandfRange(0.25f, 0.75f)).Round();
                if (!clear(spot))
                    continue;
                var texture = PuddleTextures[rng.RandiRange(0, PuddleTextures.Length - 1)];
                _puddleRoot.AddChild(new Sprite2D { Texture = texture, Position = spot, Material = _puddleMaterial });
                _puddleRects.Add(new Rect2(spot - texture.GetSize() * 0.5f, texture.GetSize()));
            }
        }
    }

    /// <summary>Jumps straight to the wetness this weather would leave, for previews.</summary>
    public void Settle(GardenWeatherState weather) => _wetness = Mathf.Clamp(weather.Rain * 1.4f, 0.0f, 1.0f);

    public void Advance(float delta, GardenWeatherState weather)
    {

        var rain = weather.Rain;
        _wetness = rain > 0.2f
            ? Mathf.MoveToward(_wetness, 1.0f, delta / PuddleFillSeconds * rain)
            : Mathf.MoveToward(_wetness, 0.0f, delta / PuddleDrySeconds);
        _puddleMaterial.SetShaderParameter("wetness", _wetness);
        _puddleMaterial.SetShaderParameter("rain", rain);

        UpdateRain(rain, weather.Wind);
        UpdateClouds(weather);
        UpdateSplashes(delta, rain);
        UpdateLightning(delta, weather.Storm);
        QueueRedraw();
    }

    public override void _Draw()
    {
        // Splash crowns: a drop hits, throws up a little crown, and the droplets fall away.
        foreach (var splash in _splashes)
        {
            var p = splash.Position;
            var alpha = 1.0f - splash.Age / 0.32f;
            var color = new Color(0.90f, 0.95f, 1.0f, 0.85f * alpha);
            if (splash.InPuddle)
            {
                var radius = 1.0f + splash.Age * 14.0f;
                DrawArc(p, radius, 0.0f, Mathf.Tau, 10, color, 1.0f);
                continue;
            }

            if (splash.Age < 0.08f)
            {
                DrawRect(new Rect2(p.X - 1.0f, p.Y, 3.0f, 1.0f), color);
            }
            else if (splash.Age < 0.18f)
            {
                DrawRect(new Rect2(p.X - 2.0f, p.Y - 2.0f, 1.0f, 2.0f), color);
                DrawRect(new Rect2(p.X, p.Y - 3.0f, 1.0f, 2.0f), color);
                DrawRect(new Rect2(p.X + 2.0f, p.Y - 2.0f, 1.0f, 2.0f), color);
            }
            else
            {
                DrawRect(new Rect2(p.X - 3.0f, p.Y - 1.0f, 1.0f, 1.0f), color);
                DrawRect(new Rect2(p.X + 3.0f, p.Y - 1.0f, 1.0f, 1.0f), color);
            }
        }
    }

    /// <summary>Rain and cloud shadows cover the view, so they ride on the Garden camera.</summary>
    public void UseCamera(Camera2D camera)
    {
        _camera = camera;
        foreach (var node in new Node[] { _nearRain, _farRain, _downpour, _cloudShadows })
        {
            if (node.GetParent() is { } parent)
                parent.RemoveChild(node);
            camera.AddChild(node);
        }
    }

    private void UpdateRain(float rain, float wind)
    {
        // CPU particles cannot thin out without restarting, so light rain is the two steady layers
        // fading in, and a storm adds a third, faster downpour on top.
        var view = ViewSize();
        foreach (var particles in new[] { _nearRain, _farRain, _downpour })
        {
            var level = particles == _downpour ? Mathf.Clamp((rain - 0.8f) * 5.0f, 0.0f, 1.0f) : Mathf.Clamp(rain * 1.3f, 0.0f, 1.0f);
            particles.Emitting = level > 0.02f;
            particles.Modulate = new Color(1.0f, 1.0f, 1.0f, level);
            particles.EmissionRectExtents = view * 0.5f + new Vector2(40.0f, 40.0f);
            particles.Direction = new Vector2(0.18f + wind * 0.32f, 1.0f).Normalized();
        }
    }

    private void UpdateClouds(GardenWeatherState weather)
    {
        _cloudMaterial.SetShaderParameter("cover", Mathf.Clamp(0.15f + weather.Cloud * 0.8f, 0.0f, 1.0f));
        _cloudMaterial.SetShaderParameter("strength", 0.07f + weather.Cloud * 0.08f);
        _cloudMaterial.SetShaderParameter("drift", new Vector2(6.0f + weather.Wind * 16.0f, 2.0f + weather.Wind * 4.0f));
        if (_camera != null)
            _cloudShadows.Scale = Vector2.One / Mathf.Max(0.1f, _camera.Zoom.X);
    }

    private void UpdateSplashes(float delta, float rain)
    {
        for (var i = _splashes.Count - 1; i >= 0; i--)
        {
            var splash = _splashes[i];
            splash.Age += delta;
            if (splash.Age > 0.32f)
                _splashes.RemoveAt(i);
            else
                _splashes[i] = splash;
        }

        if (rain <= 0.02f || _camera == null)
            return;

        var view = ViewSize();
        var origin = _camera.GetScreenCenterPosition() - view * 0.5f;
        _splashCredit += delta * rain * 140.0f;
        while (_splashCredit >= 1.0f && _splashes.Count < MaxSplashes)
        {
            _splashCredit -= 1.0f;
            var point = (origin + new Vector2(_random.Randf() * view.X, _random.Randf() * view.Y)).Round();
            if (_field.SignedDistance(point.X, point.Y) > -3.0f)
                continue;
            _splashes.Add(new Splash { Position = point, InPuddle = InPuddle(point) && _wetness > 0.3f });
        }
        _splashCredit = Mathf.Min(_splashCredit, 4.0f);
    }

    private void UpdateLightning(float delta, float storm)
    {
        if (_flashSeconds >= 0.0)
        {
            _flashSeconds += delta;
            // Two strikes in quick succession, then a slow fade.
            var t = (float)_flashSeconds;
            Flash = t < 0.06f ? 0.75f : t < 0.14f ? 0.15f : t < 0.22f ? 0.55f : Mathf.Max(0.0f, 0.55f - (t - 0.22f) * 1.3f);
            if (Flash <= 0.0f)
                _flashSeconds = -1.0;
            return;
        }

        Flash = 0.0f;
        if (storm < 0.5f)
            return;

        _lightningCountdown -= delta;
        if (_lightningCountdown > 0.0)
            return;

        _lightningCountdown = _random.RandfRange(9.0f, 22.0f);
        _flashSeconds = 0.0;
    }

    private bool InPuddle(Vector2 point)
    {
        foreach (var rect in _puddleRects)
        {
            if (rect.HasPoint(point))
                return true;
        }
        return false;
    }

    private Vector2 ViewSize()
    {
        var zoom = _camera?.Zoom.X ?? 1.0f;
        return GetViewportRect().Size / Mathf.Max(0.1f, zoom);
    }

    private static CpuParticles2D CreateRain(string name, int amount, Vector2 scale, float speed, Color color)
    {
        var fade = new Gradient();
        fade.SetColor(0, new Color(color, 0.0f));
        fade.SetColor(1, new Color(color, 0.0f));
        fade.AddPoint(0.2f, color);
        fade.AddPoint(0.8f, color);
        return new CpuParticles2D
        {
            Name = name,
            Texture = RainStreak,
            Amount = amount,
            Lifetime = 0.45,
            Preprocess = 0.5,
            LocalCoords = false,
            Emitting = false,
            EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
            EmissionRectExtents = new Vector2(360, 220),
            Direction = new Vector2(0.25f, 1.0f).Normalized(),
            Spread = 2.0f,
            Gravity = Vector2.Zero,
            InitialVelocityMin = speed * 0.85f,
            InitialVelocityMax = speed * 1.15f,
            ParticleFlagAlignY = true,
            ScaleAmountMin = scale.X,
            ScaleAmountMax = scale.Y,
            ColorRamp = fade,
            ZIndex = 50
        };
    }

    private static Texture2D? _rainStreak;

    private static Texture2D RainStreak => _rainStreak ??= GardenAtmosphereAssets.Pixels(":", "x", "x", "X", "X", "X", "X");
}

/// <summary>A node that draws itself once with a supplied painter; used for shader-driven overlays.</summary>
internal sealed partial class GardenAtmosphereCanvas : Node2D
{
    private readonly Action<CanvasItem> _paint;

    public GardenAtmosphereCanvas()
        : this(_ => { })
    {
    }

    public GardenAtmosphereCanvas(Action<CanvasItem> paint) => _paint = paint;

    public override void _Draw() => _paint(this);
}
