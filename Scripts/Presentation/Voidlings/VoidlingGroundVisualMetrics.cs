using Godot;

namespace Voidling.Presentation.Voidlings;

/// <summary>
/// Shared presentation metrics for placing a Voidling sprite on its ground pivot and drawing the
/// small ellipse directly beneath its feet. These values match the corrected garden presentation;
/// challenge screens should use this instead of inventing a second footprint/shadow proportion.
/// </summary>
public static class VoidlingGroundVisualMetrics
{
    private const float GardenAdultScale = 0.62f;
    private const float GardenShadowRadiusX = 5.2f;
    private const float GardenShadowRadiusY = 1.8f;

    public const float ShadowCenterYOffset = 0.8f;

    public static float SpriteCenterYOffset(float spriteScale)
        => -8.0f * spriteScale;

    public static Vector2 ShadowRadii(float spriteScale)
    {
        var ratio = spriteScale / GardenAdultScale;
        return new Vector2(GardenShadowRadiusX * ratio, GardenShadowRadiusY * ratio);
    }

    public static Vector2[] BuildShadowPolygon(float spriteScale, int points = 20)
    {
        var radii = ShadowRadii(spriteScale);
        var polygon = new Vector2[points];
        for (var i = 0; i < points; i++)
        {
            var angle = Mathf.Tau * i / points;
            polygon[i] = new Vector2(Mathf.Cos(angle) * radii.X, Mathf.Sin(angle) * radii.Y);
        }

        return polygon;
    }
}
