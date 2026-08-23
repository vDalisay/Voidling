using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace VoidlingGame;

public partial class RaceController : Node2D
{
    public event Action? ReturnRequested;

    private static readonly Texture2D CharacterTexture = GD.Load<Texture2D>(
        "res://Assets/Sprout Lands - Sprites - Basic pack/Characters/Basic Charakter Spritesheet.png");

    private static readonly Texture2D ObstacleTexture = GD.Load<Texture2D>(
        "res://Assets/Sprout Lands - Sprites - Basic pack/Objects/Basic Grass Biom things 1.png");

    private readonly float[] _obstacleXs = { 190.0f, 330.0f, 470.0f };
    private readonly List<Racer> _racers = new();
    private readonly List<Racer> _finishOrder = new();
    private bool _running;
    private bool _resultsShown;
    private string _selectedId = "";

    private sealed class Racer
    {
        public VoidlingData Data { get; init; } = null!;
        public AnimatedSprite2D Sprite { get; init; } = null!;
        public Random Random { get; init; } = null!;
        public float X { get; set; } = 45.0f;
        public float LaneY { get; init; }
        public int NextObstacle { get; set; }
        public float DelaySeconds { get; set; }
        public float JumpSeconds { get; set; }
        public bool Finished { get; set; }
    }

    public void Setup(VoidlingData selected)
    {
        _selectedId = selected.Id;
        var seed = GameSession.Instance.CreateRaceSeed();

        CreateBackdropLabel();
        var participants = BuildParticipants(selected, seed);

        for (var i = 0; i < participants.Count; i++)
        {
            var data = participants[i];
            var laneY = 74.0f + i * 66.0f;

            var sprite = new AnimatedSprite2D
            {
                SpriteFrames = BuildRunFrames(),
                Position = new Vector2(45, laneY - 8),
                Scale = new Vector2(0.68f, 0.68f),
                Modulate = GameRules.TintColor(data.TintHex)
            };
            AddChild(sprite);
            sprite.Play("run");

            var label = UiFactory.CreateLabel(data.Id == selected.Id ? $"{data.Name}  YOU" : $"{data.Name}  CPU", 9);
            label.Position = new Vector2(10, laneY + 13);
            label.Size = new Vector2(135, 16);
            AddChild(label);

            var racer = new Racer
            {
                Data = data,
                Sprite = sprite,
                Random = GeneticsService.CreateRandom(seed, $"race:{data.Id}:{i}"),
                LaneY = laneY
            };
            _racers.Add(racer);

            foreach (var obstacleX in _obstacleXs)
                AddObstacle(obstacleX, laneY);
        }

        _running = true;
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (!_running)
            return;

        var step = (float)delta;

        foreach (var racer in _racers)
        {
            if (racer.Finished)
                continue;

            if (racer.DelaySeconds > 0.0f)
            {
                racer.DelaySeconds = Math.Max(0.0f, racer.DelaySeconds - step);
                UpdateRacerPosition(racer, step);
                continue;
            }

            var run = GameRules.EffectiveStat(racer.Data, "run");
            var stamina = GameRules.EffectiveStat(racer.Data, "stamina");
            var progress = Mathf.Clamp((racer.X - 45.0f) / 535.0f, 0.0f, 1.0f);

            var speed = 27.0f + run * 0.34f;
            if (progress > 0.62f)
                speed *= 0.78f + stamina * 0.0022f;

            racer.X += speed * step;

            if (racer.NextObstacle < _obstacleXs.Length &&
                racer.X >= _obstacleXs[racer.NextObstacle] - 12.0f)
            {
                ResolveObstacle(racer, run);
                racer.NextObstacle++;
            }

            if (racer.X >= 580.0f)
            {
                racer.X = 580.0f;
                racer.Finished = true;
                _finishOrder.Add(racer);
                racer.Sprite.Stop();
            }

            UpdateRacerPosition(racer, step);
        }

        if (_finishOrder.Count == _racers.Count && !_resultsShown)
        {
            _running = false;
            ShowResults();
        }
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(0, 0, 640, 360), Color.FromHtml("#A7D8C7"));

