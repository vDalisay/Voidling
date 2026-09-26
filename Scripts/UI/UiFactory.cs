using System;
using System.Linq;
using Godot;
using Voidling.Presentation.UI.Common;
using Voidling.Presentation.UI.Motion;
using Voidling.Presentation.Voidlings;

namespace VoidlingGame;

public static class UiFactory
{
    public const string UiRoot = "res://Assets/Sprout Lands - UI Pack - Premium pack/UI Sprites/";

    private static readonly Texture2D ButtonTexture =
        GD.Load<Texture2D>(UiRoot + "buttons/square/Small Square Buttons.png");

    private static readonly Texture2D IconTexture =
        GD.Load<Texture2D>("res://Assets/Sprout Lands - UI Pack - Basic pack/Sprite sheets/Icons/All Icons.png");

    private static readonly Texture2D PremiumIcons = GD.Load<Texture2D>(UiRoot + "Icons/All Icons.png");
    private static readonly Texture2D SettingsIcons =
        GD.Load<Texture2D>(UiRoot + "buttons/Icon Buttons/Icon Buttons Spritesheet.png");
    private static readonly Texture2D FarmingPlants = GD.Load<Texture2D>(
        "res://Assets/Sprout Lands - Sprites - premium pack/Objects/Farming Plants.png");

    /// <summary>The readable UI face for words; numbers use <see cref="UiSkin.NumberFont"/>.</summary>
    public static readonly Font InterfaceFont = new SystemFont
    {
        FontNames = new[] { "Segoe UI", "Noto Sans", "DejaVu Sans" },
        FontWeight = 500
    };

    public static PanelContainer CreatePanel(Vector2 minimumSize, bool wood = false)
    {
        var panel = new PanelContainer { CustomMinimumSize = minimumSize };
        var style = CreatePanelStyle();
        if (wood)
            style.Texture = new AtlasTexture { Atlas = ButtonTexture, Region = new Rect2(0, 80, 16, 16) };
        panel.AddThemeStyleboxOverride("panel", style);
        return panel;
    }

    /// <summary>A light paper card from the pack's inventory blocks, for content inside a window.</summary>
    public static PanelContainer CreatePaperPanel(Vector2 minimumSize)
    {
        var panel = new PanelContainer { CustomMinimumSize = minimumSize };
        panel.AddThemeStyleboxOverride("panel", UiSkin.Paper());
        return panel;
    }

    /// <summary>The pack's window board: modal windows and the HUD panels that float over the Garden.</summary>
    public static PanelContainer CreateWindowPanel(Vector2 minimumSize, Color? tint = null)
    {
        var panel = new PanelContainer { CustomMinimumSize = minimumSize };
        panel.AddThemeStyleboxOverride("panel", UiSkin.Window(tint));
        return panel;
    }

    /// <summary>The side board: the pack's window in its own beige, a step darker than the windows.</summary>
    public static PanelContainer CreateBoardPanel(Vector2 minimumSize)
    {
        var panel = new PanelContainer { CustomMinimumSize = minimumSize };
        panel.AddThemeStyleboxOverride("panel", UiSkin.Board());
        return panel;
    }

    /// <summary>The premium panel chrome on its own, tinted, for surfaces that are not containers.</summary>
    public static StyleBoxTexture CreatePanelStylebox(Color modulate)
    {
        var style = CreatePanelStyle();
        style.ModulateColor = modulate;
        return style;
    }

