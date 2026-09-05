using System;
using System.Linq;
using Godot;
using Voidling.Application.Garden;
using Voidling.Presentation.UI.Common;

namespace VoidlingGame;

public partial class MainController
{
    /// <summary>
    /// Land ledger. Buying happens in the Shop and placing happens in the Garden, so this modal
    /// only reports what the island holds and opens the ground menu for a hex.
    /// </summary>
    private void ShowGardenModules()
    {
        var state = _session.State;
        var box = OpenModal(Tr("UI_LAND_TITLE"), new Vector2(510, 320));
        box.AddThemeConstantOverride("separation", 5);

        var placed = state.GardenModules.Where(module => module.Placed).ToList();
        var training = placed.Count(module => module.StatId.Length > 0);
        var summary = UiFactory.CreateLabel(
            string.Format(
                Tr("UI_LAND_SUMMARY"),
                placed.Count,
                training,
                state.GardenModules.Count - placed.Count,
                state.Coins),
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

        foreach (var module in placed
                     .OrderByDescending(module => module.StatId.Length > 0)
                     .ThenBy(module => module.HexR)
                     .ThenBy(module => module.HexQ))
        {
            list.AddChild(CreateLandRow(module));
        }

        foreach (var module in state.GardenModules.Where(module => !module.Placed)
                     .OrderBy(module => module.ShapeId, StringComparer.Ordinal))
        {
            list.AddChild(CreateStoredLandRow(module));
        }
    }

    private Control CreateLandRow(GardenModuleData module)
    {
        var row = UiFactory.CreatePanel(new Vector2(468, 42));
        row.CustomMinimumSize = new Vector2(468, 42);

        var controls = new HBoxContainer();
        controls.AddThemeConstantOverride("separation", 5);
        row.AddChild(controls);

        var trainingGround = module.StatId.Length > 0;
        var rate = GameRules.GardenModuleRules.PointsPerMinuteForLevel(module.Level);
        var assignedCount = _session.State.Voidlings.Count(creature =>
            string.Equals(creature.PassiveTrainingModuleId, module.Id, StringComparison.Ordinal));
        var label = UiFactory.CreateLabel(
            trainingGround
                ? $"{StatPresentationCatalog.NameFor(module.StatId).ToUpperInvariant()}  L{module.Level}  {rate:0.#}/min  •  {assignedCount} training"
                : Tr("UI_LAND_PLAIN_GROUND"),
            7);
        label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        label.VerticalAlignment = VerticalAlignment.Center;
        label.AddThemeColorOverride(
            "font_color",
            trainingGround ? StatPresentationCatalog.ColorFor(module.StatId) : Color.FromHtml("#6B8F5E"));
        controls.AddChild(label);

        var location = UiFactory.CreateLabel($"({module.HexQ}, {module.HexR})", 6);
        location.CustomMinimumSize = new Vector2(70, 30);
        location.VerticalAlignment = VerticalAlignment.Center;
        controls.AddChild(location);

        var capturedModuleId = module.Id;
        var open = UiFactory.CreateButton(Tr(trainingGround ? "UI_LAND_MANAGE" : "UI_LAND_BUILD"));
        open.CustomMinimumSize = new Vector2(96, 24);
        UiFactory.ApplyPixelFont(open, 6);
        open.Pressed += () => ShowLandHexMenu(capturedModuleId);
        controls.AddChild(open);
        return row;
    }

    private Control CreateStoredLandRow(GardenModuleData module)
    {
        var row = UiFactory.CreatePanel(new Vector2(468, 42));
        row.CustomMinimumSize = new Vector2(468, 42);

        var controls = new HBoxContainer();
        controls.AddThemeConstantOverride("separation", 5);
        row.AddChild(controls);

        var label = UiFactory.CreateLabel(
            string.Format(
                Tr("UI_INVENTORY_LAND_TILE"),
                LandShapePresentation.NameFor(module.ShapeId),
                LandShapePresentation.HexCountOf(module.ShapeId)),
            7);
        label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        label.VerticalAlignment = VerticalAlignment.Center;
        controls.AddChild(label);

        var stored = UiFactory.CreateLabel(Tr("UI_LAND_STORED"), 6);
        stored.CustomMinimumSize = new Vector2(70, 30);
        stored.VerticalAlignment = VerticalAlignment.Center;
        controls.AddChild(stored);

        var capturedModuleId = module.Id;
        var capturedShapeId = module.ShapeId;
        var place = UiFactory.CreateButton(Tr("UI_INVENTORY_PLACE"));
        place.CustomMinimumSize = new Vector2(96, 24);
        UiFactory.ApplyPixelFont(place, 6);
        place.Pressed += () =>
        {
            CloseModal();
            _garden.BeginLandPlacement(capturedModuleId, capturedShapeId);
        };
        controls.AddChild(place);
        return row;
    }

    /// <summary>
    /// The ground menu for one hex: plain grass offers the training grounds it could become,
    /// training ground reports what it does and offers its upgrade.
    /// </summary>
    private void ShowLandHexMenu(string moduleId)
    {
        var module = _session.State.GardenModules.FirstOrDefault(candidate =>
            candidate.Placed && string.Equals(candidate.Id, moduleId, StringComparison.Ordinal));
        if (module == null)
            return;

        var trainingGround = module.StatId.Length > 0;
        var box = OpenModal(Tr(trainingGround ? "UI_LAND_HEX_TITLE" : "UI_LAND_HEX_EMPTY_TITLE"), new Vector2(420, 250));
        box.AddThemeConstantOverride("separation", 5);

        if (!trainingGround)
        {
            var cost = GameRules.GardenModuleRules.TrainingConversionCost;
            var intro = UiFactory.CreateLabel(string.Format(Tr("UI_LAND_BUILD_PROMPT"), cost), 7);
            intro.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            intro.CustomMinimumSize = new Vector2(396, 30);
            box.AddChild(intro);

            var grid = new GridContainer { Columns = 3 };
            grid.AddThemeConstantOverride("h_separation", 4);
            grid.AddThemeConstantOverride("v_separation", 4);
            box.AddChild(grid);

            foreach (var statId in GameRules.StatIds)
            {
                var capturedStatId = statId;
                var button = UiFactory.CreateButton(StatPresentationCatalog.NameFor(statId).ToUpperInvariant());
                button.CustomMinimumSize = new Vector2(124, 28);
                UiFactory.ApplyPixelFont(button, 7);
                button.AddThemeColorOverride("font_color", StatPresentationCatalog.ColorFor(statId).Darkened(0.4f));
                button.Disabled = _session.State.Coins < cost;
                button.Pressed += () =>
                {
                    if (_session.ConvertHexToTrainingGround(moduleId, capturedStatId))
                        CloseModal();
                };
                grid.AddChild(button);
            }

            return;
        }

        var rate = GameRules.GardenModuleRules.PointsPerMinuteForLevel(module.Level);
        var residents = _session.State.Voidlings
            .Where(creature => string.Equals(creature.PassiveTrainingModuleId, moduleId, StringComparison.Ordinal))
            .Select(creature => creature.Name)
            .ToList();

        var detail = UiFactory.CreateLabel(
            $"{StatPresentationCatalog.NameFor(module.StatId).ToUpperInvariant()}  •  L{module.Level}  •  {rate:0.#}/min",
            8);
        detail.AddThemeColorOverride("font_color", StatPresentationCatalog.ColorFor(module.StatId));
        box.AddChild(detail);

        var occupancy = UiFactory.CreateLabel(
            residents.Count > 0
                ? string.Format(Tr("UI_LAND_HEX_RESIDENT"), string.Join(", ", residents))
                : Tr("UI_LAND_HEX_VACANT"),
            7);
        occupancy.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        occupancy.CustomMinimumSize = new Vector2(396, 30);
        box.AddChild(occupancy);

        var upgradeCost = GameRules.GardenModuleRules.UpgradeCostForLevel(module.Level);
        var upgrade = UiFactory.CreateButton(
            upgradeCost < 0 ? Tr("UI_LAND_MAX_LEVEL") : string.Format(Tr("UI_LAND_UPGRADE"), upgradeCost));
        upgrade.CustomMinimumSize = new Vector2(160, 28);
        UiFactory.ApplyPixelFont(upgrade, 7);
        upgrade.Disabled = upgradeCost < 0 || _session.State.Coins < upgradeCost;
        upgrade.Pressed += () =>
        {
            if (_session.UpgradeGardenModule(moduleId))
                CallDeferred(nameof(ShowLandHexMenu), moduleId);
        };
        box.AddChild(upgrade);
    }
}
