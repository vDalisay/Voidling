using Godot;

namespace VoidlingGame;

/// <summary>
/// Chasing food and eating it. Both are presentation states that temporarily win over roaming and
/// tile confinement; neither touches training, persistence or the race simulation.
/// </summary>
public partial class VoidlingActor
{
    /// <summary>How much faster than a stroll a Voidling moves when there is food on the ground.</summary>
    private const float TreatChaseSpeedMultiplier = 2.6f;

    private bool _chasingTreat;
    private Vector2 _treatTarget;
    private bool _eating;
    private float _eatingSquish;

    public bool IsChasingTreat => _chasingTreat;
    public Vector2 TreatTarget => _treatTarget;
    public bool IsEating => _eating;

    /// <summary>
    /// Drops whatever this Voidling was doing and runs for the food. Tile confinement is ignored
    /// while chasing, so a trainee can leave its hex for a treat and is put back afterwards.
    /// </summary>
    public void ChaseTreat(Vector2 worldPosition)
    {
        if (_pickedUp || _interactionLocked)
            return;

        _chasingTreat = true;
        _treatTarget = worldPosition;
        _restSeconds = 0.0f;
        if (_sprite != null && GodotObject.IsInstanceValid(_sprite))
            PlayForDirection((worldPosition - Position).Normalized());
    }

    public void StopChasingTreat()
    {
        if (!_chasingTreat)
            return;

        _chasingTreat = false;
        Position = ClampToWanderArea(Position);
        PickNewTarget();
        RefreshMovementState();
    }

    public void BeginEating(float seconds)
    {
        _eating = true;
        _eatingSquish = 0.0f;
        _restSeconds = seconds;
    }

    /// <summary>Driven by a tween: 0 is the resting body, 1 is the fullest squash.</summary>
    public void SetEatingSquish(float amount)
    {
        _eatingSquish = Mathf.Clamp(amount, 0.0f, 1.0f);
        ApplyEatingSquish();
    }

    public void EndEating()
    {
        _eating = false;
        _eatingSquish = 0.0f;
        ApplyEatingSquish();
        PickNewTarget();
        RefreshMovementState();
    }

    private void ApplyEatingSquish()
    {
        if (_sprite == null || !GodotObject.IsInstanceValid(_sprite) || _pickedUp)
            return;

        // Squash horizontally and stretch vertically by the same small amount, so the body keeps
        // its footprint on the ground while it chews.
        var squash = 1.0f + _eatingSquish * 0.16f;
        var stretch = 1.0f - _eatingSquish * 0.14f;
        _sprite.Scale = new Vector2(_baseScale * squash, _baseScale * stretch);
    }

    /// <summary>
    /// The chase step. Returns true when it has handled this frame, so normal roaming and tile
    /// confinement stay untouched while food is on the ground.
    /// </summary>
    private bool ProcessTreatChase(float step)
    {
        if (_eating)
        {
            // Standing still and chewing; the squish tween owns the sprite this whole time.
            if (_sprite != null && GodotObject.IsInstanceValid(_sprite)) _sprite.Stop();
            return true;
        }

        if (!_chasingTreat)
            return false;

        var toTarget = _treatTarget - Position;
        if (toTarget.LengthSquared() <= 1.0f)
            return true;

        var direction = toTarget.Normalized();
        // The chase deliberately ignores tile confinement; only the island edge still applies.
        var next = Position + direction * _walkSpeed * TreatChaseSpeedMultiplier * step;
        Position = LandClamp?.Invoke(next) ?? next;
        PlayForDirection(direction);
        return true;
    }
}
