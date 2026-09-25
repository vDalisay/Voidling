using Godot;
using Voidling.Presentation.UI.Common;
using Voidling.Presentation.UI.Motion;
using VoidlingGame;

namespace Voidling.Presentation.UI.Shop;

/// <summary>
/// Brief "you bought this" flourish: the egg pops into the middle of the screen at a whole 3x
/// scale, hops twice in pixel steps over a burst, and clears itself. Purely presentational
/// feedback for a transaction the Application layer already applied; it never takes input.
/// </summary>
public partial class PurchaseCelebration : Control
{
    private const double HoldSeconds = 1.1;

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
            Position = (new Vector2(screenSize.X * 0.5f - 70.0f, screenSize.Y * 0.5f - 46.0f)).Round(),
            Size = new Vector2(140, 92)
        };
        center.AddThemeConstantOverride("separation", 4);
        AddChild(center);

        // The 16px pack egg at exactly 3x, so it stays pixel-crisp while it celebrates.
        var holder = new Control { CustomMinimumSize = new Vector2(48, 48), MouseFilter = MouseFilterEnum.Ignore };
        var icon = new TextureRect
        {
            Texture = EggTexture,
            Modulate = tint,
            Size = new Vector2(48, 48),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore
        };
        holder.AddChild(icon);
        center.AddChild(holder);

        var label = UiFactory.CreateLabel(caption, 9);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.CustomMinimumSize = new Vector2(140, 16);
        var slip = UiSkin.Paper();
        slip.ContentMarginTop = 2;
        slip.ContentMarginBottom = 4;
        label.AddThemeStyleboxOverride("normal", slip);
        label.MouseFilter = MouseFilterEnum.Ignore;
        center.AddChild(label);

        UiMotion.Appear(center, 0.0, UiMotion.Normal, 0.3f);
        var hop = UiMotion.Loop(icon, "hop");
        if (hop != null)
        {
            foreach (var step in new[] { 0, -3, -5, -6, -5, -3, 0, 0 })
            {
                var y = step;
                hop.TweenCallback(Callable.From(() => icon.Position = new Vector2(0, y)));
                hop.TweenInterval(0.05);
            }
        }
        Callable.From(() =>
        {
            if (GodotObject.IsInstanceValid(holder))
                PixelBurst.Spawn(this, holder.GetGlobalRect().GetCenter(), PixelBurst.Palette.Leafy, 24);
        }).CallDeferred();

        var exit = CreateTween();
        exit.TweenInterval(HoldSeconds);
        exit.TweenProperty(this, "modulate:a", 0.0f, 0.25);
        exit.Finished += QueueFree;
    }
}
