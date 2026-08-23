using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace VoidlingGame;

public partial class RaceController : Node2D
{
    public event Action? ReturnRequested;

    private const float ScreenWidth = 640.0f;
    private const float ScreenHeight = 360.0f;
    private const float TrackStartX = 70.0f;
    private const float TrackEndX = 1810.0f;
    private const float TrackY = 184.0f;
    private const float TrackTop = 126.0f;
    private const float TrackBottom = 244.0f;
    private const float SwimStartX = 500.0f;
    private const float SwimEndX = 760.0f;
    private const float FlyStartX = 1080.0f;
    private const float FlyEndX = 1370.0f;
    private const float RampStartX = FlyStartX - 72.0f;
    private const float CheerDuration = 2.0f;
    private const float CheerCost = 24.0f;
    private const float RunningStaminaDrainPerSecond = 2.1f;
    private const float FlightAltitude = 38.0f;

    private static readonly Texture2D CharacterTexture = GD.Load<Texture2D>(
        "res://Assets/Sprout Lands - Sprites - Basic pack/Characters/Basic Charakter Spritesheet.png");
    private static readonly Texture2D SwimmingTexture = GD.Load<Texture2D>(
        "res://Assets/Sprout Sorry pack/Early Access/Ocean Pack/swimming.png");
    private static readonly Texture2D WaterTexture = GD.Load<Texture2D>(
        "res://Assets/Sprout Lands - Sprites - Basic pack/Tilesets/Water.png");
    private static readonly Texture2D FenceTexture = GD.Load<Texture2D>(
        "res://Assets/Sprout Lands - Sprites - Basic pack/Tilesets/Fences.png");

    private readonly float[] _obstacleXs = { 340.0f, 890.0f, 1510.0f, 1660.0f };
    private readonly float[] _racerOffsets = { -16.0f, -5.0f, 6.0f, 17.0f };
    private readonly List<Racer> _racers = new();
    private readonly List<Racer> _finishOrder = new();

    private bool _running;
    private bool _resultsShown;
    private bool _rewardGranted;
    private string _selectedId = "";
    private Racer? _player;
    private Camera2D _camera = null!;
    private Polygon2D _playerMarker = null!;
    private Button _cheerButton = null!;
    private ProgressBar _staminaBar = null!;
    private Label _staminaLabel = null!;
    private Label _sectionLabel = null!;
    private RaceMiniMap _miniMap = null!;
    private float _cheerParticleAccumulator;

    private sealed class Racer
    {
        public VoidlingData Data { get; init; } = null!;
        public AnimatedSprite2D Sprite { get; init; } = null!;
        public Polygon2D Shadow { get; init; } = null!;
        public Random Random { get; init; } = null!;
        public float X { get; set; } = TrackStartX;
        public float BaseY { get; init; }
        public int NextObstacle { get; set; }
        public float DelaySeconds { get; set; }
        public float JumpSeconds { get; set; }
        public float CheerSeconds { get; set; }
        public float MaxStamina { get; init; }
        public float CurrentStamina { get; set; }
        public bool FlightResolved { get; set; }
        public bool FlightFailed { get; set; }
        public float FlightEndX { get; set; } = FlyEndX;
        public bool Finished { get; set; }
        public string VisualMode { get; set; } = "run";
    }

