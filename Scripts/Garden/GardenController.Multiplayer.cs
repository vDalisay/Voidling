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
}
