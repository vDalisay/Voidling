using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Voidling.Presentation.UI.Common;
using VoidlingGame;

namespace Voidling.Presentation.UI.Inventory;

public readonly record struct InventoryItemViewState(string DisplayName, int Count, int IconIndex, bool UsesEggIcon = false);
public readonly record struct FailedEggViewState(string EggId, string DisplayName);
public readonly record struct EggShellViewState(string ShellId, string DisplayName, int SaleValue);
public readonly record struct IncubatingEggViewState(string EggId, string DisplayName, int SecondsRemaining);
public readonly record struct StoredEggViewState(string EggId, string DisplayName, Color TintColor);
/// <summary>A piece of ground waiting in the inventory, named and shaped by what was bought.</summary>
public readonly record struct StoredLandViewState(string ModuleId, string DisplayName, string ShapeId, Color Tint);
public sealed record InventoryScreenState(IReadOnlyList<InventoryItemViewState> Items, IReadOnlyList<FailedEggViewState> FailedEggs, IReadOnlyList<EggShellViewState> EggShells, int IncubationSkipCount, IReadOnlyList<IncubatingEggViewState> IncubatingEggs, IReadOnlyList<StoredEggViewState> StoredEggs, IReadOnlyList<StoredLandViewState> StoredLand);

/// <summary>
/// The satchel: category column, a grid of drawn slots, and one detail card carrying the single
/// action an item has. Follows the Shop's three bands, so an item is recognised by its own art
/// rather than read out of a list.
/// </summary>
public partial class InventoryScreen : HBoxContainer
{
    public const string TreatsCategory = "Treats";
    public const string EggsCategory = "Eggs";
    public const string LandCategory = "Land";
    public const string ShellsCategory = "Shells";

    private const int SlotColumns = 4;
    private const int SlotRows = 3;
    private static readonly Vector2 SlotSize = new(52, 52);

    public event Action<string>? DiscardFailedEggRequested;
    public event Action<string>? SellEggShellRequested;
    public event Action<string>? UseIncubationSkipRequested;
    public event Action<StoredEggViewState>? PlaceStoredEggRequested;
    public event Action<StoredLandViewState>? PlaceStoredLandRequested;

    private static readonly Texture2D EggTexture = GD.Load<Texture2D>("res://Assets/Sprout Lands - Sprites - Basic pack/Objects/Egg item.png");
    private static readonly Texture2D TreatTexture = GD.Load<Texture2D>(
        "res://Assets/Sprout Lands - Sprites - premium pack/Objects/Items/fruit-n-berries-items.png");

    /// <summary>One occupied slot: its art, how many, and the single thing the player can do with it.</summary>
    private sealed record Slot(string Key, string Name, string Detail, int Count, Color Tint, Func<Control> Art, string? ActionText, Action? Action);

    private InventoryScreenState? _state;
    private string _category = TreatsCategory;
    private string _selection = string.Empty;
    private VBoxContainer _categories = null!;
    private GridContainer _grid = null!;
    private VBoxContainer _detail = null!;

    public void Configure(InventoryScreenState state)
    {
        if (IsInsideTree()) throw new InvalidOperationException("InventoryScreen must be configured before it enters the scene tree.");
        _state = state ?? throw new ArgumentNullException(nameof(state));
    }

    public override void _Ready()
    {
        if (_state == null) throw new InvalidOperationException("InventoryScreen must be configured before AddChild.");
        Name = "Satchel";
        AddThemeConstantOverride("separation", 6);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;

        _categories = new VBoxContainer { Name = "Categories", CustomMinimumSize = new Vector2(82, 0) };
        _categories.AddThemeConstantOverride("separation", 5);
        AddChild(_categories);

        var gridPanel = PaperCard.Panel(new Vector2(238, 0));
        gridPanel.Name = "Slots";
        gridPanel.SizeFlagsVertical = SizeFlags.ShrinkBegin;
        AddChild(gridPanel);
        _grid = new GridContainer { Columns = SlotColumns };
        _grid.AddThemeConstantOverride("h_separation", 3);
        _grid.AddThemeConstantOverride("v_separation", 3);
        gridPanel.AddChild(_grid);

        var detailPanel = PaperCard.Panel(new Vector2(145, 225));
        detailPanel.Name = "ItemDetail";
        AddChild(detailPanel);
        _detail = new VBoxContainer();
        _detail.AddThemeConstantOverride("separation", 5);
        detailPanel.AddChild(_detail);

        NormalizeCategory();
        RebuildCategories();
        RebuildSlots();
    }

    public void FocusSelection()
        => (_grid.GetChildren().OfType<Button>().FirstOrDefault(button => button.ButtonPressed)
            ?? _categories.GetChildren().OfType<Button>().FirstOrDefault())?.GrabFocus();

    // ---- categories -----------------------------------------------------------------------

