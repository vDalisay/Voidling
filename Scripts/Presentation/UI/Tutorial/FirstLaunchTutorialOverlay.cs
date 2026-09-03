using System;
using Godot;
using VoidlingGame;

namespace Voidling.Presentation.UI.Tutorial;

/// <summary>
/// Lightweight first-launch guide overlay. It owns only tutorial presentation: concise guidance,
/// a persistent Skip action and a non-blocking visual highlight. MainController owns navigation and
/// advances the sequence so this component never reaches into gameplay/session state.
/// </summary>
public partial class FirstLaunchTutorialOverlay : Control
{
    public event Action? ContinueRequested;
    public event Action? SkipRequested;

    private Label _message = null!;
    private Button _continue = null!;
    private Rect2? _highlight;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        ZIndex = 220;

        var panel = UiFactory.CreatePanel(new Vector2(390, 70));
        panel.Position = new Vector2(125, 278);
        panel.Size = new Vector2(390, 70);
        panel.MouseFilter = MouseFilterEnum.Stop;
        AddChild(panel);

        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 5);
        panel.AddChild(content);

        _message = UiFactory.CreateLabel(string.Empty, 7);
        _message.CustomMinimumSize = new Vector2(366, 32);
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
        _continue.Pressed += () => ContinueRequested?.Invoke();
        actions.AddChild(_continue);
    }

    public void ShowStep(string message, string continueText, bool showContinue, Rect2? highlight)
    {
        _message.Text = message;
        _continue.Text = continueText;
        _continue.Visible = showContinue;
        _highlight = highlight;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!_highlight.HasValue)
            return;

        var rect = _highlight.Value;
        DrawRect(rect.Grow(3.0f), new Color(1.0f, 0.95f, 0.52f, 0.28f), true);
        DrawRect(rect.Grow(3.0f), Color.FromHtml("#FFF38A"), false, 2.0f);
    }
}
