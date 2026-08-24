using System;
using System.Linq;
using Godot;
using Voidling.Presentation.Voidlings;

namespace Voidling.Presentation.Racing;

public partial class RaceScreen
{
    private const float RaceSpriteScale = 0.72f;
    private bool _finalShadowCorrectionApplied;
    private bool _resultPresentationPolished;

    public override void _EnterTree()
    {
        AddChild(new RaceScreenPolishDriver(this)
        {
            Name = "PresentationPolishDriver"
        });
    }

    internal void ApplyPostRaceScreenPresentationFrame()
    {
        // RaceScreen's authoritative presentation pass runs on the parent before this child.
        // Correct the legacy race footprint after that pass so challenges use the same compact,
        // foot-centered shadow proportions as the garden instead of maintaining a second style.
        if (_running)
        {
            ApplyUnifiedVoidlingGroundMetrics();
            _finalShadowCorrectionApplied = false;
        }
        else if (!_finalShadowCorrectionApplied && _visuals.Count > 0)
        {
            ApplyUnifiedVoidlingGroundMetrics();
            _finalShadowCorrectionApplied = true;
        }

        if (_resultsShown && !_resultPresentationPolished)
        {
            _resultPresentationPolished = true;
            PolishResultPresentation();
        }
    }

    private void ApplyUnifiedVoidlingGroundMetrics()
    {
        var desiredSpriteBaseOffset = VoidlingGroundVisualMetrics.SpriteCenterYOffset(RaceSpriteScale);
        var correctedShadowPolygon = VoidlingGroundVisualMetrics.BuildShadowPolygon(RaceSpriteScale, 20);

        foreach (var visual in _visuals.Values)
        {
            // The main RaceScreen pass currently expresses its vertical animation relative to
            // baseY - 8. Preserve that animation offset while swapping in the shared grounded
            // sprite pivot used by the garden.
            var animatedYOffset = visual.Sprite.Position.Y - (visual.BaseY - 8.0f);
            visual.Sprite.Position = new Vector2(
                visual.Sprite.Position.X,
                visual.BaseY + desiredSpriteBaseOffset + animatedYOffset);

            visual.Shadow.Polygon = correctedShadowPolygon;
            visual.Shadow.Position = new Vector2(
                visual.Shadow.Position.X,
                visual.BaseY + VoidlingGroundVisualMetrics.ShadowCenterYOffset);
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

        // A small off-balance wobble plus the falling sweat drop gives fourth place the same
        // sheepish "anime embarrassment" cue as the supplied reference without copying art.
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
    private readonly RaceScreen _owner;

    public RaceScreenPolishDriver(RaceScreen owner)
    {
        _owner = owner;
    }

    public override void _Process(double delta)
        => _owner.ApplyPostRaceScreenPresentationFrame();
}
