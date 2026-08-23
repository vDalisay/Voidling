using Godot;

namespace VoidlingGame;

public partial class GardenController
{
    private const float EdgePanMargin = 14.0f;
    private const float EdgePanSpeed = 170.0f;

    public override void _PhysicsProcess(double delta)
    {
        if (!_inputEnabled || !GameSession.Instance.State.EdgePanning || _camera == null || !_camera.Enabled)
            return;

        // Do not move the camera while a creature interaction or explicit drag owns LMB.
        if (_draggedId.Length > 0 || _pendingGrabId.Length > 0 || _cameraDragging)
            return;

        var viewport = GetViewport();
        var mouse = viewport.GetMousePosition();
        var size = viewport.GetVisibleRect().Size;
        if (size.X <= 0.0f || size.Y <= 0.0f)
            return;

        var direction = Vector2.Zero;
        if (mouse.X >= 0.0f && mouse.X <= EdgePanMargin)
            direction.X -= 1.0f;
        else if (mouse.X <= size.X && mouse.X >= size.X - EdgePanMargin)
            direction.X += 1.0f;

        if (mouse.Y >= 0.0f && mouse.Y <= EdgePanMargin)
            direction.Y -= 1.0f;
        else if (mouse.Y <= size.Y && mouse.Y >= size.Y - EdgePanMargin)
            direction.Y += 1.0f;

        if (direction == Vector2.Zero)
            return;

        StopFollowing();
        _camera.Position += direction.Normalized() * EdgePanSpeed * (float)delta / Mathf.Max(_camera.Zoom.X, 0.01f);
        ClampCamera();
    }
}
