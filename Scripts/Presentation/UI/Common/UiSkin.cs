using System.Collections.Generic;
using Godot;
using VoidlingGame;

namespace Voidling.Presentation.UI.Common;

/// <summary>How a button reads before anything is written on it.</summary>
public enum ButtonTone
{
    /// <summary>The everyday light-wood button.</summary>
    Tan,
    /// <summary>The one action a screen is for (buy, breed, claim, place, start): leaf green.</summary>
    Primary,
    /// <summary>Irreversible or costly actions (goodbye, discard, reset): clay red.</summary>
    Danger
}

/// <summary>
/// The Sprout Lands premium UI pack, cut into the pieces the Garden's menus are built from: 9-sliced
/// buttons with real up/pressed frames, the pack's window, paper, title tag and speech bubble, its
/// selector brackets and icon buttons, the emoji sheet and the pixel number font. One source of
/// truth for regions and margins so every screen draws the same art the same way.
///
/// The pieces are recoloured one step lighter along the pack's own beige ramp as they are cut (see
/// <see cref="UiPalette"/>); the side board keeps the pack's original shade.
/// </summary>
public static class UiSkin
{
    public enum ButtonState { Normal, Hover, Pressed, Disabled }

    private const string Sprites = UiFactory.UiRoot;
    private const string Emojis = "res://Assets/Sprout Lands - UI Pack - Premium pack/emojis/";

    public static readonly Texture2D SquareButtons = GD.Load<Texture2D>(Sprites + "buttons/square/Square Buttons 26x19.png");
    public static readonly Texture2D IconButtons = GD.Load<Texture2D>(Sprites + "buttons/Icon Buttons/Icon Buttons Spritesheet.png");
    public static readonly Texture2D RoundButtons = GD.Load<Texture2D>(Sprites + "buttons/round/small colored round buttons.png");
    public static readonly Texture2D Selectors = GD.Load<Texture2D>(Sprites + "Other UI sprites/Selectors/Selectorst.png");
    public static readonly Texture2D SettingsSheet = GD.Load<Texture2D>(Sprites + "Other UI sprites/UI Settings Buttons.png");
    public static readonly Texture2D EmojiSheet = GD.Load<Texture2D>(Emojis + "Emoji spritesheet.png");
    public static readonly Texture2D Stars = GD.Load<Texture2D>(Sprites + "Icons/special icons/stars.png");
    public static readonly Texture2D ContinueArrow = GD.Load<Texture2D>(
        Sprites + "Dialouge UI/dialog box character finished talking click to continue indicator - spritesheet .png");

    private static readonly Texture2D WindowSheet = GD.Load<Texture2D>(Sprites + "Other UI sprites/Setting menu.png");
    private static readonly Texture2D TagSheet = GD.Load<Texture2D>(Sprites + "Dialouge UI/dialog box.png");
    private static readonly Texture2D BlockSheet = GD.Load<Texture2D>(Emojis + "emoji style ui/Inventory_Blocks_Spritesheet.png");

    /// <summary>Text: a warm dark brown in the palette's own hue, well above 7:1 on every surface.</summary>
    public static readonly Color Ink = Color.FromHtml("#4A3A2A");
    public static readonly Color InkSoft = Color.FromHtml("#7A6247");
    public static readonly Color InkOnGreen = Color.FromHtml("#FFF9E8");
    public static readonly Color InkOnRed = Color.FromHtml("#FFF3EC");
    public static readonly Color Leaf = Color.FromHtml("#86C95E");
    public static readonly Color Clay = Color.FromHtml("#DE8A74");
    public static readonly Color Gain = Color.FromHtml("#3F8A34");
    public static readonly Color Loss = Color.FromHtml("#B4523F");
    public static readonly Color Cream = Color.FromHtml("#FFF6DC");

    private static FontFile? _numberFont;

    private static readonly Dictionary<(ulong Sheet, Rect2 Region, IReadOnlyDictionary<int, int> Swaps), Texture2D> Recolours = new();

    /// <summary>The factory's pressed chrome, shared, so a button still wearing it can be recognised.</summary>
    public static readonly StyleBoxTexture DefaultPressed = Button(ButtonTone.Tan, ButtonState.Pressed);

