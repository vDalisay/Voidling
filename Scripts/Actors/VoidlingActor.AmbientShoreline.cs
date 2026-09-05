using Godot;
using Voidling.Presentation.Voidlings;

namespace VoidlingGame;

public partial class VoidlingActor
{
    /// <summary>
    /// Extended presentation-only Garden behavior. Swim occasionally nudges a free-roaming target
    /// toward the island shoreline; training-tile movement is never affected.
    /// </summary>
    public void ApplyAmbientStats(float run, float stamina, float swim)
    {
        var behavior = VoidlingAmbientBehaviorResolver.Resolve(run, stamina, swim);
        _walkSpeed = _baseWalkSpeed * behavior.WalkSpeedMultiplier;
        _restSecondsMin = behavior.RestSecondsMin;
        _restSecondsMax = Mathf.Max(_restSecondsMin, behavior.RestSecondsMax);
        _restSeconds = Mathf.Min(_restSeconds, _restSecondsMax);

        if (!IsOnTile && _rng.Randf() < behavior.ShorelineTargetChance)
            PickShorelineTarget();
    }

    private void PickShorelineTarget()
    {
        const float shorelineInset = 6.0f;
        var minX = _wanderBounds.Position.X;
        var maxX = _wanderBounds.End.X;
        var minY = _wanderBounds.Position.Y;
        var maxY = _wanderBounds.End.Y;
        var edge = _rng.RandiRange(0, 3);

        _target = edge switch
        {
            0 => new Vector2(minX + shorelineInset, _rng.RandfRange(minY, maxY)),
            1 => new Vector2(maxX - shorelineInset, _rng.RandfRange(minY, maxY)),
            2 => new Vector2(_rng.RandfRange(minX, maxX), minY + shorelineInset),
            _ => new Vector2(_rng.RandfRange(minX, maxX), maxY - shorelineInset)
        };
        _nextTargetSeconds = _rng.RandfRange(1.5f, 4.0f);
    }
}
