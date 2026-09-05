using System;
using Voidling.Application.Garden;

namespace VoidlingGame;

public partial class GameSession
{
    public bool PlaceGardenDecoration(string typeId, float x, float y)
    {
        if (string.IsNullOrWhiteSpace(typeId) || !float.IsFinite(x) || !float.IsFinite(y))
            return false;

        State.GardenDecorations.Add(new GardenDecorationData
        {
            Id = NewId(),
            TypeId = typeId.Trim(),
            X = x,
            Y = y
        });

        SaveAndNotify("Placed a garden decoration.");
        RaiseGardenEvent("A new decoration was placed in the garden.");
        return true;
    }

    public bool MoveGardenDecoration(string decorationId, float x, float y)
    {
        if (!float.IsFinite(x) || !float.IsFinite(y))
            return false;

        var decoration = State.GardenDecorations.Find(candidate =>
            string.Equals(candidate.Id, decorationId, StringComparison.Ordinal));
        if (decoration == null)
            return false;

        if (Math.Abs(decoration.X - x) < 0.01f && Math.Abs(decoration.Y - y) < 0.01f)
            return true;

        decoration.X = x;
        decoration.Y = y;
        SaveAndNotify("Moved a garden decoration.");
        return true;
    }

    public bool RemoveGardenDecoration(string decorationId)
    {
        var removed = State.GardenDecorations.RemoveAll(candidate =>
            string.Equals(candidate.Id, decorationId, StringComparison.Ordinal));
        if (removed == 0)
            return false;

        SaveAndNotify("Removed a garden decoration.");
        RaiseGardenEvent("A decoration was put away.");
        return true;
    }
}
