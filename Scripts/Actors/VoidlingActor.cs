using System;
using Godot;
using Voidling.Presentation.Voidlings;

namespace VoidlingGame;

public partial class VoidlingActor : Node2D
{
    public event Action<string>? Clicked;

    public string CreatureId { get; private set; } = "";
    public LifeStage Stage { get; private set; }

    private readonly RandomNumberGenerator _rng = new();
    private AnimatedSprite2D _sprite = null!;
    private Rect2 _wanderBounds;
    private Vector2 _target;
    private float _nextTargetSeconds;
    private float _baseWalkSpeed;
    private float _walkSpeed;
    private float _restSeconds;
    private float _restSecondsMin = 0.20f;
    private float _restSecondsMax = 0.60f;
    private bool _selected;
    private bool _interactionLocked;
    private bool _pickedUp;
    private float _baseScale;
    private float _baseSpriteY;

    public void Setup(VoidlingData data, Rect2 wanderBounds, Vector2 startPosition)
    {
        CreatureId = data.Id;
        Stage = data.Stage;
        _wanderBounds = wanderBounds;
        Position = startPosition;
        _baseWalkSpeed = data.Stage == LifeStage.Adult ? 20.0f : 17.0f;
        _walkSpeed = _baseWalkSpeed;
        _rng.Seed = StableSeed(data.Id);

        var isAdult = data.Stage == LifeStage.Adult;
        _baseScale = VoidlingVisualFactory.WorldScale(isAdult);
        _baseSpriteY = VoidlingGroundVisualMetrics.SpriteCenterYOffset(_baseScale);

        _sprite = new AnimatedSprite2D
        {
            SpriteFrames = VoidlingVisualFactory.GetWorldFrames(),
            Scale = Vector2.One * _baseScale,
            Position = new Vector2(0, _baseSpriteY),
            ZIndex = 2
        };
        VoidlingVisualFactory.ApplyAppearance(
            _sprite,
            data.Genome,
            data.TintHex,
            VoidlingAppearanceContext.World);
        AddChild(_sprite);
        _sprite.Play("walk_down");

        var mutationAdornment = new MutationAdornment2D();
        mutationAdornment.Setup(data, _sprite);
        AddChild(mutationAdornment);

        var area = new Area2D { InputPickable = true };
        var collision = new CollisionShape2D
        {
            Shape = new RectangleShape2D
            {
                Size = VoidlingVisualFactory.WorldHitboxSize(isAdult)
            },
            Position = new Vector2(0, _baseSpriteY)
        };
        area.AddChild(collision);
        area.InputEvent += OnInputEvent;
        AddChild(area);

        PickNewTarget();
        QueueRedraw();
    }

    public void ApplyAmbientStats(float run, float stamina)
    {
        var behavior = VoidlingAmbientBehaviorResolver.Resolve(run, stamina);
        _walkSpeed = _baseWalkSpeed * behavior.WalkSpeedMultiplier;
        _restSecondsMin = behavior.RestSecondsMin;
        _restSecondsMax = Math.Max(_restSecondsMin, behavior.RestSecondsMax);
        _restSeconds = Math.Min(_restSeconds, _restSecondsMax);
    }

    public override void _Process(double delta)
    {
        if (_selected)
            QueueRedraw();

        if (_sprite == null || _interactionLocked || _pickedUp)
            return;

        var step = (float)delta;
        if (_restSeconds > 0.0f)
        {
            _restSeconds = Math.Max(0.0f, _restSeconds - step);
            if (_restSeconds > 0.0f)
            {
                _sprite.Stop();
                return;
            }

            PickNewTarget();
            _sprite.Play("walk_down");
        }

        _nextTargetSeconds -= step;
        var toTarget = _target - Position;

        if (toTarget.LengthSquared() < 9.0f)
        {
            BeginRest();
            return;
        }

        if (_nextTargetSeconds <= 0.0f)
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
    }

    public void SetSelected(bool selected)
    {
        _selected = selected;
        QueueRedraw();
    }

    public void SetInteractionLocked(bool locked)
    {
        _interactionLocked = locked;
        RefreshMovementState();
    }

    public void PlayWalk(Vector2 direction)
    {
        if (_sprite == null)
            return;
        PlayForDirection(direction);
        _sprite.Play();
    }

    public void PlayIdle()
    {
        if (_sprite != null)
            _sprite.Stop();
    }

