using Godot;

namespace Voidling.Presentation.Voidlings;

/// <summary>
/// Shared presentation metrics for placing a Voidling sprite on its ground pivot and drawing the
/// compact ellipse beneath its feet. Art-dependent values are resolved from the canonical visual
/// definition so Garden, remote Garden and race presentation cannot drift when artwork changes.
/// </summary>
public static class VoidlingGroundVisualMetrics
{
    public static float ShadowCenterYOffset => VoidlingVisualFactory.ShadowCenterYOffset;

    public static float SpriteCenterYOffset(float spriteScale)
        => VoidlingVisualFactory.WorldSpriteCenterYOffset(spriteScale);

    public static Vector2 ShadowRadii(float spriteScale)
        => VoidlingVisualFactory.ShadowRadii(spriteScale);

    public static Vector2[] BuildShadowPolygon(float spriteScale, int points = 20)
        => VoidlingVisualFactory.BuildShadowPolygon(spriteScale, points);
}
