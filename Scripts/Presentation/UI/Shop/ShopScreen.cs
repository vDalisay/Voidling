using System;
using System.Collections.Generic;
using Godot;
using VoidlingGame;

namespace Voidling.Presentation.UI.Shop;

public readonly record struct ShopTrainingItemViewState(
    string StatId,
    string DisplayName,
    Color IdentityColor,
    int Owned,
    int Price);

public readonly record struct ShopEggViewState(
    string EggId,
    Color TintColor,
    int Number,
    int Price);

public readonly record struct ShopRareOfferViewState(
    string ItemId,
    string DisplayName,
    string Tooltip,
    int Price);

public sealed record ShopScreenState(
    int Coins,
    IReadOnlyList<ShopTrainingItemViewState> TrainingItems,
    IReadOnlyList<ShopEggViewState> Eggs,
    int EggRotationSecondsRemaining,
    ShopRareOfferViewState? RareOffer);

/// <summary>
/// Standalone shop view. It renders a supplied snapshot and emits player purchase intent.
/// It deliberately has no knowledge of GameSession, Application shop services, persistence,
/// seed allocation, save timing, or legacy gameplay facades.
/// </summary>
public partial class ShopScreen : VBoxContainer
{
    private static readonly Texture2D EggTexture = GD.Load<Texture2D>(
        "res://Assets/Sprout Lands - Sprites - Basic pack/Objects/Egg item.png");

    public event Action<string>? TrainingItemPurchaseRequested;
    public event Action<string>? EggPurchaseRequested;
    public event Action<string>? RareOfferPurchaseRequested;

    private ShopScreenState? _state;

    public void Configure(ShopScreenState state)
    {
        if (IsInsideTree())
            throw new InvalidOperationException("ShopScreen must be configured before it enters the scene tree.");

        _state = state ?? throw new ArgumentNullException(nameof(state));
    }

    public override void _Ready()
    {
        if (_state == null)
            throw new InvalidOperationException("ShopScreen must be configured before AddChild.");

        AddThemeConstantOverride("separation", 4);
        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        AddChild(BuildSummary(_state));
        AddChild(BuildAwning());
        AddChild(BuildSectionLabel(Tr("UI_SHOP_TREATS_TITLE"), Tr("UI_SHOP_TREATS_SUBTITLE")));
        AddChild(BuildTreatShelf(_state.TrainingItems));
        AddChild(BuildSectionLabel(Tr("UI_SHOP_EGGS_TITLE"), Tr("UI_SHOP_EGGS_SUBTITLE")));
        AddChild(BuildEggShelf(_state.Eggs));
    }

