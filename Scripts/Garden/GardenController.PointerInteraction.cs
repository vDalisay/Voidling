namespace VoidlingGame;

public partial class GardenController
{
    /// <summary>
    /// Starts the existing hold-to-pick-up gesture without selecting the Voidling yet.
    /// A short click is only promoted to inspection when the pointer is released over
    /// the same actor; crossing the hold threshold becomes a drag instead.
    /// </summary>
    internal void BeginVoidlingPointerInteraction(string creatureId)
    {
        if (!_inputEnabled ||
            string.IsNullOrWhiteSpace(creatureId) ||
            _draggedId.Length > 0 ||
            !_actors.ContainsKey(creatureId))
        {
            return;
        }

        _cameraDragging = false;
        _pendingGrabId = creatureId;
        _pendingGrabSeconds = 0.0f;
    }

    internal void EndVoidlingPointerInteraction(string creatureId)
    {
        if (!_inputEnabled || string.IsNullOrWhiteSpace(creatureId))
            return;

        if (string.Equals(_draggedId, creatureId, System.StringComparison.Ordinal))
        {
            DropGrabbedVoidling();
            return;
        }

        if (!string.Equals(_pendingGrabId, creatureId, System.StringComparison.Ordinal))
            return;

        ClearPendingGrab();
        HandleCompletedVoidlingClick(creatureId);
    }
}
