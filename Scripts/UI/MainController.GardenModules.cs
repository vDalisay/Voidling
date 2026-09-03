using System;
using System.Linq;
using Godot;
using Voidling.Presentation.UI.Common;

namespace VoidlingGame;

public partial class MainController
{
    /// <summary>
    /// Land ledger. Buying happens in the Shop and placing happens in the Garden, so this modal
    /// only reports what the island holds and lets the player upgrade a tile.
    /// </summary>
    private void ShowGardenModules()
    {
        var rules = GameRules.GardenModuleRules;
        var state = _session.State;
        var box = OpenModal(Tr("UI_LAND_TITLE"), new Vector2(510, 320));
        box.AddThemeConstantOverride("separation", 5);

        var placed = state.GardenModules.Count(module => module.Placed);
        var summary = UiFactory.CreateLabel(
            string.Format(Tr("UI_LAND_SUMMARY"), placed, state.GardenModules.Count - placed, state.Coins),
            7);
        summary.TooltipText = Tr("UI_LAND_HINT");
        box.AddChild(summary);

        var hint = UiFactory.CreateLabel(Tr("UI_LAND_HINT"), 6);
        hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        hint.CustomMinimumSize = new Vector2(486, 26);
        hint.AddThemeColorOverride("font_color", Color.FromHtml("#6B4B34"));
        box.AddChild(hint);

        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(486, 210),
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
            var empty = UiFactory.CreateLabel(Tr("UI_LAND_EMPTY"), 7);
            empty.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            list.AddChild(empty);
            return;
        }

        foreach (var module in state.GardenModules.OrderByDescending(module => module.Placed)
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
                $"{StatPresentationCatalog.NameFor(module.StatId).ToUpperInvariant()}  L{module.Level}  {rate:0.#}/min  •  {assignedCount} training",
                7);
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            label.VerticalAlignment = VerticalAlignment.Center;
            label.AddThemeColorOverride("font_color", StatPresentationCatalog.ColorFor(module.StatId));
            controls.AddChild(label);

            var location = UiFactory.CreateLabel(
                module.Placed ? $"({module.HexQ}, {module.HexR})" : Tr("UI_LAND_STORED"),
                6);
            location.CustomMinimumSize = new Vector2(96, 30);
            location.VerticalAlignment = VerticalAlignment.Center;
            controls.AddChild(location);

            var upgradeCost = rules.UpgradeCostForLevel(module.Level);
            var capturedModuleId = module.Id;
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
