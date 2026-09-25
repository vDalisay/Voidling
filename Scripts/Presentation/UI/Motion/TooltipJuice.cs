using Godot;

namespace Voidling.Presentation.UI.Motion;

/// <summary>
/// Pops Godot's own tooltips in. The tooltip is a popup the viewport adds under the hovered
/// control; this watches for it, then slides it up three pixels in whole steps while its paper and
/// text fade in. Styling comes from the UI root's theme (<c>TooltipPanel</c>/<c>TooltipLabel</c>).
/// </summary>
public partial class TooltipJuice : Node
{
    public override void _Ready() => GetTree().NodeAdded += OnNodeAdded;

    public override void _ExitTree()
    {
        if (GetTree() != null) GetTree().NodeAdded -= OnNodeAdded;
    }

    private void OnNodeAdded(Node node)
    {
        if (node is PopupPanel popup && popup.ThemeTypeVariation == "TooltipPanel")
            Callable.From(() => Animate(popup)).CallDeferred();
    }

    private static void Animate(PopupPanel popup)
    {
        if (UiMotion.Reduced || !GodotObject.IsInstanceValid(popup) || !popup.IsInsideTree()) return;
        var rest = popup.Position;
        var parts = popup.GetChildren(includeInternal: true);
        foreach (var part in parts)
            if (part is CanvasItem item) item.Modulate = new Color(1, 1, 1, 0);

        var tween = popup.CreateTween();
        tween.TweenMethod(Callable.From<float>(t =>
        {
            if (!GodotObject.IsInstanceValid(popup)) return;
            popup.Position = rest + new Vector2I(0, Mathf.RoundToInt(3 * (1 - UiMotionMath.EaseOutCubic(t))));
            foreach (var part in parts)
                if (part is CanvasItem item && GodotObject.IsInstanceValid(item))
                    item.Modulate = new Color(1, 1, 1, Mathf.Clamp(t * 1.6f, 0, 1));
        }), 0.0f, 1.0f, UiMotion.Quick);
    }
}
