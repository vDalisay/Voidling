using System;
using Godot;

namespace VoidlingGame;

public partial class VoidlingActor : Node2D
{
    public event Action<string>? Clicked;

    public string CreatureId { get; private set; } = "";

    private static readonly Texture2D CharacterTexture = GD.Load<Texture2D>(
        "res://Assets/Sprout Lands - Sprites - Basic pack/Characters/Basic Charakter Spritesheet.png");

    private readonly RandomNumberGenerator _rng = new();
    private AnimatedSprite2D _sprite = null!;
    private Rect2 _wanderBounds;
    private Vector2 _target;
    private float _nextTargetSeconds;
    private float _walkSpeed;
    private bool _selected;
    private bool _rare;

    public void Setup(VoidlingData data, Rect2 wanderBounds, Vector2 startPosition)
    {
        CreatureId = data.Id;
        _wanderBounds = wanderBounds;
        Position = startPosition;
        _walkSpeed = data.Stage == LifeStage.Adult ? 20.0f : 17.0f;
        _rare = data.RareTraits.Count > 0;

        _rng.Seed = (ulong)Math.Abs(data.Id.GetHashCode()) + 17UL;

        _sprite = new AnimatedSprite2D
        {
            SpriteFrames = BuildSpriteFrames(),
            Scale = new Vector2(0.62f, 0.62f),
            Position = new Vector2(0, -8),
            Modulate = GameRules.TintColor(data.TintHex)
        };
        AddChild(_sprite);
        _sprite.Play("walk_down");

        var area = new Area2D
        {
            InputPickable = true
        };
        var collision = new CollisionShape2D
        {
            Shape = new RectangleShape2D
            {
                Size = new Vector2(23, 27)
            },
            Position = new Vector2(0, -8)
        };
        area.AddChild(collision);
        area.InputEvent += OnInputEvent;
        AddChild(area);

        PickNewTarget();
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (_sprite == null)
            return;

        var step = (float)delta;
        _nextTargetSeconds -= step;

        var toTarget = _target - Position;
        if (_nextTargetSeconds <= 0.0f || toTarget.LengthSquared() < 9.0f)
        {
            PickNewTarget();
            toTarget = _target - Position;
        }

        if (toTarget.LengthSquared() > 1.0f)
        {
            var direction = toTarget.Normalized();
            Position += direction * _walkSpeed * step;
            Position = new Vector2(
                Mathf.Clamp(Position.X, _wanderBounds.Position.X, _wanderBounds.End.X),
                Mathf.Clamp(Position.Y, _wanderBounds.Position.Y, _wanderBounds.End.Y));
            PlayForDirection(direction);
        }

        if (_rare)
            QueueRedraw();
    }

    public void SetSelected(bool selected)
    {
        _selected = selected;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_selected)
        {
            DrawArc(
                new Vector2(0, 5),
                14.0f,
                0.0f,
                Mathf.Tau,
                24,
                Color.FromHtml("#FFF4A8"),
                2.0f);
        }

        if (_rare)
        {
            var t = (float)Time.GetTicksMsec() / 350.0f;
            for (var i = 0; i < 3; i++)
            {
                var angle = t + i * Mathf.Tau / 3.0f;
                var p = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 15.0f + new Vector2(0, -8);
                DrawCircle(p, 1.2f, Color.FromHtml("#FFF7B7"));
            }
        }
    }

    private void PickNewTarget()
    {
        _target = new Vector2(
            _rng.RandfRange(_wanderBounds.Position.X, _wanderBounds.End.X),
            _rng.RandfRange(_wanderBounds.Position.Y, _wanderBounds.End.Y));
        _nextTargetSeconds = _rng.RandfRange(1.5f, 4.0f);
    }

    private void PlayForDirection(Vector2 direction)
    {
        StringName animation;

        if (Mathf.Abs(direction.X) > Mathf.Abs(direction.Y))
            animation = direction.X < 0.0f ? "walk_left" : "walk_right";
        else
            animation = direction.Y < 0.0f ? "walk_up" : "walk_down";

        if (_sprite.Animation != animation)
            _sprite.Play(animation);
    }

    private void OnInputEvent(Node viewport, InputEvent inputEvent, long shapeIndex)
    {
        if (inputEvent is InputEventMouseButton mouse &&
            mouse.ButtonIndex == MouseButton.Left &&
            mouse.Pressed)
        {
            Clicked?.Invoke(CreatureId);
        }
    }

    private static SpriteFrames BuildSpriteFrames()
    {
        var frames = new SpriteFrames();
        frames.RemoveAnimation("default");

        AddDirection(frames, "walk_down", 0);
        AddDirection(frames, "walk_left", 1);
        AddDirection(frames, "walk_right", 2);
        AddDirection(frames, "walk_up", 3);

        return frames;
    }

    private static void AddDirection(SpriteFrames frames, string name, int row)
    {
        frames.AddAnimation(name);
        frames.SetAnimationLoop(name, true);
        frames.SetAnimationSpeed(name, 6.0);

        for (var column = 0; column < 4; column++)
        {
            var atlas = new AtlasTexture
            {
                Atlas = CharacterTexture,
                Region = new Rect2(column * 48, row * 48, 48, 48)
            };
            frames.AddFrame(name, atlas);
        }
    }
}
