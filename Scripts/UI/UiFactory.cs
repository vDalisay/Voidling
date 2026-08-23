using Godot;

namespace VoidlingGame;

public static class UiFactory
{
    public const string UiRoot = "res://Assets/Sprout Lands - UI Pack - Basic pack/Sprite sheets/";

    private static readonly Texture2D PanelTexture =
        GD.Load<Texture2D>(UiRoot + "Dialouge UI/dialog box big.png");

    private static readonly Texture2D ButtonTexture =
        GD.Load<Texture2D>(UiRoot + "buttons/Small Square Buttons.png");

    private static readonly Texture2D IconTexture =
        GD.Load<Texture2D>(UiRoot + "Icons/All Icons.png");

    public static PanelContainer CreatePanel(Vector2 minimumSize)
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = minimumSize
        };

        panel.AddThemeStyleboxOverride("panel", CreatePanelStyle());
        return panel;
    }

    public static Button CreateButton(string text, int iconIndex = -1)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(62, 22),
            FocusMode = Control.FocusModeEnum.None
        };

        button.AddThemeStyleboxOverride("normal", CreateButtonStyle(new Rect2(0, 0, 16, 16)));
        button.AddThemeStyleboxOverride("hover", CreateButtonStyle(new Rect2(16, 0, 16, 16)));
        button.AddThemeStyleboxOverride("pressed", CreateButtonStyle(new Rect2(0, 16, 16, 16)));
        button.AddThemeStyleboxOverride("disabled", CreateButtonStyle(new Rect2(16, 16, 16, 16)));

        button.AddThemeColorOverride("font_color", Color.FromHtml("#4F5948"));
        button.AddThemeColorOverride("font_hover_color", Color.FromHtml("#35443B"));
        button.AddThemeColorOverride("font_pressed_color", Color.FromHtml("#35443B"));
        button.AddThemeColorOverride("font_disabled_color", Color.FromHtml("#8A927B"));
        button.AddThemeFontSizeOverride("font_size", 10);

        if (iconIndex >= 0)
        {
            button.Icon = CreateIcon(iconIndex);
            button.ExpandIcon = false;
        }

        return button;
    }

    public static Label CreateLabel(string text, int size = 10)
    {
        var label = new Label
        {
            Text = text
        };
        label.AddThemeColorOverride("font_color", Color.FromHtml("#465247"));
        label.AddThemeFontSizeOverride("font_size", size);
        return label;
    }

    public static Label CreateTitle(string text)
    {
        var label = CreateLabel(text, 14);
        label.AddThemeColorOverride("font_color", Color.FromHtml("#3B5044"));
        return label;
    }

    public static AtlasTexture CreateIcon(int index)
    {
        const int columns = 18;
        var x = (index % columns) * 16;
        var y = (index / columns) * 16;

        return new AtlasTexture
        {
            Atlas = IconTexture,
            Region = new Rect2(x, y, 16, 16)
        };
    }

    public static MarginContainer Pad(Control child, int margin = 7)
    {
        var container = new MarginContainer();
        container.AddThemeConstantOverride("margin_left", margin);
        container.AddThemeConstantOverride("margin_right", margin);
        container.AddThemeConstantOverride("margin_top", margin);
        container.AddThemeConstantOverride("margin_bottom", margin);
        container.AddChild(child);
        return container;
    }

    private static StyleBoxTexture CreatePanelStyle()
    {
        var style = new StyleBoxTexture
        {
            Texture = PanelTexture
        };
        style.TextureMarginLeft = 8;
        style.TextureMarginRight = 8;
        style.TextureMarginTop = 8;
        style.TextureMarginBottom = 8;
        style.ContentMarginLeft = 8;
        style.ContentMarginRight = 8;
        style.ContentMarginTop = 8;
        style.ContentMarginBottom = 8;
        return style;
    }

    private static StyleBoxTexture CreateButtonStyle(Rect2 region)
    {
        var atlas = new AtlasTexture
        {
            Atlas = ButtonTexture,
            Region = region
        };

        var style = new StyleBoxTexture
        {
            Texture = atlas
        };
        style.TextureMarginLeft = 4;
        style.TextureMarginRight = 4;
        style.TextureMarginTop = 4;
        style.TextureMarginBottom = 4;
        style.ContentMarginLeft = 5;
        style.ContentMarginRight = 5;
        style.ContentMarginTop = 3;
        style.ContentMarginBottom = 3;
        return style;
    }
}
