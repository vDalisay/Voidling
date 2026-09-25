using Godot;
using Voidling.Presentation.UI.Audio;
using Voidling.Presentation.UI.Common;

namespace Voidling.Presentation.UI.Motion;

/// <summary>How much a button moves when touched.</summary>
public enum ButtonFeel
{
    /// <summary>Everyday buttons: hover pop, press squash, release bounce.</summary>
    Standard,
    /// <summary>A screen's call to action: bigger pops and a periodic shine.</summary>
    Primary,
    /// <summary>Slots, cards and tabs: a gentler hover so grids do not jitter.</summary>
    Soft
}

/// <summary>
/// The incremental-game feel on any button, as a component: hover (or keyboard focus) lifts it with
/// an overshoot pop, pressing squashes it, releasing bounces it home, and a click on a disabled
/// button gets a small "no" shove instead. Focus shows the pack's selector brackets, the pointer
/// turns to the pointing paw, and hover, press and refusal each have a soft sound (see
/// <see cref="UiSounds"/>). The click itself is never delayed: every effect is decoration on the
/// button's own signals.
///
/// The rest states come from the chrome (hover draws one pixel higher, pressed one lower), so a
/// button at rest is always at scale 1 on whole pixels. Many screens rebuild a list when a row is
/// picked; a rebuilt button with the name just released carries the release bounce on, so the
/// press still reads even though the node that was pressed is gone.
/// </summary>
public partial class ButtonJuice : Node
{
    private static readonly ShaderMaterial? ShineTemplate = LoadShine();
    private static string _lastReleased = string.Empty;
    private static ulong _lastReleasedMsec;

    private BaseButton _button = null!;
    private FocusCursor? _cursor;
    private ButtonFeel _feel;

    /// <summary>Gives <paramref name="button"/> the feel, once; later calls only change the feel.</summary>
    public static ButtonJuice Attach(BaseButton button, ButtonFeel feel = ButtonFeel.Standard)
    {
        foreach (var child in button.GetChildren(includeInternal: true))
        {
            if (child is not ButtonJuice existing) continue;
            existing.SetFeel(feel);
            return existing;
        }

        // Both parts join the button now: adding children from a child's _Ready is not allowed
        // while the button is still readying its own children.
        var juice = new ButtonJuice { Name = "Juice", _feel = feel, _cursor = new FocusCursor() };
        button.AddChild(juice, false, InternalMode.Front);
        button.AddChild(juice._cursor, false, InternalMode.Back);
        return juice;
    }

    public override void _Ready()
    {
        _button = GetParent<BaseButton>();

        _button.MouseEntered += OnHovered;
        _button.FocusEntered += OnFocused;
        _button.FocusExited += OnUnfocused;
        _button.ButtonDown += OnDown;
        _button.ButtonUp += OnUp;
        _button.GuiInput += OnGuiInput;
        _button.Resized += OnResized;
        OnResized();
        if (_button.MouseDefaultCursorShape == Control.CursorShape.Arrow)
            _button.MouseDefaultCursorShape = Control.CursorShape.PointingHand;

        if (_button is Button button && button.ToggleMode &&
            button.GetThemeStylebox("pressed") == UiSkin.DefaultPressed)
        {
            button.AddThemeStyleboxOverride("pressed", UiSkin.Selected);
            button.AddThemeStyleboxOverride("hover_pressed", UiSkin.SelectedHover);
        }
        ApplyShine();

        // Picked from a list that rebuilt itself: carry the press on to the new node.
        if (_button.Name == _lastReleased && Time.GetTicksMsec() - _lastReleasedMsec < 320)
            Callable.From(() => UiMotion.Release(_button)).CallDeferred();
    }

    private void SetFeel(ButtonFeel feel)
    {
        _feel = feel;
        if (IsInsideTree()) ApplyShine();
    }

    private float HoverPop => _feel switch
    {
        ButtonFeel.Primary => 0.1f,
        ButtonFeel.Soft => 0.05f,
        _ => 0.07f
    };

    private void OnHovered()
    {
        if (_button.Disabled || (_button.ButtonPressed && _button.ToggleMode)) return;
        UiMotion.Pop(_button, HoverPop);
        UiSounds.Play(_button, UiCue.Hover);
    }

    private void OnFocused()
    {
        _cursor?.ShowCursor();
        // A click also focuses; only keyboard or controller focus needs the hover pop.
        if (!_button.IsHovered() && !_button.Disabled)
            UiMotion.Pop(_button, HoverPop);
    }

    private void OnUnfocused() => _cursor?.HideCursor();

    private void OnDown()
    {
        if (_button.Disabled) return;
        UiMotion.Squash(_button);
        UiSounds.Play(_button, UiCue.Press);
    }

    private void OnUp()
    {
        _lastReleased = _button.Name;
        _lastReleasedMsec = Time.GetTicksMsec();
        UiMotion.Release(_button);
        if (_feel == ButtonFeel.Primary)
            UiMotion.Flash(_button);
    }

    private void OnGuiInput(InputEvent inputEvent)
    {
        if (!_button.Disabled ||
            inputEvent is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
            return;
        UiMotion.Nudge(_button);
        UiMotion.Flash(_button, new Color(0.8f, 0.72f, 0.68f), UiMotion.Normal);
        UiSounds.Play(_button, UiCue.Denied);
    }

    private void OnResized()
    {
        UiMotion.CenterPivot(_button);
        if (_button.Material is ShaderMaterial shine && shine.Shader == ShineTemplate?.Shader)
            shine.SetShaderParameter("width_px", _button.Size.X);
    }

    private void ApplyShine()
    {
        if (_feel != ButtonFeel.Primary || UiMotion.Reduced || ShineTemplate == null || _button.Material != null)
            return;
        var shine = (ShaderMaterial)ShineTemplate.Duplicate();
        // Desynchronise neighbouring buttons with a phase from the name, stable between runs.
        var phase = 0u;
        foreach (var character in _button.Name.ToString())
            phase = phase * 31u + character;
        shine.SetShaderParameter("phase", phase % 97u / 97.0f * 4.2f);
        shine.SetShaderParameter("width_px", _button.Size.X);
        _button.Material = shine;
    }

    private static ShaderMaterial? LoadShine()
    {
        var shader = GD.Load<Shader>("res://Resources/Presentation/UI/UiShine.gdshader");
        return shader == null ? null : new ShaderMaterial { Shader = shader };
    }
}
