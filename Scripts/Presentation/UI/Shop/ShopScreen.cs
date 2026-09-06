using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Voidling.Presentation.UI.Common;
using VoidlingGame;

namespace Voidling.Presentation.UI.Shop;

public readonly record struct ShopTrainingItemViewState(string StatId, string DisplayName, Color IdentityColor, int Owned, int Price);
public readonly record struct ShopEggViewState(string EggId, Color TintColor, int Number, int Price);
public readonly record struct ShopRareOfferViewState(string ItemId, string DisplayName, string Tooltip, int Price);
public readonly record struct ShopLandPieceViewState(string ShapeId, string DisplayName, IReadOnlyList<(int Q, int R)> Cells, int Stored, int Price);
public sealed record ShopScreenState(int Coins, IReadOnlyList<ShopTrainingItemViewState> TrainingItems, IReadOnlyList<ShopEggViewState> Eggs, ShopRareOfferViewState? RareOffer, IReadOnlyList<ShopLandPieceViewState> LandPieces);

/// <summary>Keeper's ledger: categories, readable catalogue rows, and one stable purchase receipt.</summary>
public partial class ShopScreen : VBoxContainer
{
    public const string TreatsCategory = "Treats";
    public const string EggsCategory = "Eggs";
    public const string LandCategory = "Land";
    public const string SpecialCategory = "Special";

    private static readonly Texture2D TreatTexture = GD.Load<Texture2D>(
        "res://Assets/Sprout Lands - Sprites - premium pack/Objects/Items/fruit-n-berries-items.png");
    private static readonly Texture2D EggTexture = GD.Load<Texture2D>(
        "res://Assets/Sprout Lands - Sprites - Basic pack/Objects/Egg item.png");

    public event Action<string>? TrainingItemPurchaseRequested;
    public event Action<string>? EggPurchaseRequested;
    public event Action<string>? RareOfferPurchaseRequested;
    public event Action<string>? LandPurchaseRequested;
    public event Action? InventoryRequested;
    public event Action<string, string>? SelectionChanged;

    private sealed record Product(string Key, string Name, string Status, int Price, Func<Control> Icon, Action Buy);

    private ShopScreenState? _state;
    private string _category = TreatsCategory;
    private string _selection = string.Empty;
    private VBoxContainer _categories = null!;
    private VBoxContainer _catalogue = null!;
    private VBoxContainer _receipt = null!;
    private Button? _selectedButton;

    public void Configure(ShopScreenState state, string category, string selection)
    {
        if (IsInsideTree()) throw new InvalidOperationException("ShopScreen must be configured before it enters the scene tree.");
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _category = category;
        _selection = selection;
    }

    public override void _Ready()
    {
        if (_state == null) throw new InvalidOperationException("ShopScreen must be configured before AddChild.");
        Name = "ShopLedger";
        AddThemeConstantOverride("separation", 5);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        AddChild(BuildSummary());

        var body = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 6);
        AddChild(body);
        _categories = new VBoxContainer { Name = "Categories", CustomMinimumSize = new Vector2(82, 0) };
        _categories.AddThemeConstantOverride("separation", 5);
        body.AddChild(_categories);

