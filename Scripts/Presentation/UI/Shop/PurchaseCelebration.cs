using Godot;
using VoidlingGame;

namespace Voidling.Presentation.UI.Shop;

/// <summary>
/// Brief "you bought this" flourish: the item pulses in the middle of the screen and then clears
/// itself. Purely presentational feedback for a transaction the Application layer already applied.
/// </summary>
public partial class PurchaseCelebration : Control
{
    private const double HoldSeconds = 1.15;

    private static readonly Texture2D EggTexture = GD.Load<Texture2D>(
        "res://Assets/Sprout Lands - Sprites - Basic pack/Objects/Egg item.png");

    public static void ShowEgg(Control parent, Vector2 screenSize, Color tint, string caption)
    {
        var celebration = new PurchaseCelebration
        {
            MouseFilter = MouseFilterEnum.Ignore,
            ZIndex = 200,
            Position = Vector2.Zero,
            Size = screenSize
        };
        parent.AddChild(celebration);
        celebration.Build(screenSize, tint, caption);
    }

    private void Build(Vector2 screenSize, Color tint, string caption)
    {
        var center = new VBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
            Alignment = BoxContainer.AlignmentMode.Center,
            Position = new Vector2(screenSize.X * 0.5f - 70.0f, screenSize.Y * 0.5f - 46.0f),
            Size = new Vector2(140, 92)
        };
        center.AddThemeConstantOverride("separation", 6);
        AddChild(center);

        var icon = new TextureRect
        {
            Texture = EggTexture,
            Modulate = tint,
            CustomMinimumSize = new Vector2(56, 56),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
            PivotOffset = new Vector2(28, 28)
        };
        center.AddChild(icon);

        var label = UiFactory.CreateLabel(caption, 9);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.CustomMinimumSize = new Vector2(140, 16);
        label.AddThemeColorOverride("font_color", Color.FromHtml("#F9F4D8"));
        label.AddThemeColorOverride("font_outline_color", Color.FromHtml("#465247"));
        label.AddThemeConstantOverride("outline_size", 2);
        label.MouseFilter = MouseFilterEnum.Ignore;
        center.AddChild(label);

        var pulse = CreateTween().SetLoops();
        pulse.TweenProperty(icon, "scale", new Vector2(1.18f, 1.18f), 0.28)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        pulse.TweenProperty(icon, "scale", Vector2.One, 0.28)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);

        var exit = CreateTween();
        exit.TweenInterval(HoldSeconds);
        exit.TweenProperty(this, "modulate:a", 0.0f, 0.3);
        exit.Finished += QueueFree;
    }
}
