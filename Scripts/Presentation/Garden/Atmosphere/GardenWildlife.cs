using System;
using System.Collections.Generic;
using Godot;

namespace Voidling.Presentation.Garden.Atmosphere;

/// <summary>
/// The island's small life, all from the premium packs: fish shadows circling in the shallows, a
/// rowboat moored off the south shore, lily pads with a frog that croaks after dark and in the rain,
/// bees working the island by day, pollen drifting in the light, and leaves dropping from the trees.
///
/// Placement is seeded from the island's shape, so the same island always has its boat and its frog
/// in the same places. None of it is interactive.
/// </summary>
public partial class GardenWildlife : Node2D
{
    private const string OceanPack = GardenAtmosphereAssets.EarlyAccess + "Ocean Pack/";
    private const string PlantUpdate = GardenAtmosphereAssets.EarlyAccess + "Plant update 2/";

    private static SpriteFrames[]? _fishFrames;
    private static SpriteFrames? _boatFrames;
    private static SpriteFrames? _frogFrames;
    private static SpriteFrames? _beeFrames;
    private static Texture2D[]? _lilyPads;

    private readonly List<Node2D> _placed = new();
    private readonly List<(AnimatedSprite2D Bee, Vector2 Anchor, Vector2 Radius, float Speed, float Phase)> _bees = new();
    private readonly List<CpuParticles2D> _leafEmitters = new();

    private AnimatedSprite2D? _boat;
    private float _boatBaseY;
    private AnimatedSprite2D? _frog;
    private double _croakCountdown = 4.0;
    private CpuParticles2D _pollen = null!;
    private Camera2D? _camera;
    private float _beeLevel;

    private static SpriteFrames[] FishFrames => _fishFrames ??= new[]
    {
        GardenAtmosphereAssets.Frames("swim", GardenAtmosphereAssets.Strip(OceanPack + "small fish.png", 0, 0, 16, 16, 15), 7.0),
        GardenAtmosphereAssets.Frames("swim", GardenAtmosphereAssets.Strip(OceanPack + "mediuml fish.png", 0, 0, 16, 16, 15), 6.0),
        GardenAtmosphereAssets.Frames("swim", GardenAtmosphereAssets.Strip(OceanPack + "big fish 2 swimming in cirkels.png", 0, 0, 16, 16, 15), 5.0)
    };

    private static SpriteFrames BoatFrames => _boatFrames ??= GardenAtmosphereAssets.Frames(
        "moored",
        GardenAtmosphereAssets.Strip(GardenAtmosphereAssets.Premium + "Objects/Boats.png", 0, 0, 48, 32, 2),
        1.6);

    private static SpriteFrames FrogFrames
    {
        get
        {
            if (_frogFrames != null)
                return _frogFrames;

            var sheet = PlantUpdate + "frog/frog_spritesheet.png";
            var frames = GardenAtmosphereAssets.Frames("idle", GardenAtmosphereAssets.Strip(sheet, 0, 16, 16, 16, 2), 1.5);
            frames.AddAnimation("croak");
            frames.SetAnimationLoop("croak", false);
            frames.SetAnimationSpeed("croak", 10.0);
            foreach (var frame in GardenAtmosphereAssets.Strip(sheet, 0, 32, 16, 16, 14))
                frames.AddFrame("croak", frame);
            return _frogFrames = frames;
        }
    }

    private static SpriteFrames BeeFrames => _beeFrames ??= GardenAtmosphereAssets.Frames(
        "fly",
        GardenAtmosphereAssets.Strip(PlantUpdate + "Bee/bee_spritesheet.png", 0, 0, 16, 16, 8),
        14.0);

    private static Texture2D[] LilyPads => _lilyPads ??= new[]
    {
        GardenAtmosphereAssets.Slice(GardenAtmosphereAssets.Premium + "Objects/Water Objects.png", 128, 0, 16, 16),
        GardenAtmosphereAssets.Slice(GardenAtmosphereAssets.Premium + "Objects/Water Objects.png", 144, 0, 16, 16),
        GardenAtmosphereAssets.Slice(GardenAtmosphereAssets.Premium + "Objects/Water Objects.png", 160, 0, 16, 16),
        GardenAtmosphereAssets.Slice(GardenAtmosphereAssets.Premium + "Objects/Water Objects.png", 176, 0, 16, 16)
    };

