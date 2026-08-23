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

    public bool RenameVoidling(string creatureId, string requestedName)
    {
        var creature = FindVoidling(creatureId);
        if (creature == null)
            return false;

        var cleaned = (requestedName ?? string.Empty)
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace("\t", " ")
            .Trim();

        if (cleaned.Length == 0)
        {
            ToastRequested?.Invoke("A Voidling needs a name.");
            return false;
        }

        if (cleaned.Length > 18)
            cleaned = cleaned[..18].TrimEnd();

        if (creature.Name == cleaned)
            return true;

        creature.Name = cleaned;
        SaveAndNotify($"Renamed Voidling to {cleaned}.");
        return true;
    }
}
