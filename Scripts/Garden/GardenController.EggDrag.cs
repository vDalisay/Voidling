using System;
using System.Linq;
using Godot;

namespace VoidlingGame;

/// <summary>
/// Eggs in the Garden are picked up and put down by hand, like Voidlings: press and move (or hold)
/// to lift one, let go over the island to set it down there. Where an egg sits matters — a Swamp guy
/// egg only incubates on an unused Swamp — so the drop is saved through the session. A short click
/// on a failed egg still opens its menu.
/// </summary>
public partial class GardenController
{
    private const float EggDragStartDistance = 6.0f;
    private const float EggLiftScale = 1.15f;

    private string _pendingEggId = "";
    private Vector2 _pendingEggPress;
    private float _pendingEggSeconds;
    private string _draggedEggId = "";
    private Vector2 _eggGrabOrigin;

    public bool IsDraggingEgg => _draggedEggId.Length > 0;

    private void BeginEggPointerInteraction(string eggId)
    {
        if (!_inputEnabled || _draggedId.Length > 0 || _pendingGrabId.Length > 0 || _draggedEggId.Length > 0 ||
            IsPlacingEgg || IsPlacingLand || IsPlacingTreat)
            return;

        _cameraDragging = false;
        _pendingEggId = eggId;
        _pendingEggPress = GetViewport().GetMousePosition();
        _pendingEggSeconds = 0.0f;
    }

    /// <summary>Runs the lift/carry/drop gesture each frame; true while an egg owns the pointer.</summary>
    private bool UpdateEggDrag(float delta)
    {
        if (_draggedEggId.Length > 0)
        {
            if (!Input.IsMouseButtonPressed(MouseButton.Left))
            {
                DropGrabbedEgg();
                return true;
            }

            if (_eggVisuals.TryGetValue(_draggedEggId, out var dragged))
                dragged.Holder.Position = _eggsRoot.ToLocal(GetGlobalMousePosition());
            return true;
        }

        if (_pendingEggId.Length == 0)
            return false;

        if (!Input.IsMouseButtonPressed(MouseButton.Left))
        {
            // A press that never became a carry is a click; only a failed egg has a menu.
            var eggId = _pendingEggId;
            _pendingEggId = "";
            if (_session.State.OwnedEggs.Any(egg =>
                    string.Equals(egg.Id, eggId, StringComparison.Ordinal) && egg.State == EggState.Failed))
                FailedEggSelected?.Invoke(eggId);
            return true;
        }

        _pendingEggSeconds += delta;
        if (GetViewport().GetMousePosition().DistanceTo(_pendingEggPress) >= EggDragStartDistance ||
            _pendingEggSeconds >= HoldToPickUpSeconds)
            StartEggGrab();
        return true;
    }

    private void StartEggGrab()
    {
        var eggId = _pendingEggId;
        _pendingEggId = "";
        if (!_eggVisuals.TryGetValue(eggId, out var visual))
            return;

        _draggedEggId = eggId;
        _eggGrabOrigin = visual.Holder.Position;
        visual.Holder.ZIndex = 20;
        visual.Holder.Scale = Vector2.One * EggLiftScale;
        Input.SetDefaultCursorShape(Input.CursorShape.Drag);
    }

    private void DropGrabbedEgg()
    {
        var eggId = _draggedEggId;
        _draggedEggId = "";
        Input.SetDefaultCursorShape(Input.CursorShape.Arrow);
        if (!_eggVisuals.TryGetValue(eggId, out var visual))
            return;

        visual.Holder.ZIndex = 5;
        visual.Holder.Scale = Vector2.One;

        // Letting go over open water puts the egg back where it was.
        if (ModuleIdUnderPointer().Length == 0)
        {
            visual.Holder.Position = _eggGrabOrigin;
            return;
        }

        var target = ClampToLand(_eggsRoot.ToLocal(GetGlobalMousePosition()));
        visual.Holder.Position = target;
        SpawnDust(target + new Vector2(0, 4));
        _session.MoveEgg(eggId, target);
    }

    private void CancelEggDrag()
    {
        _pendingEggId = "";
        if (_draggedEggId.Length == 0)
            return;

        if (_eggVisuals.TryGetValue(_draggedEggId, out var visual))
        {
            visual.Holder.ZIndex = 5;
            visual.Holder.Scale = Vector2.One;
            visual.Holder.Position = _eggGrabOrigin;
        }

        _draggedEggId = "";
        Input.SetDefaultCursorShape(Input.CursorShape.Arrow);
    }
}