    public override void _Ready()
    {
        var colors = new Gradient();
        colors.SetColor(0, new Color(1.0f, 0.97f, 0.75f, 0.0f));
        colors.SetColor(1, new Color(1.0f, 0.97f, 0.75f, 0.0f));
        colors.AddPoint(0.3f, new Color(1.0f, 0.97f, 0.75f, 0.85f));
        colors.AddPoint(0.7f, new Color(1.0f, 1.0f, 0.88f, 0.85f));
        _pollen = new CpuParticles2D
        {
            Name = "Pollen",
            Amount = 26,
            Lifetime = 6.0,
            Preprocess = 6.0,
            LocalCoords = false,
            EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
            EmissionRectExtents = new Vector2(420.0f, 260.0f),
            Direction = new Vector2(1.0f, -0.3f),
            Spread = 40.0f,
            Gravity = new Vector2(0.0f, -1.5f),
            InitialVelocityMin = 3.0f,
            InitialVelocityMax = 9.0f,
            ScaleAmountMin = 1.0f,
            ScaleAmountMax = 1.0f,
            ColorRamp = colors,
            ZIndex = 44,
            Emitting = false
        };

        for (var i = 0; i < 3; i++)
        {
            var bee = new AnimatedSprite2D { Name = $"Bee{i}", SpriteFrames = BeeFrames, ZIndex = 45, Visible = false };
            AddChild(bee);
            bee.Play("fly");
            bee.Frame = i * 3;
            _bees.Add((bee, Vector2.Zero, new Vector2(26.0f + i * 9.0f, 14.0f + i * 5.0f), 0.55f + i * 0.17f, i * 2.1f));
        }
    }

    /// <summary>
    /// Re-places everything that depends on the coastline. <paramref name="southShore"/> is the
    /// midpoint of the island's lowest south-facing edge, where the boat is tied up.
    /// </summary>
    public void SetIsland(GardenIslandField field, Rect2 islandBounds, Vector2? southShore)
    {
        foreach (var node in _placed)
        {
            if (GodotObject.IsInstanceValid(node))
                node.QueueFree();
        }
        _placed.Clear();
        _boat = null;
        _frog = null;

        var rng = new RandomNumberGenerator
        {
            Seed = unchecked((ulong)((long)islandBounds.Position.X * 7919L ^ (long)islandBounds.Size.X * 104729L ^ (long)islandBounds.Size.Y * 1299709L))
        };

        // Fish shadows cruising the shallows.
        for (var i = 0; i < 5; i++)
        {
            if (!TryWaterPoint(field, islandBounds, rng, 16.0f, 80.0f, out var spot))
                continue;
            var fish = new AnimatedSprite2D
            {
                Name = $"Fish{i}",
                SpriteFrames = FishFrames[i % FishFrames.Length],
                Position = spot,
                Modulate = new Color(1.0f, 1.0f, 1.0f, 0.55f),
                ZIndex = -19,
                FlipH = rng.Randf() < 0.5f
            };
            AddChild(fish);
            fish.Play("swim");
            fish.Frame = rng.RandiRange(0, 14);
            _placed.Add(fish);
        }

        // Lily pads in the shallows, with a frog on one of them.
        for (var i = 0; i < 7; i++)
        {
            if (!TryWaterPoint(field, islandBounds, rng, 8.0f, 26.0f, out var spot))
                continue;
            var pad = new Sprite2D
            {
                Name = $"LilyPad{i}",
                Texture = LilyPads[rng.RandiRange(0, LilyPads.Length - 1)],
                Position = spot,
                ZIndex = -18
            };
            AddChild(pad);
            _placed.Add(pad);

            if (_frog == null && i >= 2)
            {
                _frog = new AnimatedSprite2D
                {
                    Name = "Frog",
                    SpriteFrames = FrogFrames,
                    Position = spot + new Vector2(0.0f, -4.0f),
                    FlipH = rng.Randf() < 0.5f,
                    ZIndex = -17
                };
                AddChild(_frog);
                _frog.Play("idle");
                _frog.AnimationFinished += () => _frog?.Play("idle");
                _placed.Add(_frog);
            }
        }

        // The rowboat tied up off the south shore.
        if (southShore is { } shore)
        {
            var spot = shore + new Vector2(18.0f, GardenCoast.CliffHeight + 12.0f);
            if (field.SignedDistance(spot.X, spot.Y) > 2.0f)
            {
                _boat = new AnimatedSprite2D { Name = "Boat", SpriteFrames = BoatFrames, Position = spot, ZIndex = -2 };
                _boatBaseY = spot.Y;
                AddChild(_boat);
                _boat.Play("moored");
                _placed.Add(_boat);
            }
        }

        // Bees keep to the island.
        for (var i = 0; i < _bees.Count; i++)
        {
            var bee = _bees[i];
            var anchor = islandBounds.GetCenter() + new Vector2(rng.RandfRange(-0.3f, 0.3f) * islandBounds.Size.X, rng.RandfRange(-0.3f, 0.3f) * islandBounds.Size.Y);
            _bees[i] = (bee.Bee, anchor, bee.Radius, bee.Speed, bee.Phase);
        }
    }

