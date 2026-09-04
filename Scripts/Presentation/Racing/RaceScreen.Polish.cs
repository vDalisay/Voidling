using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using VoidlingGame;

namespace Voidling.Presentation.Racing;

public partial class RaceScreen
{
    private bool _resultPresentationPolished;

    public override void _EnterTree()
    {
        AddChild(new RaceScreenPolishDriver
        {
            Name = "PresentationPolishDriver",
            OwnerScreen = this
        });
    }

    internal void ApplyPostRaceScreenPresentationFrame()
    {
        if (_resultsShown && !_resultPresentationPolished)
        {
            _resultPresentationPolished = true;
            PolishResultPresentation();
        }
    }

    private void PolishResultPresentation()
    {
        if (_simulation == null || _entry == null)
            return;

        var canvas = GetChildren()
            .OfType<CanvasLayer>()
            .FirstOrDefault(layer => layer.Layer == 50);
        if (canvas == null)
            return;

        var panel = FindDescendant<PanelContainer>(canvas, candidate =>
            candidate.CustomMinimumSize.X >= 500.0f && candidate.CustomMinimumSize.Y >= 280.0f);
        if (panel == null)
            return;

        var selectedPlace = _simulation.FinishOrder.IndexOf(_playerId) + 1;
        if (selectedPlace <= 0)
            selectedPlace = _entry.Entrants.Count;
        var isLast = selectedPlace == _entry.Entrants.Count;

        var title = FindDescendant<Label>(panel, label =>
            label.Text.StartsWith("RACE RESULTS", StringComparison.Ordinal));
        if (title != null)
        {
            title.Text = selectedPlace == 1
                ? Tr("UI_RACE_RESULT_WIN")
                : isLast
                    ? Tr("UI_RACE_RESULT_LAST")
                    : Tr("UI_RACE_RESULT_COMPLETE");

            if (title.GetParent() is VBoxContainer box)
            {
                var placement = UiFactory.CreateLabel(
                    string.Format(Tr("UI_RACE_RESULT_PLACE"), selectedPlace),
                    8);
                placement.HorizontalAlignment = HorizontalAlignment.Center;
                box.AddChild(placement);
                box.MoveChild(placement, title.GetIndex() + 1);
            }
        }

        var stage = FindDescendant<Control>(panel, control =>
            Math.Abs(control.CustomMinimumSize.X - 480.0f) < 1.0f &&
            Math.Abs(control.CustomMinimumSize.Y - 192.0f) < 1.0f);
        if (stage != null)
            AlignPodiumEntrants(stage);

        var returnButton = FindDescendant<Button>(panel, button =>
            string.Equals(button.Text, "Return to Garden", StringComparison.Ordinal));
        if (returnButton != null)
            returnButton.Text = Tr("UI_RACE_RETURN");

        AnimateResultPanel(panel, isLast);

        if (isLast)
            SpawnAnimeSweatDrop(canvas);
        else
            SpawnCelebrationParticles(canvas, selectedPlace == 1 ? 38 : 24);
    }

    private static void AlignPodiumEntrants(Control stage)
    {
        var portraits = stage.GetChildren().OfType<TextureRect>().OrderBy(p => p.Position.X).ToList();
        var names = stage.GetChildren().OfType<Label>().OrderBy(label => label.Position.X).ToList();
        var podiums = stage.GetChildren().OfType<PanelContainer>()
            .Where(block => block.Size.Y > 20.0f)
            .OrderBy(block => block.Position.X)
            .ToList();
        var puddle = stage.GetChildren().OfType<PanelContainer>()
            .FirstOrDefault(block => block.Size.Y <= 20.0f);

        if (portraits.Count < 4 || podiums.Count < 3 || puddle == null)
            return;

        // Existing creation order along X is second, first, third, fourth. Keep entrant/place
        // identity intact and only place each portrait's feet directly on its award surface.
        for (var i = 0; i < 3; i++)
        {
            var surface = podiums[i];
            var portrait = portraits[i];
            portrait.Position = new Vector2(
                surface.Position.X + (surface.Size.X - portrait.Size.X) * 0.5f,
                surface.Position.Y - 35.0f);
            if (i < names.Count)
            {
                names[i].Position = new Vector2(
                    surface.Position.X + (surface.Size.X - names[i].Size.X) * 0.5f,
                    portrait.Position.Y - 13.0f);
            }
        }

        var fourthPortrait = portraits[3];
        fourthPortrait.Position = new Vector2(
            puddle.Position.X + (puddle.Size.X - fourthPortrait.Size.X) * 0.5f,
            puddle.Position.Y - 34.0f);
        if (names.Count >= 4)
        {
            names[3].Position = new Vector2(
                puddle.Position.X + (puddle.Size.X - names[3].Size.X) * 0.5f,
                fourthPortrait.Position.Y - 13.0f);
        }
    }

    private void AnimateResultPanel(Control panel, bool isLast)
    {
        panel.PivotOffset = panel.Size.LengthSquared() > 1.0f
            ? panel.Size * 0.5f
            : new Vector2(260.0f, 146.0f);
        panel.Scale = new Vector2(0.72f, 0.72f);
        panel.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);
        panel.RotationDegrees = isLast ? -4.0f : 0.0f;

