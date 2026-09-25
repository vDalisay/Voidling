using Godot;

namespace Voidling.Presentation.UI.Common;

/// <summary>
/// The game's mouse pointer: the Sprout Lands cat paw. An open paw everywhere, a pointing paw over
/// anything clickable and a closed paw while dragging. The 16px sprites are scaled by the same whole
/// factor the 640x360 game is stretched by, with nearest-neighbour sampling, so the paw's pixels are
/// the size of the game's own and stay crisp; the scale follows the window when it is resized.
/// </summary>
public partial class PixelCursor : Node
{
    private const string Folder = "res://Assets/Sprout Lands - UI Pack - Premium pack/UI Sprites/Mouse sprites/";
    private const int BaseHeight = 360;

    // Each paw and the pixel that clicks: the tip of its upper-left toe.
    private static readonly (string File, Input.CursorShape Shape, Vector2I Tip)[] Paws =
    {
        ("Catpaw Mouse icon.png", Input.CursorShape.Arrow, new Vector2I(5, 1)),
        ("Catpaw pointing Mouse icon.png", Input.CursorShape.PointingHand, new Vector2I(4, 1)),
        ("Catpaw holding Mouse icon.png", Input.CursorShape.Drag, new Vector2I(6, 2)),
        ("Catpaw holding Mouse icon.png", Input.CursorShape.Move, new Vector2I(6, 2))
    };

    private int _scale;

    public override void _Ready()
    {
        // A headless run (smokes, CI) has no pointer to dress.
        if (DisplayServer.GetName() == "headless")
            return;

        Apply();
        GetTree().Root.SizeChanged += Apply;
    }

    public override void _ExitTree()
    {
        if (_scale == 0)
            return;

        if (GetTree()?.Root is { } root)
            root.SizeChanged -= Apply;
        // Hand the pointer back before the renderer shuts down, or its textures outlive it.
        foreach (var (_, shape, _) in Paws)
            Input.SetCustomMouseCursor(null, shape);
        _scale = 0;
    }

    private void Apply()
    {
        var scale = Mathf.Max(1, DisplayServer.WindowGetSize().Y / BaseHeight);
        if (scale == _scale)
            return;

        _scale = scale;
        foreach (var (file, shape, tip) in Paws)
        {
            var image = GD.Load<Texture2D>(Folder + file)?.GetImage();
            if (image == null || image.IsEmpty())
                continue;

            image = (Image)image.Duplicate();
            if (image.IsCompressed())
                image.Decompress();
            image.Resize(image.GetWidth() * scale, image.GetHeight() * scale, Image.Interpolation.Nearest);
            Input.SetCustomMouseCursor(ImageTexture.CreateFromImage(image), shape, tip * scale);
        }
    }
}
