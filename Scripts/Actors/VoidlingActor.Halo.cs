using Godot;

namespace VoidlingGame;

public partial class VoidlingActor
{
    public override void _Ready()
    {
        if (!_angelMutation)
            return;

        var childScale = _baseScale < 0.5f;
        var halo = new PerspectiveHaloWorld
        {
            Position = new Vector2(0, childScale ? -13.5f : -26.0f),
            RadiusX = childScale ? 5.2f : 9.2f,
            RadiusY = childScale ? 1.55f : 2.65f,
            ZIndex = 4
        };
        AddChild(halo);

        // Suppress the legacy circular arc in VoidlingActor._Draw(). The dedicated
        // child now renders the mutation as a front-facing perspective ellipse.
        _angelMutation = false;
        QueueRedraw();
    }
}
