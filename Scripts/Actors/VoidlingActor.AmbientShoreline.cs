using Godot;
using Voidling.Presentation.Voidlings;

namespace VoidlingGame;

public partial class VoidlingActor
{
    /// <summary>
    /// Extended presentation-only Garden behavior. Swim occasionally nudges a free-roaming target
    /// toward the island shoreline; training-tile movement is never affected.
    /// </summary>
    private float _shorelineTargetChance;

    public void ApplyAmbientStats(float run, float stamina, float swim)
    {
        var behavior = VoidlingAmbientBehaviorResolver.Resolve(run, stamina, swim);
        _walkSpeed = _baseWalkSpeed * behavior.WalkSpeedMultiplier;
        _restSecondsMin = behavior.RestSecondsMin;
        _restSecondsMax = Mathf.Max(_restSecondsMin, behavior.RestSecondsMax);
        _restSeconds = Mathf.Min(_restSeconds, _restSecondsMax);

        // Only the stats are applied here. The shoreline pull used to be rolled on the spot, and
        // this runs on every state change - every autosave and income tick - so it repeatedly
        // redirected a Voidling that was already walking somewhere and reset its patience with it.
        // The roll now happens where it belongs: when a destination is actually being chosen.
        _shorelineTargetChance = behavior.ShorelineTargetChance;
    }

    /// <summary>
    /// Swim occasionally sends a free-roaming Voidling to the water's edge instead of anywhere on
    /// the island. Returns null when this walk is an ordinary one.
    /// </summary>
    private Vector2? TryPickShorelineTarget()
    {
        if (IsOnTile || _rng.Randf() >= _shorelineTargetChance)
            return null;

        const float shorelineInset = 6.0f;
        var minX = _wanderBounds.Position.X;
        var maxX = _wanderBounds.End.X;
        var minY = _wanderBounds.Position.Y;
        var maxY = _wanderBounds.End.Y;

        var boxedTarget = _rng.RandiRange(0, 3) switch
        {
            0 => new Vector2(minX + shorelineInset, _rng.RandfRange(minY, maxY)),
            1 => new Vector2(maxX - shorelineInset, _rng.RandfRange(minY, maxY)),
            2 => new Vector2(_rng.RandfRange(minX, maxX), minY + shorelineInset),
            _ => new Vector2(_rng.RandfRange(minX, maxX), maxY - shorelineInset)
        };

        // Current Garden land is a player-grown cluster of large hex pieces rather than a fixed
        // rectangle. The authored land clamp turns an edge-biased point into actual shoreline
        // instead of asking the Voidling to walk over water.
        return boxedTarget;
    }
}
