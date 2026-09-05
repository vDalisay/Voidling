using Godot;
using Voidling.Presentation.UI.Settings;

namespace VoidlingGame;

public partial class MainController
{
    private void ShowSettingsExtended()
    {
        var box = OpenModal(Tr("UI_SETTINGS_TITLE"), new Vector2(410, 318));
        var screen = new SettingsScreen();
        screen.Configure(new SettingsScreenState(
            _session.State.MasterVolume,
            _session.State.SoundEffectVolume,
            _session.State.UiSoundVolume,
            _session.State.EdgePanning,
            _session.State.AutoFinishRaces));

        screen.MasterVolumeChanged += _session.SetMasterVolume;
        screen.SoundEffectVolumeChanged += _session.SetSoundEffectVolume;
        screen.UiSoundVolumeChanged += _session.SetUiSoundVolume;
        screen.EdgePanningChanged += _session.SetEdgePanning;
        screen.AutoFinishRacesChanged += _session.SetAutoFinishRaces;
        box.AddChild(screen);
        var reset = UiFactory.CreateButton(Tr("UI_TOP_RESET"));
        reset.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
        reset.AddThemeColorOverride("font_color", Color.FromHtml("#914E42"));
        reset.Pressed += ShowResetConfirm;
        box.AddChild(reset);
    }
}
