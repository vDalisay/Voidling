using System;
using Godot;
using Voidling.Presentation.UI.Audio;
using Voidling.Presentation.UI.Common;
using Voidling.Presentation.UI.Motion;
using VoidlingGame;

namespace Voidling.Presentation.UI.Settings;

public readonly record struct SettingsScreenState(
    float MasterVolume,
    float SoundEffectVolume,
    float UiSoundVolume,
    bool EdgePanning,
    bool AutoFinishRaces,
    bool GardenTint,
    bool ReduceMotion = false);

/// <summary>
/// Settings on the same paper card as the rest of the overhauled screens: audio in one section,
/// the play switches in another, each switch reading as a premium check or cross rather than a
/// button whose own label has to say what state it is in.
/// </summary>
public partial class SettingsScreen : VBoxContainer
{
    public event Action<float>? MasterVolumeChanged;
    public event Action<float>? SoundEffectVolumeChanged;
    public event Action<float>? UiSoundVolumeChanged;
    public event Action<bool>? EdgePanningChanged;
    public event Action<bool>? AutoFinishRacesChanged;
    public event Action<bool>? GardenTintChanged;
    public event Action<bool>? ReduceMotionChanged;

    // The pack's toggle switch: knob left on a bark track (off), knob right on a leaf track (on),
    // and the knob-right-on-bark frame in between, stepped through when a switch flips.
    private static readonly Texture2D SwitchOff = SwitchFrame(2);
    private static readonly Texture2D SwitchMoving = SwitchFrame(34);
    private static readonly Texture2D SwitchOn = SwitchFrame(66);

    private static AtlasTexture SwitchFrame(int x)
        => new() { Atlas = UiSkin.SettingsSheet, Region = new Rect2(x, 151, 28, 18) };

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
        play.AddChild(BuildSwitch(
            "GardenTint", Tr("UI_SETTINGS_GARDEN_TINT"), Tr("UI_SETTINGS_GARDEN_TINT_TOOLTIP"), _state.GardenTint,
            enabled => { _state = _state with { GardenTint = enabled }; GardenTintChanged?.Invoke(enabled); }));
        play.AddChild(BuildSwitch(
            "ReduceMotion", Tr("UI_SETTINGS_REDUCE_MOTION"), Tr("UI_SETTINGS_REDUCE_MOTION_TOOLTIP"), _state.ReduceMotion,
            enabled => { _state = _state with { ReduceMotion = enabled }; ReduceMotionChanged?.Invoke(enabled); }));
    }

    /// <summary>A headed paper card; the heading is the only text on the screen that is not a control.</summary>
    private VBoxContainer Section(string name, string heading)
    {
        AddChild(PaperCard.Header(heading));
        var panel = PaperCard.Panel(Vector2.Zero);
        panel.Name = name;
        var style = UiSkin.Paper();
        style.ContentMarginTop = style.ContentMarginBottom = 6;
        panel.AddThemeStyleboxOverride("panel", style);
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
        // A wooden groove and a leaf-green fill, square-cornered so the track stays pixel-crisp.
        var rail = UiSkin.BarTrack();
        rail.ContentMarginTop = rail.ContentMarginBottom = 2;
        volume.AddThemeStyleboxOverride("slider", rail);
        var filled = new StyleBoxFlat
        {
            BgColor = Color.FromHtml("#78B85A"), AntiAliasing = false, ContentMarginTop = 2, ContentMarginBottom = 2,
            BorderColor = Color.FromHtml("#B8E08F"), BorderWidthTop = 1
        };
        volume.AddThemeStyleboxOverride("grabber_area", filled);
        volume.AddThemeStyleboxOverride("grabber_area_highlight", filled);
        row.AddChild(volume);
        var volumeLabel = UiFactory.CreateLabel(FormatVolume(currentValue * 100.0f), 7);
        volumeLabel.CustomMinimumSize = new Vector2(46, 22);
        volumeLabel.HorizontalAlignment = HorizontalAlignment.Right;
        volumeLabel.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(volumeLabel);
        volume.ValueChanged += value =>
        {
            volumeLabel.Text = FormatVolume((float)value);
            UiMotion.Pop(volumeLabel, 0.12f);
            changed((float)value / 100.0f);
        };
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

        // The row itself does not stay pushed in: the switch at its right end shows the state.
        button.AddThemeStyleboxOverride("pressed", UiSkin.Button(ButtonTone.Tan, UiSkin.ButtonState.Normal));
        button.AddThemeStyleboxOverride("hover_pressed", UiSkin.Button(ButtonTone.Tan, UiSkin.ButtonState.Hover));

        var mark = new TextureRect
        {
            Name = "Mark",
            Texture = enabled ? SwitchOn : SwitchOff,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Keep,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AnchorLeft = 1, AnchorRight = 1, AnchorTop = 0.5f, AnchorBottom = 0.5f,
            OffsetLeft = -36, OffsetRight = -8, OffsetTop = -10, OffsetBottom = 8,
            PivotOffset = new Vector2(14, 9)
        };
        button.AddChild(mark);
        button.Pressed += () =>
        {
            var on = button.ButtonPressed;
            UiSounds.Play(button, on ? UiCue.ToggleOn : UiCue.ToggleOff);
            // The knob slides across in the pack's own frames; the setting has already changed.
            var slide = UiMotion.Start(mark, "switch");
            if (slide == null)
            {
                mark.Texture = on ? SwitchOn : SwitchOff;
            }
            else
            {
                mark.Texture = SwitchMoving;
                slide.TweenInterval(0.07);
                slide.TweenCallback(Callable.From(() => mark.Texture = on ? SwitchOn : SwitchOff));
                UiMotion.Pop(mark, 0.18f, UiMotion.Normal);
            }
            changed(on);
        };
        return button;
    }

    private string FormatVolume(float percent) => string.Format(Tr("UI_SETTINGS_VOLUME"), Mathf.RoundToInt(percent));
}
