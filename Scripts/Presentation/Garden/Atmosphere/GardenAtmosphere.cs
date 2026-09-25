using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using Voidling.Presentation.Voidlings;

namespace Voidling.Presentation.Garden.Atmosphere;

/// <summary>
/// Owns the Garden's sea, light, weather and wildlife, and keeps them in step with the local hour
/// and the island's shape. The Garden controller composes one of these and feeds it island changes,
/// the Voidlings to reflect, and the clock; everything else happens in here.
///
/// Presentation only: the hour and weather resolved here never reach care, training, racing,
/// genetics, economy or persistence.
/// </summary>
public partial class GardenAtmosphere : Node2D
{
    private GardenSea _sea = null!;
    private GardenReflections _reflections = null!;
    private GardenLighting _lighting = null!;
    private GardenWeather _weatherView = null!;
    private GardenWildlife _wildlife = null!;
    private GardenAtmosphereCanvas _sunShafts = null!;
    private ShaderMaterial _sunShaftMaterial = null!;
    private Camera2D? _camera;
    private bool _active = true;

    private static ShaderMaterial? _foliageMaterial;

    /// <summary>
    /// The one material every tree shares, so the wind is set once per frame for all of them.
    /// </summary>
    public static ShaderMaterial FoliageMaterial => _foliageMaterial ??= new ShaderMaterial
    {
        Shader = GD.Load<Shader>(GardenAtmosphereAssets.ShaderRoot + "GardenFoliageSway.gdshader")
    };
    private GardenIslandField _field = GardenIslandField.Empty;

    private DateTime _clock = DateTime.Now;
    private bool _tintEnabled = true;
    private float? _hourOverride;
    private GardenWeatherKind? _weatherOverride;
    private GardenWeatherState _weather = GardenWeatherState.For(GardenWeatherKind.Clear);
    private double _clockRefresh;

    /// <summary>The island field every effect shares; rebuilt when land changes.</summary>
    public GardenIslandField Field => _field;

    /// <summary>The hour the Garden is currently lit for, override included.</summary>
    public float Hour => _hourOverride ?? (float)_clock.TimeOfDay.TotalHours;

    public GardenWeatherState Weather => _weather;

    public override void _Ready()
    {
        _sea = new GardenSea { Name = "Sea", ZIndex = -20 };
        AddChild(_sea);
        _reflections = new GardenReflections { Name = "Reflections" };
        AddChild(_reflections);
        _lighting = new GardenLighting { Name = "Lighting" };
        AddChild(_lighting);
        _weatherView = new GardenWeather { Name = "Weather" };
        AddChild(_weatherView);
        _wildlife = new GardenWildlife { Name = "Wildlife" };
        AddChild(_wildlife);
        _sunShaftMaterial = new ShaderMaterial
        {
            Shader = GD.Load<Shader>(GardenAtmosphereAssets.ShaderRoot + "GardenSunShafts.gdshader")
        };
        _sunShafts = new GardenAtmosphereCanvas(canvas => canvas.DrawRect(new Rect2(-560, -340, 1120, 680), Colors.White))
        {
            Name = "SunShafts",
            ZIndex = 34,
            Material = _sunShaftMaterial
        };
        _sea.SetReflections(_reflections.Texture);

        ReadCommandLineOverrides();
        RefreshWeather();
    }

    /// <summary>
    /// The Garden's own camera. Rain, cloud shadows, sun shafts and pollen ride on it; it is passed
    /// in rather than looked up because another screen's camera is current while a race runs.
    /// </summary>
    public void UseCamera(Camera2D camera)
    {
        _camera = camera;
        if (_sunShafts.GetParent() is { } parent)
            parent.RemoveChild(_sunShafts);
        camera.AddChild(_sunShafts);
        _weatherView.UseCamera(camera);
        _wildlife.UseCamera(camera);
    }

    /// <summary>
    /// Everything that depends on the island's shape. <paramref name="clear"/> says whether a spot on
    /// land is free for ground decals such as puddles (not under a tree trunk, for instance).
    /// </summary>
    public void SetIsland(
        GardenIslandField field,
        Rect2 islandBounds,
        IReadOnlyList<Vector2> hexCenters,
        float hexInnerRadius,
        Vector2? southShore,
        Func<Vector2, bool> clear)
    {
        _field = field;
        _sea.SetField(field);
        _lighting.SetIsland(field, islandBounds);
        _weatherView.SetIsland(field, hexCenters, hexInnerRadius, clear);
        _wildlife.SetIsland(field, islandBounds, southShore);
    }

    /// <summary>Tree canopies, for their falling leaves.</summary>
    public void SetTrees(Node2D actorsRoot, IEnumerable<Vector2> canopies) => _wildlife.SetTrees(actorsRoot, canopies);

    public void SetMushrooms(Node2D actorsRoot, IEnumerable<Vector2> spots) => _lighting.SetMushrooms(actorsRoot, spots);

    /// <summary>
    /// Reflects a Voidling in the sea and, when it wears an angel halo, lets the halo glow after dark.
    /// </summary>
    public void TrackVoidling(
        string id,
        AnimatedSprite2D body,
        VoidlingVisualAppearance appearance,
        Func<Vector2> ground,
        float bodyHeight,
        bool haloGlow)
    {
        _reflections.Track(id, body, appearance, ground, bodyHeight);
        if (haloGlow)
            _lighting.TrackHalo(id, () => ground() + new Vector2(0.0f, -bodyHeight - 4.0f));
        else
            _lighting.UntrackHalo(id);
    }

    public void UntrackVoidling(string id)
    {
        _reflections.Untrack(id);
        _lighting.UntrackHalo(id);
    }

