using System;
using Godot;
using Voidling.Presentation.Voidlings;

namespace VoidlingGame;

public partial class VoidlingActor : Node2D
{
    public event Action<string>? Clicked;

    public string CreatureId { get; private set; } = "";
    public LifeStage Stage { get; private set; }
    public bool IsPointerHovered => _hovered;

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
    private bool _hovered;
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
        Stage = data.Stage;
        _wanderBounds = wanderBounds;
        Position = startPosition;
        _baseWalkSpeed = data.Stage == LifeStage.Adult ? 20.0f : 17.0f;
        _walkSpeed = _baseWalkSpeed;
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
        area.MouseEntered += () => SetHovered(true);
        area.MouseExited += () => SetHovered(false);
        AddChild(area);

        PickNewTarget();
        QueueRedraw();
    }

    /// <summary>
    /// Applies presentation-only roaming flavor from the Voidling's current effective stats.
    /// This never feeds back into authoritative training, persistence, or race simulation.
    /// </summary>
    /// <summary>
    /// Raised while training on running ground, so the Garden can kick up its dust at the feet.
    /// Presentation only: the tile's training rate is unaffected by how often this fires.
    /// </summary>
    public event Action<VoidlingActor>? RunningStride;

    private float _dustSeconds;
    private float _zoomieSeconds;

    /// <summary>How much faster than a stroll a sustained dash across running ground is.</summary>
    private const float DashSpeedMultiplier = 2.6f;

    /// <summary>How much faster than a stroll a fit of zoomies is.</summary>
    private const float ZoomieSpeedMultiplier = 2.2f;

    /// <summary>Gap between puffs of a dash trail. Short enough to read as one continuous streak.</summary>
    private const float DustIntervalSeconds = 0.09f;

    /// <summary>Chance that a finished walk turns into a fit of zoomies instead of another stroll.</summary>
    private const float ZoomieChance = 0.06f;

    /// <summary>True while this Voidling is sprinting: a training dash, zoomies, or a scripted run.</summary>
    private bool IsDashing => _zoomieSeconds > 0.0f || _scriptDust || (IsOnTile && _tileAnimation == "run");

    /// <summary>
    /// A sprinting Voidling kicks up dust the whole way rather than once in a while, so a straight
    /// dash leaves a visible line behind it. Walking and swim ground stay quiet.
    /// </summary>
    private void TickDustTrail(float step)
    {
        if (!IsDashing || _restSeconds > 0.0f || _eating || _pickedUp || _interactionLocked)
            return;

        _dustSeconds -= step;
        if (_dustSeconds > 0.0f)
            return;

        _dustSeconds = DustIntervalSeconds;
        RunningStride?.Invoke(this);
    }

    /// <summary>Speed for this frame: a stroll, a dash across training ground, or zoomies.</summary>
    private float CurrentSpeed
    {
        get
        {
            if (_zoomieSeconds > 0.0f)
                return _walkSpeed * ZoomieSpeedMultiplier;
            return IsOnTile && _tileAnimation == "run" ? _walkSpeed * DashSpeedMultiplier : _walkSpeed;
        }
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
        if (_selected || _hovered)
            QueueRedraw();

        if (_sprite == null || _interactionLocked || _pickedUp)
            return;

        var step = (float)delta;
        // Food on the ground outranks both roaming and tile confinement.
        if (ProcessTreatChase(step))
            return;

        // A Garden encounter drives this Voidling directly while it lasts.
        if (ProcessScriptedMove(step))
            return;

        TickDustTrail(step);
        _zoomieSeconds = Math.Max(0.0f, _zoomieSeconds - step);

        // A Voidling pauses between the legs of a walk, and between dashes across training ground,
        // rather than sliding from destination to destination without ever stopping.
        if (_restSeconds > 0.0f)
        {
            _restSeconds = Math.Max(0.0f, _restSeconds - step);
            if (_restSeconds > 0.0f)
            {
                _sprite.Stop();
                return;
            }

            PickNewTarget();
            _sprite.Play(IsOnTile ? _tileAnimation : "walk_down");
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
            var nextPosition = Position + direction * CurrentSpeed * step;
            var clampedPosition = ClampToWanderArea(nextPosition);
            var hitBoundary = clampedPosition.DistanceSquaredTo(nextPosition) > 0.01f;
            Position = clampedPosition;
            if (hitBoundary && !IsOnTile)
                PickNewTarget();
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

    /// <summary>
    /// Pulls a free-roaming Voidling back onto land. The island is a cluster of hexes, not a
    /// rectangle, so the box alone would let it stroll onto the water.
    /// </summary>
    public Func<Vector2, Vector2>? LandClamp { get; set; }

    /// <summary>
    /// Somewhere on the island worth walking to. The wander box is the island's bounding
    /// rectangle, which on a small or oddly shaped island is mostly water; drawing from it and
    /// clamping afterwards kept pulling destinations back to whichever hex the Voidling already
    /// stood on. Asking for real ground instead is what lets it cross the island.
    /// </summary>
    public Func<Vector2>? LandTarget { get; set; }

    /// <summary>Widens the roaming area as the island grows.</summary>
    public void SetWanderArea(Rect2 bounds, bool repath = false)
    {
        if (_wanderBounds == bounds && !repath)
            return;

        _wanderBounds = bounds;
        Position = ClampToWanderArea(Position);
        if (repath)
            PickNewTarget();
    }

    private Vector2 ClampToWanderArea(Vector2 position)
    {
        if (!IsOnTile)
        {
            var boxed = new Vector2(
                Mathf.Clamp(position.X, _wanderBounds.Position.X, _wanderBounds.End.X),
                Mathf.Clamp(position.Y, _wanderBounds.Position.Y, _wanderBounds.End.Y));
            return LandClamp?.Invoke(boxed) ?? boxed;
        }

        var offset = position - _tileCenter;
        return offset.Length() <= _tileRadius ? position : _tileCenter + offset.Normalized() * _tileRadius;
    }

    public void SetSelected(bool selected)
    {
        _selected = selected;
        QueueRedraw();
    }

    private void SetHovered(bool hovered)
    {
        _hovered = hovered;
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

        if (_selected || _hovered)
        {
            var phase = (float)Time.GetTicksMsec() / 220.0f;
            var pulse = (Mathf.Sin(phase) + 1.0f) * 0.5f;
            var baseRadius = _baseScale < 0.5f ? 4.2f : 7.2f;
            var radius = baseRadius + (_selected ? pulse * 1.25f : 0.6f);
            var color = Color.FromHtml("#FFF4A8");
            color.A = _selected ? 0.70f + pulse * 0.25f : 0.38f;
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

        // Picking a Voidling up, putting it on a tile or taking it off one all end whatever fit of
        // zoomies it was in; otherwise it would carry the sprint into its next situation.
        _restSeconds = 0.0f;
        _zoomieSeconds = 0.0f;
        PickNewTarget();
        _sprite.Play(IsOnTile ? _tileAnimation : "walk_down");
    }

    /// <summary>
    /// A finished leg of a walk. Off the tiles this is the short breather at a spot, and now and
    /// then it becomes a fit of zoomies instead. On running ground it is the beat between two
    /// sustained dashes. A Voidling already zooming does not stop until the fit runs out.
    /// </summary>
    private void BeginRest()
    {
        if (_zoomieSeconds > 0.0f)
        {
            PickNewTarget();
            return;
        }

        if (!IsOnTile && _rng.Randf() < ZoomieChance)
        {
            _zoomieSeconds = _rng.RandfRange(3.0f, 6.0f);
            PickNewTarget();
            _sprite.Play("walk_down");
            return;
        }

        _restSeconds = IsOnTile
            ? _rng.RandfRange(0.5f, 1.3f)
            : _rng.RandfRange(_restSecondsMin, _restSecondsMax);
        _sprite.Stop();
    }

    /// <summary>
    /// Longest single leg of an ordinary stroll, and the shortest. The long end is deliberately
    /// wider than a hex: a leg that cannot cross the ground a Voidling stands on leaves it milling
    /// about on one tile forever. Zoomies ignore the cap and cross the island.
    /// </summary>
    private const float MinWalkLegLength = 60.0f;
    private const float MaxWalkLegLength = 200.0f;

    private void PickNewTarget()
    {
        _target = IsOnTile ? PickTileTarget() : PickRoamTarget();

        // Long enough to actually arrive, plus some slack. A flat timer was shorter than the walk
        // across a single hex, so a Voidling gave up on every destination that was not already
        // next to it and never left the ground it stood on. This stays a give-up guard: arriving
        // early simply starts a rest, and hitting the island edge repaths at once.
        _nextTargetSeconds = Position.DistanceTo(_target) / Mathf.Max(1.0f, CurrentSpeed) +
                             _rng.RandfRange(1.0f, 3.0f);
    }

    /// <summary>
    /// On running ground the destination is the opposite rim rather than a point anywhere on the
    /// tile, so training reads as a sustained dash along a straight line and its dust trail has
    /// room to draw. Other activities keep browsing their own tile.
    /// </summary>
    private Vector2 PickTileTarget()
    {
        if (_tileAnimation != "run")
        {
            return _tileCenter + Vector2.Right.Rotated(_rng.RandfRange(0.0f, Mathf.Tau)) *
                   _rng.RandfRange(0.0f, _tileRadius);
        }

        var fromCenter = Position - _tileCenter;
        var awayAngle = fromCenter.LengthSquared() > 1.0f
            ? fromCenter.Angle()
            : _rng.RandfRange(0.0f, Mathf.Tau);
        var angle = awayAngle + Mathf.Pi + _rng.RandfRange(-0.5f, 0.5f);
        return _tileCenter + Vector2.Right.Rotated(angle) * _tileRadius * _rng.RandfRange(0.78f, 0.98f);
    }

    /// <summary>
    /// A free-roaming Voidling walks a leg of a set length toward somewhere on the island and
    /// pauses there instead of committing to one long uninterrupted crossing. Zoomies are the
    /// exception: covering ground is the whole point of the fit.
    /// </summary>
    private Vector2 PickRoamTarget()
    {
        var destination =
            TryPickShorelineTarget() ??
            LandTarget?.Invoke() ??
            new Vector2(
                _rng.RandfRange(_wanderBounds.Position.X, _wanderBounds.End.X),
                _rng.RandfRange(_wanderBounds.Position.Y, _wanderBounds.End.Y));

        if (_zoomieSeconds > 0.0f)
            return ClampToWanderArea(destination);

        var toDestination = destination - Position;
        var distance = toDestination.Length();
        var leg = _rng.RandfRange(MinWalkLegLength, MaxWalkLegLength);
        if (distance > leg)
            destination = Position + toDestination / distance * leg;
        return ClampToWanderArea(destination);
    }

    /// <summary>
    /// Any real sideways component decides which way a Voidling looks. The up and down animations
    /// keep whatever horizontal facing came before them, so letting them win a mostly-vertical walk
    /// leaves a creature drifting left while still facing right.
    /// </summary>
    private const float FacingDeadzone = 0.12f;

    private void PlayForDirection(Vector2 direction)
    {
        // A Voidling on a land tile wears the race frames, which only carry its activity loop.
        // Facing comes from a flip there instead of a per-direction animation.
        if (IsOnTile)
        {
            if (Mathf.Abs(direction.X) > FacingDeadzone)
                _sprite.FlipH = direction.X < 0.0f;
            if (_sprite.Animation != _tileAnimation)
                _sprite.Play(_tileAnimation);
            return;
        }

        StringName animation;
        if (Mathf.Abs(direction.X) > FacingDeadzone)
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
