using System;
using System.Linq;
using Godot;
using Voidling.Presentation.Voidlings;

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

    private static readonly Font PixelFont = GD.Load<Font>(
        "res://Assets/Sprout Lands - UI Pack - Basic pack/fonts/pixelFont-7-8x14-sproutLands.ttf");

    public static PanelContainer CreatePanel(Vector2 minimumSize)
    {
        var panel = new PanelContainer { CustomMinimumSize = minimumSize };
        panel.AddThemeStyleboxOverride("panel", CreatePanelStyle());
        return panel;
    }

    public static Button CreateButton(string text, int iconIndex = -1)
    {
        var useEyeIcon = text == "◉";
        var button = new Button
        {
            Text = useEyeIcon ? "" : text,
            CustomMinimumSize = new Vector2(72, 24),
            FocusMode = Control.FocusModeEnum.None
        };

        ApplyButtonChrome(button);
        ApplyPixelFont(button, 10);

        if (useEyeIcon)
        {
            var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            center.AddChild(new EyeIcon());
            button.AddChild(center);
        }
        else if (iconIndex >= 0)
        {
            button.Icon = CreateIcon(iconIndex);
            button.ExpandIcon = false;
        }

        return button;
    }

    public static void ApplyButtonChrome(Button button)
    {
        button.AddThemeStyleboxOverride("normal", CreateButtonStyle(new Rect2(0, 0, 16, 16), Colors.White));
        button.AddThemeStyleboxOverride("hover", CreateButtonStyle(new Rect2(0, 0, 16, 16), Color.FromHtml("#FFF6C9")));
        button.AddThemeStyleboxOverride("pressed", CreateButtonStyle(new Rect2(16, 0, 16, 16), Colors.White));
        button.AddThemeStyleboxOverride("hover_pressed", CreateButtonStyle(new Rect2(16, 0, 16, 16), Color.FromHtml("#FFF2B4")));
        button.AddThemeStyleboxOverride("disabled", CreateButtonStyle(new Rect2(0, 0, 16, 16), new Color(0.72f, 0.74f, 0.68f, 0.78f)));
        button.AddThemeStyleboxOverride("focus", CreateButtonStyle(new Rect2(0, 0, 16, 16), Color.FromHtml("#FFF6C9")));

        button.AddThemeColorOverride("font_color", Color.FromHtml("#4F5948"));
        button.AddThemeColorOverride("font_hover_color", Color.FromHtml("#2F4437"));
        button.AddThemeColorOverride("font_pressed_color", Color.FromHtml("#2F4437"));
        button.AddThemeColorOverride("font_hover_pressed_color", Color.FromHtml("#2F4437"));
        button.AddThemeColorOverride("font_disabled_color", Color.FromHtml("#8A927B"));
    }

    public static Label CreateLabel(string text, int size = 10)
    {
        var label = new Label { Text = text };
        label.AddThemeColorOverride("font_color", Color.FromHtml("#465247"));
        ApplyPixelFont(label, size);
        return label;
    }

    public static Label CreateTitle(string text)
    {
        var label = CreateLabel(text, 14);
        label.AddThemeColorOverride("font_color", Color.FromHtml("#3B5044"));
        return label;
    }

    public static TextureRect CreatePortrait(VoidlingData data, Vector2 minimumSize)
    {
        var hasAngel = GameRules.HasMutation(data, GameRules.AngelMutationId);
        var otherTraits = data.RareTraits?.Count(t =>
            !string.Equals(t.TraitId, GameRules.AngelMutationId, StringComparison.OrdinalIgnoreCase)) ?? 0;

        return CreatePortrait(
            data.Name,
            GameRules.TintColor(data.TintHex),
            hasAngel,
            otherTraits,
            minimumSize);
    }

    public static TextureRect CreatePortrait(
        Color tintColor,
        bool hasAngelMutation,
        int otherMutationCount,
        Vector2 minimumSize)
        => CreatePortrait(string.Empty, tintColor, hasAngelMutation, otherMutationCount, minimumSize);

    public static TextureRect CreatePortrait(
        string displayName,
        Color tintColor,
        bool hasAngelMutation,
        int otherMutationCount,
        Vector2 minimumSize)
    {
        var portrait = new TextureRect
        {
            Texture = VoidlingVisualCatalog.PortraitTexture(displayName),
            CustomMinimumSize = minimumSize,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        SetPortraitData(portrait, displayName, tintColor, hasAngelMutation, otherMutationCount);
        return portrait;
    }

    public static void SetPortraitData(TextureRect portrait, VoidlingData data)
    {
        var hasAngel = GameRules.HasMutation(data, GameRules.AngelMutationId);
        var otherTraits = data.RareTraits?.Count(t =>
            !string.Equals(t.TraitId, GameRules.AngelMutationId, StringComparison.OrdinalIgnoreCase)) ?? 0;

        SetPortraitData(
            portrait,
            data.Name,
            GameRules.TintColor(data.TintHex),
            hasAngel,
            otherTraits);
    }

    public static void SetPortraitData(
        TextureRect portrait,
        Color tintColor,
        bool hasAngelMutation,
        int otherMutationCount)
        => SetPortraitData(portrait, string.Empty, tintColor, hasAngelMutation, otherMutationCount);

    public static void SetPortraitData(
        TextureRect portrait,
        string displayName,
        Color tintColor,
        bool hasAngelMutation,
        int otherMutationCount)
    {
        portrait.Texture = VoidlingVisualCatalog.PortraitTexture(displayName);
        portrait.SelfModulate = VoidlingVisualCatalog.Modulate(displayName, tintColor);

        var oldBadge = portrait.GetNodeOrNull<Control>("__mutation_badge");
        if (oldBadge != null && GodotObject.IsInstanceValid(oldBadge))
            oldBadge.Free();

        var oldHalo = portrait.GetNodeOrNull<Control>("__mutation_halo");
        if (oldHalo != null && GodotObject.IsInstanceValid(oldHalo))
            oldHalo.Free();

        if (!hasAngelMutation && otherMutationCount <= 0)
            return;

        var requestedSpritePixels = Math.Max(
            16.0f,
            Math.Min(portrait.CustomMinimumSize.X, portrait.CustomMinimumSize.Y));
        var badge = new HaloBadge
        {
            Name = "__mutation_badge",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 5,
            ShowAngel = hasAngelMutation,
            SparkleCount = Math.Max(0, otherMutationCount),
            NominalSpritePixels = requestedSpritePixels
        };
        badge.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        portrait.AddChild(badge);
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

    public static MarginContainer Pad(Control child, int margin = 10)
    {
        var container = new MarginContainer();
        container.AddThemeConstantOverride("margin_left", margin);
        container.AddThemeConstantOverride("margin_right", margin);
        container.AddThemeConstantOverride("margin_top", margin);
        container.AddThemeConstantOverride("margin_bottom", margin);
        container.AddChild(child);
        return container;
    }

    public static void ApplyPixelFont(Control control, int size)
    {
        control.AddThemeFontOverride("font", PixelFont);
        control.AddThemeFontSizeOverride("font_size", size);
    }

    private static StyleBoxTexture CreatePanelStyle()
    {
        var style = new StyleBoxTexture { Texture = PanelTexture };
        style.TextureMarginLeft = 8;
        style.TextureMarginRight = 8;
        style.TextureMarginTop = 8;
        style.TextureMarginBottom = 8;
        style.ContentMarginLeft = 12;
        style.ContentMarginRight = 12;
        style.ContentMarginTop = 11;
        style.ContentMarginBottom = 11;
        return style;
    }

    private static StyleBoxTexture CreateButtonStyle(Rect2 region, Color modulate)
    {
        var atlas = new AtlasTexture { Atlas = ButtonTexture, Region = region };
        var style = new StyleBoxTexture
        {
            Texture = atlas,
            ModulateColor = modulate
        };
        style.TextureMarginLeft = 4;
        style.TextureMarginRight = 4;
        style.TextureMarginTop = 4;
        style.TextureMarginBottom = 4;
        style.ContentMarginLeft = 7;
        style.ContentMarginRight = 7;
        style.ContentMarginTop = 3;
        style.ContentMarginBottom = 3;
        return style;
    }
}