    /// <summary>A light drift of leaves from every tree canopy; heavier in wind.</summary>
    public void SetTrees(Node2D actorsRoot, IEnumerable<Vector2> canopies)
    {
        foreach (var emitter in _leafEmitters)
        {
            if (GodotObject.IsInstanceValid(emitter))
                emitter.QueueFree();
        }
        _leafEmitters.Clear();

        var colors = new Gradient();
        colors.SetColor(0, Color.FromHtml("#8FB35A"));
        colors.SetColor(1, Color.FromHtml("#C9B458"));
        foreach (var canopy in canopies)
        {
            var emitter = new CpuParticles2D
            {
                Name = "Leaves",
                Position = canopy,
                Amount = 2,
                Lifetime = 3.2,
                Preprocess = 3.0,
                LocalCoords = false,
                EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
                EmissionRectExtents = new Vector2(16.0f, 8.0f),
                Direction = new Vector2(0.4f, 1.0f),
                Spread = 30.0f,
                Gravity = new Vector2(3.0f, 9.0f),
                InitialVelocityMin = 2.0f,
                InitialVelocityMax = 6.0f,
                AngularVelocityMin = -120.0f,
                AngularVelocityMax = 120.0f,
                ScaleAmountMin = 1.0f,
                ScaleAmountMax = 2.0f,
                ColorInitialRamp = colors,
                ZIndex = 3
            };
            actorsRoot.AddChild(emitter);
            _leafEmitters.Add(emitter);
        }
    }

    public void Advance(float delta, float hour, GardenWeatherState weather)
    {
        var time = (float)(Time.GetTicksMsec() / 1000.0);
        var daylight = 1.0f - GardenSky.Night(hour);

        if (_boat != null && GodotObject.IsInstanceValid(_boat))
            _boat.Position = new Vector2(_boat.Position.X, _boatBaseY + Mathf.Round(Mathf.Sin(time * 1.4f) * (0.6f + weather.Wind)));

        UpdateFrog(delta, hour, weather);

        _pollen.Emitting = daylight > 0.5f && weather.Rain < 0.1f;
        _pollen.Modulate = new Color(1.0f, 1.0f, 1.0f, Mathf.Clamp(daylight * (0.6f + GardenSky.GoldenHour(hour)), 0.0f, 1.0f));

        _beeLevel = Mathf.MoveToward(_beeLevel, daylight > 0.8f && weather.Rain < 0.1f && weather.Wind < 0.6f ? 1.0f : 0.0f, delta * 0.5f);
        foreach (var (bee, anchor, radius, speed, phase) in _bees)
        {
            bee.Visible = _beeLevel > 0.02f;
            if (!bee.Visible)
                continue;
            var t = time * speed + phase;
            var offset = new Vector2(Mathf.Sin(t) * radius.X, Mathf.Sin(t * 2.0f + phase) * radius.Y);
            var next = (anchor + offset).Round();
            bee.FlipH = next.X < bee.Position.X;
            bee.Position = next;
            bee.Modulate = new Color(1.0f, 1.0f, 1.0f, _beeLevel);
        }

        foreach (var emitter in _leafEmitters)
            emitter.SpeedScale = 0.8f + weather.Wind * 1.6f;
    }

    private void UpdateFrog(float delta, float hour, GardenWeatherState weather)
    {
        if (_frog == null || !GodotObject.IsInstanceValid(_frog))
            return;

        // Frogs sing after dark and in the rain.
        var chorus = Mathf.Max(GardenSky.Night(hour), weather.Rain);
        if (chorus < 0.4f || _frog.Animation == "croak")
            return;

        _croakCountdown -= delta;
        if (_croakCountdown > 0.0)
            return;

        _croakCountdown = 3.0 + GD.Randf() * 6.0;
        _frog.Play("croak");
    }

    /// <summary>Pollen drifts through whatever the Garden camera is looking at.</summary>
    public void UseCamera(Camera2D camera)
    {
        _camera = camera;
        if (_pollen.GetParent() is { } parent)
            parent.RemoveChild(_pollen);
        camera.AddChild(_pollen);
    }

    private static bool TryWaterPoint(GardenIslandField field, Rect2 bounds, RandomNumberGenerator rng, float minDistance, float maxDistance, out Vector2 point)
    {
        var area = bounds.Grow(maxDistance);
        for (var attempt = 0; attempt < 60; attempt++)
        {
            point = new Vector2(
                Mathf.Round(rng.RandfRange(area.Position.X, area.End.X)),
                Mathf.Round(rng.RandfRange(area.Position.Y, area.End.Y)));
            var distance = field.SignedDistance(point.X, point.Y);
            if (distance >= minDistance && distance <= maxDistance)
                return true;
        }

        point = Vector2.Zero;
        return false;
    }
}