    public void Setup(VoidlingData selected)
    {
        _selectedId = selected.Id;
        var seed = GameSession.Instance.CreateRaceSeed();
        var participants = BuildParticipants(selected, seed);

        AddWaterSection(SwimStartX, SwimEndX);
        AddWaterSection(FlyStartX, FlyEndX);
        AddFlightRamp();

        for (var i = 0; i < participants.Count; i++)
        {
            var data = participants[i];
            var staminaStat = GameRules.EffectiveStat(data, "stamina");
            var maxStamina = 72.0f + staminaStat * 1.05f;

            var shadow = new Polygon2D
            {
                Polygon = BuildEllipsePoints(9.0f, 3.4f, 18),
                Color = new Color(0.15f, 0.18f, 0.16f, 0.34f),
                Position = new Vector2(TrackStartX, TrackY + _racerOffsets[i] + 2.0f),
                ZIndex = 7 + i
            };
            AddChild(shadow);

            var sprite = new AnimatedSprite2D
            {
                SpriteFrames = BuildRaceFrames(),
                Position = new Vector2(TrackStartX, TrackY + _racerOffsets[i] - 8.0f),
                Scale = new Vector2(0.72f, 0.72f),
                Modulate = GameRules.TintColor(data.TintHex),
                ZIndex = 10 + i
            };
            AddChild(sprite);
            sprite.Play("run");

            // Challenge mutation presentation is derived from this participant's own
            // traits. This prevents a selected Angel preview from leaking onto every
            // racer and keeps mutations visible during the minigame itself.
            var mutationAdornment = new MutationAdornment2D();
            mutationAdornment.Setup(data, sprite);
            AddChild(mutationAdornment);

            var racer = new Racer
            {
                Data = data,
                Sprite = sprite,
                Shadow = shadow,
                Random = GeneticsService.CreateRandom(seed, $"race:{data.Id}:{i}"),
                BaseY = TrackY + _racerOffsets[i],
                MaxStamina = maxStamina,
                CurrentStamina = maxStamina
            };
            _racers.Add(racer);

            if (data.Id == selected.Id)
                _player = racer;
        }

        foreach (var obstacleX in _obstacleXs)
            AddHurdle(obstacleX);

        AddWorldLabel("SWIM", (SwimStartX + SwimEndX) * 0.5f, 108, GameRules.StatColor("swim"));
        AddWorldLabel("GLIDE / SWIM", (FlyStartX + FlyEndX) * 0.5f, 108, GameRules.StatColor("fly"));

        CreateCamera();
        CreatePlayerMarker();
        CreateHud();
        _running = true;
        QueueRedraw();
        UpdatePlayerTracking();
        UpdateHud();
    }

    public override void _Process(double delta)
    {
        if (!_running || _player == null)
            return;

        var step = (float)delta;
        foreach (var racer in _racers)
        {
            if (!racer.Finished)
                AdvanceRacer(racer, step, true);
        }

        if (_player.CheerSeconds > 0.0f && !_player.Finished)
        {
            _cheerParticleAccumulator += step;
            while (_cheerParticleAccumulator >= 0.075f)
            {
                _cheerParticleAccumulator -= 0.075f;
                SpawnCheerSpeedParticle();
            }
        }
        else
        {
            _cheerParticleAccumulator = 0.0f;
        }

        UpdatePlayerTracking();
        UpdateHud();

        if (GameSession.Instance.State.AutoFinishRaces && _finishOrder.Count < _racers.Count)
        {
            if (_player.Finished)
            {
                FastForwardCpuFinishers();
            }
            else if (_racers.Where(r => r != _player).All(r => r.Finished))
            {
                FinishPlayerAsLast();
            }
        }

        if (_finishOrder.Count == _racers.Count && !_resultsShown)
        {
            _running = false;
            ShowResults();
        }
    }

    public override void _Draw()
    {
        var worldLeft = -ScreenWidth;
        var worldWidth = TrackEndX + ScreenWidth * 2.0f;

        DrawRect(new Rect2(worldLeft, 0, worldWidth, ScreenHeight), Color.FromHtml("#A7D8C7"));
        DrawRect(new Rect2(worldLeft, 102, worldWidth, 166), Color.FromHtml("#8EBE85"));
        DrawRect(new Rect2(worldLeft, TrackTop, worldWidth, TrackBottom - TrackTop), Color.FromHtml("#D9774E"));
        DrawLine(new Vector2(worldLeft, TrackTop), new Vector2(worldLeft + worldWidth, TrackTop), Color.FromHtml("#E9B777"), 4.0f);
        DrawLine(new Vector2(worldLeft, TrackBottom), new Vector2(worldLeft + worldWidth, TrackBottom), Color.FromHtml("#9C6049"), 4.0f);

        for (var x = worldLeft + 40.0f; x < worldLeft + worldWidth; x += 80.0f)
            DrawLine(new Vector2(x, TrackY), new Vector2(x + 28, TrackY), new Color(0.95f, 0.72f, 0.52f, 0.30f), 2.0f);

        DrawRect(new Rect2(TrackEndX - 10, TrackTop - 8, 20, TrackBottom - TrackTop + 16), Color.FromHtml("#F5F0DE"));
        const float square = 10.0f;
        for (var row = 0; row < 13; row++)
        {
            for (var col = 0; col < 2; col++)
            {
                if ((row + col) % 2 == 0)
                    DrawRect(new Rect2(TrackEndX - 10 + col * square, TrackTop - 6 + row * square, square, square), Color.FromHtml("#39423D"));
            }
        }
        DrawLine(new Vector2(TrackEndX - 14, TrackTop - 10), new Vector2(TrackEndX - 14, TrackBottom + 10), Color.FromHtml("#68584B"), 4.0f);
        DrawLine(new Vector2(TrackEndX + 14, TrackTop - 10), new Vector2(TrackEndX + 14, TrackBottom + 10), Color.FromHtml("#68584B"), 4.0f);
    }

