using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Voidling.Application.Garden;
using Voidling.Presentation.UI.Common;

namespace VoidlingGame;

public partial class MainController
{
    private PanelContainer? _landInspector;
    private string _selectedModuleId = string.Empty;

    private static readonly Vector2 LandSlotSize = new(58, 48);
    private const int LandSlotColumns = 5;
    private const int LandSlotRows = 3;

    /// <summary>
    /// The island's ground: a grid of the pieces you own, each drawn as its own footprint, and one
    /// card carrying the single thing that piece can do. Buying still happens in the Shop and
    /// placing still happens in the Garden; a hex coordinate is never shown, because the shape is
    /// what the player recognises.
    /// </summary>
    private void ShowGardenModules()
    {
        var box = OpenModal(Tr("UI_LAND_TITLE"), new Vector2(520, 292), ShowGardenBuild);
        box.AddThemeConstantOverride("separation", 5);

        var body = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 6);
        box.AddChild(body);

        var gridPanel = PaperCard.Panel(new Vector2(316, 0));
        gridPanel.Name = "LandSlots";
        gridPanel.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
        body.AddChild(gridPanel);
        var grid = new GridContainer { Columns = LandSlotColumns };
        grid.AddThemeConstantOverride("h_separation", 3);
        grid.AddThemeConstantOverride("v_separation", 3);
        gridPanel.AddChild(grid);

        var detailPanel = PaperCard.Panel(new Vector2(172, 232));
        detailPanel.Name = "LandDetail";
        body.AddChild(detailPanel);
        var detail = new VBoxContainer();
        detail.AddThemeConstantOverride("separation", 4);
        detailPanel.AddChild(detail);

        var pieces = LandPieces().ToArray();
        if (pieces.Length == 0)
        {
            for (var filler = 0; filler < LandSlotColumns * LandSlotRows; filler++)
                grid.AddChild(PaperCard.EmptySlot(LandSlotSize));
            detail.AddChild(UiFactory.CreateLabel(Tr("UI_INVENTORY_EMPTY"), 7));
            return;
        }
        if (pieces.All(piece => piece.Id != _selectedModuleId)) _selectedModuleId = pieces[0].Id;

        foreach (var piece in pieces) grid.AddChild(BuildLandSlot(piece));
        for (var filler = pieces.Length; filler < LandSlotColumns * LandSlotRows; filler++)
            grid.AddChild(PaperCard.EmptySlot(LandSlotSize));

