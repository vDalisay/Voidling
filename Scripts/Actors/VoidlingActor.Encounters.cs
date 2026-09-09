using Godot;

namespace VoidlingGame;

/// <summary>
/// The Voidling's side of a Garden encounter: while one is running, the Garden drives where this
/// creature goes and which way it looks instead of its own roaming. Presentation only - nothing
/// here touches training, persistence or the race simulation.
/// </summary>
public partial class VoidlingActor
{
    private bool _scripted;
    private bool _scriptHolding;
    private bool _scriptDust;
    private Vector2 _scriptTarget;
    private float _scriptSpeedMultiplier = 1.0f;

    /// <summary>Close enough to a scripted destination to count as arrived.</summary>
    private const float ScriptedArrivalRadius = 5.0f;

    public bool IsScripted => _scripted;

    /// <summary>True when this Voidling is just wandering the island and could be pulled into an encounter.</summary>
    public bool IsAvailableForEncounter =>
        !_scripted && !IsOnTile && !_chasingTreat && !_eating && !_pickedUp && !_interactionLocked &&
        _zoomieSeconds <= 0.0f && _sprite != null && GodotObject.IsInstanceValid(_sprite);

    public bool ScriptedArrived =>
        Position.DistanceTo(_scriptTarget) <= ScriptedArrivalRadius;

    /// <summary>Sends this Voidling to a world position under the Garden's direction.</summary>
    public void BeginScriptedRun(Vector2 target, float speedMultiplier, bool dust)
    {
        if (_pickedUp || _interactionLocked)
            return;

        _scripted = true;
        _scriptHolding = false;
        _scriptDust = dust;
        _scriptTarget = target;
        _scriptSpeedMultiplier = Mathf.Max(0.1f, speedMultiplier);
        _restSeconds = 0.0f;
    }

    /// <summary>Stands still, looking at something. Used for the beat where two Voidlings meet.</summary>
    public void HoldScripted(Vector2 facePoint)
    {
        if (!_scripted)
            return;

        _scriptHolding = true;
        _scriptDust = false;
        FaceTowards(facePoint);
        if (_sprite != null && GodotObject.IsInstanceValid(_sprite))
            _sprite.Stop();
    }

    /// <summary>Hands this Voidling back to its own roaming.</summary>
    public void EndScripted()
    {
        if (!_scripted)
            return;

        _scripted = false;
        _scriptHolding = false;
        _scriptDust = false;
        Position = ClampToWanderArea(Position);
        RefreshMovementState();
    }

    public void FaceTowards(Vector2 point)
    {
        var direction = point - Position;
        if (direction.LengthSquared() > 0.01f)
            PlayForDirection(direction.Normalized());
    }

    /// <summary>Startled: whip around to look the other way.</summary>
    public void FaceAwayFrom(Vector2 point) => FaceTowards(Position * 2.0f - point);

    /// <summary>
    /// A short hop in place. The hatch jump owns the interaction lock because nothing may interrupt
    /// it; an encounter hop is just a flourish on top of whatever the Voidling is already doing.
    /// </summary>
    public void PlayHop(float height = 12.0f, double seconds = 0.36)
    {
        if (_sprite == null || !GodotObject.IsInstanceValid(_sprite) || _pickedUp)
            return;

        var rest = new Vector2(0, _baseSpriteY);
        _sprite.Position = rest;
        var hop = CreateTween();
        hop.TweenProperty(_sprite, "position", rest - new Vector2(0, height), seconds * 0.45)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        hop.TweenProperty(_sprite, "position", rest, seconds * 0.55)
            .SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out);
    }

    /// <summary>
    /// The scripted step. Returns true when it has handled this frame, so roaming stays untouched
    /// for as long as the encounter lasts.
    /// </summary>
    private bool ProcessScriptedMove(float step)
    {
        if (!_scripted)
            return false;

        if (_scriptHolding)
            return true;

        TickDustTrail(step);

        var toTarget = _scriptTarget - Position;
        if (toTarget.Length() <= ScriptedArrivalRadius)
        {
            if (_sprite != null && GodotObject.IsInstanceValid(_sprite))
                _sprite.Stop();
            return true;
        }

        var direction = toTarget.Normalized();
        var next = Position + direction * _walkSpeed * _scriptSpeedMultiplier * step;
        Position = ClampToWanderArea(next);
        PlayForDirection(direction);
        return true;
    }
}
