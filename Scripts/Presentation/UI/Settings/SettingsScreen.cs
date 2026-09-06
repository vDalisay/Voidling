using System;
using Godot;
using Voidling.Presentation.UI.Common;
using VoidlingGame;

namespace Voidling.Presentation.UI.Settings;

public readonly record struct SettingsScreenState(
    float MasterVolume,
    float SoundEffectVolume,
    float UiSoundVolume,
    bool EdgePanning,
    bool AutoFinishRaces);

/// <summary>
/// Settings on the same paper card as the rest of the overhauled screens: audio in one section,
/// the two switches in another, each switch reading as a premium check or cross rather than a
/// button whose own label has to say what state it is in.
/// </summary>
public partial class SettingsScreen : VBoxContainer
{
    public event Action<float>? MasterVolumeChanged;
    public event Action<float>? SoundEffectVolumeChanged;
    public event Action<float>? UiSoundVolumeChanged;
    public event Action<bool>? EdgePanningChanged;
    public event Action<bool>? AutoFinishRacesChanged;

    private static readonly Texture2D CheckMark = GD.Load<Texture2D>(
        UiFactory.UiRoot + "Other UI sprites/Xs and check marks/1s/check mark.png");
    private static readonly Texture2D CrossMark = GD.Load<Texture2D>(
        UiFactory.UiRoot + "Other UI sprites/Xs and check marks/1s/X.png");

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

        Name = "Settings";
        AddThemeConstantOverride("separation", 6);
        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        var audio = Section("Audio", Tr("UI_SETTINGS_AUDIO"));
        audio.AddChild(BuildVolumeRow(Tr("UI_SETTINGS_MASTER"), _state.MasterVolume, value => { _state = _state with { MasterVolume = value }; MasterVolumeChanged?.Invoke(value); }));
        audio.AddChild(BuildVolumeRow(Tr("UI_SETTINGS_SFX"), _state.SoundEffectVolume, value => { _state = _state with { SoundEffectVolume = value }; SoundEffectVolumeChanged?.Invoke(value); }));
        audio.AddChild(BuildVolumeRow(Tr("UI_SETTINGS_UI"), _state.UiSoundVolume, value => { _state = _state with { UiSoundVolume = value }; UiSoundVolumeChanged?.Invoke(value); }));

        var play = Section("Play", Tr("UI_SETTINGS_PLAY"));
        play.AddChild(BuildSwitch(
            "EdgePan", Tr("UI_SETTINGS_EDGE_PAN"), Tr("UI_SETTINGS_EDGE_PAN_TOOLTIP"), _state.EdgePanning,
            enabled => { _state = _state with { EdgePanning = enabled }; EdgePanningChanged?.Invoke(enabled); }));
        play.AddChild(BuildSwitch(
            "AutoFinish", Tr("UI_SETTINGS_AUTO_FINISH"), Tr("UI_SETTINGS_AUTO_FINISH_TOOLTIP"), _state.AutoFinishRaces,
            enabled => { _state = _state with { AutoFinishRaces = enabled }; AutoFinishRacesChanged?.Invoke(enabled); }));
    }

    /// <summary>A headed paper card; the heading is the only text on the screen that is not a control.</summary>
    private VBoxContainer Section(string name, string heading)
    {
        AddChild(PaperCard.Header(heading));
        var panel = PaperCard.Panel(Vector2.Zero);
        panel.Name = name;
        panel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        AddChild(panel);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 4);
        panel.AddChild(box);
        return box;
    }

    private Control BuildVolumeRow(string channelName, float currentValue, Action<float> changed)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        var channel = UiFactory.CreateLabel(channelName, 7);
        channel.CustomMinimumSize = new Vector2(62, 22);
        channel.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(channel);
        var volume = new HSlider { MinValue = 0, MaxValue = 100, Step = 5, Value = currentValue * 100.0f, CustomMinimumSize = new Vector2(210, 22), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        var thumb = GD.Load<Texture2D>(UiFactory.UiRoot + "Other UI sprites/Sliders/slider_b_1.png");
        volume.AddThemeIconOverride("grabber", thumb);
        volume.AddThemeIconOverride("grabber_highlight", thumb);
        var rail = new StyleBoxFlat { BgColor = Color.FromHtml("#B5BEA9"), ContentMarginTop = 2, ContentMarginBottom = 2 };
        volume.AddThemeStyleboxOverride("slider", rail);
        volume.AddThemeStyleboxOverride("grabber_area", new StyleBoxFlat { BgColor = Color.FromHtml("#708969"), ContentMarginTop = 2, ContentMarginBottom = 2 });
        row.AddChild(volume);
        var volumeLabel = UiFactory.CreateLabel(FormatVolume(currentValue * 100.0f), 7);
        volumeLabel.CustomMinimumSize = new Vector2(46, 22);
        volumeLabel.HorizontalAlignment = HorizontalAlignment.Right;
        volumeLabel.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(volumeLabel);
        volume.ValueChanged += value => { volumeLabel.Text = FormatVolume((float)value); changed((float)value / 100.0f); };
        return row;
    }

    private Button BuildSwitch(string name, string label, string tooltip, bool enabled, Action<bool> changed)
    {
        var button = UiFactory.CreateButton(label);
        button.Name = name;
        button.ToggleMode = true;
        button.ButtonPressed = enabled;
        button.CustomMinimumSize = new Vector2(232, 26);
        button.Alignment = HorizontalAlignment.Left;
        button.TooltipText = tooltip;
        UiFactory.ApplyPixelFont(button, 7);

        var mark = new TextureRect
        {
            Name = "Mark",
            Texture = enabled ? CheckMark : CrossMark,
            CustomMinimumSize = new Vector2(16, 16),
            Size = new Vector2(16, 16),
            Position = new Vector2(206, 5),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        button.AddChild(mark);
        button.Pressed += () =>
        {
            mark.Texture = button.ButtonPressed ? CheckMark : CrossMark;
            changed(button.ButtonPressed);
        };
        return button;
    }

    private string FormatVolume(float percent) => string.Format(Tr("UI_SETTINGS_VOLUME"), Mathf.RoundToInt(percent));
}
