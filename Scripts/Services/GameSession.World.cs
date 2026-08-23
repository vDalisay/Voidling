using Godot;
using Voidling.Application.Roster;

namespace VoidlingGame;

public partial class GameSession
{
    public void MoveVoidling(string creatureId, Vector2 worldPosition)
    {
        if (_roster!.Move(State, creatureId, worldPosition.X, worldPosition.Y))
            Save();
    }

    public bool RenameVoidling(string creatureId, string requestedName)
    {
        var result = _roster!.Rename(State, creatureId, requestedName);
        if (result.Failure == RenameFailure.CreatureNotFound)
            return false;
        if (result.Failure == RenameFailure.EmptyName)
        {
            ToastRequested?.Invoke("A Voidling needs a name.");
            return false;
        }
        if (!result.Changed)
            return true;

        SaveAndNotify($"Renamed Voidling to {result.Name}.");
        return true;
    }
}
