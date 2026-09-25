using System;
using System.Collections.Generic;
using Godot;
using Voidling.Presentation.Lighting;

namespace Voidling.Presentation.Garden.Atmosphere;

/// <summary>
/// The Garden's light. Ambient colour is a <see cref="CanvasModulate"/>, so it darkens the world
/// while real lights add on top of it: the sun (or moon) as a directional light, and fireflies,
/// glowing mushrooms and halos as point lights. Voidlings, trees and mushrooms carry generated normal
/// maps, so the edges turned towards a light catch it and the far edges fall into shade.
///
/// The sun's share of the light is taken back out of the ambient colour, so anything without a
/// normal map (the ground, the sea) keeps exactly the brightness the palette gives it. Lit sprites
/// are measured the same way in their shader (BalancedKeyLight): whatever faces the viewer is lit
/// like the ground, so a Voidling keeps the colours it was drawn with and only its edges change.
/// </summary>
public partial class GardenLighting : Node2D
{
    private const int FireflyCount = 18;
    private const int FireflyLightEvery = 3;

    private static readonly Color FireflyCore = new(0.93f, 1.0f, 0.55f);
    private static readonly Color FireflyGlow = new(0.72f, 0.95f, 0.35f);
    private static readonly Color MushroomGlow = new(0.62f, 0.52f, 1.0f);
    private static readonly Color HaloGlow = new(1.0f, 0.88f, 0.58f);

    private static Texture2D? _lightTexture;
    private static Texture2D? _glowTexture;
    private static Texture2D[]? _mushroomTextures;

    private readonly List<Firefly> _fireflies = new();
    private readonly List<Mushroom> _mushrooms = new();
    private readonly Dictionary<string, (PointLight2D Light, Func<Vector2> Anchor)> _halos = new(StringComparer.Ordinal);
    private readonly RandomNumberGenerator _random = new() { Seed = 0x6A7D3E };

    private CanvasModulate _ambient = null!;
    private DirectionalLight2D _sun = null!;
    private Color _sunColor = Colors.White;
    private float _sunEnergy;
    private Node2D _fireflyRoot = null!;
    private Color _ambientColor = Colors.White;
    private Rect2 _islandBounds = new(0, 0, 1, 1);
    private GardenIslandField _field = GardenIslandField.Empty;
    private float _fireflyLevel;
    private float _glowLevel;

    private sealed class Firefly
    {
        public Node2D Node { get; init; } = null!;
        public Sprite2D Glow { get; init; } = null!;
        public PointLight2D? Light { get; init; }
        public Vector2 Anchor { get; set; }
        public Vector2 Radius { get; init; }
        public Vector2 Speed { get; init; }
        public float Phase { get; init; }
    }

    private sealed class Mushroom
    {
        public Node2D Node { get; init; } = null!;
        public Sprite2D Glow { get; init; } = null!;
        public PointLight2D Light { get; init; } = null!;
        public float Phase { get; init; }
    }

    internal static Texture2D LightTexture => _lightTexture ??= GardenAtmosphereAssets.BandedLight(96, 6);
    internal static Texture2D GlowTexture => _glowTexture ??= GardenAtmosphereAssets.BandedLight(24, 4);

    private static Texture2D[] MushroomTextures => _mushroomTextures ??= new[]
    {
        SpriteNormalMaps.Lit(GardenAtmosphereAssets.Slice(GardenAtmosphereAssets.Premium + "Objects/Mushrooms, Flowers, Stones.png", 48, 0, 16, 16)),
        SpriteNormalMaps.Lit(GardenAtmosphereAssets.Slice(GardenAtmosphereAssets.Premium + "Objects/Mushrooms, Flowers, Stones.png", 64, 0, 16, 16)),
        SpriteNormalMaps.Lit(GardenAtmosphereAssets.Slice(GardenAtmosphereAssets.Premium + "Objects/Mushrooms, Flowers, Stones.png", 80, 0, 16, 16))
    };

    public override void _Ready()
    {
        _ambient = new CanvasModulate { Name = "Ambient", Color = Colors.White };
        AddChild(_ambient);
        _sun = new DirectionalLight2D
        {
            Name = "Sun",
            Energy = 0.0f,
            Height = 0.55f,
            BlendMode = Light2D.BlendModeEnum.Add,
            Enabled = false
        };
        AddChild(_sun);
        _fireflyRoot = new Node2D { Name = "Fireflies", ZIndex = 40 };
        AddChild(_fireflyRoot);

        for (var i = 0; i < FireflyCount; i++)
            _fireflies.Add(CreateFirefly(i));
    }

    /// <summary>The ambient colour everything unlit is multiplied by, eased towards each frame.</summary>
    public Color AmbientTarget { get; set; } = Colors.White;