        var pop = CreateTween();
        pop.TweenProperty(panel, "scale", new Vector2(1.06f, 1.06f), 0.24)
            .SetTrans(Tween.TransitionType.Back)
            .SetEase(Tween.EaseType.Out);
        pop.TweenProperty(panel, "scale", Vector2.One, 0.11)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);

        var fade = CreateTween();
        fade.TweenProperty(panel, "modulate", Colors.White, 0.14)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);

        if (!isLast)
            return;

        var tilt = CreateTween();
        tilt.TweenInterval(0.20);
        tilt.TweenProperty(panel, "rotation_degrees", 2.8f, 0.14)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);
        tilt.TweenProperty(panel, "rotation_degrees", -2.0f, 0.12)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.InOut);
        tilt.TweenProperty(panel, "rotation_degrees", 0.0f, 0.18)
            .SetTrans(Tween.TransitionType.Back)
            .SetEase(Tween.EaseType.Out);
    }

    private void SpawnCelebrationParticles(CanvasLayer canvas, int count)
    {
        var colors = new[]
        {
            Color.FromHtml("#F2D45C"),
            Color.FromHtml("#E7655A"),
            Color.FromHtml("#78C96A"),
            Color.FromHtml("#B47AE5"),
            Color.FromHtml("#F7F3E7")
        };

        for (var i = 0; i < count; i++)
        {
            var width = 2.0f + (float)_vfxRandom.NextDouble() * 3.0f;
            var height = 2.0f + (float)_vfxRandom.NextDouble() * 4.0f;
            var piece = new Polygon2D
            {
                Polygon = new[]
                {
                    new Vector2(-width, -height),
                    new Vector2(width, -height),
                    new Vector2(width, height),
                    new Vector2(-width, height)
                },
                Color = colors[i % colors.Length],
                Position = new Vector2(
                    245.0f + (float)_vfxRandom.NextDouble() * 150.0f,
                    42.0f + (float)_vfxRandom.NextDouble() * 28.0f),
                Rotation = (float)_vfxRandom.NextDouble() * Mathf.Tau,
                ZIndex = 70
            };
            canvas.AddChild(piece);

            var destination = piece.Position + new Vector2(
                (float)(_vfxRandom.NextDouble() * 320.0 - 160.0),
                120.0f + (float)_vfxRandom.NextDouble() * 150.0f);
            var duration = 0.75 + _vfxRandom.NextDouble() * 0.55;

            var movement = CreateTween();
            movement.TweenProperty(piece, "position", destination, duration)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.Out);

            var spin = CreateTween();
            spin.TweenProperty(
                piece,
                "rotation",
                piece.Rotation + Mathf.Pi * (2.0f + (float)_vfxRandom.NextDouble() * 3.0f),
                duration);

            var fade = CreateTween();
            fade.TweenInterval(Math.Max(0.18, duration - 0.30));
            fade.TweenProperty(piece, "modulate:a", 0.0f, 0.30);
            fade.Finished += piece.QueueFree;
        }
    }

    private void SpawnAnimeSweatDrop(CanvasLayer canvas)
    {
        var drop = new Polygon2D
        {
            Polygon = new[]
            {
                new Vector2(0, -10),
                new Vector2(7, -1),
                new Vector2(6, 6),
                new Vector2(0, 11),
                new Vector2(-6, 6),
                new Vector2(-7, -1)
            },
            Color = Color.FromHtml("#7ED2F2"),
            Position = new Vector2(82, 42),
            Scale = new Vector2(0.45f, 0.45f),
            ZIndex = 75
        };
        canvas.AddChild(drop);

        var highlight = new Line2D
        {
            Width = 1.5f,
            DefaultColor = new Color(1.0f, 1.0f, 1.0f, 0.86f),
            Points = new[] { new Vector2(-2.5f, -4.0f), new Vector2(-3.5f, 1.5f) }
        };
        drop.AddChild(highlight);

        var fall = CreateTween();
        fall.TweenInterval(0.15);
        fall.TweenProperty(drop, "scale", Vector2.One, 0.16)
            .SetTrans(Tween.TransitionType.Back)
            .SetEase(Tween.EaseType.Out);
        fall.TweenProperty(drop, "position:y", 76.0f, 0.34)
            .SetTrans(Tween.TransitionType.Bounce)
            .SetEase(Tween.EaseType.Out);
        fall.TweenInterval(0.55);
        fall.TweenProperty(drop, "modulate:a", 0.0f, 0.24);
        fall.Finished += drop.QueueFree;
    }

    private static T? FindDescendant<T>(Node root, Func<T, bool> predicate) where T : Node
    {
        foreach (var child in root.GetChildren())
        {
            if (child is T match && predicate(match))
                return match;

            var nested = FindDescendant(child, predicate);
            if (nested != null)
                return nested;
        }

        return null;
    }
}

internal sealed partial class RaceScreenPolishDriver : Node
{
    public RaceScreen OwnerScreen { get; init; } = null!;

    public override void _Process(double delta)
        => OwnerScreen.ApplyPostRaceScreenPresentationFrame();
}
