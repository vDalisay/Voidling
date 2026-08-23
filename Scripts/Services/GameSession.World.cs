using Godot;

namespace VoidlingGame;

public partial class GameSession
{
    public void MoveVoidling(string creatureId, Vector2 worldPosition)
    {
        var creature = FindVoidling(creatureId);
        if (creature == null)
            return;

        creature.WorldX = worldPosition.X;
        creature.WorldY = worldPosition.Y;
        Save();
    }
}
