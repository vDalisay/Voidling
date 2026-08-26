using Godot;

namespace VoidlingGame;

public partial class GardenController
{
    /// <summary>
    /// Presentation-only lookup used when a locally owned Voidling is first shared into a connected
    /// Garden. Normal wandering still does not write back into persisted WorldX/WorldY.
    /// </summary>
    public bool TryGetActorPosition(string creatureId, out Vector2 position)
    {
        position = default;
        if (string.IsNullOrWhiteSpace(creatureId) ||
            !_actors.TryGetValue(creatureId, out var actor) ||
            !GodotObject.IsInstanceValid(actor))
        {
            return false;
        }

        position = actor.Position;
        return true;
    }

    /// <summary>
    /// Samples live Garden presentation for an already-owned actor. This is intentionally separate
    /// from persisted creature placement and is used only by lossy connected-Garden replication.
    /// </summary>
    public bool TryGetActorConnectedGardenPresentation(
        string creatureId,
        out Vector2 position,
        out float facingX,
        out string animationState)
    {
        position = default;
        facingX = 0.0f;
        animationState = "idle";

        return !string.IsNullOrWhiteSpace(creatureId) &&
               _actors.TryGetValue(creatureId, out var actor) &&
               GodotObject.IsInstanceValid(actor) &&
               actor.TryGetConnectedGardenPresentation(out position, out facingX, out animationState);
    }
}
