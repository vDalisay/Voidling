using System.Linq;
using Godot;
using Voidling.Presentation.Voidlings;
using VoidlingGame;

namespace Voidling.Presentation.Racing;

public partial class RaceScreen
{
    private bool _customResultPortraitsApplied;

    private void ApplyCustomVoidlingRaceVisuals()
    {
        foreach (var visual in _visuals.Values)
        {
            var displayName = visual.Entrant.Participant.DisplayName;
            if (!VoidlingVisualCatalog.UsesCustomVisual(displayName))
                continue;

            visual.Sprite.SpriteFrames = VoidlingVisualCatalog.BuildRaceFrames(displayName);
            visual.Sprite.Scale = Vector2.One * VoidlingVisualCatalog.RaceCustomScale;
            visual.Sprite.Modulate = Colors.White;
            visual.Sprite.SpeedScale = 1.0f;
            visual.Sprite.FlipH = false;
            visual.Sprite.Play(visual.VisualMode == "swim" ? "swim" : "run");

            // Keep the race footprint aligned with the same smaller custom presentation used
            // in the garden. The polish pass refines this further from the live sprite scale.
            visual.Shadow.Polygon = BuildEllipsePoints(5.2f, 1.8f, 18);
        }
    }

    internal void ApplyCustomVoidlingResultPortraits()
    {
        if (_customResultPortraitsApplied || !_resultsShown || _simulation == null || _entry == null)
            return;

        var canvas = GetChildren().OfType<CanvasLayer>().FirstOrDefault(layer => layer.Layer == 50);
        if (canvas == null)
            return;

        var stage = FindDescendant<Control>(canvas, control =>
            Mathf.Abs(control.CustomMinimumSize.X - 480.0f) < 1.0f &&
            Mathf.Abs(control.CustomMinimumSize.Y - 192.0f) < 1.0f);
        if (stage == null)
            return;

        var portraits = stage.GetChildren().OfType<TextureRect>()
            .OrderBy(portrait => portrait.Position.X)
            .ToList();
        if (portraits.Count < 4 || _simulation.FinishOrder.Count < 4)
            return;

        var byId = _entry.Entrants.ToDictionary(entrant => entrant.Participant.CreatureId);
        var finishers = _simulation.FinishOrder.Select(id => byId[id]).ToList();

        // The result stage is laid out left-to-right as second, first, third, fourth.
        var displayedEntrants = new[] { finishers[1], finishers[0], finishers[2], finishers[3] };
        for (var i = 0; i < 4; i++)
        {
            var entrant = displayedEntrants[i];
            UiFactory.SetPortraitData(
                portraits[i],
                entrant.Participant.DisplayName,
                ParseTint(entrant.Participant.TintHex),
                entrant.HasAngelMutation,
                entrant.OtherMutationCount);
        }

        _customResultPortraitsApplied = true;
    }
}
