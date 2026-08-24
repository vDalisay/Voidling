using System;
using System.Collections.Generic;
using Godot;
using VoidlingGame;

namespace Voidling.Presentation.UI.Inventory;

public readonly record struct InventoryItemViewState(
    string DisplayName,
    int Count,
    int IconIndex,
    bool UsesEggIcon = false);

public sealed record InventoryScreenState(IReadOnlyList<InventoryItemViewState> Items);

/// <summary>
/// Read-only inventory view over a supplied snapshot. It owns presentation layout/assets only and
/// has no dependency on GameSession, gameplay rules, persistence, or mutation services.
/// </summary>
public partial class InventoryScreen : VBoxContainer
{
    private static readonly Texture2D EggTexture = GD.Load<Texture2D>(
        "res://Assets/Sprout Lands - Sprites - Basic pack/Objects/Egg item.png");

    private InventoryScreenState? _state;

    public void Configure(InventoryScreenState state)
    {
        if (IsInsideTree())
            throw new InvalidOperationException("InventoryScreen must be configured before it enters the scene tree.");

        _state = state ?? throw new ArgumentNullException(nameof(state));
    }

    public override void _Ready()
    {
        if (_state == null)
            throw new InvalidOperationException("InventoryScreen must be configured before AddChild.");

        AddThemeConstantOverride("separation", 7);
        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        AddChild(UiFactory.CreateLabel(Tr("UI_INVENTORY_SUBTITLE"), 9));

        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(340, 198),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        AddChild(scroll);

        var list = new VBoxContainer();
        list.AddThemeConstantOverride("separation", 5);
        scroll.AddChild(list);

        foreach (var item in _state.Items)
            list.AddChild(CreateInventoryRow(CreateItemIcon(item), item.DisplayName, item.Count));
    }

    private static Texture2D CreateItemIcon(InventoryItemViewState item)
    {
        if (!item.UsesEggIcon)
            return UiFactory.CreateIcon(item.IconIndex);

        return new AtlasTexture
        {
            Atlas = EggTexture,
            Region = new Rect2(0, 0, EggTexture.GetWidth(), EggTexture.GetHeight())
        };
    }

    private static Control CreateInventoryRow(Texture2D iconTexture, string itemName, int count)
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(328, 32) };
        var style = new StyleBoxFlat
        {
            BgColor = Color.FromHtml("#F0D9A8"),
            BorderColor = Color.FromHtml("#C59670")
        };
        style.SetBorderWidthAll(1);
        style.ContentMarginLeft = style.ContentMarginRight = 7;
        style.ContentMarginTop = style.ContentMarginBottom = 4;
        panel.AddThemeStyleboxOverride("panel", style);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        panel.AddChild(row);
        row.AddChild(new TextureRect
        {
            Texture = iconTexture,
            CustomMinimumSize = new Vector2(22, 22),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore
        });

        var name = UiFactory.CreateLabel(itemName, 8);
        name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        name.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(name);

        var amount = UiFactory.CreateLabel($"x{count}", 9);
        amount.CustomMinimumSize = new Vector2(42, 20);
        amount.HorizontalAlignment = HorizontalAlignment.Right;
        amount.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(amount);
        return panel;
    }
}
