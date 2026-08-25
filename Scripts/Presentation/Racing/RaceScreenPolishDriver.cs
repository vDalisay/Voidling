using Godot;

namespace Voidling.Presentation.Racing;

internal partial class RaceScreenPolishDriver : Node
{
    public RaceScreen OwnerScreen { get; init; } = null!;

    public override void _Process(double delta)
    {
        OwnerScreen.ApplyPostRaceScreenPresentationFrame();
    }
}
