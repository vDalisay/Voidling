using System;
using Godot;
using Voidling.Presentation.UI.Common;
using Voidling.Presentation.UI.Motion;
using VoidlingGame;

namespace Voidling.Presentation.UI.Tutorial;

/// <summary>
/// Lightweight first-launch guide overlay. It owns only tutorial presentation: concise guidance,
/// a persistent Skip action and a non-blocking visual highlight. MainController owns navigation and
/// advances the sequence so this component never reaches into gameplay/session state.
///
/// The guide is the pack's dialog board with its "click to continue" arrow bobbing beside Next;
/// each step pops the board and types its text in, and the highlighted area wears the pack's
/// selector brackets, breathing in whole-pixel steps.
/// </summary>
public partial class FirstLaunchTutorialOverlay : Control
{
    public event Action? ContinueRequested;
    public event Action? SkipRequested;

    private static readonly Rect2[] Corners =
    {
        new(36, 4, 8, 9), new(52, 4, 8, 9), new(36, 20, 8, 9), new(52, 20, 8, 9)
    };

    private PanelContainer _panel = null!;
    private Label _message = null!;
    private Button _continue = null!;
    private TextureRect _arrow = null!;
    private Rect2? _highlight;
    private int _breath;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        ZIndex = 220;

        _panel = UiFactory.CreateWindowPanel(new Vector2(342, 82));
        _panel.Position = new Vector2(110, 184);
        _panel.Size = new Vector2(342, 82);
        _panel.MouseFilter = MouseFilterEnum.Stop;
        AddChild(_panel);

        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 5);
        _panel.AddChild(content);

        _message = UiFactory.CreateLabel(string.Empty, 7);
        _message.CustomMinimumSize = new Vector2(318, 32);
        _message.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        content.AddChild(_message);

        var actions = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.End
        };
        actions.AddThemeConstantOverride("separation", 6);
        content.AddChild(actions);

        var skip = UiFactory.CreateButton("Skip");
        skip.CustomMinimumSize = new Vector2(70, 22);
        skip.Pressed += () => SkipRequested?.Invoke();
        actions.AddChild(skip);

        _continue = UiFactory.CreateButton("Next");
        _continue.CustomMinimumSize = new Vector2(76, 22);
        UiFactory.ApplyPrimaryStyle(_continue);
        _continue.Pressed += () => ContinueRequested?.Invoke();
        actions.AddChild(_continue);

        // The pack's "click to continue" arrow, cycling through its seven frames.
        var arrowTexture = new AtlasTexture { Atlas = UiSkin.ContinueArrow, Region = new Rect2(0, 0, 16, 16) };
        _arrow = new TextureRect
        {
            Texture = arrowTexture,
            CustomMinimumSize = new Vector2(16, 16),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            StretchMode = TextureRect.StretchModeEnum.KeepCentered,
            MouseFilter = MouseFilterEnum.Ignore
        };
        actions.AddChild(_arrow);
        var frames = UiMotion.Loop(_arrow, "frames");
        if (frames != null)
        {
            for (var frame = 0; frame < 7; frame++)
            {
                var region = new Rect2(frame * 16, 0, 16, 16);
                frames.TweenCallback(Callable.From(() => arrowTexture.Region = region));
                frames.TweenInterval(0.1);
            }
        }

        var breathe = UiMotion.Loop(this, "breathe");
        if (breathe == null) return;
        breathe.TweenInterval(0.34);
        breathe.TweenCallback(Callable.From(() => { _breath = 1 - _breath; if (_highlight.HasValue) QueueRedraw(); }));
    }

    public void ShowStep(string message, string continueText, bool showContinue, Rect2? highlight)
    {
        _message.Text = message;
        _continue.Text = continueText;
        _continue.Visible = showContinue;
        _arrow.Visible = showContinue;
        _highlight = highlight;
        QueueRedraw();

        UiMotion.Appear(_panel, 0.0, UiMotion.Quick, 0.06f);
        var typing = UiMotion.Start(_message, "type");
        if (typing == null) { _message.VisibleRatio = 1; return; }
        _message.VisibleRatio = 0;
        typing.TweenProperty(_message, "visible_ratio", 1.0f, Mathf.Clamp(message.Length * 0.012f, 0.2f, 0.9f));
    }

    public override void _Draw()
    {
        if (!_highlight.HasValue)
            return;

        var rect = _highlight.Value;
        DrawRect(rect.Grow(3.0f), new Color(1.0f, 0.95f, 0.52f, 0.16f), true);
        // The pack's selector brackets on the highlighted area's corners.
        var outset = 4 + _breath;
        var area = rect.Grow(outset);
        DrawTextureRectRegion(UiSkin.Selectors, new Rect2(area.Position, new Vector2(8, 9)), Corners[0]);
        DrawTextureRectRegion(UiSkin.Selectors, new Rect2(new Vector2(area.End.X - 8, area.Position.Y), new Vector2(8, 9)), Corners[1]);
        DrawTextureRectRegion(UiSkin.Selectors, new Rect2(new Vector2(area.Position.X, area.End.Y - 9), new Vector2(8, 9)), Corners[2]);
        DrawTextureRectRegion(UiSkin.Selectors, new Rect2(new Vector2(area.End.X - 8, area.End.Y - 9), new Vector2(8, 9)), Corners[3]);
    }
}