    /// <summary>A picked tab or slot: the pressed frame in honey (see <see cref="UiPalette.Honey"/>).</summary>
    public static readonly StyleBoxTexture Selected = ButtonFrame(DownFrame(1), Colors.White, pressed: true, swaps: UiPalette.HoneyFace);
    public static readonly StyleBoxTexture SelectedHover = ButtonFrame(DownFrame(1), new Color(1.03f, 1.025f, 1.01f), pressed: true, swaps: UiPalette.HoneyFace);

    // The 26x19 sheet: four colour rows (white, light, mid and dark tan); the up frame on the left,
    // the pressed frame (one pixel shorter, lip spent) on the right.
    private static Rect2 UpFrame(int row) => new(11, 6 + row * 32, 26, 19);
    private static Rect2 DownFrame(int row) => new(59, 7 + row * 32, 26, 18);

    /// <summary>
    /// A 9-sliced pack button. Hover draws the frame one pixel higher and pressed one pixel lower
    /// (the pressed frame's lip is spent), so rest states move in whole pixels without any tween.
    /// </summary>
    public static StyleBoxTexture Button(ButtonTone tone, ButtonState state)
    {
        var row = tone == ButtonTone.Tan ? 1 : 0;
        // Tan buttons wear the lighter face; green and red are tinted from the pack's white row.
        var swaps = tone == ButtonTone.Tan ? UiPalette.LightFace : null;
        var tint = tone switch
        {
            ButtonTone.Primary => Leaf,
            ButtonTone.Danger => Clay,
            _ => Colors.White
        };
        return state switch
        {
            ButtonState.Hover => ButtonFrame(UpFrame(row), tone == ButtonTone.Tan ? new Color(1.04f, 1.035f, 1.02f) : tint.Lightened(0.14f), hover: true, swaps: swaps),
            ButtonState.Pressed => ButtonFrame(DownFrame(row), tint, pressed: true, swaps: swaps),
            ButtonState.Disabled => ButtonFrame(UpFrame(0), new Color(0.86f, 0.82f, 0.74f, 0.8f)),
            _ => ButtonFrame(UpFrame(row), tint, swaps: swaps)
        };
    }

    /// <summary>A glyph on the pack's square icon buttons: its up frame and its pressed frame.</summary>
    public readonly record struct IconGlyph(Rect2 Up, Rect2 Down)
    {
        public static readonly IconGlyph Close = new(new Rect2(5, 324, 22, 24), new Rect2(37, 326, 22, 22));
        public static readonly IconGlyph SmallClose = new(new Rect2(135, 5, 18, 20), new Rect2(167, 7, 18, 18));
        public static readonly IconGlyph Back = new(new Rect2(6, 68, 20, 22), new Rect2(37, 70, 20, 20));
        public static readonly IconGlyph Forward = new(new Rect2(134, 36, 20, 22), new Rect2(165, 38, 20, 20));
        public static readonly IconGlyph Settings = new(new Rect2(5, 132, 22, 24), new Rect2(37, 134, 22, 22));
        public static readonly IconGlyph SoundOn = new(new Rect2(5, 452, 22, 24), new Rect2(37, 454, 22, 22));
        public static readonly IconGlyph SoundOff = new(new Rect2(69, 420, 22, 24), new Rect2(101, 422, 22, 22));
        public static readonly IconGlyph Plus = new(new Rect2(133, 100, 22, 24), new Rect2(165, 102, 22, 22));
        public static readonly IconGlyph Minus = new(new Rect2(133, 132, 22, 24), new Rect2(165, 134, 22, 22));
    }