        for (var i = 0; i < 4; i++)
        {
            var y = 54 + i * 66;
            DrawRect(new Rect2(24, y, 592, 48), Color.FromHtml("#C8DC7A"));
            DrawLine(new Vector2(24, y + 48), new Vector2(616, y + 48), Color.FromHtml("#7F9D65"), 2.0f);
        }

        DrawLine(new Vector2(579, 48), new Vector2(579, 310), Color.FromHtml("#F4F0D9"), 3.0f);
        for (var y = 48; y < 310; y += 12)
        {
            if ((y / 12) % 2 == 0)
                DrawRect(new Rect2(579, y, 5, 6), Color.FromHtml("#596159"));
        }
    }

    private void ResolveObstacle(Racer racer, float run)
    {
        var avoidChance = Mathf.Clamp(0.28f + run / 100.0f * 0.67f, 0.28f, 0.95f);

        if (racer.Random.NextDouble() <= avoidChance)
            racer.JumpSeconds = 0.58f;
        else
        {
            racer.DelaySeconds = 0.62f + (100.0f - run) / 100.0f * 0.55f;
            racer.X -= 4.0f;
        }
    }

    private void UpdateRacerPosition(Racer racer, float delta)
    {
        var yOffset = 0.0f;

        if (racer.JumpSeconds > 0.0f)
        {
            racer.JumpSeconds = Math.Max(0.0f, racer.JumpSeconds - delta);
            var normalized = 1.0f - racer.JumpSeconds / 0.58f;
            yOffset = -Mathf.Sin(normalized * Mathf.Pi) * 12.0f;
        }

        racer.Sprite.Position = new Vector2(racer.X, racer.LaneY - 8 + yOffset);
    }

    private List<VoidlingData> BuildParticipants(VoidlingData selected, ulong seed)
    {
        // Minigames are Chao-style: exactly one owned entrant; every opponent is generated for the event.
        var result = new List<VoidlingData> { selected };
        var cpuNames = new[] { "Fern", "Moss", "Puck", "Clover", "Pebble", "Dew" };

        for (var cpuIndex = 0; cpuIndex < 3; cpuIndex++)
        {
            var cpuSeed = seed + (ulong)(100 + cpuIndex * 17);
            var genome = GeneticsService.CreateRandomGenome(cpuSeed);
            var cpu = new VoidlingData
            {
                Id = $"cpu-{cpuIndex}-{cpuSeed}",
                Name = cpuNames[(int)(cpuSeed % (ulong)cpuNames.Length)],
                Genome = genome,
                Stage = LifeStage.Adult,
                TintHex = GeneticsService.ResolveTint(genome),
                TrainingPoints = GameRules.StatIds.ToDictionary(id => id, _ => 0)
            };
            result.Add(cpu);
        }

        return result;
    }

    private void AddObstacle(float x, float laneY)
    {
        var atlas = new AtlasTexture
        {
            Atlas = ObstacleTexture,
            Region = new Rect2(64, 48, 16, 16)
        };

        var obstacle = new Sprite2D
        {
            Texture = atlas,
            Position = new Vector2(x, laneY),
            Scale = new Vector2(1.3f, 1.3f)
        };
        AddChild(obstacle);
    }

    private void CreateBackdropLabel()
    {
        var title = UiFactory.CreateTitle("AUTOMATED RUN TRIAL");
        title.Position = new Vector2(224, 12);
        title.Size = new Vector2(230, 24);
        AddChild(title);
    }

    private void ShowResults()
    {
        _resultsShown = true;
        var selectedPlace = _finishOrder.FindIndex(r => r.Data.Id == _selectedId) + 1;
        if (selectedPlace <= 0)
            selectedPlace = 4;

        GameSession.Instance.AddRaceReward(selectedPlace);

        var canvas = new CanvasLayer();
        AddChild(canvas);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        canvas.AddChild(center);

        var panel = UiFactory.CreatePanel(new Vector2(280, 185));
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