    public static Button CreateButton(string text, int iconIndex = -1)
    {
        var useEyeIcon = text == "◉";
        var button = new Button
        {
            Text = useEyeIcon ? "" : text,
            CustomMinimumSize = new Vector2(72, 24),
            FocusMode = Control.FocusModeEnum.All
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

    /// <summary>
    /// The pack's 26x19 wooden button, 9-sliced, with real up and pressed frames and the juice
    /// component (hover pop, press squash, release bounce, focus brackets). Every factory button
    /// wears this, so a restyle here reaches every screen.
    /// </summary>
    public static void ApplyButtonChrome(Button button)
        => ApplyButtonChrome(button, ButtonTone.Tan);

    public static void ApplyButtonChrome(Button button, ButtonTone tone)
    {
        button.AddThemeStyleboxOverride("normal", UiSkin.Button(tone, UiSkin.ButtonState.Normal));
        button.AddThemeStyleboxOverride("hover", UiSkin.Button(tone, UiSkin.ButtonState.Hover));
        var pressed = tone == ButtonTone.Tan ? UiSkin.DefaultPressed : UiSkin.Button(tone, UiSkin.ButtonState.Pressed);
        button.AddThemeStyleboxOverride("pressed", pressed);
        button.AddThemeStyleboxOverride("hover_pressed", pressed);
        button.AddThemeStyleboxOverride("disabled", UiSkin.Button(tone, UiSkin.ButtonState.Disabled));
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());

        var ink = tone switch
        {
            ButtonTone.Primary => UiSkin.InkOnGreen,
            ButtonTone.Danger => UiSkin.InkOnRed,
            _ => UiSkin.Ink
        };
        button.AddThemeColorOverride("font_color", ink);
        button.AddThemeColorOverride("font_hover_color", ink);
        button.AddThemeColorOverride("font_pressed_color", ink);
        button.AddThemeColorOverride("font_hover_pressed_color", ink);
        button.AddThemeColorOverride("font_focus_color", ink);
        button.AddThemeColorOverride("font_disabled_color", Color.FromHtml("#9A8C78"));
        // Light text on the green and red buttons, outlined in the button's own dark edge.
        if (tone != ButtonTone.Tan)
        {
            button.AddThemeColorOverride("font_outline_color", tone == ButtonTone.Primary
                ? Color.FromHtml("#2E5A22") : Color.FromHtml("#7A2E22"));
            button.AddThemeConstantOverride("outline_size", 3);
        }
        else
        {
            button.RemoveThemeColorOverride("font_outline_color");
            button.RemoveThemeConstantOverride("outline_size");
        }
        button.AddThemeColorOverride("icon_disabled_color", new Color(1, 1, 1, 0.45f));
        ButtonJuice.Attach(button, tone == ButtonTone.Tan ? ButtonFeel.Standard : ButtonFeel.Primary);
    }

    public static Label CreateLabel(string text, int size = 10)
    {
        var label = new Label { Text = text };
        label.AddThemeColorOverride("font_color", UiSkin.Ink);
        ApplyPixelFont(label, size);
        return label;
    }

    public static Label CreateTitle(string text)
    {
        var label = CreateLabel(text, 14);
        return label;
    }

    public static TextureRect CreatePortrait(VoidlingData data, Vector2 minimumSize)
    {
        var hasAngel = GameRules.HasMutation(data, GameRules.AngelMutationId);
        var otherTraits = data.RareTraits?.Count(t =>
            !string.Equals(t.TraitId, GameRules.AngelMutationId, StringComparison.OrdinalIgnoreCase)) ?? 0;
        return CreatePortrait(
            VoidlingVisualAppearance.From(data.Appearance, data.TintHex),
            hasAngel,
            otherTraits,
            minimumSize);
    }

    public static TextureRect CreatePortrait(
        VoidlingVisualAppearance appearance,
        bool hasAngelMutation,
        int otherMutationCount,
        Vector2 minimumSize)
    {
        var portrait = VoidlingPortraitComposer.Create(appearance, minimumSize);
        SetPortraitData(portrait, appearance, hasAngelMutation, otherMutationCount);
        return portrait;
    }

    // Compatibility overload for UI projections that have not yet been enriched with semantic
    // appearance. Production creature surfaces should prefer the VoidlingVisualAppearance overload.
    public static TextureRect CreatePortrait(
        Color tintColor,
        bool hasAngelMutation,
        int otherMutationCount,
        Vector2 minimumSize)
        => CreatePortrait(
            LegacyAppearance(tintColor),
            hasAngelMutation,
            otherMutationCount,
            minimumSize);

    public static VBoxContainer CreateVoidlingCard(
        string name,
        VoidlingVisualAppearance appearance,
        bool hasAngelMutation,
        int otherMutationCount,
        Action<bool> toggled,
        out Button card)
    {
        var entry = new VBoxContainer { CustomMinimumSize = new Vector2(84, 78) };
        entry.AddThemeConstantOverride("separation", 1);

        card = CreateButton("");
        card.CustomMinimumSize = new Vector2(80, 58);
        card.ToggleMode = true;
        card.KeepPressedOutside = true;
        var portrait = CreatePortrait(
            appearance,
            hasAngelMutation,
            otherMutationCount,
            new Vector2(48, 48));
        portrait.Position = new Vector2(16, 4);
        portrait.Size = new Vector2(48, 48);
        card.AddChild(portrait);
        card.Toggled += pressed => toggled(pressed);
        entry.AddChild(card);

        var label = CreateLabel(name, 6);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        label.AddThemeColorOverride("font_color", UiSkin.Ink);
        entry.AddChild(label);
        return entry;
    }

    public static VBoxContainer CreateVoidlingCard(
        string name,
        Color tintColor,
        bool hasAngelMutation,
        int otherMutationCount,
        Action<bool> toggled,
        out Button card)
        => CreateVoidlingCard(
            name,
            LegacyAppearance(tintColor),
            hasAngelMutation,
            otherMutationCount,
            toggled,
            out card);

    public static void SetPortraitData(TextureRect portrait, VoidlingData data)
    {
        var hasAngel = GameRules.HasMutation(data, GameRules.AngelMutationId);
        var otherTraits = data.RareTraits?.Count(t =>
            !string.Equals(t.TraitId, GameRules.AngelMutationId, StringComparison.OrdinalIgnoreCase)) ?? 0;
        SetPortraitData(
            portrait,
            VoidlingVisualAppearance.From(data.Appearance, data.TintHex),
            hasAngel,
            otherTraits);
    }

    public static void SetPortraitData(
        TextureRect portrait,
        VoidlingVisualAppearance appearance,
        bool hasAngelMutation,
        int otherMutationCount)
    {
        VoidlingPortraitComposer.Apply(portrait, appearance);

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
            ZIndex = 50,
            ShowAngel = hasAngelMutation,
            SparkleCount = Math.Max(0, otherMutationCount),
            NominalSpritePixels = requestedSpritePixels,
            VisualTypeId = appearance.VisualTypeId
        };
        badge.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        portrait.AddChild(badge);
    }

