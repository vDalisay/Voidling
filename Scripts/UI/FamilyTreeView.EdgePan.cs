using Godot;

namespace VoidlingGame;

public partial class FamilyTreeView
{
    private const float TreeEdgePanMargin = 12.0f;
    private const float TreeEdgePanSpeed = 120.0f;

    public bool EdgePanningEnabled { get; set; } = true;

    public override void _Process(double delta)
    {
        // The connection flow pulses are time-driven, so the view repaints every frame.
        QueueRedraw();

        if (_panning || !EdgePanningEnabled)
            return;

        var mouse = GetLocalMousePosition();
        if (mouse.X < 0.0f || mouse.Y < 0.0f || mouse.X > Size.X || mouse.Y > Size.Y)
            return;

        var direction = Vector2.Zero;
        if (mouse.X <= TreeEdgePanMargin)
            direction.X += 1.0f;
        else if (mouse.X >= Size.X - TreeEdgePanMargin)
            direction.X -= 1.0f;

        if (mouse.Y <= TreeEdgePanMargin)
            direction.Y += 1.0f;
        else if (mouse.Y >= Size.Y - TreeEdgePanMargin)
            direction.Y -= 1.0f;

        if (direction == Vector2.Zero)
            return;

        _panOffset += direction.Normalized() * TreeEdgePanSpeed * (float)delta;
        ClampPan();
        ApplyView();
        QueueRedraw();
    }
}
