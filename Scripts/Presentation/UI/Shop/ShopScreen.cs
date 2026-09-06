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
public sealed record ShopScreenState(int Coins, IReadOnlyList<ShopTrainingItemViewState> TrainingItems, IReadOnlyList<ShopEggViewState> Eggs, int EggRotationSecondsRemaining, ShopRareOfferViewState? RareOffer, IReadOnlyList<ShopLandPieceViewState> LandPieces);

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
    public event Action<string, string>? SelectionChanged;

    private sealed record Product(string Key, string Name, string Description, string Status, int Price, Func<Control> Icon, Action Buy);

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
        _categories = new VBoxContainer { Name = "Categories", CustomMinimumSize = new Vector2(88, 0) };
        _categories.AddThemeConstantOverride("separation", 5);
        body.AddChild(_categories);

        var scroll = new ScrollContainer
        {
            Name = "Catalogue",
            CustomMinimumSize = new Vector2(220, 250),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        UiFactory.StyleScroll(scroll);
        body.AddChild(scroll);
        _catalogue = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _catalogue.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(_catalogue);

        var receiptPanel = UiFactory.CreatePanel(new Vector2(170, 250));
        receiptPanel.Name = "Receipt";
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
        var welcome = UiFactory.CreateLabel($"{Tr("UI_SHOP_WELCOME")}  •  {string.Format(Tr("UI_SHOP_ROTATION"), FormatRotation(_state!.EggRotationSecondsRemaining))}", 7);
        welcome.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        welcome.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        row.AddChild(welcome);
        var wallet = UiFactory.CreatePanel(new Vector2(112, 24));
        var walletLabel = UiFactory.CreateLabel(string.Format(Tr("UI_SHOP_WALLET"), _state.Coins), 8);
        walletLabel.HorizontalAlignment = HorizontalAlignment.Center;
        walletLabel.VerticalAlignment = VerticalAlignment.Center;
        wallet.AddChild(walletLabel);
        row.AddChild(wallet);
        return row;
    }

    private void RebuildCategories()
    {
        Clear(_categories);
        AddCategory(TreatsCategory, Tr("UI_SHOP_TREATS_TITLE"));
        AddCategory(EggsCategory, Tr("UI_SHOP_EGGS_TITLE"));
        AddCategory(LandCategory, Tr("UI_SHOP_LAND_TITLE"));
        if (_state!.RareOffer != null) AddCategory(SpecialCategory, Tr("UI_SHOP_SPECIAL_TITLE"));
    }

    private void AddCategory(string category, string text)
    {
        var button = UiFactory.CreateButton(text);
        button.Name = "Category" + category;
        button.ToggleMode = true;
        button.ButtonPressed = category == _category;
        button.CustomMinimumSize = new Vector2(88, 30);
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
        button.CustomMinimumSize = new Vector2(208, 44);
        button.Pressed += () => SelectProduct(product);
        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        row.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, LayoutPresetMode.Minsize, 4);
        row.AddThemeConstantOverride("separation", 6);
        var icon = product.Icon();
        icon.CustomMinimumSize = new Vector2(Mathf.Max(28, icon.CustomMinimumSize.X), Mathf.Max(28, icon.CustomMinimumSize.Y));
        row.AddChild(icon);
        var copy = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        copy.AddThemeConstantOverride("separation", 0);
        copy.AddChild(UiFactory.CreateLabel(product.Name, 7));
        copy.AddChild(UiFactory.CreateLabel(product.Status, 5));
        row.AddChild(copy);
        var price = UiFactory.CreateLabel(product.Price.ToString(), 8);
        price.CustomMinimumSize = new Vector2(35, 28);
        price.HorizontalAlignment = HorizontalAlignment.Right;
        price.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(price);
        button.AddChild(row);
        return button;
    }

    private void BuildReceipt(Product product)
    {
        var title = UiFactory.CreateLabel(Tr("UI_SHOP_RECEIPT"), 7);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        _receipt.AddChild(title);
        var iconCenter = new CenterContainer { CustomMinimumSize = new Vector2(0, 54) };
        var icon = product.Icon();
        icon.CustomMinimumSize = new Vector2(48, 48);
        iconCenter.AddChild(icon);
        _receipt.AddChild(iconCenter);
        var name = UiFactory.CreateLabel(product.Name, 9);
        name.HorizontalAlignment = HorizontalAlignment.Center;
        name.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _receipt.AddChild(name);
        var description = UiFactory.CreateLabel(product.Description, 6);
        description.CustomMinimumSize = new Vector2(0, 67);
        description.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _receipt.AddChild(description);
        _receipt.AddChild(UiFactory.CreateLabel(product.Status, 6));
        _receipt.AddChild(new ColorRect { Color = Color.FromHtml("#B7926F"), CustomMinimumSize = new Vector2(1, 1), MouseFilter = MouseFilterEnum.Ignore });
        var price = UiFactory.CreateLabel(string.Format(Tr("UI_SHOP_PRICE"), product.Price), 8);
        price.HorizontalAlignment = HorizontalAlignment.Center;
        _receipt.AddChild(price);
        var buy = UiFactory.CreateButton(string.Format(Tr("UI_SHOP_BUY"), product.Price));
        buy.Name = "BuySelected";
        buy.CustomMinimumSize = new Vector2(148, 26);
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
                    TrainingItemEffectPresentation.Tooltip(item.DisplayName), string.Format(Tr("UI_SHOP_OWNED"), item.Owned), item.Price,
                    () => AtlasIcon(TreatTexture, new Rect2(iconIndex % 4 * 16, iconIndex / 4 * 16, 16, 16)),
                    () => TrainingItemPurchaseRequested?.Invoke(item.StatId));
            }
            yield break;
        }
        if (_category == EggsCategory)
        {
            foreach (var egg in _state!.Eggs)
                yield return new Product("egg:" + egg.EggId, string.Format(Tr("UI_SHOP_MYSTERY_EGG"), egg.Number),
                    Tr("UI_SHOP_HIDDEN_DNA"), Tr("UI_SHOP_IN_STOCK"), egg.Price,
                    () => EggIcon(egg.TintColor), () => EggPurchaseRequested?.Invoke(egg.EggId));
            yield break;
        }
        if (_category == LandCategory)
        {
            foreach (var piece in _state!.LandPieces)
                yield return new Product("land:" + piece.ShapeId, piece.DisplayName, Tr("UI_SHOP_LAND_SUBTITLE"),
                    string.Format(Tr("UI_SHOP_OWNED"), piece.Stored), piece.Price,
                    () => BuildShapeSwatch(piece.Cells), () => LandPurchaseRequested?.Invoke(piece.ShapeId));
            yield break;
        }
        if (_state!.RareOffer is { } offer)
            yield return new Product("special:" + offer.ItemId, offer.DisplayName, offer.Tooltip, Tr("UI_SHOP_IN_STOCK"), offer.Price,
                () => Icon(UiFactory.CreateIcon(19)), () => RareOfferPurchaseRequested?.Invoke(offer.ItemId));
    }

    private static string FormatRotation(int seconds)
    {
        var time = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return time.TotalHours >= 1 ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}" : $"{time.Minutes}:{time.Seconds:00}";
    }

    private static TextureRect AtlasIcon(Texture2D atlas, Rect2 region)
        => Icon(new AtlasTexture { Atlas = atlas, Region = region });

    private static TextureRect EggIcon(Color tint)
    {
        var icon = Icon(EggTexture);
        icon.SelfModulate = tint;
        return icon;
    }

    private static TextureRect Icon(Texture2D texture)
        => new() { Texture = texture, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore };

    private static Control BuildShapeSwatch(IReadOnlyList<(int Q, int R)> cells)
    {
        const float width = 42, height = 28, ratio = 1.7f;
        var units = cells.Select(cell => new Vector2(1.5f * cell.Q, cell.R + cell.Q * 0.5f)).ToArray();
        var spanX = units.Max(point => point.X) - units.Min(point => point.X) + 2;
        var spanY = units.Max(point => point.Y) - units.Min(point => point.Y) + 1;
        var edge = Mathf.Min(width / spanX, height / (ratio * spanY));
        var tileHeight = edge * ratio;
        var centers = units.Select(point => new Vector2(point.X * edge, point.Y * tileHeight)).ToArray();
        var middle = new Vector2((centers.Max(p => p.X) + centers.Min(p => p.X)) / 2, (centers.Max(p => p.Y) + centers.Min(p => p.Y)) / 2);
        var origin = new Vector2(width / 2, height / 2) - middle;
        var holder = new Control { CustomMinimumSize = new Vector2(width, height), MouseFilter = MouseFilterEnum.Ignore };
        var tint = Color.FromHtml("#8FC57E");
        foreach (var center in centers)
        {
            var polygon = HexShape.Corners(edge, tileHeight);
            var outline = HexShape.Outline(edge, tileHeight);
            for (var i = 0; i < polygon.Length; i++) polygon[i] += origin + center;
            for (var i = 0; i < outline.Length; i++) outline[i] += origin + center;
            holder.AddChild(new Polygon2D { Polygon = polygon, Color = tint });
            holder.AddChild(new Line2D { Points = outline, DefaultColor = tint.Darkened(0.45f), Width = 1 });
        }
        return holder;
    }

    private static void Clear(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is CanvasItem canvasItem) canvasItem.Visible = false;
            if (child is Control control) control.MouseFilter = MouseFilterEnum.Ignore;
            child.QueueFree();
        }
    }
}