    public static void SetPortraitData(
        TextureRect portrait,
        Color tintColor,
        bool hasAngelMutation,
        int otherMutationCount)
        => SetPortraitData(
            portrait,
            LegacyAppearance(tintColor),
            hasAngelMutation,
            otherMutationCount);

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

    // Premium sheet: 16 columns, four colour variants per glyph. Keep legacy icon indices intact.
    public static AtlasTexture CreateGardenIcon(int column, int row) => new()
    {
        Atlas = PremiumIcons,
        Region = new Rect2(column * 16, row * 16, 16, 16)
    };

    public static AtlasTexture CreateSettingsIcon(int column, int row) => new()
    {
        Atlas = SettingsIcons,
        Region = new Rect2(column * 32, row * 32, 32, 32)
    };

    public static AtlasTexture CreateSproutIcon() => new()
    {
        Atlas = FarmingPlants,
        Region = new Rect2(16, 16, 16, 16)
    };

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

    /// <summary>
    /// The leaf-green call to action (Buy, Breed, Claim, Place, Start): the pack's white button
    /// tinted green, with a bigger pop and a periodic shine.
    /// </summary>
    public static void ApplyPrimaryStyle(Button button) => ApplyButtonChrome(button, ButtonTone.Primary);

    /// <summary>Clay-red chrome for actions that cannot be taken back (goodbye, discard, reset).</summary>
    public static void ApplyDangerStyle(Button button) => ApplyButtonChrome(button, ButtonTone.Danger);

    public static void ApplyPixelFont(Control control, int size)
    {
        // Keep the existing call sites and size hierarchy; use readable mixed-case UI type.
        control.AddThemeFontOverride("font", InterfaceFont);
        control.AddThemeFontSizeOverride("font_size", Math.Max(8, size));
        if (control is RichTextLabel)
        {
            control.AddThemeFontOverride("normal_font", InterfaceFont);
            control.AddThemeFontSizeOverride("normal_font_size", Math.Max(8, size));
        }
    }

    // Emphasize labels without introducing another font asset.
    public static void SetLabelBold(Label label, bool bold)
    {
        label.AddThemeConstantOverride("outline_size", bold ? 2 : 0);
        label.AddThemeColorOverride("font_outline_color", label.GetThemeColor("font_color"));
    }

    public static Color ParseTint(string tintHex)
    {
        try { return Color.FromHtml(tintHex); }
        catch { return Color.FromHtml("#F6F0C9"); }
    }

    private static VoidlingVisualAppearance LegacyAppearance(Color tintColor)
        => new(
            VoidlingAppearanceData.DefaultVisualTypeId,
            -1.0f,
            Array.Empty<string>(),
            tintColor.ToHtml());