    /// <summary>A category is offered only when the save holds something in it.</summary>
    private IEnumerable<string> AvailableCategories()
    {
        if (_state!.Items.Any(item => !item.UsesEggIcon && item.Count > 0)) yield return TreatsCategory;
        if (_state.StoredEggs.Count > 0 || _state.IncubatingEggs.Count > 0 ||
            _state.FailedEggs.Count > 0 || _state.IncubationSkipCount > 0) yield return EggsCategory;
        if (_state.StoredLand.Count > 0) yield return LandCategory;
        if (_state.EggShells.Count > 0) yield return ShellsCategory;
    }

    private void NormalizeCategory()
    {
        var available = AvailableCategories().ToArray();
        if (available.Length == 0) { _category = TreatsCategory; return; }
        if (!available.Contains(_category)) _category = available[0];
    }

    private void RebuildCategories()
    {
        PaperCard.Clear(_categories);
        foreach (var category in AvailableCategories())
        {
            var button = UiFactory.CreateButton(Tr("UI_INVENTORY_CATEGORY_" + category.ToUpperInvariant()));
            button.Name = "Category" + category;
            button.ToggleMode = true;
            button.ButtonPressed = category == _category;
            button.CustomMinimumSize = new Vector2(82, 28);
            button.Alignment = HorizontalAlignment.Left;
            UiFactory.ApplyPixelFont(button, 6);
            var captured = category;
            button.Pressed += () =>
            {
                _category = captured;
                _selection = string.Empty;
                foreach (var other in _categories.GetChildren().OfType<Button>())
                    other.ButtonPressed = other.Name == "Category" + _category;
                RebuildSlots();
                FocusSelection();
            };
            _categories.AddChild(button);
        }
    }

    // ---- slots ----------------------------------------------------------------------------

    private void RebuildSlots()
    {
        PaperCard.Clear(_grid);
        PaperCard.Clear(_detail);
        var slots = Slots().ToArray();
        if (slots.Length == 0)
        {
            for (var filler = 0; filler < SlotColumns * SlotRows; filler++) _grid.AddChild(PaperCard.EmptySlot(SlotSize));
            _detail.AddChild(UiFactory.CreateLabel(Tr("UI_INVENTORY_EMPTY"), 7));
            return;
        }
        if (slots.All(slot => slot.Key != _selection)) _selection = slots[0].Key;

        foreach (var slot in slots) _grid.AddChild(BuildSlot(slot));
        for (var filler = slots.Length; filler < SlotColumns * SlotRows; filler++)
            _grid.AddChild(PaperCard.EmptySlot(SlotSize));

        BuildDetail(slots.First(slot => slot.Key == _selection));
    }

    private Button BuildSlot(Slot slot)
    {
        var button = UiFactory.CreateButton(string.Empty);
        button.Name = "Slot_" + slot.Key.Replace(':', '_');
        button.ToggleMode = true;
        button.ButtonPressed = slot.Key == _selection;
        button.CustomMinimumSize = SlotSize;
        button.TooltipText = slot.Name;
        var art = slot.Art();
        // Drawn land footprints carry their own size; sprites take the slot's default.
        var artSize = art.CustomMinimumSize == Vector2.Zero ? new Vector2(30, 30) : art.CustomMinimumSize;
        art.CustomMinimumSize = artSize;
        art.Size = artSize;
        art.Position = ((SlotSize - artSize) * 0.5f) - new Vector2(0, 4);
        art.MouseFilter = MouseFilterEnum.Ignore;
        button.AddChild(art);
        if (slot.Count > 1)
        {
            var count = UiFactory.CreateLabel("x" + slot.Count, 6);
            count.Position = new Vector2(2, 36);
            count.Size = new Vector2(48, 12);
            count.HorizontalAlignment = HorizontalAlignment.Right;
            count.MouseFilter = MouseFilterEnum.Ignore;
            button.AddChild(count);
        }
        if (slot.Key == _selection) button.AddChild(PaperCard.Star(new Vector2(34, 0), 14));
        button.Pressed += () => { _selection = slot.Key; RebuildSlots(); FocusSelection(); };
        return button;
    }

