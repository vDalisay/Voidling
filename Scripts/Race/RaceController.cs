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
    private const float CheerDuration = 2.0f;
    private const float CheerCost = 24.0f;
    private const float RunningStaminaDrainPerSecond = 2.1f;

    private static readonly Texture2D CharacterTexture = GD.Load<Texture2D>(
        "res://Assets/Sprout Lands - Sprites - Basic pack/Characters/Basic Charakter Spritesheet.png");

    private static readonly Texture2D ObstacleTexture = GD.Load<Texture2D>(
        "res://Assets/Sprout Lands - Sprites - Basic pack/Objects/Basic Grass Biom things 1.png");

    private readonly float[] _obstacleXs = { 360.0f, 650.0f, 930.0f, 1215.0f, 1490.0f, 1680.0f };
    private readonly float[] _racerOffsets = { -16.0f, -5.0f, 6.0f, 17.0f };
    private readonly List<Racer> _racers = new();
    private readonly List<Racer> _finishOrder = new();

    private bool _running;
    private bool _resultsShown;
    private string _selectedId = "";
    private Racer? _player;
    private Camera2D _camera = null!;
    private Polygon2D _playerMarker = null!;
    private Button _cheerButton = null!;
    private ProgressBar _staminaBar = null!;
    private Label _staminaLabel = null!;
    private RaceMiniMap _miniMap = null!;

    private sealed class Racer
    {
        public VoidlingData Data { get; init; } = null!;
        public AnimatedSprite2D Sprite { get; init; } = null!;
        public Random Random { get; init; } = null!;
        public float X { get; set; } = TrackStartX;
        public float BaseY { get; init; }
        public int NextObstacle { get; set; }
        public float DelaySeconds { get; set; }
        public float JumpSeconds { get; set; }
        public float CheerSeconds { get; set; }
        public float MaxStamina { get; init; }
        public float CurrentStamina { get; set; }
        public bool Finished { get; set; }
    }

    public void Setup(VoidlingData selected)
    {
        _selectedId = selected.Id;
        var seed = GameSession.Instance.CreateRaceSeed();
        var participants = BuildParticipants(selected, seed);

        for (var i = 0; i < participants.Count; i++)
        {
            var data = participants[i];
            var staminaStat = GameRules.EffectiveStat(data, "stamina");
            var maxStamina = 72.0f + staminaStat * 1.05f;

            var sprite = new AnimatedSprite2D
            {
                SpriteFrames = BuildRunFrames(),
                Position = new Vector2(TrackStartX, TrackY + _racerOffsets[i] - 8.0f),
                Scale = new Vector2(0.72f, 0.72f),
                Modulate = GameRules.TintColor(data.TintHex),
                ZIndex = 10 + i
            };
            AddChild(sprite);
            sprite.Play("run");

            var racer = new Racer
            {
                Data = data,
                Sprite = sprite,
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
            AddObstacle(obstacleX);

        CreateCamera();
        CreatePlayerMarker();
        CreateHud();
        _running = true;
        QueueRedraw();
        UpdateHud();
    }

    public override void _Process(double delta)
    {
        if (!_running || _player == null)
            return;

        var step = (float)delta;

        foreach (var racer in _racers)
        {
            if (racer.Finished)
                continue;

            racer.CurrentStamina = Math.Max(0.0f, racer.CurrentStamina - RunningStaminaDrainPerSecond * step);
            racer.CheerSeconds = Math.Max(0.0f, racer.CheerSeconds - step);

            if (racer.DelaySeconds > 0.0f)
            {
                racer.DelaySeconds = Math.Max(0.0f, racer.DelaySeconds - step);
                UpdateRacerPosition(racer, step);
                continue;
            }

            var run = GameRules.EffectiveStat(racer.Data, "run");
            var speed = 31.0f + run * 0.36f;
            var staminaRatio = racer.MaxStamina <= 0.0f ? 0.0f : racer.CurrentStamina / racer.MaxStamina;

            if (staminaRatio < 0.18f)
                speed *= 0.90f;
            if (racer.CurrentStamina <= 0.01f)
                speed *= 0.84f;
            if (racer.CheerSeconds > 0.0f)
                speed *= 1.22f;

            racer.X += speed * step;

            if (racer.NextObstacle < _obstacleXs.Length &&
                racer.X >= _obstacleXs[racer.NextObstacle] - 14.0f)
            {
                ResolveObstacle(racer, run);
                racer.NextObstacle++;
            }

            if (racer.X >= TrackEndX)
            {
                racer.X = TrackEndX;
                racer.Finished = true;
                _finishOrder.Add(racer);
                racer.Sprite.Stop();
            }

            UpdateRacerPosition(racer, step);
        }

        UpdatePlayerTracking();
        UpdateHud();

        if (_finishOrder.Count == _racers.Count && !_resultsShown)
        {
            _running = false;
            ShowResults();
        }
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(0, 0, TrackEndX + 180.0f, ScreenHeight), Color.FromHtml("#A7D8C7"));
        DrawRect(new Rect2(0, 102, TrackEndX + 180.0f, 166), Color.FromHtml("#8EBE85"));

        // One shared Pokeathlon/Chao-style track rather than four isolated lanes.
        DrawRect(new Rect2(20, 126, TrackEndX + 90.0f, 118), Color.FromHtml("#D9774E"));
        DrawLine(new Vector2(20, 126), new Vector2(TrackEndX + 90.0f, 126), Color.FromHtml("#E9B777"), 4.0f);
        DrawLine(new Vector2(20, 244), new Vector2(TrackEndX + 90.0f, 244), Color.FromHtml("#9C6049"), 4.0f);

        for (var x = 40.0f; x < TrackEndX; x += 80.0f)
            DrawLine(new Vector2(x, 184), new Vector2(x + 28, 184), new Color(0.95f, 0.72f, 0.52f, 0.34f), 2.0f);

        DrawLine(new Vector2(TrackEndX, 122), new Vector2(TrackEndX, 250), Colors.White, 4.0f);
        for (var y = 126.0f; y < 246.0f; y += 12.0f)
        {
            if (((int)y / 12) % 2 == 0)
                DrawRect(new Rect2(TrackEndX, y, 6, 6), Color.FromHtml("#4B514A"));
        }
    }

    private void CreateCamera()
    {
        _camera = new Camera2D
        {
            Enabled = true,
            Position = new Vector2(ScreenWidth * 0.5f, ScreenHeight * 0.5f),
            PositionSmoothingEnabled = true,
            PositionSmoothingSpeed = 7.0f
        };
        AddChild(_camera);
    }

    private void CreatePlayerMarker()
    {
        _playerMarker = new Polygon2D
        {
            Polygon = new Vector2[]
            {
                new(-7, -9),
                new(7, -9),
                new(0, 0)
            },
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
        title.Position = new Vector2(266, 10);
        title.Size = new Vector2(170, 24);
        canvas.AddChild(title);

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
        background.CornerRadiusTopLeft = 2;
        background.CornerRadiusTopRight = 2;
        background.CornerRadiusBottomLeft = 2;
        background.CornerRadiusBottomRight = 2;
        var fill = new StyleBoxFlat { BgColor = Color.FromHtml("#B9D76D") };
        fill.CornerRadiusTopLeft = 2;
        fill.CornerRadiusTopRight = 2;
        fill.CornerRadiusBottomLeft = 2;
        fill.CornerRadiusBottomRight = 2;
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
        UpdateHud();
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

        var cameraX = Mathf.Clamp(_player.X, ScreenWidth * 0.5f, TrackEndX - ScreenWidth * 0.5f + 70.0f);
        _camera.Position = new Vector2(cameraX, ScreenHeight * 0.5f);
        _playerMarker.Position = new Vector2(_player.X, _player.Sprite.Position.Y - 20.0f);
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
        if (racer.JumpSeconds > 0.0f)
        {
            racer.JumpSeconds = Math.Max(0.0f, racer.JumpSeconds - delta);
            var normalized = 1.0f - racer.JumpSeconds / 0.58f;
            yOffset = -Mathf.Sin(normalized * Mathf.Pi) * 17.0f;
        }

        racer.Sprite.Position = new Vector2(racer.X, racer.BaseY - 8.0f + yOffset);
    }

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

    private void AddObstacle(float x)
    {
        var atlas = new AtlasTexture
        {
            Atlas = ObstacleTexture,
            Region = new Rect2(64, 48, 16, 16)
        };

        var obstacle = new Sprite2D
        {
            Texture = atlas,
            Position = new Vector2(x, TrackY + 10.0f),
            Scale = new Vector2(1.45f, 1.45f),
            ZIndex = 5
        };
        AddChild(obstacle);
    }

    private void ShowResults()
    {
        _resultsShown = true;
        var selectedPlace = _finishOrder.FindIndex(r => r.Data.Id == _selectedId) + 1;
        if (selectedPlace <= 0)
            selectedPlace = 4;

        GameSession.Instance.AddRaceReward(selectedPlace);

        var canvas = new CanvasLayer { Layer = 50 };
        AddChild(canvas);
        var shade = new ColorRect
        {
            Color = new Color(0.12f, 0.18f, 0.16f, 0.45f),
            Position = Vector2.Zero,
            Size = new Vector2(ScreenWidth, ScreenHeight),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        canvas.AddChild(shade);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        canvas.AddChild(center);

        var panel = UiFactory.CreatePanel(new Vector2(290, 190));
        center.AddChild(panel);
        var box = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        box.AddThemeConstantOverride("separation", 5);
        panel.AddChild(box);
        box.AddChild(UiFactory.CreateTitle($"FINISH — #{selectedPlace}"));

        for (var i = 0; i < _finishOrder.Count; i++)
        {
            var racer = _finishOrder[i];
            var marker = racer.Data.Id == _selectedId ? "  < YOU" : "  CPU";
            box.AddChild(UiFactory.CreateLabel($"{i + 1}. {racer.Data.Name}{marker}", 10));
        }

        var button = UiFactory.CreateButton("Return to Garden");
        button.Pressed += () => ReturnRequested?.Invoke();
        box.AddChild(button);
    }

    private static SpriteFrames BuildRunFrames()
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
                Region = new Rect2(column * 48, 2 * 48, 48, 48)
            };
            frames.AddFrame("run", atlas);
        }

        return frames;
    }
}
