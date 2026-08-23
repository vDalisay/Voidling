using System;
using Godot;

namespace VoidlingGame;

public partial class MainController
{
    private void ShowSettingsExtended()
    {
        var box = OpenModal(Tr("UI_SETTINGS_TITLE"), new Vector2(365, 252));
        box.AddChild(UiFactory.CreateLabel(Tr("UI_SETTINGS_AUDIO"), 9));

        var volumeRow = new HBoxContainer();
        volumeRow.AddThemeConstantOverride("separation", 8);
        var volumeLabel = UiFactory.CreateLabel(
            string.Format(Tr("UI_SETTINGS_VOLUME"), Mathf.RoundToInt(GameSession.Instance.State.MasterVolume * 100)),
            7);
        volumeLabel.CustomMinimumSize = new Vector2(90, 22);
        volumeRow.AddChild(volumeLabel);
        var volume = new HSlider
        {
            MinValue = 0,
            MaxValue = 100,
            Step = 5,
            Value = GameSession.Instance.State.MasterVolume * 100,
            CustomMinimumSize = new Vector2(220, 22),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        volume.ValueChanged += value =>
        {
            GameSession.Instance.SetMasterVolume((float)value / 100.0f);
            volumeLabel.Text = string.Format(Tr("UI_SETTINGS_VOLUME"), Mathf.RoundToInt((float)value));
        };
        volumeRow.AddChild(volume);
        box.AddChild(volumeRow);

        box.AddChild(UiFactory.CreateLabel(Tr("UI_SETTINGS_CAMERA"), 9));
        var edgePan = UiFactory.CreateButton(GameSession.Instance.State.EdgePanning
            ? Tr("UI_SETTINGS_EDGE_PAN_ON")
            : Tr("UI_SETTINGS_EDGE_PAN_OFF"));
        edgePan.ToggleMode = true;
        edgePan.ButtonPressed = GameSession.Instance.State.EdgePanning;
        edgePan.CustomMinimumSize = new Vector2(190, 25);
        edgePan.TooltipText = Tr("UI_SETTINGS_EDGE_PAN_TOOLTIP");
        edgePan.Pressed += () =>
        {
            GameSession.Instance.SetEdgePanning(edgePan.ButtonPressed);
            edgePan.Text = edgePan.ButtonPressed
                ? Tr("UI_SETTINGS_EDGE_PAN_ON")
                : Tr("UI_SETTINGS_EDGE_PAN_OFF");
        };
        box.AddChild(edgePan);

        box.AddChild(UiFactory.CreateLabel(Tr("UI_SETTINGS_RACE"), 9));
        var autoFinish = UiFactory.CreateButton(GameSession.Instance.State.AutoFinishRaces
            ? Tr("UI_SETTINGS_AUTO_FINISH_ON")
            : Tr("UI_SETTINGS_AUTO_FINISH_OFF"));
        autoFinish.ToggleMode = true;
        autoFinish.ButtonPressed = GameSession.Instance.State.AutoFinishRaces;
        autoFinish.CustomMinimumSize = new Vector2(190, 25);
        autoFinish.TooltipText = Tr("UI_SETTINGS_AUTO_FINISH_TOOLTIP");
        autoFinish.Pressed += () =>
        {
            GameSession.Instance.SetAutoFinishRaces(autoFinish.ButtonPressed);
            autoFinish.Text = autoFinish.ButtonPressed
                ? Tr("UI_SETTINGS_AUTO_FINISH_ON")
                : Tr("UI_SETTINGS_AUTO_FINISH_OFF");
        };
        box.AddChild(autoFinish);

        box.AddChild(UiFactory.CreateLabel(Tr("UI_SETTINGS_HINT"), 6));
    }
}