    /// <summary>
    /// Dresses a text-less button as one of the pack's square icon buttons at its native size: up,
    /// a one-pixel hover lift, the pressed frame sitting on the same baseline, and a faded disabled.
    /// </summary>
    public static void ApplyIconButton(Button button, IconGlyph glyph)
    {
        StyleBoxTexture Frame(Rect2 region, Color tint, float expandTop = 0, float expandBottom = 0) => new()
        {
            Texture = Recoloured(IconButtons, region, UiPalette.LightFace),
            ModulateColor = tint,
            ExpandMarginTop = expandTop,
            ExpandMarginBottom = expandBottom
        };

        var lift = glyph.Up.Size.Y - glyph.Down.Size.Y;
        button.Text = string.Empty;
        button.Icon = null;
        button.CustomMinimumSize = glyph.Up.Size;
        button.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        button.AddThemeStyleboxOverride("normal", Frame(glyph.Up, Colors.White));
        button.AddThemeStyleboxOverride("hover", Frame(glyph.Up, new Color(1.04f, 1.035f, 1.02f), 1, -1));
        button.AddThemeStyleboxOverride("pressed", Frame(glyph.Down, Colors.White, -lift));
        button.AddThemeStyleboxOverride("hover_pressed", Frame(glyph.Down, Colors.White, -lift));
        button.AddThemeStyleboxOverride("disabled", Frame(glyph.Up, new Color(0.85f, 0.82f, 0.76f, 0.6f)));
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
    }

    private static readonly Rect2 WindowRegion = new(139, 12, 106, 122);

    /// <summary>The pack's window panel, lightened: modal windows and the HUD panels floating over the Garden.</summary>
    public static StyleBoxTexture Window(Color? tint = null)
        => Slice(Recoloured(WindowSheet, WindowRegion, UiPalette.LightWindow), tint ?? Colors.White, (4, 4, 4, 8), (11, 11, 10, 11));

    /// <summary>
    /// The same window in the pack's own beige, a step darker than the windows: the side board that
    /// frames the Garden screen, as the menu rail always looked.
    /// </summary>
    public static StyleBoxTexture Board()
        => Slice(Cut(WindowSheet, WindowRegion), Colors.White, (4, 4, 4, 8), (11, 11, 10, 11));

    /// <summary>Light paper blocks for cards and slots inside a window.</summary>
    public static StyleBoxTexture Paper(Color? tint = null)
        => Slice(Recoloured(BlockSheet, new Rect2(9, 9, 30, 32), UiPalette.LightInset), tint ?? Colors.White, (8, 8, 8, 9), (8, 8, 7, 9));

    /// <summary>A sunken well for an item's art on a detail card.</summary>
    public static StyleBoxTexture Well()
        => Slice(Recoloured(BlockSheet, new Rect2(57, 9, 30, 32), UiPalette.LightInset), Colors.White, (8, 8, 8, 9), (6, 6, 5, 7));

    /// <summary>
    /// The tag-shaped title plate a window's name sits on: the pack's small dialog box, notch and
    /// all. At its native 28px height the notch is drawn 1:1; only the width stretches.
    /// </summary>
    public static StyleBoxTexture TitleTag()
        => Slice(Recoloured(TagSheet, new Rect2(7, 11, 30, 28), UiPalette.LightFace), Colors.White, (8, 4, 12, 12), (11, 8, 3, 6));

    /// <summary>The native height of <see cref="TitleTag"/>.</summary>
    public const int TitleTagHeight = 28;

    /// <summary>One cell of the pack's 32x32 emoji sheet.</summary>
    public static AtlasTexture Emoji(int column, int row)
        => new() { Atlas = EmojiSheet, Region = new Rect2(column * 32, row * 32, 32, 32) };

    /// <summary>
    /// The pack's 7x7 pixel font, for numbers only (it has no lowercase or accents, so words stay in
    /// the UI font). Crisp at whole multiples of <see cref="NumberFontSize"/>; anti-aliasing is off.
    /// </summary>
    public static FontFile NumberFont
    {
        get
        {
            if (_numberFont != null) return _numberFont;
            _numberFont = GD.Load<FontFile>(
                "res://Assets/Sprout Lands - UI Pack - Premium pack/fonts/Font files TTF/pixelFont-4-7x7-sproutLands.ttf");
            _numberFont.Antialiasing = TextServer.FontAntialiasing.None;
            _numberFont.Hinting = TextServer.Hinting.None;
            _numberFont.SubpixelPositioning = TextServer.SubpixelPositioning.Disabled;
            _numberFont.GenerateMipmaps = false;
            return _numberFont;
        }
    }

