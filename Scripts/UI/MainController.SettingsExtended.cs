using Godot;
using Voidling.Presentation.UI.Settings;

namespace VoidlingGame;

public partial class MainController
{
    private void ShowSettingsExtended()
    {
        var box = OpenModal(Tr("UI_SETTINGS_TITLE"), new Vector2(365, 252));
        var screen = new SettingsScreen();
        screen.Configure(new SettingsScreenState(
            GameSession.Instance.State.MasterVolume,
            GameSession.Instance.State.EdgePanning,
            GameSession.Instance.State.AutoFinishRaces));

        screen.MasterVolumeChanged += GameSession.Instance.SetMasterVolume;
        screen.EdgePanningChanged += GameSession.Instance.SetEdgePanning;
        screen.AutoFinishRacesChanged += GameSession.Instance.SetAutoFinishRaces;
        box.AddChild(screen);
    }
}
