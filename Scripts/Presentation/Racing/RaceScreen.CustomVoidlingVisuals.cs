using Godot;
using Voidling.Presentation.Voidlings;

namespace Voidling.Presentation.Racing;

public partial class RaceScreen
{
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
}