    /// <summary>The size at which one font pixel is one UI pixel.</summary>
    public const int NumberFontSize = 9;

    /// <summary>Numbers in the pack's pixel font, optionally outlined so they read on any art.</summary>
    public static void ApplyNumberFont(Control control, int scale = 1, Color? color = null, Color? outline = null)
    {
        control.AddThemeFontOverride("font", NumberFont);
        control.AddThemeFontSizeOverride("font_size", NumberFontSize * scale);
        control.AddThemeColorOverride("font_color", color ?? Ink);
        if (!outline.HasValue) return;
        control.AddThemeColorOverride("font_outline_color", outline.Value);
        control.AddThemeConstantOverride("outline_size", 2 * scale);
    }

    /// <summary>
    /// The sunken bed of a progress bar or slider: square pixels in the ramp's beige, with a tan
    /// lower lip so it reads as pressed into the paper around it.
    /// </summary>
    public static StyleBoxFlat BarTrack() => new()
    {
        BgColor = UiPalette.Beige, AntiAliasing = false,
        BorderColor = UiPalette.Tan, BorderWidthBottom = 1
    };

    private static StyleBoxTexture ButtonFrame(Rect2 region, Color tint, bool hover = false, bool pressed = false,
        IReadOnlyDictionary<int, int>? swaps = null)
    {
        // Content sits above the lip; hover lifts text and art one pixel, pressed pushes them down one.
        var piece = swaps == null ? Cut(SquareButtons, region) : Recoloured(SquareButtons, region, swaps);
        var style = Slice(piece, tint, (4, 4, 4, pressed ? 4 : 5),
            (7, 7, hover ? 2 : pressed ? 4 : 3, hover ? 6 : pressed ? 4 : 5));
        if (hover)
        {
            style.ExpandMarginTop = 1;
            style.ExpandMarginBottom = -1;
        }
        else if (pressed)
        {
            style.ExpandMarginTop = -1;
        }
        return style;
    }

    private static AtlasTexture Cut(Texture2D sheet, Rect2 region) => new() { Atlas = sheet, Region = region };

    /// <summary>
    /// One region of a pack sheet with its colours swapped (see <see cref="PaletteSwap"/>), cut and
    /// recoloured once and shared. A sheet whose pixels cannot be read comes back uncoloured.
    /// </summary>
    private static Texture2D Recoloured(Texture2D sheet, Rect2 region, IReadOnlyDictionary<int, int> swaps)
    {
        var key = (sheet.GetInstanceId(), region, swaps);
        if (Recolours.TryGetValue(key, out var texture))
            return texture;

        var image = sheet.GetImage();
        if (image == null || image.IsEmpty())
        {
            texture = Cut(sheet, region);
        }
        else
        {
            image = (Image)image.Duplicate();
            if (image.IsCompressed())
                image.Decompress();
            if (image.GetFormat() != Image.Format.Rgba8)
                image.Convert(Image.Format.Rgba8);
            var piece = image.GetRegion(new Rect2I((Vector2I)region.Position, (Vector2I)region.Size));
            var pixels = PaletteSwap.Apply(piece.GetData(), swaps);
            texture = ImageTexture.CreateFromImage(
                Image.CreateFromData(piece.GetWidth(), piece.GetHeight(), false, Image.Format.Rgba8, pixels));
        }

        Recolours[key] = texture;
        return texture;
    }

    private static StyleBoxTexture Slice(Texture2D piece, Color tint,
        (int Left, int Right, int Top, int Bottom) slice, (int Left, int Right, int Top, int Bottom) content)
        => new()
        {
            Texture = piece,
            ModulateColor = tint,
            TextureMarginLeft = slice.Left,
            TextureMarginRight = slice.Right,
            TextureMarginTop = slice.Top,
            TextureMarginBottom = slice.Bottom,
            ContentMarginLeft = content.Left,
            ContentMarginRight = content.Right,
            ContentMarginTop = content.Top,
            ContentMarginBottom = content.Bottom
        };
}
