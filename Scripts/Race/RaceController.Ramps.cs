using Godot;

namespace VoidlingGame;

public partial class RaceController
{
    public override void _Ready()
    {
        // Setup() creates the legacy center-lane ramp markers. Add the full-height
        // versions deferred so they are drawn afterward and visually replace them.
        CallDeferred(nameof(AddFullHeightFlightRamps));
    }

    private void AddFullHeightFlightRamps()
    {
        AddFullHeightFlightRamp(FlyStartX, true);
        AddFullHeightFlightRamp(FlyEndX, false);
    }

    private void AddFullHeightFlightRamp(float x, bool launch)
    {
        var halfHeight = (TrackBottom - TrackTop) * 0.5f;
        const float halfWidth = 27.0f;
        const float rise = 15.0f;

        var polygon = launch
            ? new[]
            {
                new Vector2(-halfWidth, -halfHeight),
                new Vector2(halfWidth, -halfHeight - rise),
                new Vector2(halfWidth, halfHeight - rise),
                new Vector2(-halfWidth, halfHeight)
            }
            : new[]
            {
                new Vector2(-halfWidth, -halfHeight - rise),
                new Vector2(halfWidth, -halfHeight),
                new Vector2(halfWidth, halfHeight),
                new Vector2(-halfWidth, halfHeight - rise)
            };

        var ramp = new Polygon2D
        {
            Polygon = polygon,
            Color = Color.FromHtml("#D99B63"),
            Position = new Vector2(x, TrackY),
            ZIndex = 8
        };
        AddChild(ramp);

        var upperEdge = new Line2D
        {
            Width = 2.0f,
            DefaultColor = Color.FromHtml("#8D654F"),
            Points = launch
                ? new[] { polygon[0], polygon[1] }
                : new[] { polygon[0], polygon[1] },
            ZIndex = 1
        };
        ramp.AddChild(upperEdge);

        var lowerEdge = new Line2D
        {
            Width = 2.0f,
            DefaultColor = Color.FromHtml("#A87555"),
            Points = launch
                ? new[] { polygon[3], polygon[2] }
                : new[] { polygon[3], polygon[2] },
            ZIndex = 1
        };
        ramp.AddChild(lowerEdge);

        // Light lane seams make it obvious that the ramp continues through every
        // vertical lane rather than being a single centered wedge.
        for (var lane = 1; lane < 4; lane++)
        {
            var laneY = -halfHeight + lane * (TrackBottom - TrackTop) / 4.0f;
            var seam = new Line2D
            {
                Width = 1.0f,
                DefaultColor = new Color(1.0f, 0.84f, 0.64f, 0.45f),
                Points = launch
                    ? new[] { new Vector2(-halfWidth, laneY), new Vector2(halfWidth, laneY - rise) }
                    : new[] { new Vector2(-halfWidth, laneY - rise), new Vector2(halfWidth, laneY) },
                ZIndex = 1
            };
            ramp.AddChild(seam);
        }
    }
}