    public void SetScenery(IEnumerable<(Texture2D Texture, Vector2 Center, Vector2 Scale, float GroundY)> scenery)
        => _reflections.SetScenery(scenery);

    /// <summary>
    /// The player's local time and whether the Garden tint is on. With the tint off the ambient light
    /// stays white, as the setting promises, though weather and wildlife still happen.
    /// </summary>
    public void SetClock(DateTime localTime, bool tintEnabled)
    {
        _clock = localTime;
        _tintEnabled = tintEnabled;
    }

    /// <summary>Pins the hour and/or the weather, for review screenshots and development.</summary>
    public void Preview(float? hour, GardenWeatherKind? weather)
    {
        _hourOverride = hour;
        _weatherOverride = weather;
        RefreshWeather();
        _weatherView.Settle(_weather);
    }

    public override void _Process(double delta)
    {
        // Hidden behind a race or a full-screen menu: stop rendering the reflection buffer and skip
        // the per-frame work until the Garden is back.
        var active = IsVisibleInTree();
        if (active != _active)
        {
            _active = active;
            _reflections.SetRendering(active);
        }
        if (!active)
            return;

        var step = (float)delta;
        _clockRefresh -= delta;
        if (_clockRefresh <= 0.0)
        {
            _clockRefresh = 0.5;
            _clock = DateTime.Now;
            RefreshWeather();
        }

        var hour = Hour;
        var night = GardenSky.Night(hour);
        var mist = GardenSky.Mist(hour) * (1.0f - _weather.Rain);

        _sea.SetConditions(night, _weather.Rain, mist, _weather.Wind);
        FoliageMaterial.SetShaderParameter("wind", _weather.Wind);
        if (_camera != null)
            _sunShafts.Scale = Vector2.One / Mathf.Max(0.1f, _camera.Zoom.X);
        _sunShaftMaterial.SetShaderParameter(
            "strength",
            _tintEnabled ? GardenSky.GoldenHour(hour) * (1.0f - _weather.Cloud * 0.85f) * 0.11f : 0.0f);
        _wildlife.Advance(step, hour, _weather);
        _weatherView.Advance(step, _weather);
        _lighting.Flash = _weatherView.Flash;
        _lighting.LightsEnabled = _tintEnabled;
        AimSun(hour, night);
        _lighting.AmbientTarget = AmbientFor(hour);
        _lighting.Advance(
            step,
            GardenSky.Fireflies(hour) * (1.0f - _weather.Rain),
            Mathf.Max(night, _weather.Rain * 0.6f));
        _reflections.Sync(GetViewport(), _field);
    }

    /// <summary>
    /// The sun crosses the sky from the east (screen right) in the morning to the west in the evening,
    /// high at noon and low and golden near its ends; after dark the same light becomes a dim, cool
    /// moon. Cloud softens it: an overcast sky gives flatter, more even light.
    /// </summary>
    private void AimSun(float hour, float night)
    {
        var day = Mathf.Clamp((hour - 6.0f) / 13.0f, 0.0f, 1.0f);
        var golden = GardenSky.GoldenHour(hour);
        var sunColor = new Color(1.0f, 0.97f, 0.90f).Lerp(new Color(1.0f, 0.78f, 0.52f), golden);
        var moonColor = new Color(0.62f, 0.72f, 1.0f);
        var clear = 1.0f - _weather.Cloud * 0.75f;

        // A low sun rakes across the bodies, so it is stronger (and warmer) near the golden hours.
        var energy = Mathf.Lerp(0.46f * clear * (1.0f + golden * 0.4f), 0.26f * clear, night);
        var travel = Mathf.Lerp(Mathf.Lerp(38.0f, -38.0f, day), 30.0f, night);
        var height = Mathf.Lerp(Mathf.Lerp(0.26f, 0.70f, Mathf.Sin(day * Mathf.Pi)), 0.5f, night);
        _lighting.SetSun(sunColor.Lerp(moonColor, night), energy, travel, height);
    }

    /// <summary>
    /// Day, dusk and night from the shared palette, then dimmed and cooled by cloud and rain. The
    /// Garden tint setting switches all of it off.
    /// </summary>
    private Color AmbientFor(float hour)
    {
        if (!_tintEnabled)
            return Colors.White;

        var local = _clock.Date.AddHours(hour);
        var color = GardenEnvironmentPalette.Resolve(local);
        var overcast = new Color(0.80f, 0.84f, 0.90f);
        var wet = new Color(0.70f, 0.75f, 0.85f);
        color *= Colors.White.Lerp(overcast, _weather.Cloud * 0.7f);
        color *= Colors.White.Lerp(wet, _weather.Rain * 0.8f);
        color.A = 1.0f;
        return color;
    }

    private void RefreshWeather()
    {
        _weather = _weatherOverride is { } kind
            ? GardenWeatherState.For(kind)
            : GardenWeatherSchedule.Resolve(_clock);
    }

    private void ReadCommandLineOverrides()
    {
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            if (arg.StartsWith("--voidling-garden-hour=", StringComparison.OrdinalIgnoreCase) &&
                float.TryParse(arg[(arg.IndexOf('=') + 1)..], NumberStyles.Float, CultureInfo.InvariantCulture, out var hour))
            {
                _hourOverride = Mathf.PosMod(hour, 24.0f);
            }
            else if (arg.StartsWith("--voidling-garden-weather=", StringComparison.OrdinalIgnoreCase) &&
                     Enum.TryParse<GardenWeatherKind>(arg[(arg.IndexOf('=') + 1)..], ignoreCase: true, out var weather))
            {
                _weatherOverride = weather;
            }
        }
    }
}