    private void BuildDetail(Slot slot)
    {
        var artPanel = new PanelContainer { CustomMinimumSize = new Vector2(0, 56) };
        artPanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = Color.FromHtml("#F7E5BD"),
            CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3,
            CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3
        });
        var center = new CenterContainer();
        var art = slot.Art();
        if (art.CustomMinimumSize == Vector2.Zero) art.CustomMinimumSize = new Vector2(46, 46);
        center.AddChild(art);
        artPanel.AddChild(center);
        _detail.AddChild(artPanel);

        var name = UiFactory.CreateLabel(slot.Name, 9);
        name.Name = "DetailName";
        name.HorizontalAlignment = HorizontalAlignment.Center;
        name.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        name.AddThemeColorOverride("font_color", PaperCard.Ink(slot.Tint));
        _detail.AddChild(name);

        var detail = UiFactory.CreateLabel(slot.Detail, 7);
        detail.HorizontalAlignment = HorizontalAlignment.Center;
        _detail.AddChild(detail);

        _detail.AddChild(new ColorRect
        {
            Color = Color.FromHtml("#B7926F"),
            CustomMinimumSize = new Vector2(1, 1),
            MouseFilter = MouseFilterEnum.Ignore
        });
        _detail.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });

        if (slot.ActionText == null || slot.Action == null) return;
        var action = UiFactory.CreateButton(slot.ActionText);
        action.Name = "ItemAction";
        action.CustomMinimumSize = new Vector2(125, 28);
        UiFactory.ApplyPrimaryStyle(action);
        action.Pressed += slot.Action;
        _detail.AddChild(action);
    }

    // ---- what each category holds ---------------------------------------------------------

    private IEnumerable<Slot> Slots()
    {
        if (_category == TreatsCategory)
        {
            var index = 0;
            foreach (var item in _state!.Items.Where(candidate => !candidate.UsesEggIcon))
            {
                var iconIndex = index++;
                if (item.Count <= 0) continue;
                yield return new Slot("treat:" + iconIndex, item.DisplayName,
                    string.Format(Tr("UI_SHOP_OWNED"), item.Count), item.Count, Color.FromHtml("#6B8F5E"),
                    () => AtlasArt(TreatTexture, new Rect2(iconIndex % 4 * 16, iconIndex / 4 * 16, 16, 16)), null, null);
            }
            yield break;
        }
        if (_category == EggsCategory)
        {
            foreach (var egg in _state!.StoredEggs)
            {
                var captured = egg;
                yield return new Slot("egg:" + egg.EggId, egg.DisplayName, Tr("UI_INVENTORY_UNPLACED"), 1, egg.TintColor,
                    () => TintedEgg(captured.TintColor), Tr("UI_INVENTORY_PLACE"),
                    () => PlaceStoredEggRequested?.Invoke(captured));
            }
            foreach (var egg in _state.IncubatingEggs)
            {
                var captured = egg;
                var canSkip = _state.IncubationSkipCount > 0;
                yield return new Slot("incubating:" + egg.EggId, egg.DisplayName,
                    string.Format(Tr("UI_INVENTORY_INCUBATING"), egg.SecondsRemaining), 1, Color.FromHtml("#7C8F5E"),
                    () => TintedEgg(new Color(1, 1, 1)),
                    canSkip ? string.Format(Tr("UI_INVENTORY_USE_SKIP"), _state.IncubationSkipCount) : null,
                    canSkip ? () => UseIncubationSkipRequested?.Invoke(captured.EggId) : null);
            }
            foreach (var failed in _state.FailedEggs)
            {
                var captured = failed;
                yield return new Slot("failed:" + failed.EggId, failed.DisplayName, Tr("UI_INVENTORY_FAILED_EGGS"), 1,
                    Color.FromHtml("#9C514B"), () => TintedEgg(Color.FromHtml("#9C514B")),
                    Tr("UI_INVENTORY_DISCARD"), () => DiscardFailedEggRequested?.Invoke(captured.EggId));
            }
            yield break;
        }
        if (_category == LandCategory)
        {
            foreach (var land in _state!.StoredLand)
            {
                var captured = land;
                yield return new Slot("land:" + land.ModuleId, land.DisplayName, Tr("UI_LAND_STORED"), 1, land.Tint,
                    () => LandShapePresentation.CreateShapeArt(captured.ShapeId, captured.Tint), Tr("UI_INVENTORY_PLACE"),
                    () => PlaceStoredLandRequested?.Invoke(captured));
            }
            yield break;
        }
        foreach (var shell in _state!.EggShells)
        {
            var captured = shell;
            yield return new Slot("shell:" + shell.ShellId, shell.DisplayName,
                string.Format(Tr("UI_INVENTORY_SHELL_VALUE"), shell.SaleValue), 1, Color.FromHtml("#8A7A5A"),
                () => TintedEgg(new Color(0.78f, 0.74f, 0.66f)), string.Format(Tr("UI_INVENTORY_SELL"), shell.SaleValue),
                () => SellEggShellRequested?.Invoke(captured.ShellId));
        }
    }

    // ---- art ------------------------------------------------------------------------------

    private static TextureRect AtlasArt(Texture2D atlas, Rect2 region)
        => Art(new AtlasTexture { Atlas = atlas, Region = region });

    private static TextureRect TintedEgg(Color tint)
    {
        var art = Art(EggTexture);
        art.SelfModulate = tint;
        return art;
    }

    private static TextureRect Art(Texture2D texture) => new()
    {
        Texture = texture,
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        MouseFilter = MouseFilterEnum.Ignore
    };

}
