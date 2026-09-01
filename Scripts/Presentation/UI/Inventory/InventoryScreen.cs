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

public readonly record struct FailedEggViewState(string EggId, string DisplayName);
public readonly record struct EggShellViewState(string ShellId, string DisplayName, int SaleValue);
public readonly record struct IncubatingEggViewState(string EggId, string DisplayName, int SecondsRemaining);

public sealed record InventoryScreenState(
    IReadOnlyList<InventoryItemViewState> Items,
    IReadOnlyList<FailedEggViewState> FailedEggs,
    IReadOnlyList<EggShellViewState> EggShells,
    int IncubationSkipCount,
    IReadOnlyList<IncubatingEggViewState> IncubatingEggs);

/// <summary>
/// Inventory view over a supplied snapshot. It emits cleanup/sale/utility-use intent rather than
/// reaching into GameSession or mutating authoritative egg state itself.
/// </summary>
public partial class InventoryScreen : VBoxContainer
{
    public event Action<string>? DiscardFailedEggRequested;
    public event Action<string>? SellEggShellRequested;
    public event Action<string>? UseIncubationSkipRequested;

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

        if (_state.IncubationSkipCount > 0)
        {
            list.AddChild(UiFactory.CreateLabel($"INCUBATION SKIPS  x{_state.IncubationSkipCount}", 8));
            if (_state.IncubatingEggs.Count == 0)
            {
                list.AddChild(UiFactory.CreateLabel("No egg currently needs an incubation skip.", 6));
            }
            else
            {
                foreach (var egg in _state.IncubatingEggs)
                    list.AddChild(CreateIncubationSkipRow(egg));
            }
        }

        if (_state.EggShells.Count > 0)
        {
            list.AddChild(UiFactory.CreateLabel("EGGSHELLS", 8));
            foreach (var shell in _state.EggShells)
                list.AddChild(CreateEggShellRow(shell));
        }

        if (_state.FailedEggs.Count <= 0)
            return;

        var failedTitle = UiFactory.CreateLabel(Tr("UI_INVENTORY_FAILED_EGGS"), 8);
        failedTitle.AddThemeColorOverride("font_color", Color.FromHtml("#9C514B"));
        list.AddChild(failedTitle);

        foreach (var failedEgg in _state.FailedEggs)
            list.AddChild(CreateFailedEggRow(failedEgg));
    }

    private static Texture2D CreateItemIcon(InventoryItemViewState item)
    {
        if (!item.UsesEggIcon)
            return UiFactory.CreateIcon(item.IconIndex);

        return CreateEggTexture();
    }

    private static AtlasTexture CreateEggTexture()
        => new()
        {
            Atlas = EggTexture,
            Region = new Rect2(0, 0, EggTexture.GetWidth(), EggTexture.GetHeight())
        };

    private static Control CreateInventoryRow(Texture2D iconTexture, string itemName, int count)
    {
        var panel = CreateRowPanel();
        var row = CreateRow(panel);
        row.AddChild(CreateRowIcon(iconTexture));

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

    private Control CreateIncubationSkipRow(IncubatingEggViewState egg)
    {
        var panel = CreateRowPanel();
        var row = CreateRow(panel);
        row.AddChild(CreateRowIcon(CreateEggTexture()));

        var name = UiFactory.CreateLabel($"{egg.DisplayName} • {egg.SecondsRemaining}s", 7);
        name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        name.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(name);

        var use = UiFactory.CreateButton("Use Skip");
        use.CustomMinimumSize = new Vector2(76, 22);
        UiFactory.ApplyPixelFont(use, 7);
        use.Pressed += () => UseIncubationSkipRequested?.Invoke(egg.EggId);
        row.AddChild(use);
        return panel;
    }

    private Control CreateEggShellRow(EggShellViewState shell)
    {
        var panel = CreateRowPanel();
        var row = CreateRow(panel);
        row.AddChild(CreateRowIcon(CreateEggTexture()));

        var name = UiFactory.CreateLabel(shell.DisplayName, 8);
        name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        name.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(name);

        var sell = UiFactory.CreateButton($"Sell +{shell.SaleValue}");
        sell.CustomMinimumSize = new Vector2(76, 22);
        UiFactory.ApplyPixelFont(sell, 7);
        sell.Pressed += () => SellEggShellRequested?.Invoke(shell.ShellId);
        row.AddChild(sell);
        return panel;
    }

    private Control CreateFailedEggRow(FailedEggViewState failedEgg)
    {
        var panel = CreateRowPanel();
        var row = CreateRow(panel);
        row.AddChild(CreateRowIcon(CreateEggTexture()));

        var name = UiFactory.CreateLabel(failedEgg.DisplayName, 8);
        name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        name.VerticalAlignment = VerticalAlignment.Center;
        name.AddThemeColorOverride("font_color", Color.FromHtml("#9C514B"));
        row.AddChild(name);

        var discard = UiFactory.CreateButton(Tr("UI_INVENTORY_DISCARD"));
        discard.CustomMinimumSize = new Vector2(66, 22);
        UiFactory.ApplyPixelFont(discard, 7);
        discard.Pressed += () => DiscardFailedEggRequested?.Invoke(failedEgg.EggId);
        row.AddChild(discard);
        return panel;
    }

    private static PanelContainer CreateRowPanel()
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
        return panel;
    }

    private static HBoxContainer CreateRow(PanelContainer panel)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        panel.AddChild(row);
        return row;
    }

    private static TextureRect CreateRowIcon(Texture2D iconTexture)
        => new()
        {
            Texture = iconTexture,
            CustomMinimumSize = new Vector2(22, 22),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
}
