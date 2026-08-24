using Godot;

namespace Voidling.Presentation.Voidlings;

public readonly record struct AngelHaloVisual(
    Vector2 Center,
    float RadiusX,
    float RadiusY,
    float BackWidth,
    float FrontWidth,
    float ShineWidth);

/// <summary>
/// Canonical geometry for Voidling mutation adornments. World actors, challenge sprites and UI
/// portraits all derive their halo proportions from this one definition so visual style changes
/// do not need to be repeated in every screen.
/// </summary>
public static class VoidlingMutationVisualMetrics
{
    private const float ReferenceScale = 0.62f;
    private const float ReferenceHaloCenterY = -29.0f;
    private const float ReferenceRadiusX = 8.8f;
    private const float ReferenceRadiusY = 2.8f;
    private const float ReferenceBackWidth = 1.5f;
    private const float ReferenceFrontWidth = 2.0f;
    private const float ReferenceShineWidth = 0.9f;

    public static AngelHaloVisual ForGroundedSprite(float spriteScale)
    {
        var ratio = Mathf.Max(0.05f, spriteScale / ReferenceScale);
        return new AngelHaloVisual(
            new Vector2(0, ReferenceHaloCenterY * ratio),
            ReferenceRadiusX * ratio,
            ReferenceRadiusY * ratio,
            Mathf.Max(0.8f, ReferenceBackWidth * ratio),
            Mathf.Max(1.0f, ReferenceFrontWidth * ratio),
            Mathf.Max(0.7f, ReferenceShineWidth * ratio));
    }

    public static AngelHaloVisual ForSpriteTarget(float spriteScale)
    {
        var grounded = ForGroundedSprite(spriteScale);
        var spriteCenterY = VoidlingGroundVisualMetrics.SpriteCenterYOffset(spriteScale);
        return grounded with { Center = grounded.Center - new Vector2(0, spriteCenterY) };
    }

    public static AngelHaloVisual ForPortrait(float nominalSpritePixels, Vector2 controlSize)
    {
        // 48px is the source frame size. Tie halo dimensions to the intended sprite display size,
        // not to a card/button that may stretch the TextureRect horizontally.
        var spriteScale = Mathf.Max(0.20f, nominalSpritePixels / 48.0f);
        var target = ForSpriteTarget(spriteScale);
        return target with
        {
            Center = new Vector2(controlSize.X * 0.5f, controlSize.Y * 0.5f + target.Center.Y)
        };
    }

    public static Vector2[] BuildEllipse(AngelHaloVisual halo, int points = 32)
    {
        var ellipse = new Vector2[points];
        for (var i = 0; i < points; i++)
        {
            var angle = Mathf.Tau * i / points;
            ellipse[i] = halo.Center + new Vector2(
                Mathf.Cos(angle) * halo.RadiusX,
                Mathf.Sin(angle) * halo.RadiusY);
        }

        return ellipse;
    }
}
