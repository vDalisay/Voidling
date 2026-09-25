using System.Collections.Generic;
using Godot;

namespace Voidling.Presentation.UI.Common;

/// <summary>
/// The Garden menus' colours, built on the Sprout Lands pack's own warm beige ramp so recoloured
/// pack art and code-drawn chrome always agree.
///
/// Large surfaces sit one step lighter than the pack draws them, with one lighter cream added on
/// top, so windows read as light paper over the Garden rather than wood. Value does the layering:
/// the side board (the pack's original beige) frames the screen, windows are lighter, the cards and
/// buttons on them lighter still, and sunken wells and tracks drop back a step. Outlines keep the
/// pack's dark browns, so shapes stay crisp against the busy Garden. Hue stays in one warm family
/// (analogous, calm); the few saturated colours are the accents, leaf green for the one action a
/// screen is for and clay red for irreversible ones, sitting either side of the beige on the colour
/// wheel so they stand out without clashing. Text is a warm dark brown, well above 7:1 on every
/// surface.
/// </summary>
public static class UiPalette
{
    /// <summary>Highlights on cards and buttons: the one shade the pack does not have.</summary>
    public static readonly Color Highlight = Color.FromHtml("#FFF8E6");
    /// <summary>Cards, buttons and title tags.</summary>
    public static readonly Color Parchment = Color.FromHtml("#F3E5C2");
    /// <summary>Window bodies.</summary>
    public static readonly Color Sand = Color.FromHtml("#E8CFA6");
    /// <summary>The side board, and the beds of sunken wells, tracks and unfilled bars.</summary>
    public static readonly Color Beige = Color.FromHtml("#DCB98A");
    /// <summary>Inner lines, button lips, dividers and scroll pegs.</summary>
    public static readonly Color Tan = Color.FromHtml("#C49A6C");
    /// <summary>Outlines.</summary>
    public static readonly Color Bark = Color.FromHtml("#AA7959");
    /// <summary>The darkest outline and drop edges.</summary>
    public static readonly Color Umber = Color.FromHtml("#90625D");
    /// <summary>
    /// A picked tab, slot or row: the beige warmed and saturated towards gold, its hue neighbour.
    /// Selection stands out by colour and by sitting pressed in, not by turning dark.
    /// </summary>
    public static readonly Color Honey = Color.FromHtml("#F1D59B");

    /// <summary>The modulate that turns a parchment card honey, for selections drawn as a tint.</summary>
    public static Color HoneyOnParchment
        => new(Honey.R / Parchment.R, Honey.G / Parchment.G, Honey.B / Parchment.B);

    private const int PackHighlight = 0xF3E5C2;
    private const int PackLight = 0xE8CFA6;
    private const int PackMid = 0xDCB98A;
    private const int PackLip = 0xC49A6C;
    private const int PackEdge = 0xAA7959;
    private const int PackOutline = 0x90625D;
    private const int NewHighlight = 0xFFF8E6;

    /// <summary>A pack window lifted to the light window body; its outline is untouched.</summary>
    internal static readonly IReadOnlyDictionary<int, int> LightWindow = new Dictionary<int, int>
    {
        [PackMid] = PackLight,
        [PackLight] = PackHighlight
    };

    /// <summary>
    /// Pack buttons and title tags one step lighter. Lips and glyphs keep the pack's tan so icons
    /// gain contrast on the lighter face.
    /// </summary>
    internal static readonly IReadOnlyDictionary<int, int> LightFace = new Dictionary<int, int>
    {
        [PackLight] = PackHighlight,
        [PackHighlight] = NewHighlight
    };

    /// <summary>A pressed pack button in <see cref="Honey"/>, with a warmer highlight.</summary>
    internal static readonly IReadOnlyDictionary<int, int> HoneyFace = new Dictionary<int, int>
    {
        [PackLight] = 0xF1D59B,
        [PackHighlight] = 0xFBEBC3
    };

    /// <summary>
    /// Cards and wells inside a window: one step lighter, with outlines softened one step too, so
    /// the window keeps the strongest frame and its contents read as quieter layers.
    /// </summary>
    internal static readonly IReadOnlyDictionary<int, int> LightInset = new Dictionary<int, int>
    {
        [PackMid] = PackLight,
        [PackLight] = PackHighlight,
        [PackHighlight] = NewHighlight,
        [PackLip] = PackMid,
        [PackEdge] = PackLip,
        [PackOutline] = PackEdge
    };
}
