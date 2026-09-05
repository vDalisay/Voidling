using Godot;

namespace VoidlingGame;

/// <summary>
/// Small scene-tree adapter that lets the Garden decoration partial own placement input without
/// adding a second _Process/_Input override to GardenController.
/// </summary>
public partial class GardenDecorationInputDriver : Node
{
    private GardenController? _garden;

    public void Configure(GardenController garden) => _garden = garden;

    public override void _Process(double delta) => _garden?.UpdateDecorationPlacementGhost();

    // Placement owns LMB/RMB before the Garden's normal unhandled-input camera bindings see them.
    public override void _Input(InputEvent inputEvent) => _garden?.HandleDecorationPlacementInput(inputEvent);
}