    private void AdvanceRacer(Racer racer, float step, bool updateVisual)
    {
        racer.CheerSeconds = Math.Max(0.0f, racer.CheerSeconds - step);
        var staminaDrain = RunningStaminaDrainPerSecond;

        if (racer.DelaySeconds > 0.0f)
        {
            racer.DelaySeconds = Math.Max(0.0f, racer.DelaySeconds - step);
            racer.CurrentStamina = Math.Max(0.0f, racer.CurrentStamina - staminaDrain * 0.35f * step);
            if (updateVisual)
                UpdateRacerPosition(racer, step);
            return;
        }

        var run = GameRules.EffectiveStat(racer.Data, "run");
        var swim = GameRules.EffectiveStat(racer.Data, "swim");
        var fly = GameRules.EffectiveStat(racer.Data, "fly");
        var speed = 31.0f + run * 0.36f;

        if (InSwimSection(racer.X))
        {
            speed = 24.0f + swim * 0.35f;
            staminaDrain += 1.1f;
            if (updateVisual)
                SetVisualMode(racer, "swim");
        }
        else if (InFlySection(racer.X))
        {
            if (!racer.FlightResolved)
                ResolveFlightPlan(racer, fly);

            if (!racer.FlightFailed && racer.X >= racer.FlightEndX && racer.FlightEndX < FlyEndX - 1.0f)
                racer.FlightFailed = true;

            if (racer.FlightFailed)
            {
                speed = 23.0f + swim * 0.33f;
                staminaDrain += 1.25f;
                if (updateVisual)
                    SetVisualMode(racer, "swim");
            }
            else
            {
                speed = 28.0f + fly * 0.40f;
                staminaDrain += 0.85f;
                if (updateVisual)
                    SetVisualMode(racer, "glide");
            }
        }
        else
        {
            if (racer.X >= FlyEndX)
            {
                racer.FlightResolved = false;
                racer.FlightFailed = false;
                racer.FlightEndX = FlyEndX;
            }

            if (updateVisual)
                SetVisualMode(racer, "run");
        }

        var staminaRatio = racer.MaxStamina <= 0.0f ? 0.0f : racer.CurrentStamina / racer.MaxStamina;
        if (staminaRatio < 0.18f)
            speed *= 0.90f;
        if (racer.CurrentStamina <= 0.01f)
            speed *= 0.84f;
        if (racer.CheerSeconds > 0.0f)
            speed *= 1.22f;

        racer.CurrentStamina = Math.Max(0.0f, racer.CurrentStamina - staminaDrain * step);
        racer.X += speed * step;

        if (racer.NextObstacle < _obstacleXs.Length && racer.X >= _obstacleXs[racer.NextObstacle] - 14.0f)
        {
            ResolveObstacle(racer, run);
            racer.NextObstacle++;
        }

        if (racer.X >= TrackEndX)
        {
            racer.X = TrackEndX;
            racer.Finished = true;
            if (!_finishOrder.Contains(racer))
                _finishOrder.Add(racer);
            if (updateVisual)
                racer.Sprite.Stop();
        }

        if (updateVisual)
            UpdateRacerPosition(racer, step);
    }

    private static void ResolveFlightPlan(Racer racer, float fly)
    {
        racer.FlightResolved = true;
        racer.FlightFailed = false;

        var sectionWidth = FlyEndX - FlyStartX;
        var glideDistance = 82.0f + Mathf.Clamp(fly, 0.0f, 100.0f) * 2.55f;
        racer.FlightEndX = Math.Min(FlyEndX, FlyStartX + Math.Min(sectionWidth, glideDistance));
    }

    private void FastForwardCpuFinishers()
    {
        const float simulationStep = 0.08f;
        var guard = 0;
        while (_finishOrder.Count < _racers.Count && guard++ < 12000)
        {
            foreach (var racer in _racers)
            {
                if (!racer.Finished)
                    AdvanceRacer(racer, simulationStep, false);
            }
        }

        foreach (var racer in _racers.Where(r => !r.Finished).OrderByDescending(r => r.X))
        {
            racer.X = TrackEndX;
            racer.Finished = true;
            if (!_finishOrder.Contains(racer))
                _finishOrder.Add(racer);
        }
    }

    private void FinishPlayerAsLast()
    {
        if (_player == null || _player.Finished)
            return;

        _player.X = TrackEndX;
        _player.Finished = true;
        _player.Sprite.Stop();
        if (!_finishOrder.Contains(_player))
            _finishOrder.Add(_player);
        UpdateRacerPosition(_player, 0.0f);
    }

