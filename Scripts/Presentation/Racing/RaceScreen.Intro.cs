using System.Collections.Generic;
using Godot;
using VoidlingGame;

namespace Voidling.Presentation.Racing;

public partial class RaceScreen
{
    private async void PlayRaceIntro()
    {
        if (_entry == null)
            return;

        var layer = new CanvasLayer { Layer = 100 };
        AddChild(layer);
        var loading = new Control
        {
            Position = new Vector2(ScreenWidth + 24, 0),
            Size = new Vector2(ScreenWidth, ScreenHeight),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        layer.AddChild(loading);
        loading.AddChild(new ColorRect
        {
            Color = Color.FromHtml("#FFF2A8"),
            Size = new Vector2(ScreenWidth, ScreenHeight),
            MouseFilter = Control.MouseFilterEnum.Ignore
        });
        loading.AddChild(CreateSawtoothEdge(0, -24, Color.FromHtml("#FFF2A8")));
        loading.AddChild(CreateSawtoothEdge(ScreenWidth, ScreenWidth + 24, Color.FromHtml("#FFF2A8")));

        var heading = UiFactory.CreateTitle(Tr("UI_RACE_LOADING"));
        heading.Position = new Vector2(180, 58);
        heading.Size = new Vector2(280, 30);
        heading.HorizontalAlignment = HorizontalAlignment.Center;
        loading.AddChild(heading);

        var entrant = _entry.Entrants[_vfxRandom.Next(_entry.Entrants.Count)];
        var portrait = CreateEntrantPortrait(entrant, new Vector2(88, 88));
        portrait.Position = new Vector2(276, 126);
        portrait.Size = new Vector2(88, 88);
        portrait.PivotOffset = new Vector2(44, 44);
        loading.AddChild(portrait);

        var enter = CreateTween();
        enter.TweenProperty(loading, "position:x", 0.0f, 0.28)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        await ToSignal(enter, Tween.SignalName.Finished);
        if (!IsInsideTree())
            return;

        var pulse = CreateTween().SetLoops(2);
        pulse.TweenProperty(portrait, "scale", new Vector2(1.12f, 1.12f), 0.2)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        pulse.TweenProperty(portrait, "scale", Vector2.One, 0.2)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        await ToSignal(pulse, Tween.SignalName.Finished);
        if (!IsInsideTree())
            return;

        var exit = CreateTween();
        exit.TweenProperty(loading, "position:x", -ScreenWidth - 24, 0.28)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
        await ToSignal(exit, Tween.SignalName.Finished);
        if (!IsInsideTree())
            return;
        loading.QueueFree();

        var countdown = UiFactory.CreateTitle(string.Empty);
        countdown.Position = new Vector2(220, 132);
        countdown.Size = new Vector2(200, 80);
        countdown.HorizontalAlignment = HorizontalAlignment.Center;
        countdown.VerticalAlignment = VerticalAlignment.Center;
        countdown.AddThemeFontSizeOverride("font_size", 38);
        countdown.AddThemeColorOverride("font_color", Color.FromHtml("#FFF2A8"));
        countdown.AddThemeColorOverride("font_outline_color", Color.FromHtml("#465247"));
        countdown.AddThemeConstantOverride("outline_size", 5);
        layer.AddChild(countdown);

        foreach (var text in new[] { "3", "2", "1", Tr("UI_RACE_COUNTDOWN_GO") })
        {
            countdown.Text = text;
            countdown.Scale = new Vector2(1.3f, 1.3f);
            countdown.Modulate = Colors.White;
            var beat = CreateTween().SetParallel(true);
            beat.TweenProperty(countdown, "scale", Vector2.One, 0.42)
                .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            beat.TweenProperty(countdown, "modulate:a", 0.0f, 0.42).SetDelay(0.25);
            await ToSignal(beat, Tween.SignalName.Finished);
            if (!IsInsideTree())
                return;
        }

        layer.QueueFree();
        _running = true;
        UpdateHud();
    }

    private static Polygon2D CreateSawtoothEdge(float edgeX, float toothX, Color color)
        => new() { Polygon = BuildSawtoothEdge(edgeX, toothX), Color = color, ZIndex = 1 };

    private static Vector2[] BuildSawtoothEdge(float edgeX, float toothX)
    {
        var points = new List<Vector2> { new(edgeX, 0) };
        for (var y = 0.0f; y < ScreenHeight; y += 18.0f)
        {
            points.Add(new Vector2(toothX, y + 9.0f));
            points.Add(new Vector2(edgeX, Mathf.Min(y + 18.0f, ScreenHeight)));
        }
        return points.ToArray();
    }
}
