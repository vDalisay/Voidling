using System;
using System.Linq;
using Godot;
using Voidling.Presentation.UI.Common;

namespace VoidlingGame;

public partial class MainController
{
    private void ShowGardenModules()
    {
        var rules = GameRules.GardenModuleRules;
        var state = _session.State;
        var box = OpenModal("Garden modules", new Vector2(510, 320));
        box.AddThemeConstantOverride("separation", 5);

        var placed = state.GardenModules.Count(module => module.SlotIndex >= 0);
        var summary = UiFactory.CreateLabel(
            $"Training zones  •  {placed}/{Math.Max(1, rules.SlotCount)} logical slots used  •  {state.Coins} sprouts",
            7);
        summary.TooltipText = "Slot geometry, costs and rates are prototype balance data and remain authorable.";
        box.AddChild(summary);

        var buyLabel = UiFactory.CreateLabel("BUY MODULE", 7);
        buyLabel.AddThemeColorOverride("font_color", Color.FromHtml("#6B4B34"));
        box.AddChild(buyLabel);

        var buyRow = new HBoxContainer();
        buyRow.AddThemeConstantOverride("separation", 4);
        box.AddChild(buyRow);
        foreach (var statId in GameRules.StatIds)
        {
            var capturedStat = statId;
            var buy = UiFactory.CreateButton($"{StatPresentationCatalog.NameFor(statId)} {rules.PurchaseCost}");
            buy.CustomMinimumSize = new Vector2(94, 22);
            UiFactory.ApplyPixelFont(buy, 6);
            buy.Disabled = state.Coins < rules.PurchaseCost;
            buy.Pressed += () =>
            {
                if (_session.BuyGardenModule(capturedStat))
                    CallDeferred(nameof(ShowGardenModules));
            };
            buyRow.AddChild(buy);
        }

        var ownedLabel = UiFactory.CreateLabel("OWNED / PLACED", 7);
        ownedLabel.AddThemeColorOverride("font_color", Color.FromHtml("#6B4B34"));
        box.AddChild(ownedLabel);

        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(486, 176),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        box.AddChild(scroll);

        var list = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(474, 1),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        list.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(list);

        if (state.GardenModules.Count == 0)
        {
            var empty = UiFactory.CreateLabel(
                "No training modules yet. Buy one above, then place it into a logical Garden slot.",
                7);
            empty.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            list.AddChild(empty);
            return;
        }

        foreach (var module in state.GardenModules.OrderBy(module => module.SlotIndex < 0 ? int.MaxValue : module.SlotIndex)
                     .ThenBy(module => module.StatId, StringComparer.Ordinal)
                     .ThenBy(module => module.Id, StringComparer.Ordinal))
        {
            var row = UiFactory.CreatePanel(new Vector2(468, 42));
            row.CustomMinimumSize = new Vector2(468, 42);
            list.AddChild(row);

            var controls = new HBoxContainer();
            controls.AddThemeConstantOverride("separation", 5);
            row.AddChild(controls);

            var rate = rules.PointsPerMinuteForLevel(module.Level);
            var assignedCount = state.Voidlings.Count(creature =>
                string.Equals(creature.PassiveTrainingModuleId, module.Id, StringComparison.Ordinal));
            var label = UiFactory.CreateLabel(
                $"{StatPresentationCatalog.NameFor(module.StatId).ToUpperInvariant()}  L{module.Level}  {rate:0.#}/min  •  {assignedCount} assigned",
                7);
            label.CustomMinimumSize = new Vector2(190, 30);
            label.VerticalAlignment = VerticalAlignment.Center;
            label.AddThemeColorOverride("font_color", StatPresentationCatalog.ColorFor(module.StatId));
            controls.AddChild(label);

            var slot = new OptionButton { CustomMinimumSize = new Vector2(106, 24) };
            StyleOption(slot);
            slot.AddItem("Stored");
            for (var i = 0; i < Math.Max(1, rules.SlotCount); i++)
                slot.AddItem($"Slot {i + 1}");
            slot.Select(Math.Clamp(module.SlotIndex + 1, 0, Math.Max(1, rules.SlotCount)));
            var capturedModuleId = module.Id;
            slot.ItemSelected += index =>
            {
                var targetSlot = (int)index - 1;
                if (_session.PlaceGardenModule(capturedModuleId, targetSlot))
                    CallDeferred(nameof(ShowGardenModules));
            };
            controls.AddChild(slot);

            var upgradeCost = rules.UpgradeCostForLevel(module.Level);
            var upgrade = UiFactory.CreateButton(upgradeCost < 0 ? "MAX" : $"Up {upgradeCost}");
            upgrade.CustomMinimumSize = new Vector2(78, 24);
            UiFactory.ApplyPixelFont(upgrade, 6);
            upgrade.Disabled = upgradeCost < 0 || state.Coins < upgradeCost;
            upgrade.Pressed += () =>
            {
                if (_session.UpgradeGardenModule(capturedModuleId))
                    CallDeferred(nameof(ShowGardenModules));
            };
            controls.AddChild(upgrade);
        }
    }
}