        var catalogueColumn = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        catalogueColumn.AddThemeConstantOverride("separation", 4);
        body.AddChild(catalogueColumn);
        var context = new HBoxContainer();
        context.AddChild(new Control { CustomMinimumSize = new Vector2(32, 0) });
        var typeHeader = UiFactory.CreateLabel(Tr("UI_SHOP_TYPE"), 7);
        typeHeader.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        context.AddChild(typeHeader);
        var priceHeader = UiFactory.CreateLabel(Tr("UI_SHOP_PRICE_HEADER"), 7);
        priceHeader.CustomMinimumSize = new Vector2(38, 0);
        priceHeader.HorizontalAlignment = HorizontalAlignment.Right;
        context.AddChild(priceHeader);
        catalogueColumn.AddChild(context);
        var scroll = new ScrollContainer
        {
            Name = "Catalogue",
            CustomMinimumSize = new Vector2(185, 205),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        UiFactory.StyleScroll(scroll);
        catalogueColumn.AddChild(scroll);
        _catalogue = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _catalogue.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(_catalogue);

        var receiptPanel = UiFactory.CreatePanel(new Vector2(145, 225));
        receiptPanel.Name = "Receipt";
        var receiptStyle = (StyleBoxTexture)receiptPanel.GetThemeStylebox("panel").Duplicate();
        receiptStyle.ModulateColor = new Color(230f / 220f, 212f / 224f, 173f / 210f);
        receiptPanel.AddThemeStyleboxOverride("panel", receiptStyle);
        body.AddChild(receiptPanel);
        _receipt = new VBoxContainer();
        _receipt.AddThemeConstantOverride("separation", 5);
        receiptPanel.AddChild(_receipt);

        NormalizeSelection();
        RebuildCategories();
        RebuildProducts();
    }

    public void FocusSelection()
        => (_selectedButton ?? _categories.GetChildren().OfType<Button>().FirstOrDefault())?.GrabFocus();