    /// <summary>A brief white flash added over the ambient light, for lightning.</summary>
    public float Flash { get; set; }

    /// <summary>
    /// False when the Garden tint setting is off: no sun, no light pools, just the artwork's colours.
    /// </summary>
    public bool LightsEnabled { get; set; } = true;

    /// <summary>
    /// Points the sun (or the moon, at night). <paramref name="travelDegrees"/> is the direction the
    /// light travels, clockwise from straight down the screen; <paramref name="height"/> runs from 0
    /// (grazing) to 1 (overhead).
    /// </summary>
    public void SetSun(Color color, float energy, float travelDegrees, float height)
    {
        _sunColor = color;
        _sunEnergy = LightsEnabled ? Math.Max(0.0f, energy) : 0.0f;
        _sun.Color = color;
        _sun.Energy = _sunEnergy;
        _sun.Enabled = _sunEnergy > 0.001f;
        _sun.RotationDegrees = travelDegrees;
        _sun.Height = Math.Clamp(height, 0.05f, 1.0f);
    }

    public void SetIsland(GardenIslandField field, Rect2 islandBounds)
    {
        _field = field;
        _islandBounds = islandBounds;
        foreach (var firefly in _fireflies)
            firefly.Anchor = RandomLandPoint();
    }

    /// <summary>
    /// Replaces the island's glowing mushrooms. They live with the actors, like the trees, so a
    /// Voidling behind one is drawn behind it.
    /// </summary>
    public void SetMushrooms(Node2D actorsRoot, IEnumerable<Vector2> spots)
    {
        foreach (var mushroom in _mushrooms)
        {
            if (GodotObject.IsInstanceValid(mushroom.Node))
                mushroom.Node.QueueFree();
        }
        _mushrooms.Clear();

        var index = 0;
        foreach (var spot in spots)
        {
            var node = new Node2D { Name = "GlowMushroom", Position = spot };
            node.AddChild(new Sprite2D
            {
                Texture = MushroomTextures[index % MushroomTextures.Length],
                Material = SpriteNormalMaps.LitMaterial,
                Position = new Vector2(0.0f, -6.0f),
                ZIndex = 2
            });
            var glow = new Sprite2D
            {
                Texture = GlowTexture,
                Position = new Vector2(0.0f, -6.0f),
                Scale = Vector2.One * 1.6f,
                Modulate = new Color(MushroomGlow, 0.0f),
                Material = AdditiveMaterial,
                ZIndex = 3
            };
            node.AddChild(glow);
            var light = new PointLight2D
            {
                Texture = LightTexture,
                TextureScale = 1.25f,
                Color = MushroomGlow,
                Energy = 0.0f,
                Height = 14.0f,
                Position = new Vector2(0.0f, -6.0f),
                Enabled = false
            };
            node.AddChild(light);
            actorsRoot.AddChild(node);
            _mushrooms.Add(new Mushroom { Node = node, Glow = glow, Light = light, Phase = index * 1.37f });
            index++;
        }
    }

    /// <summary>
    /// A soft light that follows an angel-halo Voidling after dark. <paramref name="anchor"/> is the
    /// point the halo floats over.
    /// </summary>
    public void TrackHalo(string id, Func<Vector2> anchor)
    {
        UntrackHalo(id);
        var light = new PointLight2D
        {
            Name = "Halo_" + id,
            Texture = LightTexture,
            TextureScale = 0.9f,
            Color = HaloGlow,
            Height = 22.0f,
            Energy = 0.0f,
            Enabled = false
        };
        AddChild(light);
        _halos[id] = (light, anchor);
    }

    public void UntrackHalo(string id)
    {
        if (_halos.Remove(id, out var halo) && GodotObject.IsInstanceValid(halo.Light))
            halo.Light.QueueFree();
    }

    /// <summary>
    /// Updates the light for this frame. <paramref name="fireflies"/> and <paramref name="glow"/> are
    /// 0..1 levels chosen by the Garden's hour and weather.
    /// </summary>
    public void Advance(float delta, float fireflies, float glow)
    {
        // The sun adds its share to every surface facing it, so the ambient gives that share back.
        var sunShare = _sunColor * _sunEnergy;
        var ambient = new Color(
            Math.Max(AmbientTarget.R - sunShare.R, AmbientTarget.R * 0.35f),
            Math.Max(AmbientTarget.G - sunShare.G, AmbientTarget.G * 0.35f),
            Math.Max(AmbientTarget.B - sunShare.B, AmbientTarget.B * 0.35f));
        var target = ambient.Lerp(new Color(0.95f, 0.97f, 1.0f), Math.Clamp(Flash, 0.0f, 1.0f));
        _ambientColor = Flash > 0.01f ? target : _ambientColor.Lerp(target, 1.0f - Mathf.Exp(-2.5f * delta));
        _ambient.Color = _ambientColor;

        _fireflyLevel = Mathf.MoveToward(_fireflyLevel, fireflies, delta * 0.35f);
        _glowLevel = Mathf.MoveToward(_glowLevel, LightsEnabled ? glow : 0.0f, delta * 0.35f);
        var time = (float)(Time.GetTicksMsec() / 1000.0);
        UpdateFireflies(time);
        UpdateMushrooms(time);
        UpdateHalos(time);
    }

