using Godot;
using VoidlingGame;

namespace Voidling.Presentation.Racing;

public partial class RaceScreen
{
    /// <summary>Canvas layer the pause menu lives on, above the results overlay.</summary>
    internal const int PauseCanvasLayer = 60;

    private CanvasLayer? _pauseMenu;

    /// <summary>
    /// Escape opens a pause menu with a way out of the race. Without it a race can only be left by
    /// finishing it, which strands the player whenever the results screen cannot be reached.
    /// </summary>
    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (!inputEvent.IsActionPressed("ui_cancel"))
            return;

        GetViewport().SetInputAsHandled();
        if (_pauseMenu == null)
            OpenPauseMenu();
        else
            ClosePauseMenu();
    }

    private void OpenPauseMenu()
    {
        _pauseMenu = new CanvasLayer { Layer = PauseCanvasLayer };
        AddChild(_pauseMenu);

        _pauseMenu.AddChild(new ColorRect
        {
            Color = new Color(0.12f, 0.18f, 0.16f, 0.55f),
            Position = Vector2.Zero,
            Size = new Vector2(ScreenWidth, ScreenHeight),
            MouseFilter = Control.MouseFilterEnum.Stop
        });

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _pauseMenu.AddChild(center);

        var panel = UiFactory.CreatePanel(new Vector2(280, 132));
        center.AddChild(panel);

        var box = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        box.AddThemeConstantOverride("separation", 8);
        panel.AddChild(box);

        var title = UiFactory.CreateTitle(Tr("UI_RACE_PAUSED"));
        title.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(title);

        var resume = UiFactory.CreateButton(Tr("UI_RACE_RESUME"));
        resume.CustomMinimumSize = new Vector2(200, 26);
        resume.Pressed += ClosePauseMenu;
        box.AddChild(resume);

        var quit = UiFactory.CreateButton(Tr("UI_RACE_QUIT"));
        quit.CustomMinimumSize = new Vector2(200, 26);
        quit.Pressed += () =>
        {
            ClosePauseMenu();
            ReturnRequested?.Invoke();
        };
        box.AddChild(quit);

        // Only the live race pauses. The results screen keeps its own controls responsive.
        _pausedRunning = _running;
        _running = false;
    }

    private void ClosePauseMenu()
    {
        if (_pauseMenu == null)
            return;

        _pauseMenu.QueueFree();
        _pauseMenu = null;
        _running = _pausedRunning && !_resultsShown;
    }
}
