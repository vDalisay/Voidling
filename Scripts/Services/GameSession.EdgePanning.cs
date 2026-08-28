namespace VoidlingGame;

public partial class GameSession
{
    public void SetEdgePanning(bool enabled)
    {
        if (!_settings!.SetEdgePanning(State, enabled))
            return;

        Save(showFeedback: true);
        StateChanged?.Invoke();
    }
}
