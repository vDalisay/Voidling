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
            _session.State.MasterVolume,
            _session.State.EdgePanning,
            _session.State.AutoFinishRaces));

        screen.MasterVolumeChanged += _session.SetMasterVolume;
        screen.EdgePanningChanged += _session.SetEdgePanning;
        screen.AutoFinishRacesChanged += _session.SetAutoFinishRaces;
        box.AddChild(screen);
    }
}
