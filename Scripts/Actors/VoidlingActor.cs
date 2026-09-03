using System;
using Godot;
using Voidling.Presentation.Voidlings;

namespace VoidlingGame;

public partial class VoidlingActor : Node2D
{
    public event Action<string>? Clicked;

    public string CreatureId { get; private set; } = "";

    private readonly RandomNumberGenerator _rng = new();
    private AnimatedSprite2D _sprite = null!;
    private Rect2 _wanderBounds;
    private Vector2 _target;
    private float _nextTargetSeconds;
    private float _walkSpeed;
    private bool _selected;
    private bool _interactionLocked;
    private bool _pickedUp;
    private float _baseScale;
    private float _baseSpriteY;
    private string _visualTypeId = VoidlingAppearanceData.DefaultVisualTypeId;
    private float _heldScaleMultiplier = 1.0f;
    private float _heldSpriteYOffset;
    private float _shadowCenterYOffset;
    private VoidlingVisualAppearance _appearance;
    private Vector2 _tileCenter;
    private float _tileRadius;
    private StringName _tileAnimation = "";

    public void Setup(VoidlingData data, Rect2 wanderBounds, Vector2 startPosition)
    {
        CreatureId = data.Id;
        _wanderBounds = wanderBounds;
        Position = startPosition;
        _walkSpeed = data.Stage == LifeStage.Adult ? 20.0f : 17.0f;
        _rng.Seed = StableSeed(data.Id);

        var appearance = VoidlingVisualAppearance.From(data.Appearance, data.TintHex);
        _appearance = appearance;
        var definition = VoidlingVisualFactory.ResolveDefinition(appearance.VisualTypeId);
        _visualTypeId = definition.DefinitionId;
        var isAdult = data.Stage == LifeStage.Adult;
        _baseScale = VoidlingVisualFactory.WorldScale(isAdult, _visualTypeId);
        _baseSpriteY = VoidlingVisualFactory.WorldSpriteCenterYOffset(_baseScale, _visualTypeId);
        _heldScaleMultiplier = definition.HeldScaleMultiplier;
        _heldSpriteYOffset = definition.HeldSpriteYOffset;
        _shadowCenterYOffset = VoidlingVisualFactory.ShadowCenterYOffset(_baseScale, _visualTypeId);

        _sprite = new AnimatedSprite2D
        {
            Scale = Vector2.One * _baseScale,
            Position = new Vector2(0, _baseSpriteY),
            ZIndex = 2
        };
        VoidlingVisualFactory.ApplyAppearance(_sprite, appearance, race: false);
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
                Size = VoidlingVisualFactory.WorldHitboxSize(isAdult, _visualTypeId)
            },
            Position = new Vector2(0, _baseSpriteY)
        };
        area.AddChild(collision);
        area.InputEvent += OnInputEvent;
        AddChild(area);

        PickNewTarget();
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (_selected)
            QueueRedraw();

        if (_sprite == null || _interactionLocked || _pickedUp)
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
            Position = ClampToWanderArea(Position);
            PlayForDirection(direction);
        }
    }

    /// <summary>True while this Voidling is training on a land tile and stays on that ground.</summary>
    public bool IsOnTile => _tileRadius > 0.0f;

    /// <summary>
    /// Keeps a training Voidling on its own tile, doing the activity that tile trains. It only
    /// leaves when the player picks it up and puts it down somewhere else.
    /// </summary>
    public void ConfineToTile(Vector2 center, float radius, StringName activityAnimation)
    {
        if (_tileRadius > 0.0f &&
            _tileCenter.IsEqualApprox(center) &&
            Mathf.IsEqualApprox(_tileRadius, radius) &&
            _tileAnimation == activityAnimation)
        {
            return;
        }

        _tileCenter = center;
        _tileRadius = Mathf.Max(1.0f, radius);
        _tileAnimation = activityAnimation;
        VoidlingVisualFactory.ApplyAppearance(_sprite, _appearance, race: true);
        Position = ClampToWanderArea(Position);
        RefreshMovementState();
    }

    public void ReleaseFromTile()
    {
        if (_tileRadius <= 0.0f)
            return;

        _tileRadius = 0.0f;
        _tileAnimation = "";
        _sprite.FlipH = false;
        VoidlingVisualFactory.ApplyAppearance(_sprite, _appearance, race: false);
        Position = ClampToWanderArea(Position);
        RefreshMovementState();
    }

    private Vector2 ClampToWanderArea(Vector2 position)
    {
        if (!IsOnTile)
        {
            return new Vector2(
                Mathf.Clamp(position.X, _wanderBounds.Position.X, _wanderBounds.End.X),
                Mathf.Clamp(position.Y, _wanderBounds.Position.Y, _wanderBounds.End.Y));
        }

        var offset = position - _tileCenter;
        return offset.Length() <= _tileRadius ? position : _tileCenter + offset.Normalized() * _tileRadius;
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
                    ? _baseScale * _heldScaleMultiplier
                    : _baseScale);
            if (pickedUp)
            {
                _sprite.Position = new Vector2(
                    0,
                    _baseSpriteY + _heldSpriteYOffset);
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
        var shadowRadii = VoidlingVisualFactory.ShadowRadii(_baseScale, _visualTypeId);
        if (_pickedUp)
            shadowRadii.X *= 1.08f;
        DrawEllipse(
            new Vector2(0, _shadowCenterYOffset),
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

        PickNewTarget();
        _sprite.Play(IsOnTile ? _tileAnimation : "walk_down");
    }

    private void PickNewTarget()
    {
        _target = IsOnTile
            ? _tileCenter + Vector2.Right.Rotated(_rng.RandfRange(0.0f, Mathf.Tau)) *
              _rng.RandfRange(0.0f, _tileRadius)
            : new Vector2(
                _rng.RandfRange(_wanderBounds.Position.X, _wanderBounds.End.X),
                _rng.RandfRange(_wanderBounds.Position.Y, _wanderBounds.End.Y));
        _nextTargetSeconds = _rng.RandfRange(1.5f, 4.0f);
    }

    private void PlayForDirection(Vector2 direction)
    {
        // A Voidling on a land tile wears the race frames, which only carry its activity loop.
        // Facing comes from a flip there instead of a per-direction animation.
        if (IsOnTile)
        {
            _sprite.FlipH = direction.X < 0.0f;
            if (_sprite.Animation != _tileAnimation)
                _sprite.Play(_tileAnimation);
            return;
        }

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

        // While the player is placing an egg or a land tile, the click belongs to the Garden.
        // Falling through without handling it keeps tiles placeable where a Voidling stands.
        if (garden != null && (garden.IsPlacingEgg || garden.IsPlacingLand))
            return;

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
