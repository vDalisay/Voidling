using System;
using Godot;
using VoidlingGame;

namespace Voidling.Presentation.UI.Settings;

public readonly record struct SettingsScreenState(
    float MasterVolume,
    bool EdgePanning,
    bool AutoFinishRaces);

/// <summary>
/// Standalone settings view. It renders current values and emits player intent; it does not
/// know about GameSession, persistence, AudioServer, or application services.
/// </summary>
public partial class SettingsScreen : VBoxContainer
{
    public event Action<float>? MasterVolumeChanged;
    public event Action<bool>? EdgePanningChanged;
    public event Action<bool>? AutoFinishRacesChanged;

    private SettingsScreenState _state;
    private bool _configured;

    public void Configure(SettingsScreenState state)
    {
        if (IsInsideTree())
            throw new InvalidOperationException("SettingsScreen must be configured before it enters the scene tree.");

        _state = state;
        _configured = true;
    }

    public override void _Ready()
    {
        if (!_configured)
            throw new InvalidOperationException("SettingsScreen must be configured before AddChild.");

        AddThemeConstantOverride("separation", 4);
        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        AddChild(UiFactory.CreateLabel(Tr("UI_SETTINGS_AUDIO"), 9));
        AddChild(BuildVolumeRow());

        AddChild(UiFactory.CreateLabel(Tr("UI_SETTINGS_CAMERA"), 9));
        AddChild(BuildEdgePanButton());

        AddChild(UiFactory.CreateLabel(Tr("UI_SETTINGS_RACE"), 9));
        AddChild(BuildAutoFinishButton());

        AddChild(UiFactory.CreateLabel(Tr("UI_SETTINGS_HINT"), 6));
    }

    private Control BuildVolumeRow()
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);

        var volumeLabel = UiFactory.CreateLabel(FormatVolume(_state.MasterVolume * 100.0f), 7);
        volumeLabel.CustomMinimumSize = new Vector2(90, 22);
        row.AddChild(volumeLabel);

        var volume = new HSlider
        {
            MinValue = 0,
            MaxValue = 100,
            Step = 5,
            Value = _state.MasterVolume * 100.0f,
            CustomMinimumSize = new Vector2(220, 22),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        volume.ValueChanged += value =>
        {
            var normalized = (float)value / 100.0f;
            _state = _state with { MasterVolume = normalized };
            volumeLabel.Text = FormatVolume((float)value);
            MasterVolumeChanged?.Invoke(normalized);
        };
        row.AddChild(volume);
        return row;
    }

    private Button BuildEdgePanButton()
    {
        var button = UiFactory.CreateButton(EdgePanText(_state.EdgePanning));
        button.ToggleMode = true;
        button.ButtonPressed = _state.EdgePanning;
        button.CustomMinimumSize = new Vector2(190, 25);
        button.TooltipText = Tr("UI_SETTINGS_EDGE_PAN_TOOLTIP");
        button.Pressed += () =>
        {
            _state = _state with { EdgePanning = button.ButtonPressed };
            button.Text = EdgePanText(button.ButtonPressed);
            EdgePanningChanged?.Invoke(button.ButtonPressed);
        };
        return button;
    }

    private Button BuildAutoFinishButton()
    {
        var button = UiFactory.CreateButton(AutoFinishText(_state.AutoFinishRaces));
        button.ToggleMode = true;
        button.ButtonPressed = _state.AutoFinishRaces;
        button.CustomMinimumSize = new Vector2(190, 25);
        button.TooltipText = Tr("UI_SETTINGS_AUTO_FINISH_TOOLTIP");
        button.Pressed += () =>
        {
            _state = _state with { AutoFinishRaces = button.ButtonPressed };
            button.Text = AutoFinishText(button.ButtonPressed);
            AutoFinishRacesChanged?.Invoke(button.ButtonPressed);
        };
        return button;
    }

    private string FormatVolume(float percent)
        => string.Format(Tr("UI_SETTINGS_VOLUME"), Mathf.RoundToInt(percent));

    private string EdgePanText(bool enabled)
        => Tr(enabled ? "UI_SETTINGS_EDGE_PAN_ON" : "UI_SETTINGS_EDGE_PAN_OFF");

    private string AutoFinishText(bool enabled)
        => Tr(enabled ? "UI_SETTINGS_AUTO_FINISH_ON" : "UI_SETTINGS_AUTO_FINISH_OFF");
}