    private static StyleBoxTexture CreatePanelStyle()
    {
        var style = new StyleBoxTexture
        {
            Texture = new AtlasTexture { Atlas = ButtonTexture, Region = new Rect2(0, 0, 16, 16) }
        };
        style.TextureMarginLeft = 7;
        style.TextureMarginRight = 7;
        style.TextureMarginTop = 7;
        style.TextureMarginBottom = 7;
        style.ContentMarginLeft = 12;
        style.ContentMarginRight = 12;
        style.ContentMarginTop = 11;
        style.ContentMarginBottom = 11;
        return style;
    }

    public static void StyleScroll(ScrollContainer scroll)
    {
        var scrollbar = scroll.GetVScrollBar();
        scrollbar.CustomMinimumSize = new Vector2(5, 0);
        scrollbar.AddThemeStyleboxOverride("scroll", new StyleBoxFlat
        {
            BgColor = new Color(0.55f, 0.42f, 0.3f, 0.18f), AntiAliasing = false,
            ContentMarginLeft = 1, ContentMarginRight = 1
        });
        scrollbar.AddThemeStyleboxOverride("scroll_focus", new StyleBoxEmpty());
        // A wooden peg: square pixels, a lit left edge, never anti-aliased.
        static StyleBoxFlat Thumb(Color color) => new()
        {
            BgColor = color, AntiAliasing = false,
            BorderColor = color.Lightened(0.25f), BorderWidthLeft = 1,
            ContentMarginLeft = 2, ContentMarginRight = 2, ContentMarginTop = 8, ContentMarginBottom = 8
        };
        scrollbar.AddThemeStyleboxOverride("grabber", Thumb(UiPalette.Tan));
        scrollbar.AddThemeStyleboxOverride("grabber_highlight", Thumb(UiPalette.Tan.Lightened(0.12f)));
        scrollbar.AddThemeStyleboxOverride("grabber_pressed", Thumb(UiPalette.Bark));
    }

    public static void StyleInput(LineEdit input)
    {
        input.AddThemeStyleboxOverride("normal", UiSkin.Well());
        input.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        input.AddThemeStyleboxOverride("read_only", UiSkin.Well());
        input.AddThemeColorOverride("font_color", UiSkin.Ink);
        input.AddThemeColorOverride("font_placeholder_color", UiSkin.InkSoft);
        input.AddThemeColorOverride("caret_color", UiSkin.Ink);
        input.AddThemeColorOverride("selection_color", new Color(0.56f, 0.8f, 0.42f, 0.45f));
    }

    /// <summary>
    /// The UI root's theme: whatever a screen does not style itself (tooltips, stray buttons, line
    /// edits) still comes out in the pack's chrome and the UI face.
    /// </summary>
    public static Theme CreateRootTheme()
    {
        var theme = new Theme { DefaultFont = InterfaceFont, DefaultFontSize = 8 };
        theme.SetStylebox("normal", "Button", UiSkin.Button(ButtonTone.Tan, UiSkin.ButtonState.Normal));
        theme.SetStylebox("hover", "Button", UiSkin.Button(ButtonTone.Tan, UiSkin.ButtonState.Hover));
        theme.SetStylebox("pressed", "Button", UiSkin.DefaultPressed);
        theme.SetStylebox("hover_pressed", "Button", UiSkin.DefaultPressed);
        theme.SetStylebox("disabled", "Button", UiSkin.Button(ButtonTone.Tan, UiSkin.ButtonState.Disabled));
        theme.SetStylebox("focus", "Button", new StyleBoxEmpty());
        foreach (var colour in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color", "font_focus_color" })
            theme.SetColor(colour, "Button", UiSkin.Ink);
        theme.SetStylebox("normal", "LineEdit", UiSkin.Well());
        theme.SetStylebox("focus", "LineEdit", new StyleBoxEmpty());
        theme.SetColor("font_color", "LineEdit", UiSkin.Ink);
        theme.SetColor("font_placeholder_color", "LineEdit", UiSkin.InkSoft);
        theme.SetColor("caret_color", "LineEdit", UiSkin.Ink);

        var tooltip = UiSkin.Paper();
        tooltip.ContentMarginLeft = tooltip.ContentMarginRight = 7;
        tooltip.ContentMarginTop = 4;
        tooltip.ContentMarginBottom = 6;
        theme.SetStylebox("panel", "TooltipPanel", tooltip);
        theme.SetColor("font_color", "TooltipLabel", UiSkin.Ink);
        theme.SetFont("font", "TooltipLabel", InterfaceFont);
        theme.SetFontSize("font_size", "TooltipLabel", 8);
        return theme;
    }
}