    private Control BuildSummary(ShopScreenState state)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);

        var welcome = UiFactory.CreateLabel(
            $"{Tr("UI_SHOP_WELCOME")}  •  Rotation {FormatRotation(state.EggRotationSecondsRemaining)}",
            7);
        welcome.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        welcome.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        row.AddChild(welcome);

        if (state.RareOffer is { } offer)
        {
            var rare = UiFactory.CreateButton($"{offer.DisplayName}  {offer.Price}");
            rare.CustomMinimumSize = new Vector2(154, 24);
            rare.TooltipText = offer.Tooltip;
            UiFactory.ApplyPixelFont(rare, 6);
            rare.Pressed += () => RareOfferPurchaseRequested?.Invoke(offer.ItemId);
            row.AddChild(rare);
        }

        var wallet = UiFactory.CreatePanel(new Vector2(112, 24));
        var walletLabel = UiFactory.CreateLabel(
            string.Format(Tr("UI_SHOP_WALLET"), state.Coins), 8);
        walletLabel.HorizontalAlignment = HorizontalAlignment.Center;
        walletLabel.VerticalAlignment = VerticalAlignment.Center;
        wallet.AddChild(walletLabel);
        row.AddChild(wallet);
        return row;
    }

    private static string FormatRotation(int secondsRemaining)
    {
        var safeSeconds = Math.Max(0, secondsRemaining);
        var time = TimeSpan.FromSeconds(safeSeconds);
        return time.TotalHours >= 1.0
            ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}"
            : $"{time.Minutes}:{time.Seconds:00}";
    }

    private static Control BuildAwning()
    {
        var awning = new HBoxContainer { CustomMinimumSize = new Vector2(518, 9) };
        awning.AddThemeConstantOverride("separation", 0);
        for (var i = 0; i < 14; i++)
        {
            awning.AddChild(new ColorRect
            {
                Color = Color.FromHtml(i % 2 == 0 ? "#E8C977" : "#749B75"),
                CustomMinimumSize = new Vector2(24, 9),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                MouseFilter = Control.MouseFilterEnum.Ignore
            });
        }

        return awning;
    }

    private static Control BuildSectionLabel(string title, string subtitle)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);

        var heading = UiFactory.CreateLabel(title, 9);
        heading.CustomMinimumSize = new Vector2(125, 14);
        heading.AddThemeColorOverride("font_color", Color.FromHtml("#6B4B34"));
        row.AddChild(heading);

        var note = UiFactory.CreateLabel(subtitle, 6);
        note.VerticalAlignment = VerticalAlignment.Center;
        note.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        note.AddThemeColorOverride("font_color", Color.FromHtml("#786C5B"));
        row.AddChild(note);
        return row;
    }

    private Control BuildTreatShelf(IReadOnlyList<ShopTrainingItemViewState> items)
    {
        var shelf = CreateStallShelf();
        var grid = new GridContainer { Columns = 5 };
        grid.AddThemeConstantOverride("h_separation", 5);
        grid.AddThemeConstantOverride("v_separation", 3);
        shelf.GetNode<VBoxContainer>("ShelfBox").AddChild(grid);

        foreach (var item in items)
            grid.AddChild(BuildTreatProduct(item));

        return shelf;
    }

    private Control BuildEggShelf(IReadOnlyList<ShopEggViewState> eggs)
    {
        var shelf = CreateStallShelf();
        var grid = new GridContainer { Columns = 3 };
        grid.AddThemeConstantOverride("h_separation", 7);
        grid.AddThemeConstantOverride("v_separation", 3);
        shelf.GetNode<VBoxContainer>("ShelfBox").AddChild(grid);

        foreach (var egg in eggs)
            grid.AddChild(BuildEggProduct(egg));

        return shelf;
    }

    private Control BuildTreatProduct(ShopTrainingItemViewState item)
    {
        var card = CreateMarketCard(new Vector2(94, 70));
        card.TooltipText = TrainingItemEffectPresentation.Tooltip(item.DisplayName);
        var column = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        column.AddThemeConstantOverride("separation", 1);
        card.AddChild(column);

        var display = new CenterContainer { CustomMinimumSize = new Vector2(82, 24) };
        var packet = new PanelContainer { CustomMinimumSize = new Vector2(29, 21) };
        var packetStyle = new StyleBoxFlat
        {
            BgColor = item.IdentityColor,
            BorderColor = Color.FromHtml("#66594C")
        };
        packetStyle.SetBorderWidthAll(1);
        packetStyle.CornerRadiusTopLeft = packetStyle.CornerRadiusTopRight = 2;
        packetStyle.CornerRadiusBottomLeft = packetStyle.CornerRadiusBottomRight = 2;
        packet.AddThemeStyleboxOverride("panel", packetStyle);

        var initialText = item.DisplayName.Length == 0
            ? "?"
            : item.DisplayName[0].ToString().ToUpperInvariant();
        var initial = UiFactory.CreateLabel(initialText, 9);
        initial.HorizontalAlignment = HorizontalAlignment.Center;
        initial.VerticalAlignment = VerticalAlignment.Center;
        packet.AddChild(initial);
        display.AddChild(packet);
        column.AddChild(display);

        var name = UiFactory.CreateLabel(
            $"{item.DisplayName} {TrainingItemEffectPresentation.BaseEffectText}", 6);
        name.HorizontalAlignment = HorizontalAlignment.Center;
        name.AddThemeColorOverride("font_color", item.IdentityColor.Darkened(0.35f));
        column.AddChild(name);

        var stock = UiFactory.CreateLabel(string.Format(Tr("UI_SHOP_OWNED"), item.Owned), 5);
        stock.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(stock);

        var buy = UiFactory.CreateButton(string.Format(Tr("UI_SHOP_BUY"), item.Price));
        buy.CustomMinimumSize = new Vector2(78, 19);
        UiFactory.ApplyPixelFont(buy, 6);
        buy.TooltipText = card.TooltipText;
        buy.Pressed += () => TrainingItemPurchaseRequested?.Invoke(item.StatId);
        column.AddChild(buy);
        return card;
    }

    private Control BuildEggProduct(ShopEggViewState egg)
    {
        var card = CreateMarketCard(new Vector2(160, 70));
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 5);
        card.AddChild(row);

        var eggVisual = new TextureRect
        {
            Texture = EggTexture,
            SelfModulate = egg.TintColor,
            CustomMinimumSize = new Vector2(34, 34),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        row.AddChild(eggVisual);

        var info = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        info.AddThemeConstantOverride("separation", 1);
        info.AddChild(UiFactory.CreateLabel(
            string.Format(Tr("UI_SHOP_MYSTERY_EGG"), egg.Number), 6));

        var hint = UiFactory.CreateLabel(Tr("UI_SHOP_HIDDEN_DNA"), 5);
        hint.AddThemeColorOverride("font_color", Color.FromHtml("#786C5B"));
        info.AddChild(hint);

        var buy = UiFactory.CreateButton(string.Format(Tr("UI_SHOP_BUY"), egg.Price));
        buy.CustomMinimumSize = new Vector2(91, 19);
        UiFactory.ApplyPixelFont(buy, 6);
        buy.Pressed += () => EggPurchaseRequested?.Invoke(egg.EggId);
        info.AddChild(buy);
        row.AddChild(info);
        return card;
    }

    private static PanelContainer CreateStallShelf()
    {
        var shelf = new PanelContainer { CustomMinimumSize = new Vector2(518, 78) };
        var wood = new StyleBoxFlat
        {
            BgColor = Color.FromHtml("#B98255"),
            BorderColor = Color.FromHtml("#76513C"),
            ContentMarginLeft = 7,
            ContentMarginRight = 7,
            ContentMarginTop = 5,
            ContentMarginBottom = 7
        };
        wood.SetBorderWidthAll(2);
        wood.CornerRadiusTopLeft = wood.CornerRadiusTopRight = 3;
        wood.CornerRadiusBottomLeft = wood.CornerRadiusBottomRight = 3;
        shelf.AddThemeStyleboxOverride("panel", wood);

        var shelfBox = new VBoxContainer { Name = "ShelfBox" };
        shelfBox.AddThemeConstantOverride("separation", 2);
        shelf.AddChild(shelfBox);

        var lip = new ColorRect
        {
            Color = Color.FromHtml("#6E4936"),
            CustomMinimumSize = new Vector2(1, 5),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        shelfBox.AddChild(lip);
        shelfBox.MoveChild(lip, 0);
        return shelf;
    }

    private static PanelContainer CreateMarketCard(Vector2 minimumSize)
    {
        var card = new PanelContainer { CustomMinimumSize = minimumSize };
        var style = new StyleBoxFlat
        {
            BgColor = Color.FromHtml("#F4E5BD"),
            BorderColor = Color.FromHtml("#8A6248"),
            ContentMarginLeft = 4,
            ContentMarginRight = 4,
            ContentMarginTop = 3,
            ContentMarginBottom = 3
        };
        style.SetBorderWidthAll(1);
        style.CornerRadiusTopLeft = style.CornerRadiusTopRight = 2;
        style.CornerRadiusBottomLeft = style.CornerRadiusBottomRight = 2;
        card.AddThemeStyleboxOverride("panel", style);
        return card;
    }
}