    public void SetPickedUp(bool pickedUp)
    {
        var wasPickedUp = _pickedUp;
        _pickedUp = pickedUp;
        ZIndex = pickedUp ? 90 : 0;

        if (_sprite != null)
        {
            _sprite.Scale = Vector2.One * (
                pickedUp
                    ? _baseScale * VoidlingVisualFactory.HeldScaleMultiplier
                    : _baseScale);
            if (pickedUp)
            {
                _sprite.Position = new Vector2(
                    0,
                    _baseSpriteY + VoidlingVisualFactory.HeldSpriteYOffset);
            }
            else if (wasPickedUp)
            {
                var drop = CreateTween();
                drop.TweenProperty(_sprite, "position", new Vector2(0, _baseSpriteY), 0.20)
                    .SetTrans(Tween.TransitionType.Bounce)
                    .SetEase(Tween.EaseType.Out);
            }
            else
            {
                _sprite.Position = new Vector2(0, _baseSpriteY);
            }
        }

        RefreshMovementState();
        QueueRedraw();
    }

    public async void PlayHatchJump()
    {
        if (_sprite == null)
            return;

        _interactionLocked = true;
        _sprite.Stop();
        var start = new Vector2(0, _baseSpriteY + 2.0f);
        _sprite.Position = start;

        var jump = CreateTween();
        jump.TweenProperty(_sprite, "position", new Vector2(0, _baseSpriteY - 15.0f), 0.20)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        jump.TweenProperty(_sprite, "position", new Vector2(0, _baseSpriteY), 0.24)
            .SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out);
        await ToSignal(jump, Tween.SignalName.Finished);

        _interactionLocked = false;
        RefreshMovementState();
    }

    public override void _Draw()
    {
        var shadowAlpha = _pickedUp ? 0.26f : 0.20f;
        var shadowRadii = VoidlingGroundVisualMetrics.ShadowRadii(_baseScale);
        if (_pickedUp)
            shadowRadii.X *= 1.08f;
        DrawEllipse(
            new Vector2(0, VoidlingGroundVisualMetrics.ShadowCenterYOffset),
            shadowRadii,
            new Color(0.20f, 0.24f, 0.20f, shadowAlpha));

        if (_selected)
        {
            var phase = (float)Time.GetTicksMsec() / 220.0f;
            var pulse = (Mathf.Sin(phase) + 1.0f) * 0.5f;
            var baseRadius = _baseScale < 0.5f ? 4.2f : 7.2f;
            var radius = baseRadius + pulse * 1.25f;
            var color = Color.FromHtml("#FFF4A8");
            color.A = 0.70f + pulse * 0.25f;
            DrawArc(Vector2.Zero, radius, 0.0f, Mathf.Tau, 24, color, 1.0f, false);
        }
    }

    private void RefreshMovementState()
    {
        if (_sprite == null)
            return;

        if (_interactionLocked || _pickedUp)
        {
            _sprite.Stop();
            return;
        }

        _restSeconds = 0.0f;
        PickNewTarget();
        _sprite.Play("walk_down");
    }

    private void PickNewTarget()
    {
        _target = new Vector2(
            _rng.RandfRange(_wanderBounds.Position.X, _wanderBounds.End.X),
            _rng.RandfRange(_wanderBounds.Position.Y, _wanderBounds.End.Y));
        _nextTargetSeconds = _rng.RandfRange(1.5f, 4.0f);
    }

    private void BeginRest()
    {
        _restSeconds = _rng.RandfRange(_restSecondsMin, _restSecondsMax);
        _sprite.Stop();
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
        if (!_interactive ||
            inputEvent is not InputEventMouseButton mouse ||
            mouse.ButtonIndex != MouseButton.Left)
        {
            return;
        }

        var garden = FindGardenController();
        if (mouse.Pressed)
        {
            garden?.BeginVoidlingPointerInteraction(CreatureId);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (garden != null)
            garden.EndVoidlingPointerInteraction(CreatureId);
        else
            Clicked?.Invoke(CreatureId);
        GetViewport().SetInputAsHandled();
    }

    private GardenController? FindGardenController()
    {
        Node? current = GetParent();
        while (current != null)
        {
            if (current is GardenController garden)
                return garden;
            current = current.GetParent();
        }
        return null;
    }

    private void DrawEllipse(Vector2 center, Vector2 radii, Color color, int points = 20)
    {
        var polygon = new Vector2[points];
        for (var i = 0; i < points; i++)
        {
            var angle = Mathf.Tau * i / points;
            polygon[i] = center + new Vector2(Mathf.Cos(angle) * radii.X, Mathf.Sin(angle) * radii.Y);
        }
        DrawColoredPolygon(polygon, color);
    }

    private static ulong StableSeed(string text)
    {
        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        var hash = offset;
        foreach (var c in text)
        {
            hash ^= c;
            hash *= prime;
        }
        return hash;
    }
}