    private void UpdateHalos(float time)
    {
        foreach (var (light, anchor) in _halos.Values)
        {
            var level = _glowLevel * (0.9f + 0.1f * Mathf.Sin(time * 2.2f));
            light.Enabled = level > 0.02f;
            light.Energy = 0.75f * level;
            if (light.Enabled)
                light.GlobalPosition = anchor();
        }
    }

    private void UpdateFireflies(float time)
    {
        _fireflyRoot.Visible = _fireflyLevel > 0.01f;
        if (!_fireflyRoot.Visible)
            return;

        foreach (var firefly in _fireflies)
        {
            var wander = new Vector2(
                Mathf.Sin(time * firefly.Speed.X + firefly.Phase) + 0.4f * Mathf.Sin(time * firefly.Speed.X * 2.7f + firefly.Phase * 2.0f),
                Mathf.Sin(time * firefly.Speed.Y + firefly.Phase * 1.3f) + 0.3f * Mathf.Sin(time * firefly.Speed.Y * 3.1f));
            firefly.Node.Position = (firefly.Anchor + wander * firefly.Radius).Round();

            // Each one blinks on its own slow cycle, as fireflies do.
            var blink = Mathf.Clamp(Mathf.Sin(time * 1.3f + firefly.Phase * 3.0f) * 1.6f + 0.6f, 0.0f, 1.0f);
            var level = blink * _fireflyLevel;
            firefly.Node.Modulate = new Color(1.0f, 1.0f, 1.0f, level);
            firefly.Glow.Modulate = new Color(FireflyGlow, 0.55f * level);
            if (firefly.Light != null)
            {
                firefly.Light.Enabled = LightsEnabled && level > 0.02f;
                firefly.Light.Energy = 1.1f * level;
            }
        }
    }

    private void UpdateMushrooms(float time)
    {
        foreach (var mushroom in _mushrooms)
        {
            var pulse = 0.82f + 0.18f * Mathf.Sin(time * 1.6f + mushroom.Phase);
            var level = _glowLevel * pulse;
            mushroom.Glow.Modulate = new Color(MushroomGlow, 0.65f * level);
            mushroom.Light.Enabled = level > 0.02f;
            mushroom.Light.Energy = 1.15f * level;
        }
    }

    private Firefly CreateFirefly(int index)
    {
        var node = new Node2D { Name = $"Firefly{index}" };
        var glow = new Sprite2D { Texture = GlowTexture, Material = AdditiveMaterial, Scale = Vector2.One * 0.7f };
        node.AddChild(glow);
        node.AddChild(new Polygon2D
        {
            Polygon = new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1) },
            Color = FireflyCore,
            Material = AdditiveMaterial
        });

        PointLight2D? light = null;
        if (index % FireflyLightEvery == 0)
        {
            light = new PointLight2D
            {
                Texture = LightTexture,
                TextureScale = 0.8f,
                Color = FireflyGlow,
                Height = 16.0f,
                Energy = 0.0f,
                Enabled = false
            };
            node.AddChild(light);
        }

        _fireflyRoot.AddChild(node);
        return new Firefly
        {
            Node = node,
            Glow = glow,
            Light = light,
            Anchor = Vector2.Zero,
            Radius = new Vector2(_random.RandfRange(18.0f, 46.0f), _random.RandfRange(10.0f, 26.0f)),
            Speed = new Vector2(_random.RandfRange(0.12f, 0.32f), _random.RandfRange(0.15f, 0.40f)),
            Phase = _random.RandfRange(0.0f, Mathf.Tau)
        };
    }

    private Vector2 RandomLandPoint()
    {
        for (var attempt = 0; attempt < 24; attempt++)
        {
            var point = new Vector2(
                _random.RandfRange(_islandBounds.Position.X, _islandBounds.End.X),
                _random.RandfRange(_islandBounds.Position.Y, _islandBounds.End.Y));
            if (_field.SignedDistance(point.X, point.Y) < 10.0f)
                return point;
        }

        return _islandBounds.GetCenter();
    }

    private static CanvasItemMaterial? _additive;

    internal static CanvasItemMaterial AdditiveMaterial => _additive ??= new CanvasItemMaterial
    {
        BlendMode = CanvasItemMaterial.BlendModeEnum.Add,
        LightMode = CanvasItemMaterial.LightModeEnum.Unshaded
    };
}