    private Control BuildSummary()
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        var wallet = UiFactory.CreatePanel(new Vector2(112, 24));
        var walletStyle = (StyleBoxTexture)wallet.GetThemeStylebox("panel").Duplicate();
        walletStyle.ModulateColor = new Color(232f / 220f, 207f / 224f, 166f / 210f);
        wallet.AddThemeStyleboxOverride("panel", walletStyle);
        var walletRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        walletRow.AddThemeConstantOverride("separation", 3);
        walletRow.AddChild(SproutIcon(12));
        var walletLabel = UiFactory.CreateLabel(_state!.Coins.ToString(), 8);
        walletLabel.HorizontalAlignment = HorizontalAlignment.Center;
        walletLabel.VerticalAlignment = VerticalAlignment.Center;
        walletRow.AddChild(walletLabel);
        wallet.AddChild(walletRow);
        row.AddChild(wallet);
        return row;
    }

    private void RebuildCategories()
    {
        Clear(_categories);
        AddCategory(TreatsCategory, Tr("UI_SHOP_CATEGORY_TREATS"), AtlasIconTexture(TreatTexture, new Rect2(16, 0, 16, 16)));
        AddCategory(EggsCategory, Tr("UI_SHOP_CATEGORY_EGGS"), EggTexture);
        AddCategory(LandCategory, Tr("UI_SHOP_CATEGORY_LAND"), customIcon: LandShapePresentation.CreateShapeArt("single", Color.FromHtml("#8FC57E"), 18, 14));
        if (_state!.RareOffer != null) AddCategory(SpecialCategory, Tr("UI_SHOP_CATEGORY_RARE"), UiFactory.CreateIcon(19));
        _categories.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
        var inventory = UiFactory.CreateButton(Tr("UI_SHOP_OPEN_INVENTORY"));
        inventory.Name = "OpenInventory";
        inventory.CustomMinimumSize = new Vector2(82, 24);
        UiFactory.ApplyPixelFont(inventory, 6);
        inventory.Pressed += () => InventoryRequested?.Invoke();
        _categories.AddChild(inventory);
    }

    private void AddCategory(string category, string text, Texture2D? icon = null, Control? customIcon = null)
    {
        var button = UiFactory.CreateButton(text);
        button.Name = "Category" + category;
        button.ToggleMode = true;
        button.ButtonPressed = category == _category;
        button.CustomMinimumSize = new Vector2(82, 30);
        button.Alignment = HorizontalAlignment.Left;
        button.Icon = icon;
        button.ExpandIcon = false;
        button.IconAlignment = HorizontalAlignment.Left;
        button.AddThemeConstantOverride("icon_max_width", 14);
        if (customIcon != null)
        {
            button.Text = string.Empty;
            var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            row.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, LayoutPresetMode.Minsize, 7);
            row.AddThemeConstantOverride("separation", 5);
            row.AddChild(customIcon);
            row.AddChild(UiFactory.CreateLabel(text, 7));
            button.AddChild(row);
        }
        UiFactory.ApplyPixelFont(button, 6);
        button.Pressed += () => SelectCategory(category);
        _categories.AddChild(button);
    }

    private void SelectCategory(string category)
    {
        _category = category;
        _selection = string.Empty;
        NormalizeSelection();
        SelectionChanged?.Invoke(_category, _selection);
        foreach (var button in _categories.GetChildren().OfType<Button>())
            button.ButtonPressed = button.Name == "Category" + _category;
        RebuildProducts();
        FocusSelection();
    }

    private void SelectProduct(Product product)
    {
        _selection = product.Key;
        SelectionChanged?.Invoke(_category, _selection);
        RebuildProducts();
        FocusSelection();
    }

    private void NormalizeSelection()
    {
        var products = Products().ToArray();
        if (products.Length == 0) { _selection = string.Empty; return; }
        if (products.All(product => product.Key != _selection)) _selection = products[0].Key;
    }

    private void RebuildProducts()
    {
        Clear(_catalogue);
        Clear(_receipt);
        _selectedButton = null;
        var products = Products().ToArray();
        if (products.Length == 0)
        {
            _catalogue.AddChild(UiFactory.CreateLabel(Tr("UI_SHOP_EMPTY"), 7));
            _receipt.AddChild(UiFactory.CreateLabel(Tr("UI_SHOP_SELECT_HINT"), 7));
            return;
        }
        foreach (var product in products)
        {
            var button = BuildProductRow(product);
            _catalogue.AddChild(button);
            if (product.Key == _selection) _selectedButton = button;
        }
        BuildReceipt(products.First(product => product.Key == _selection));
    }

    private Button BuildProductRow(Product product)
    {
        var button = UiFactory.CreateButton(string.Empty);
        button.Name = "Product_" + product.Key.Replace(':', '_');
        button.ToggleMode = true;
        button.ButtonPressed = product.Key == _selection;
        button.CustomMinimumSize = new Vector2(180, 36);
        button.Pressed += () => SelectProduct(product);
        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        row.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, LayoutPresetMode.Minsize, 4);
        row.AddThemeConstantOverride("separation", 6);
        var icon = product.Icon();
        icon.CustomMinimumSize = new Vector2(Mathf.Max(26, icon.CustomMinimumSize.X), Mathf.Max(26, icon.CustomMinimumSize.Y));
        row.AddChild(icon);
        var copy = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        copy.AddThemeConstantOverride("separation", 0);
        copy.AddChild(UiFactory.CreateLabel(product.Name, 7));
        copy.AddChild(UiFactory.CreateLabel(product.Status, 5));
        row.AddChild(copy);
        var price = new HBoxContainer { CustomMinimumSize = new Vector2(38, 26), Alignment = BoxContainer.AlignmentMode.End };
        price.AddThemeConstantOverride("separation", 2);
        price.AddChild(SproutIcon(10));
        var priceValue = UiFactory.CreateLabel(product.Price.ToString(), 8);
        priceValue.VerticalAlignment = VerticalAlignment.Center;
        price.AddChild(priceValue);
        row.AddChild(price);
        button.AddChild(row);
        return button;
    }

    private void BuildReceipt(Product product)
    {
        var iconPanel = new PanelContainer { CustomMinimumSize = new Vector2(0, 48) };
        iconPanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = Color.FromHtml("#F7E5BD"),
            CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3,
            CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3
        });
        var iconCenter = new CenterContainer();
        var icon = product.Icon();
        icon.CustomMinimumSize = new Vector2(44, 44);
        iconCenter.AddChild(icon);
        iconPanel.AddChild(iconCenter);
        _receipt.AddChild(iconPanel);
        var name = UiFactory.CreateLabel(product.Name, 9);
        name.HorizontalAlignment = HorizontalAlignment.Center;
        name.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _receipt.AddChild(name);
        _receipt.AddChild(UiFactory.CreateLabel(product.Status, 6));
        _receipt.AddChild(new ColorRect { Color = Color.FromHtml("#B7926F"), CustomMinimumSize = new Vector2(1, 1), MouseFilter = MouseFilterEnum.Ignore });
        var price = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        price.AddThemeConstantOverride("separation", 3);
        price.AddChild(UiFactory.CreateLabel(Tr("UI_SHOP_PRICE_HEADER").ToUpperInvariant(), 8));
        price.AddChild(SproutIcon(11));
        price.AddChild(UiFactory.CreateLabel(product.Price.ToString(), 8));
        _receipt.AddChild(price);
        var buy = UiFactory.CreateButton(string.Format(Tr("UI_SHOP_BUY"), product.Price));
        buy.Icon = UiFactory.CreateSproutIcon();
        buy.AddThemeConstantOverride("icon_max_width", 12);
        buy.Name = "BuySelected";
        buy.CustomMinimumSize = new Vector2(125, 28);
        UiFactory.ApplyPrimaryStyle(buy);
        buy.Pressed += product.Buy;
        _receipt.AddChild(buy);
    }

    private IEnumerable<Product> Products()
    {
        if (_category == TreatsCategory)
        {
            var index = 0;
            foreach (var item in _state!.TrainingItems)
            {
                var iconIndex = index++;
                yield return new Product("treat:" + item.StatId, item.DisplayName + " treat",
                    string.Format(Tr("UI_SHOP_OWNED"), item.Owned), item.Price,
                    () => AtlasIcon(TreatTexture, new Rect2(iconIndex % 4 * 16, iconIndex / 4 * 16, 16, 16)),
                    () => TrainingItemPurchaseRequested?.Invoke(item.StatId));
            }
            yield break;
        }
        if (_category == EggsCategory)
        {
            foreach (var egg in _state!.Eggs)
                yield return new Product("egg:" + egg.EggId, string.Format(Tr("UI_SHOP_MYSTERY_EGG"), egg.Number),
                    Tr("UI_SHOP_IN_STOCK"), egg.Price,
                    () => EggIcon(egg.TintColor), () => EggPurchaseRequested?.Invoke(egg.EggId));
            yield break;
        }
        if (_category == LandCategory)
        {
            foreach (var piece in _state!.LandPieces)
                yield return new Product("land:" + piece.ShapeId, piece.DisplayName,
                    string.Format(Tr("UI_SHOP_OWNED"), piece.Stored), piece.Price,
                    () => LandShapePresentation.CreateShapeArt(piece.ShapeId, Color.FromHtml("#8FC57E"), 42, 28), () => LandPurchaseRequested?.Invoke(piece.ShapeId));
            yield break;
        }
        if (_state!.RareOffer is { } offer)
            yield return new Product("special:" + offer.ItemId, offer.DisplayName, Tr("UI_SHOP_IN_STOCK"), offer.Price,
                () => Icon(UiFactory.CreateIcon(19)), () => RareOfferPurchaseRequested?.Invoke(offer.ItemId));
    }

    private static TextureRect AtlasIcon(Texture2D atlas, Rect2 region)
        => Icon(new AtlasTexture { Atlas = atlas, Region = region });

    private static TextureRect SproutIcon(float size)
    {
        var icon = Icon(UiFactory.CreateSproutIcon());
        icon.CustomMinimumSize = new Vector2(size, size);
        return icon;
    }

    private static AtlasTexture AtlasIconTexture(Texture2D atlas, Rect2 region)
        => new() { Atlas = atlas, Region = region };

    private static TextureRect EggIcon(Color tint)
    {
        var icon = Icon(EggTexture);
        icon.SelfModulate = tint;
        return icon;
    }

    private static TextureRect Icon(Texture2D texture)
        => new() { Texture = texture, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore };

    private static void Clear(Node node) => PaperCard.Clear(node);
}
