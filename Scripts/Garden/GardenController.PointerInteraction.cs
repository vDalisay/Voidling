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

        // The oversized ground Area2D can see the same initial LMB press before the
        // Voidling's Area2D does. Claiming a Voidling gesture must therefore cancel
        // any floor-pan that was armed by that press. Keep the whole press/hold/release
        // gesture owned by the Voidling instead of letting the camera inherit it.
        _cameraDragging = false;
        _pendingGrabId = creatureId;
        _pendingGrabSeconds = 0.0f;
    }

    internal void EndVoidlingPointerInteraction(string creatureId)
    {
        if (!_inputEnabled || string.IsNullOrWhiteSpace(creatureId))
            return;

        // Once the hold threshold has promoted the gesture to a drag, releasing over
        // the actor is a drop only. It must never open the inspection panel.
        if (string.Equals(_draggedId, creatureId, System.StringComparison.Ordinal))
        {
            DropGrabbedVoidling();
            return;
        }

        // A normal click is press + release over the same actor before the hold timer.
        if (!string.Equals(_pendingGrabId, creatureId, System.StringComparison.Ordinal))
            return;

        ClearPendingGrab();
        HandleCompletedVoidlingClick(creatureId);
    }
}
