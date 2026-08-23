using Godot;

namespace VoidlingGame;

public partial class MainController
{
    private void ShowSettingsExtended()
    {
        var box = OpenModal("SETTINGS", new Vector2(365, 252));
        box.AddChild(UiFactory.CreateLabel("Audio", 9));

        var volumeRow = new HBoxContainer();
        volumeRow.AddThemeConstantOverride("separation", 8);
        var volumeLabel = UiFactory.CreateLabel($"Volume {Mathf.RoundToInt(GameSession.Instance.State.MasterVolume * 100)}%", 7);
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
            volumeLabel.Text = $"Volume {Mathf.RoundToInt((float)value)}%";
        };
        volumeRow.AddChild(volume);
        box.AddChild(volumeRow);

        box.AddChild(UiFactory.CreateLabel("Camera", 9));
        var edgePan = UiFactory.CreateButton(GameSession.Instance.State.EdgePanning ? "Edge Pan: ON" : "Edge Pan: OFF");
        edgePan.ToggleMode = true;
        edgePan.ButtonPressed = GameSession.Instance.State.EdgePanning;
        edgePan.CustomMinimumSize = new Vector2(190, 25);
        edgePan.TooltipText = "Move the cursor to an edge to pan the garden or family tree.";
        edgePan.Pressed += () =>
        {
            GameSession.Instance.SetEdgePanning(edgePan.ButtonPressed);
            edgePan.Text = edgePan.ButtonPressed ? "Edge Pan: ON" : "Edge Pan: OFF";
        };
        box.AddChild(edgePan);

        box.AddChild(UiFactory.CreateLabel("Race", 9));
        var autoFinish = UiFactory.CreateButton(GameSession.Instance.State.AutoFinishRaces ? "Auto Finish: ON" : "Auto Finish: OFF");
        autoFinish.ToggleMode = true;
        autoFinish.ButtonPressed = GameSession.Instance.State.AutoFinishRaces;
        autoFinish.CustomMinimumSize = new Vector2(190, 25);
        autoFinish.TooltipText = "Finish once either you finish or every CPU has already finished.";
        autoFinish.Pressed += () =>
        {
            GameSession.Instance.SetAutoFinishRaces(autoFinish.ButtonPressed);
            autoFinish.Text = autoFinish.ButtonPressed ? "Auto Finish: ON" : "Auto Finish: OFF";
        };
        box.AddChild(autoFinish);

        box.AddChild(UiFactory.CreateLabel("ESC opens/closes this menu from the garden.", 6));
    }
}
