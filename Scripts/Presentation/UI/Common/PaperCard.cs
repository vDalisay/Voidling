using Godot;
using VoidlingGame;

namespace Voidling.Presentation.UI.Common;

/// <summary>
/// The warm paper chrome every overhauled screen is built from: a card one shade lighter than the
/// window, a column header, a drawn empty slot that keeps a grid's shape, and the ink correction
/// that keeps stat identity colours readable on paper.
/// </summary>
public static class PaperCard
{
    private static readonly Texture2D WoodStars = GD.Load<Texture2D>(
        UiFactory.UiRoot + "Icons/special icons/stars in wood.png");

    /// <summary>The window's warm paper, one shade lighter, so a card reads as part of the same sheet.</summary>
    public static PanelContainer Panel(Vector2 minimumSize)
    {
        var panel = UiFactory.CreatePanel(minimumSize);
        var style = (StyleBoxTexture)panel.GetThemeStylebox("panel").Duplicate();
        style.ModulateColor = new Color(247f / 220f, 233f / 224f, 197f / 210f);
        panel.AddThemeStyleboxOverride("panel", style);
        return panel;
    }

    /// <summary>A quiet column header, the screens' replacement for an explanatory sentence.</summary>
    public static Label Header(string text, float width = 0.0f)
    {
        var label = UiFactory.CreateLabel(text, 7);
        if (width > 0) label.CustomMinimumSize = new Vector2(width, 0);
        return label;
    }

    /// <summary>
    /// An empty slot, drawn rather than omitted, so a grid keeps its shape however little the
    /// player owns instead of collapsing around a short last page.
    /// </summary>
    public static PanelContainer EmptySlot(Vector2 size)
    {
        var slot = new PanelContainer { CustomMinimumSize = size, MouseFilter = Control.MouseFilterEnum.Ignore };
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.72f, 0.66f, 0.52f, 0.30f),
            BorderColor = new Color(0.62f, 0.55f, 0.42f, 0.45f)
        };
        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(3);
        slot.AddThemeStyleboxOverride("panel", style);
        return slot;
    }

    /// <summary>The premium wooden star that marks whichever slot is picked.</summary>
    public static TextureRect Star(Vector2 position, float size = 17.0f, bool filled = true) => new()
    {
        Texture = new AtlasTexture { Atlas = WoodStars, Region = new Rect2(filled ? 0 : 32, 0, 32, 32) },
        Position = position,
        Size = new Vector2(size, size),
        CustomMinimumSize = new Vector2(size, size),
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        MouseFilter = Control.MouseFilterEnum.Ignore
    };

    /// <summary>A filled/empty star row, the shared way a level 1-3 is shown.</summary>
    public static HBoxContainer StarRating(int level, int maximum = 3, float size = 15.0f)
    {
        var row = new HBoxContainer
        {
            Name = "StarRating",
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        row.AddThemeConstantOverride("separation", 1);
        for (var star = 1; star <= maximum; star++)
            row.AddChild(Star(Vector2.Zero, size, star <= level));
        return row;
    }

    /// <summary>
    /// Stat identity colours are authored for the dark Garden inspector; on paper the pale ones
    /// (swim yellow, stamina white) vanish, so darken by however much luminance is over.
    /// </summary>
    public static Color Ink(Color color)
        => color.Darkened(Mathf.Clamp(color.Luminance - 0.35f, 0.0f, 0.6f));

    /// <summary>
    /// Free a container's children, hiding them first so a rebuilt band cannot flash. They leave
    /// the tree before the deferred free, because a queued-but-still-parented child keeps its node
    /// name and Godot would rename the replacement built in the same frame.
    /// </summary>
    public static void Clear(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is CanvasItem canvasItem) canvasItem.Visible = false;
            if (child is Control control) control.MouseFilter = Control.MouseFilterEnum.Ignore;
            node.RemoveChild(child);
            child.QueueFree();
        }
    }
}
