using Godot;

namespace Voidling.Presentation.UI.Common;

/// <summary>
/// The pack emoji each Garden window wears on its title tag, so a screen is recognised by its
/// picture before its name: a sprout for the market, a hammer for building, a clipboard for the
/// journal. Cells are (column, row) on the 32px emoji sheet.
/// </summary>
public static class ScreenIcons
{
    public static Texture2D Shop => UiSkin.Emoji(0, 15);
    public static Texture2D Inventory => UiSkin.Emoji(0, 16);
    public static Texture2D Breeding => UiSkin.Emoji(2, 9);
    public static Texture2D Race => UiSkin.Emoji(1, 10);
    public static Texture2D Journal => UiSkin.Emoji(1, 24);
    public static Texture2D Build => UiSkin.Emoji(5, 24);
    public static Texture2D Land => UiSkin.Emoji(2, 15);
    public static Texture2D Decorate => UiSkin.Emoji(1, 15);
    public static Texture2D Activities => UiSkin.Emoji(0, 31);
    public static Texture2D Missions => UiSkin.Emoji(0, 8);
    public static Texture2D GardenMenu => UiSkin.Emoji(9, 9);
    public static Texture2D Online => UiSkin.Emoji(7, 9);
    public static Texture2D Treats => UiSkin.Emoji(7, 15);
    public static Texture2D Family => UiSkin.Emoji(0, 9);
    public static Texture2D Details => UiSkin.Emoji(4, 25);
    public static Texture2D Warning => UiSkin.Emoji(5, 8);
    public static Texture2D Settings => UiSkin.Emoji(8, 8);
}