    private void CreateCamera()
    {
        _camera = new Camera2D
        {
            Enabled = true,
            Position = _player == null ? new Vector2(TrackStartX, ScreenHeight * 0.5f) : new Vector2(_player.X, ScreenHeight * 0.5f),
            Zoom = Vector2.One,
            PositionSmoothingEnabled = false
        };
        AddChild(_camera);
    }

    private void CreatePlayerMarker()
    {
        _playerMarker = new Polygon2D
        {
            Polygon = new Vector2[] { new(-7, -9), new(7, -9), new(0, 0) },
            Color = Color.FromHtml("#FFF26E"),
            ZIndex = 40
        };
        AddChild(_playerMarker);
    }

    private void CreateHud()
    {
        var canvas = new CanvasLayer { Layer = 20 };
        AddChild(canvas);

        var title = UiFactory.CreateTitle("SPROUT RUN");
        title.Position = new Vector2(255, 8);
        title.Size = new Vector2(180, 24);
        canvas.AddChild(title);

        _sectionLabel = UiFactory.CreateLabel("RUN", 8);
        _sectionLabel.Position = new Vector2(292, 34);
        _sectionLabel.Size = new Vector2(120, 18);
        _sectionLabel.HorizontalAlignment = HorizontalAlignment.Center;
        canvas.AddChild(_sectionLabel);

        var cheerPanel = UiFactory.CreatePanel(new Vector2(228, 70));
        cheerPanel.Position = new Vector2(14, 276);
        cheerPanel.Size = new Vector2(228, 70);
        canvas.AddChild(cheerPanel);

        var cheerBox = new VBoxContainer();
        cheerBox.AddThemeConstantOverride("separation", 3);
        cheerPanel.AddChild(cheerBox);

        var cheerRow = new HBoxContainer();
        cheerRow.AddThemeConstantOverride("separation", 6);
        _cheerButton = UiFactory.CreateButton("CHEER!");
        _cheerButton.CustomMinimumSize = new Vector2(88, 27);
        _cheerButton.Pressed += CheerPlayer;
        cheerRow.AddChild(_cheerButton);
        _staminaLabel = UiFactory.CreateLabel("STAMINA", 7);
        _staminaLabel.VerticalAlignment = VerticalAlignment.Center;
        cheerRow.AddChild(_staminaLabel);
        cheerBox.AddChild(cheerRow);

        _staminaBar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 100,
            Value = 100,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(196, 16)
        };
        UiFactory.ApplyPixelFont(_staminaBar, 7);
        var background = new StyleBoxFlat { BgColor = Color.FromHtml("#66594C") };
        background.CornerRadiusTopLeft = background.CornerRadiusTopRight = 2;
        background.CornerRadiusBottomLeft = background.CornerRadiusBottomRight = 2;
        var fill = new StyleBoxFlat { BgColor = GameRules.StatColor("stamina") };
        fill.CornerRadiusTopLeft = fill.CornerRadiusTopRight = 2;
        fill.CornerRadiusBottomLeft = fill.CornerRadiusBottomRight = 2;
        _staminaBar.AddThemeStyleboxOverride("background", background);
        _staminaBar.AddThemeStyleboxOverride("fill", fill);
        cheerBox.AddChild(_staminaBar);

        var mapPanel = UiFactory.CreatePanel(new Vector2(205, 64));
        mapPanel.Position = new Vector2(421, 282);
        mapPanel.Size = new Vector2(205, 64);
        canvas.AddChild(mapPanel);

