using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Voidling.Domain.Garden;
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

public partial class InventoryScreen : VBoxContainer
{
    public event Action<string>? DiscardFailedEggRequested;
    public event Action<string>? SellEggShellRequested;
    public event Action<string>? UseIncubationSkipRequested;
    public event Action<StoredEggViewState>? PlaceStoredEggRequested;
    public event Action<StoredLandViewState>? PlaceStoredLandRequested;
    private static readonly Texture2D EggTexture = GD.Load<Texture2D>("res://Assets/Sprout Lands - Sprites - Basic pack/Objects/Egg item.png");
    private InventoryScreenState? _state;

    public void Configure(InventoryScreenState state)
    {
        if (IsInsideTree()) throw new InvalidOperationException("InventoryScreen must be configured before it enters the scene tree.");
        _state = state ?? throw new ArgumentNullException(nameof(state));
    }

    public override void _Ready()
    {
        if (_state == null) throw new InvalidOperationException("InventoryScreen must be configured before AddChild.");
        AddThemeConstantOverride("separation", 7); SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; AddChild(UiFactory.CreateLabel(Tr("UI_INVENTORY_SUBTITLE"), 9));
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(340, 198), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill }; AddChild(scroll);
        UiFactory.StyleScroll(scroll);
        var list = new VBoxContainer(); list.AddThemeConstantOverride("separation", 5); scroll.AddChild(list);
        foreach (var item in _state.Items) list.AddChild(CreateInventoryRow(CreateItemIcon(item), item.DisplayName, item.Count));
        if (_state.StoredEggs.Count > 0)
        {
            list.AddChild(UiFactory.CreateLabel(Tr("UI_INVENTORY_STORED_EGGS"), 8));
            list.AddChild(UiFactory.CreateLabel(Tr("UI_INVENTORY_PLACE_HINT"), 6));
            foreach (var egg in _state.StoredEggs) list.AddChild(CreateStoredEggRow(egg));
        }
        if (_state.StoredLand.Count > 0)
        {
            list.AddChild(UiFactory.CreateLabel(Tr("UI_INVENTORY_LAND"), 8));
            list.AddChild(UiFactory.CreateLabel(Tr("UI_INVENTORY_LAND_HINT"), 6));
            foreach (var land in _state.StoredLand) list.AddChild(CreateStoredLandRow(land));
        }
        if (_state.IncubationSkipCount > 0)
        {
            list.AddChild(UiFactory.CreateLabel($"INCUBATION SKIPS  x{_state.IncubationSkipCount}", 8));
            if (_state.IncubatingEggs.Count == 0) list.AddChild(UiFactory.CreateLabel("No egg currently needs an incubation skip.", 6));
            else foreach (var egg in _state.IncubatingEggs) list.AddChild(CreateIncubationSkipRow(egg));
        }
        if (_state.EggShells.Count > 0)
        {
            list.AddChild(UiFactory.CreateLabel("EGGSHELLS", 8));
            foreach (var shell in _state.EggShells) list.AddChild(CreateEggShellRow(shell));
        }
        if (_state.FailedEggs.Count <= 0) return;
        var failedTitle = UiFactory.CreateLabel(Tr("UI_INVENTORY_FAILED_EGGS"), 8); failedTitle.AddThemeColorOverride("font_color", Color.FromHtml("#9C514B")); list.AddChild(failedTitle);
        foreach (var failedEgg in _state.FailedEggs) list.AddChild(CreateFailedEggRow(failedEgg));
    }

    private static Texture2D CreateItemIcon(InventoryItemViewState item) => !item.UsesEggIcon ? UiFactory.CreateIcon(item.IconIndex) : CreateEggTexture();
    private static AtlasTexture CreateEggTexture() => new() { Atlas = EggTexture, Region = new Rect2(0, 0, EggTexture.GetWidth(), EggTexture.GetHeight()) };

    private static Control CreateInventoryRow(Texture2D iconTexture, string itemName, int count)
    {
        var panel = CreateRowPanel(); var row = CreateRow(panel); row.AddChild(CreateRowIcon(iconTexture)); var name = UiFactory.CreateLabel(itemName, 8); name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; name.VerticalAlignment = VerticalAlignment.Center; row.AddChild(name); var amount = UiFactory.CreateLabel($"x{count}", 9); amount.CustomMinimumSize = new Vector2(42, 20); amount.HorizontalAlignment = HorizontalAlignment.Right; amount.VerticalAlignment = VerticalAlignment.Center; row.AddChild(amount); return panel;
    }

    private Control CreateStoredEggRow(StoredEggViewState egg)
    {
        var panel = CreateRowPanel(); var row = CreateRow(panel); var icon = CreateRowIcon(CreateEggTexture()); icon.Modulate = egg.TintColor; row.AddChild(icon); var name = UiFactory.CreateLabel(egg.DisplayName, 8); name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; name.VerticalAlignment = VerticalAlignment.Center; row.AddChild(name); var place = UiFactory.CreateButton(Tr("UI_INVENTORY_PLACE")); place.CustomMinimumSize = new Vector2(76, 22); UiFactory.ApplyPixelFont(place, 7); place.Pressed += () => PlaceStoredEggRequested?.Invoke(egg); row.AddChild(place); return panel;
    }

    private Control CreateStoredLandRow(StoredLandViewState land)
    {
        var panel = CreateRowPanel(); var row = CreateRow(panel); row.AddChild(CreateShapeIcon(land.ShapeId, land.Tint)); var name = UiFactory.CreateLabel(land.DisplayName, 8); name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; name.VerticalAlignment = VerticalAlignment.Center; row.AddChild(name); var place = UiFactory.CreateButton(Tr("UI_INVENTORY_PLACE")); place.CustomMinimumSize = new Vector2(76, 22); UiFactory.ApplyPixelFont(place, 7); place.Pressed += () => PlaceStoredLandRequested?.Invoke(land); row.AddChild(place); return panel;
    }

    /// <summary>
    /// The footprint of the piece, scaled to fit and coloured by the ground it carries, so two
    /// rows in the inventory are told apart at a glance instead of by their text alone.
    /// </summary>
    private static Control CreateShapeIcon(string shapeId, Color tint)
    {
        var shape = GardenTileShape.Find(shapeId) ?? GardenTileShape.Single;
        const float boxWidth = 40.0f;
        const float boxHeight = 26.0f;
        const float ratio = 1.7f;

        var units = shape.Cells.Select(cell => new Vector2(1.5f * cell.Q, cell.R + cell.Q * 0.5f)).ToArray();
        var spanX = units.Max(unit => unit.X) - units.Min(unit => unit.X) + 2.0f;
        var spanY = units.Max(unit => unit.Y) - units.Min(unit => unit.Y) + 1.0f;
        var topEdge = Mathf.Min(boxWidth / spanX, boxHeight / (ratio * spanY));
        var height = topEdge * ratio;

        var centers = units.Select(unit => new Vector2(unit.X * topEdge, unit.Y * height)).ToArray();
        var middle = new Vector2(
            (centers.Max(center => center.X) + centers.Min(center => center.X)) * 0.5f,
            (centers.Max(center => center.Y) + centers.Min(center => center.Y)) * 0.5f);
        var origin = new Vector2(boxWidth * 0.5f, boxHeight * 0.5f) - middle;

        var holder = new Control { CustomMinimumSize = new Vector2(boxWidth, boxHeight), MouseFilter = Control.MouseFilterEnum.Ignore };
        foreach (var center in centers)
        {
            var polygon = HexShape.Corners(topEdge, height); for (var i = 0; i < polygon.Length; i++) polygon[i] += origin + center;
            var outline = HexShape.Outline(topEdge, height); for (var i = 0; i < outline.Length; i++) outline[i] += origin + center;
            holder.AddChild(new Polygon2D { Polygon = polygon, Color = tint });
            holder.AddChild(new Line2D { Points = outline, DefaultColor = tint.Darkened(0.45f), Width = 1.0f, JointMode = Line2D.LineJointMode.Round });
        }
        return holder;
    }

    private Control CreateIncubationSkipRow(IncubatingEggViewState egg)
    {
        var panel = CreateRowPanel(); var row = CreateRow(panel); row.AddChild(CreateRowIcon(CreateEggTexture())); var name = UiFactory.CreateLabel($"{egg.DisplayName} • {egg.SecondsRemaining}s", 7); name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; name.VerticalAlignment = VerticalAlignment.Center; row.AddChild(name); var use = UiFactory.CreateButton("Use Skip"); use.CustomMinimumSize = new Vector2(76, 22); UiFactory.ApplyPixelFont(use, 7); use.Pressed += () => UseIncubationSkipRequested?.Invoke(egg.EggId); row.AddChild(use); return panel;
    }

    private Control CreateEggShellRow(EggShellViewState shell)
    {
        var panel = CreateRowPanel(); var row = CreateRow(panel); row.AddChild(CreateRowIcon(CreateEggTexture())); var name = UiFactory.CreateLabel(shell.DisplayName, 8); name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; name.VerticalAlignment = VerticalAlignment.Center; row.AddChild(name); var sell = UiFactory.CreateButton($"Sell +{shell.SaleValue}"); sell.CustomMinimumSize = new Vector2(76, 22); UiFactory.ApplyPixelFont(sell, 7); sell.Pressed += () => SellEggShellRequested?.Invoke(shell.ShellId); row.AddChild(sell); return panel;
    }

    private Control CreateFailedEggRow(FailedEggViewState failedEgg)
    {
        var panel = CreateRowPanel(); var row = CreateRow(panel); row.AddChild(CreateRowIcon(CreateEggTexture())); var name = UiFactory.CreateLabel(failedEgg.DisplayName, 8); name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; name.VerticalAlignment = VerticalAlignment.Center; name.AddThemeColorOverride("font_color", Color.FromHtml("#9C514B")); row.AddChild(name); var discard = UiFactory.CreateButton(Tr("UI_INVENTORY_DISCARD")); discard.CustomMinimumSize = new Vector2(66, 22); UiFactory.ApplyPixelFont(discard, 7); discard.Pressed += () => DiscardFailedEggRequested?.Invoke(failedEgg.EggId); row.AddChild(discard); return panel;
    }

    private static PanelContainer CreateRowPanel()
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(328, 32) }; var style = new StyleBoxFlat { BgColor = Color.FromHtml("#F0D9A8"), BorderColor = Color.FromHtml("#C59670") }; style.SetBorderWidthAll(1); style.ContentMarginLeft = style.ContentMarginRight = 7; style.ContentMarginTop = style.ContentMarginBottom = 4; panel.AddThemeStyleboxOverride("panel", style); return panel;
    }
    private static HBoxContainer CreateRow(PanelContainer panel) { var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 8); panel.AddChild(row); return row; }
    private static TextureRect CreateRowIcon(Texture2D iconTexture) => new() { Texture = iconTexture, CustomMinimumSize = new Vector2(22, 22), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = Control.MouseFilterEnum.Ignore };
}
