namespace VoidlingGame;

public partial class GameSession
{
    public void SetEdgePanning(bool enabled)
    {
        State.EdgePanning = enabled;
        Save();
        StateChanged?.Invoke();
    }
}