        BuildLandDetail(detail, pieces.First(piece => piece.Id == _selectedModuleId));
    }

    /// <summary>Training grounds first, then plain ground, then what is still in storage.</summary>
    private IEnumerable<GardenModuleData> LandPieces()
        => _session.State.GardenModules
            .Where(module => module.Placed)
            .OrderByDescending(module => module.StatId.Length > 0)
            .ThenBy(module => module.HexR)
            .ThenBy(module => module.HexQ)
            .Concat(_session.State.GardenModules
                .Where(module => !module.Placed)
                .OrderBy(module => module.ShapeId, StringComparer.Ordinal));

    private Button BuildLandSlot(GardenModuleData module)
    {
        var trainingGround = module.StatId.Length > 0;
        var tint = LandShapePresentation.TintFor(module.StatId);
        var button = UiFactory.CreateButton(string.Empty);
        button.Name = "Land_" + module.Id;
        button.ToggleMode = true;
        button.ButtonPressed = module.Id == _selectedModuleId;
        button.CustomMinimumSize = LandSlotSize;
        button.TooltipText = trainingGround
            ? StatPresentationCatalog.NameFor(module.StatId)
            : Tr("UI_LAND_PLAIN_GROUND");

        var art = LandShapePresentation.CreateShapeArt(module.ShapeId, tint);
        art.Position = (LandSlotSize - art.CustomMinimumSize) * 0.5f - new Vector2(0, 4);
        // Ground still in storage is the same piece, dimmed, rather than a separate list.
        art.Modulate = new Color(1, 1, 1, module.Placed ? 1.0f : 0.5f);
        button.AddChild(art);

        if (trainingGround)
        {
            var level = UiFactory.CreateLabel("L" + module.Level, 6);
            level.Position = new Vector2(2, 33);
            level.Size = new Vector2(54, 12);
            level.HorizontalAlignment = HorizontalAlignment.Right;
            level.MouseFilter = Control.MouseFilterEnum.Ignore;
            level.AddThemeColorOverride("font_color", PaperCard.Ink(StatPresentationCatalog.ColorFor(module.StatId)));
            button.AddChild(level);
        }
        if (module.Id == _selectedModuleId) button.AddChild(PaperCard.Star(new Vector2(42, 1), 14));

        var capturedId = module.Id;
        button.Pressed += () => { _selectedModuleId = capturedId; CallDeferred(nameof(ShowGardenModules)); };
        return button;
    }

    private void BuildLandDetail(VBoxContainer detail, GardenModuleData module)
    {
        var trainingGround = module.StatId.Length > 0;
        var tint = LandShapePresentation.TintFor(module.StatId);

        var artPanel = new PanelContainer { CustomMinimumSize = new Vector2(0, 54) };
        artPanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = Color.FromHtml("#F7E5BD") });
        var center = new CenterContainer();
        center.AddChild(LandShapePresentation.CreateShapeArt(module.ShapeId, tint));
        artPanel.AddChild(center);
        detail.AddChild(artPanel);

        var name = UiFactory.CreateLabel(
            trainingGround ? StatPresentationCatalog.NameFor(module.StatId).ToUpperInvariant() : Tr("UI_LAND_PLAIN_GROUND"), 9);
        name.Name = "LandName";
        name.HorizontalAlignment = HorizontalAlignment.Center;
        name.AddThemeColorOverride("font_color", PaperCard.Ink(
            trainingGround ? StatPresentationCatalog.ColorFor(module.StatId) : Color.FromHtml("#6B8F5E")));
        detail.AddChild(name);

        if (!module.Placed)
        {
            var stored = UiFactory.CreateLabel(Tr("UI_LAND_STORED"), 7);
            stored.HorizontalAlignment = HorizontalAlignment.Center;
            detail.AddChild(stored);
            detail.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });
            var capturedId = module.Id;
            var capturedShapeId = module.ShapeId;
            var place = UiFactory.CreateButton(Tr("UI_INVENTORY_PLACE"));
            place.Name = "LandAction";
            place.CustomMinimumSize = new Vector2(152, 26);
            UiFactory.ApplyPrimaryStyle(place);
            place.Pressed += () => { CloseModal(); _garden.BeginLandPlacement(capturedId, capturedShapeId); };
            detail.AddChild(place);
            return;
        }

        if (trainingGround)
        {
            detail.AddChild(PaperCard.StarRating(module.Level, GameRules.GardenModuleRules.MaxLevel, 15.0f));
            var rate = UiFactory.CreateLabel(
                string.Format(Tr("UI_LAND_RATE"), GameRules.GardenModuleRules.PointsPerMinuteForLevel(module.Level).ToString("0.#")), 7);
            rate.HorizontalAlignment = HorizontalAlignment.Center;
            detail.AddChild(rate);

            var residents = _session.State.Voidlings
                .Where(creature => string.Equals(creature.PassiveTrainingModuleId, module.Id, StringComparison.Ordinal))
                .Select(creature => creature.Name)
                .ToList();
            var occupancy = UiFactory.CreateLabel(
                residents.Count > 0
                    ? string.Format(Tr("UI_LAND_HEX_RESIDENT"), string.Join(", ", residents))
                    : Tr("UI_LAND_HEX_VACANT"), 6);
            occupancy.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            occupancy.HorizontalAlignment = HorizontalAlignment.Center;
            detail.AddChild(occupancy);
            detail.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });

            var upgradeCost = GameRules.GardenModuleRules.UpgradeCostForLevel(module.Level);
            var capturedId = module.Id;
            var upgrade = UiFactory.CreateButton(
                upgradeCost < 0 ? Tr("UI_LAND_MAX_LEVEL") : string.Format(Tr("UI_LAND_UPGRADE"), upgradeCost));
            upgrade.Name = "LandAction";
            upgrade.CustomMinimumSize = new Vector2(152, 26);
            UiFactory.ApplyPrimaryStyle(upgrade);
            upgrade.Disabled = upgradeCost < 0 || _session.State.Coins < upgradeCost;
            upgrade.Pressed += () =>
            {
                if (_session.UpgradeGardenModule(capturedId)) CallDeferred(nameof(ShowGardenModules));
            };
            detail.AddChild(upgrade);
            return;
        }

        // Plain placed ground: the choice of which training ground to build is the card's content.
        var cost = GameRules.GardenModuleRules.TrainingConversionCost;
        var price = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        price.AddThemeConstantOverride("separation", 3);
        price.AddChild(UiFactory.CreateLabel(Tr("UI_SHOP_PRICE_HEADER").ToUpperInvariant(), 7));
        price.AddChild(new TextureRect
        {
            Texture = UiFactory.CreateSproutIcon(),
            CustomMinimumSize = new Vector2(11, 11),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore
        });
        price.AddChild(UiFactory.CreateLabel(cost.ToString(), 8));
        detail.AddChild(price);
        var choices = new VBoxContainer { Name = "LandAction" };
        choices.AddThemeConstantOverride("separation", 3);
        detail.AddChild(choices);
        foreach (var statId in GameRules.StatIds)
        {
            var capturedStatId = statId;
            var capturedId = module.Id;
            var choice = UiFactory.CreateButton(StatPresentationCatalog.NameFor(statId).ToUpperInvariant());
            choice.CustomMinimumSize = new Vector2(152, 24);
            UiFactory.ApplyPixelFont(choice, 7);
            choice.AddThemeColorOverride("font_color", PaperCard.Ink(StatPresentationCatalog.ColorFor(statId)));
            choice.Disabled = _session.State.Coins < cost;
            choice.Pressed += () =>
            {
                if (_session.ConvertHexToTrainingGround(capturedId, capturedStatId))
                    CallDeferred(nameof(ShowGardenModules));
            };
            choices.AddChild(choice);
        }
    }

    /// <summary>The selected hex uses the same non-blocking right inspector slot as a Voidling.</summary>
    private void ShowLandHexMenu(string moduleId)
    {
        var module = _session.State.GardenModules.FirstOrDefault(candidate =>
            candidate.Placed && string.Equals(candidate.Id, moduleId, StringComparison.Ordinal));
        if (module == null)
            return;

        CloseLandInspector();
        _selectedId = string.Empty;
        _garden.ClearSelection();
        _garden.StopFollowing();
        RebuildDetailsPanel();

        var trainingGround = module.StatId.Length > 0;
        var inspector = UiFactory.CreatePanel(new Vector2(162, 210));
        inspector.Name = "LandInspector";
        inspector.Position = new Vector2(468, 82);
        inspector.Size = new Vector2(162, 210);
        inspector.ZIndex = 18;
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 5);
        inspector.AddChild(box);

        var heading = new HBoxContainer();
        var title = UiFactory.CreateLabel(Tr(trainingGround ? "UI_LAND_HEX_TITLE" : "UI_LAND_HEX_EMPTY_TITLE"), 9);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        heading.AddChild(title);
        var close = UiFactory.CreateButton("×");
        close.Name = "CloseLandInspector";
        close.CustomMinimumSize = new Vector2(20, 20);
        close.Pressed += CloseLandInspector;
        heading.AddChild(close);
        box.AddChild(heading);

        if (!trainingGround)
        {
            var cost = GameRules.GardenModuleRules.TrainingConversionCost;
            var intro = UiFactory.CreateLabel(string.Format(Tr("UI_LAND_BUILD_PROMPT"), cost), 7);
            intro.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            intro.CustomMinimumSize = new Vector2(138, 42);
            box.AddChild(intro);

            var grid = new GridContainer { Columns = 2 };
            grid.AddThemeConstantOverride("h_separation", 4);
            grid.AddThemeConstantOverride("v_separation", 4);
            box.AddChild(grid);

            foreach (var statId in GameRules.StatIds)
            {
                var capturedStatId = statId;
                var button = UiFactory.CreateButton(StatPresentationCatalog.NameFor(statId).ToUpperInvariant());
                button.CustomMinimumSize = new Vector2(67, 28);
                UiFactory.ApplyPixelFont(button, 7);
                button.AddThemeColorOverride("font_color", StatPresentationCatalog.ColorFor(statId).Darkened(0.4f));
                button.Disabled = _session.State.Coins < cost;
                button.Pressed += () =>
                {
                    if (_session.ConvertHexToTrainingGround(moduleId, capturedStatId))
                        CallDeferred(nameof(ShowLandHexMenu), moduleId);
                };
                grid.AddChild(button);
            }
        }
        else
        {
            var rate = GameRules.GardenModuleRules.PointsPerMinuteForLevel(module.Level);
            var residents = _session.State.Voidlings
                .Where(creature => string.Equals(creature.PassiveTrainingModuleId, moduleId, StringComparison.Ordinal))
                .Select(creature => creature.Name)
                .ToList();
            var detail = UiFactory.CreateLabel(
                $"{StatPresentationCatalog.NameFor(module.StatId).ToUpperInvariant()}  •  L{module.Level}\n{rate:0.#}/min",
                8);
            detail.AddThemeColorOverride("font_color", StatPresentationCatalog.ColorFor(module.StatId));
            box.AddChild(detail);
            var occupancy = UiFactory.CreateLabel(
                residents.Count > 0
                    ? string.Format(Tr("UI_LAND_HEX_RESIDENT"), string.Join(", ", residents))
                    : Tr("UI_LAND_HEX_VACANT"),
                7);
            occupancy.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            occupancy.CustomMinimumSize = new Vector2(138, 42);
            box.AddChild(occupancy);
            var upgradeCost = GameRules.GardenModuleRules.UpgradeCostForLevel(module.Level);
            var upgrade = UiFactory.CreateButton(
                upgradeCost < 0 ? Tr("UI_LAND_MAX_LEVEL") : string.Format(Tr("UI_LAND_UPGRADE"), upgradeCost));
            upgrade.CustomMinimumSize = new Vector2(138, 28);
            UiFactory.ApplyPixelFont(upgrade, 7);
            upgrade.Disabled = upgradeCost < 0 || _session.State.Coins < upgradeCost;
            upgrade.Pressed += () =>
            {
                if (_session.UpgradeGardenModule(moduleId))
                    CallDeferred(nameof(ShowLandHexMenu), moduleId);
            };
            box.AddChild(upgrade);
        }

        _landInspector = inspector;
        _uiRoot.AddChild(inspector);
    }

    private void CloseLandInspector()
    {
        if (_landInspector == null || !GodotObject.IsInstanceValid(_landInspector)) return;
        _landInspector.Visible = false;
        _landInspector.QueueFree();
        _landInspector = null;
    }
}
