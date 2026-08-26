using Godot;

namespace VoidlingGame;

public partial class VoidlingActor
{
    /// <summary>
    /// Samples only presentation state for lossy connected-Garden replication. No movement is
    /// persisted by this path and callers cannot mutate this actor through the returned values.
    /// </summary>
    public bool TryGetConnectedGardenPresentation(
        out Vector2 position,
        out float facingX,
        out string animationState)
    {
        position = Position;
        facingX = 0.0f;
        animationState = "idle";

        if (_sprite == null || !GodotObject.IsInstanceValid(_sprite))
            return false;

        var animation = _sprite.Animation.ToString();
        if (_sprite.IsPlaying() && !string.IsNullOrWhiteSpace(animation))
            animationState = animation;

        if (animation.EndsWith("_left", System.StringComparison.Ordinal))
            facingX = -1.0f;
        else if (animation.EndsWith("_right", System.StringComparison.Ordinal))
            facingX = 1.0f;

        return true;
    }
}
