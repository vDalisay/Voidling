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
            Position = new Vector2(0, childScale ? -10.5f : -22.0f),
            Compact = childScale,
            ZIndex = 4
        };
        AddChild(halo);

        // Suppress the legacy circular arc in VoidlingActor._Draw(). The dedicated
        // child now renders the mutation as a front-facing perspective ellipse.
        _angelMutation = false;
        QueueRedraw();
    }
}