        _miniMap = new RaceMiniMap { CustomMinimumSize = new Vector2(181, 40) };
        mapPanel.AddChild(_miniMap);
    }

    private void CheerPlayer()
    {
        if (!_running || _player == null || _player.Finished)
            return;
        if (_player.CheerSeconds > 0.0f || _player.CurrentStamina < CheerCost)
            return;

        _player.CurrentStamina -= CheerCost;
        _player.CheerSeconds = CheerDuration;
        _cheerParticleAccumulator = 0.075f;
        SpawnCheerSpeedParticle();
        UpdateHud();
    }

    private void SpawnCheerSpeedParticle()
    {
        if (_player == null)
            return;

        var yJitter = (float)(_player.Random.NextDouble() * 18.0 - 9.0);
        var length = 9.0f + (float)_player.Random.NextDouble() * 8.0f;
        var streak = new Line2D
        {
            Width = 1.7f,
            DefaultColor = Color.FromHtml("#FFF0A1"),
            ZIndex = 8,
            Points = new[]
            {
                new Vector2(_player.X - 13.0f - length, _player.Sprite.Position.Y + 8.0f + yJitter),
                new Vector2(_player.X - 13.0f, _player.Sprite.Position.Y + 8.0f + yJitter)
            }
        };
        AddChild(streak);

        var tween = CreateTween().SetParallel(true);
        tween.TweenProperty(streak, "position:x", -20.0f, 0.28)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(streak, "modulate:a", 0.0f, 0.28);
        tween.Finished += streak.QueueFree;
    }

    private void UpdateHud()
    {
        if (_player == null || _staminaBar == null || _miniMap == null)
            return;

        _staminaBar.MaxValue = _player.MaxStamina;
        _staminaBar.Value = _player.CurrentStamina;
        _staminaLabel.Text = $"STAMINA {Mathf.CeilToInt(_player.CurrentStamina)} / {Mathf.CeilToInt(_player.MaxStamina)}";
        _cheerButton.Disabled = !_running || _player.Finished || _player.CheerSeconds > 0.0f || _player.CurrentStamina < CheerCost;
        _cheerButton.Text = _player.CheerSeconds > 0.0f ? "CHEERING!" : "CHEER!";

        if (InSwimSection(_player.X))
        {
            _sectionLabel.Text = "SWIM";
            _sectionLabel.AddThemeColorOverride("font_color", GameRules.StatColor("swim"));
        }
        else if (InFlySection(_player.X))
        {
            _sectionLabel.Text = _player.FlightFailed ? "SWIM" : "GLIDE";
            _sectionLabel.AddThemeColorOverride("font_color", _player.FlightFailed ? GameRules.StatColor("swim") : GameRules.StatColor("fly"));
        }
        else if (InLaunchRamp(_player.X))
        {
            _sectionLabel.Text = "TAKEOFF";
            _sectionLabel.AddThemeColorOverride("font_color", GameRules.StatColor("fly"));
        }
        else
        {
            _sectionLabel.Text = "RUN";
            _sectionLabel.AddThemeColorOverride("font_color", GameRules.StatColor("run"));
        }

        var points = _racers.Select(racer => new RaceMiniMapPoint
        {
            Id = racer.Data.Id,
            Color = GameRules.TintColor(racer.Data.TintHex),
            Progress = Mathf.Clamp((racer.X - TrackStartX) / (TrackEndX - TrackStartX), 0.0f, 1.0f),
            IsPlayer = racer.Data.Id == _selectedId
        }).ToList();
        _miniMap.SetPoints(points);
    }

    private void UpdatePlayerTracking()
    {
        if (_player == null)
            return;

        _camera.Zoom = Vector2.One;
        _camera.Position = new Vector2(_player.X, ScreenHeight * 0.5f);
        _playerMarker.Position = new Vector2(_player.X, _player.Sprite.Position.Y - 21.0f);
    }

    private void ResolveObstacle(Racer racer, float run)
    {
        var avoidChance = Mathf.Clamp(0.28f + run / 100.0f * 0.67f, 0.28f, 0.95f);
        if (racer.Random.NextDouble() <= avoidChance)
            racer.JumpSeconds = 0.58f;
        else
        {
            racer.DelaySeconds = 0.62f + (100.0f - run) / 100.0f * 0.55f;
            racer.X -= 5.0f;
        }
    }

    private void UpdateRacerPosition(Racer racer, float delta)
    {
        var yOffset = 0.0f;
        var shadowScale = Vector2.One;
        var shadowAlpha = 0.34f;

        if (racer.JumpSeconds > 0.0f)
        {
            racer.JumpSeconds = Math.Max(0.0f, racer.JumpSeconds - delta);
            var normalized = 1.0f - racer.JumpSeconds / 0.58f;
            yOffset = -Mathf.Sin(normalized * Mathf.Pi) * 17.0f;
            shadowScale = new Vector2(0.75f, 0.75f);
        }
        else if (InSwimSection(racer.X))
        {
            yOffset = 7.0f + Mathf.Sin((float)Time.GetTicksMsec() / 150.0f + racer.BaseY) * 2.0f;
            shadowScale = new Vector2(0.45f, 0.45f);
            shadowAlpha = 0.15f;
        }
        else if (InLaunchRamp(racer.X))
        {
            var progress = Mathf.Clamp((racer.X - RampStartX) / (FlyStartX - RampStartX), 0.0f, 1.0f);
            progress = progress * progress * (3.0f - 2.0f * progress);
            yOffset = -FlightAltitude * progress;
            racer.Sprite.Rotation = -0.09f * progress;
            shadowScale = Vector2.One.Lerp(new Vector2(0.52f, 0.52f), progress);
            shadowAlpha = Mathf.Lerp(0.34f, 0.14f, progress);
        }
        else if (InFlySection(racer.X))
        {
            if (racer.FlightFailed)
            {
                yOffset = 7.0f + Mathf.Sin((float)Time.GetTicksMsec() / 150.0f + racer.BaseY) * 2.0f;
                racer.Sprite.Rotation = 0.0f;
                shadowScale = new Vector2(0.45f, 0.45f);
                shadowAlpha = 0.15f;
            }
            else
            {
                var glideSpan = Math.Max(1.0f, racer.FlightEndX - FlyStartX);
                var glideProgress = Mathf.Clamp((racer.X - FlyStartX) / glideSpan, 0.0f, 1.0f);
                var easedDescent = Mathf.Pow(glideProgress, 1.35f);
                var fallsIntoWater = racer.FlightEndX < FlyEndX - 1.0f;
                var destinationY = fallsIntoWater ? 7.0f : 0.0f;
                yOffset = Mathf.Lerp(-FlightAltitude, destinationY, easedDescent) +
                          Mathf.Sin((float)Time.GetTicksMsec() / 210.0f + racer.BaseY) * 1.2f * (1.0f - glideProgress);
                racer.Sprite.Rotation = Mathf.Lerp(-0.08f, 0.08f, glideProgress);
                shadowScale = new Vector2(
                    Mathf.Lerp(0.48f, fallsIntoWater ? 0.45f : 0.82f, glideProgress),
                    Mathf.Lerp(0.48f, fallsIntoWater ? 0.45f : 0.82f, glideProgress));
                shadowAlpha = Mathf.Lerp(0.11f, fallsIntoWater ? 0.15f : 0.26f, glideProgress);
            }
        }
        else
        {
            racer.Sprite.Rotation = 0.0f;
        }

        racer.Sprite.Position = new Vector2(racer.X, racer.BaseY - 8.0f + yOffset);
        racer.Shadow.Position = new Vector2(racer.X, racer.BaseY + 2.0f);
        racer.Shadow.Scale = shadowScale;
        var shadowColor = racer.Shadow.Color;
        shadowColor.A = shadowAlpha;
        racer.Shadow.Color = shadowColor;
    }

    private static void SetVisualMode(Racer racer, string mode)
    {
        if (racer.VisualMode == mode)
            return;

        racer.VisualMode = mode;
        racer.Sprite.Rotation = 0.0f;
        switch (mode)
        {
            case "swim":
                racer.Sprite.Play("swim");
                racer.Sprite.SpeedScale = 1.0f;
                break;
            case "glide":
                racer.Sprite.Play("run");
                racer.Sprite.SpeedScale = 0.38f;
                break;
            default:
                racer.Sprite.Play("run");
                racer.Sprite.SpeedScale = 1.0f;
                break;
        }
    }

    private static bool InSwimSection(float x) => x >= SwimStartX && x < SwimEndX;
    private static bool InFlySection(float x) => x >= FlyStartX && x < FlyEndX;
    private static bool InLaunchRamp(float x) => x >= RampStartX && x < FlyStartX;

    private List<VoidlingData> BuildParticipants(VoidlingData selected, ulong seed)
    {
        var result = new List<VoidlingData> { selected };
        var cpuNames = new[] { "Fern", "Moss", "Puck", "Clover", "Pebble", "Dew" };

        for (var cpuIndex = 0; cpuIndex < 3; cpuIndex++)
        {
            var cpuSeed = seed + (ulong)(100 + cpuIndex * 17);
            var genome = GeneticsService.CreateRandomGenome(cpuSeed);
            result.Add(new VoidlingData
            {
                Id = $"cpu-{cpuIndex}-{cpuSeed}",
                Name = cpuNames[(int)(cpuSeed % (ulong)cpuNames.Length)],
                Genome = genome,
                Stage = LifeStage.Adult,
                TintHex = GeneticsService.ResolveTint(genome),
                TrainingPoints = GameRules.StatIds.ToDictionary(id => id, _ => 0)
            });
        }

        return result;
    }

    private void AddWaterSection(float startX, float endX)
    {
        var tile = new AtlasTexture
        {
            Atlas = WaterTexture,
            Region = new Rect2(0, 0, 16, 16)
        };

        for (var x = startX + 8.0f; x < endX; x += 16.0f)
        {
            for (var y = TrackTop + 8.0f; y < TrackBottom; y += 16.0f)
            {
                var sprite = new Sprite2D
                {
                    Texture = tile,
                    Position = new Vector2(x, y),
                    ZIndex = 2
                };
                AddChild(sprite);
            }
        }
    }

    private void AddFlightRamp()
    {
        // The takeoff structure spans the entire visible depth of the track so every
        // racer visually climbs the same ramp instead of walking over a center-lane prop.
        const float laneBandHeight = 28.0f;
        var width = FlyStartX - RampStartX;
        var centerX = (RampStartX + FlyStartX) * 0.5f;

        for (var y = TrackTop; y < TrackBottom; y += laneBandHeight)
        {
            var bandHeight = Math.Min(laneBandHeight, TrackBottom - y);
            var ramp = new Polygon2D
            {
                Polygon = new[]
                {
                    new Vector2(-width * 0.5f, bandHeight * 0.45f),
                    new Vector2(width * 0.5f, -bandHeight * 0.45f),
                    new Vector2(width * 0.5f, bandHeight * 0.5f),
                    new Vector2(-width * 0.5f, bandHeight * 0.5f)
                },
                Color = Color.FromHtml("#D99B63"),
                Position = new Vector2(centerX, y + bandHeight * 0.5f),
                ZIndex = 4
            };
            AddChild(ramp);

            var edge = new Line2D
            {
                Width = 2.0f,
                DefaultColor = Color.FromHtml("#8D654F"),
                Points = new[]
                {
                    new Vector2(-width * 0.5f, bandHeight * 0.45f),
                    new Vector2(width * 0.5f, -bandHeight * 0.45f)
                },
                ZIndex = 5
            };
            ramp.AddChild(edge);
        }
    }

    private void AddHurdle(float x)
    {
        var tile = new AtlasTexture
        {
            Atlas = FenceTexture,
            Region = new Rect2(0, 0, 16, 16)
        };

        for (var y = TrackTop + 9.0f; y < TrackBottom; y += 18.0f)
        {
            var obstacle = new Sprite2D
            {
                Texture = tile,
                Position = new Vector2(x, y),
                Scale = new Vector2(1.15f, 1.15f),
                ZIndex = 6
            };
            AddChild(obstacle);
        }
    }

    private void AddWorldLabel(string text, float x, float y, Color color)
    {
        var label = UiFactory.CreateLabel(text, 8);
        label.Position = new Vector2(x - 45, y);
        label.Size = new Vector2(90, 16);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_outline_color", Color.FromHtml("#465247"));
        label.AddThemeConstantOverride("outline_size", 1);
        label.ZIndex = 4;
        AddChild(label);
    }

    private void ShowResults()
    {
        _resultsShown = true;
        var selectedPlace = _finishOrder.FindIndex(r => r.Data.Id == _selectedId) + 1;
        if (selectedPlace <= 0)
            selectedPlace = 4;

        if (!_rewardGranted)
        {
            _rewardGranted = true;
            GameSession.Instance.AddRaceReward(selectedPlace);
        }

        var canvas = new CanvasLayer { Layer = 50 };
        AddChild(canvas);
        var shade = new ColorRect
        {
            Color = new Color(0.12f, 0.18f, 0.16f, 0.55f),
            Position = Vector2.Zero,
            Size = new Vector2(ScreenWidth, ScreenHeight),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        canvas.AddChild(shade);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        canvas.AddChild(center);

        var panel = UiFactory.CreatePanel(new Vector2(520, 292));
        center.AddChild(panel);
        var box = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        box.AddThemeConstantOverride("separation", 4);
        panel.AddChild(box);
        var resultTitle = UiFactory.CreateTitle($"RACE RESULTS — YOU #{selectedPlace}");
        resultTitle.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(resultTitle);

        var stage = new Control { CustomMinimumSize = new Vector2(480, 192) };
        box.AddChild(stage);

        if (_finishOrder.Count >= 4)
        {
            AddPodiumSlot(stage, _finishOrder[1], 2, new Vector2(55, 105), new Vector2(100, 67));
            AddPodiumSlot(stage, _finishOrder[0], 1, new Vector2(181, 72), new Vector2(112, 100));
            AddPodiumSlot(stage, _finishOrder[2], 3, new Vector2(319, 120), new Vector2(92, 52));
            AddFourthPlacePuddle(stage, _finishOrder[3], new Vector2(428, 112));
        }

        var button = UiFactory.CreateButton("Return to Garden");
        button.CustomMinimumSize = new Vector2(160, 25);
        button.Pressed += () => ReturnRequested?.Invoke();
        box.AddChild(button);
    }

    private static void AddPodiumSlot(Control stage, Racer racer, int place, Vector2 blockPosition, Vector2 blockSize)
    {
        var block = new PanelContainer { Position = blockPosition, Size = blockSize };
        var style = new StyleBoxFlat
        {
            BgColor = place == 1 ? Color.FromHtml("#EBCB63") : place == 2 ? Color.FromHtml("#CDD0C8") : Color.FromHtml("#C28B5D"),
            BorderColor = Color.FromHtml("#7E6856")
        };
        style.SetBorderWidthAll(2);
        style.CornerRadiusTopLeft = style.CornerRadiusTopRight = 3;
        block.AddThemeStyleboxOverride("panel", style);
        stage.AddChild(block);

        var placeLabel = UiFactory.CreateLabel(place.ToString(), 14);
        placeLabel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        placeLabel.HorizontalAlignment = HorizontalAlignment.Center;
        placeLabel.VerticalAlignment = VerticalAlignment.Center;
        block.AddChild(placeLabel);

        var portraitX = blockPosition.X + (blockSize.X - 48.0f) * 0.5f;
        var portraitY = blockPosition.Y - 49.0f;
        var portrait = UiFactory.CreatePortrait(racer.Data, new Vector2(48, 48));
        portrait.Position = new Vector2(portraitX, portraitY);
        portrait.Size = new Vector2(48, 48);
        stage.AddChild(portrait);

        var name = UiFactory.CreateLabel(racer.Data.Name, 6);
        name.Position = new Vector2(blockPosition.X + (blockSize.X - 100.0f) * 0.5f, portraitY - 15.0f);
        name.Size = new Vector2(100, 14);
        name.HorizontalAlignment = HorizontalAlignment.Center;
        stage.AddChild(name);
    }

    private static void AddFourthPlacePuddle(Control stage, Racer racer, Vector2 position)
    {
        var puddle = new PanelContainer
        {
            Position = new Vector2(position.X - 8, position.Y + 41),
            Size = new Vector2(62, 14)
        };
        var puddleStyle = new StyleBoxFlat
        {
            BgColor = Color.FromHtml("#7FC5C8"),
            BorderColor = Color.FromHtml("#5A9DA6")
        };
        puddleStyle.SetBorderWidthAll(1);
        puddleStyle.CornerRadiusTopLeft = puddleStyle.CornerRadiusTopRight = 8;
        puddleStyle.CornerRadiusBottomLeft = puddleStyle.CornerRadiusBottomRight = 8;
        puddle.AddThemeStyleboxOverride("panel", puddleStyle);
        stage.AddChild(puddle);

        var portrait = UiFactory.CreatePortrait(racer.Data, new Vector2(48, 48));
        portrait.Position = position;
        portrait.Size = new Vector2(48, 48);
        stage.AddChild(portrait);

        var name = UiFactory.CreateLabel($"4th • {racer.Data.Name}", 6);
        name.Position = new Vector2(position.X - 18, position.Y - 17);
        name.Size = new Vector2(84, 14);
        name.HorizontalAlignment = HorizontalAlignment.Center;
        stage.AddChild(name);
    }

    private static Vector2[] BuildEllipsePoints(float radiusX, float radiusY, int count)
    {
        var points = new Vector2[count];
        for (var i = 0; i < count; i++)
        {
            var angle = Mathf.Tau * i / count;
            points[i] = new Vector2(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY);
        }
        return points;
    }

    private static SpriteFrames BuildRaceFrames()
    {
        var frames = new SpriteFrames();
        frames.RemoveAnimation("default");

        frames.AddAnimation("run");
        frames.SetAnimationLoop("run", true);
        frames.SetAnimationSpeed("run", 8.0);
        for (var column = 0; column < 4; column++)
        {
            var atlas = new AtlasTexture
            {
                Atlas = CharacterTexture,
                Region = new Rect2(column * 48, 3 * 48, 48, 48)
            };
            frames.AddFrame("run", atlas);
        }

        frames.AddAnimation("swim");
        frames.SetAnimationLoop("swim", true);
        frames.SetAnimationSpeed("swim", 10.0);
        for (var column = 0; column < 8; column++)
        {
            var atlas = new AtlasTexture
            {
                Atlas = SwimmingTexture,
                Region = new Rect2(column * 48, 3 * 48, 48, 48)
            };
            frames.AddFrame("swim", atlas);
        }

        return frames;
    }
}
