namespace VoidlingGame;

public partial class VoidlingActor
{
    // Pointer gestures are disabled only while scripted actor interaction is locked.
    // Picked-up actors must continue receiving the release event so the Garden can drop them.
    private bool _interactive => !_interactionLocked;
}
