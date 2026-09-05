using Godot;

namespace VoidlingGame;

public partial class GardenController
{
    private Area2D? _groundPanArea;
    private Vector2 _groundPressPosition;
    private bool _groundPressWasPan;

    public override void _EnterTree()
    {
        // Install after the scene's normal _Ready pass so the camera, session and actor roots
        // have already been resolved by GardenController._Ready(). Keeping these presentation
        // installers here also prevents implemented partials from silently becoming dead code.
        CallDeferred(nameof(InstallLmbGroundPan));
        CallDeferred(nameof(InstallGardenEnvironmentPresentation));
        CallDeferred(nameof(InstallLifecyclePresentation));
        CallDeferred(nameof(InstallDecorationPresentation));
    }

    private void InstallLmbGroundPan()
    {
        if (_groundPanArea != null && GodotObject.IsInstanceValid(_groundPanArea))
            return;

        // A low-Z, oversized pickable area represents the garden floor. Voidling
        // Area2Ds sit above it, so clicking a creature still goes to the creature.
        // Starting on empty floor instead arms the existing camera-drag path.
        _groundPanArea = new Area2D
        {
            Name = "LmbGroundPanArea",
            InputPickable = true,
            ZIndex = -100,
            CollisionLayer = 1,
            CollisionMask = 0
        };

        var shape = new CollisionShape2D
        {
            Shape = new RectangleShape2D { Size = new Vector2(2400, 1600) },
            Position = new Vector2(416, 240)
        };
        _groundPanArea.AddChild(shape);
        _groundPanArea.InputEvent += OnGroundPanInput;
        AddChild(_groundPanArea);

        // If an overlapping creature wins the click after the floor saw it, cancel
        // floor panning immediately. This keeps selection/hold-to-pick-up reliable.
        VoidlingSelected += _ => _cameraDragging = false;
    }

    private void OnGroundPanInput(Node viewport, InputEvent inputEvent, long shapeIndex)
    {
        if (!_inputEnabled || inputEvent is not InputEventMouseButton mouse ||
            mouse.ButtonIndex != MouseButton.Left)
            return;

        if (mouse.Pressed)
        {
            if (_draggedId.Length > 0 || _pendingGrabId.Length > 0 || IsPlacingDecoration)
                return;

            _cameraDragging = true;
            _groundPressWasPan = true;
            _groundPressPosition = mouse.Position;
            StopFollowing();
        }
        else if (_groundPressWasPan)
        {
            _cameraDragging = false;
            _groundPressWasPan = false;

            // A press that never travelled is a click on the ground, not a camera drag, so it
            // belongs to the hex underneath: that is how the player opens a hex's menu.
            if (!IsPlacingLand && !IsPlacingEgg && mouse.Position.DistanceTo(_groundPressPosition) <= 5.0f)
                SelectLandHexUnderPointer();
        }
    }
}
