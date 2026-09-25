using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Voidling.Application.Garden;
using Voidling.Domain.Garden;
using Voidling.Presentation.UI.Common;
using Voidling.Presentation.UI.Motion;

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
    /// card carrying what that piece can do. Buying still happens in the Shop and placing land still
    /// happens in the Garden; a hex coordinate is never shown, because the shape is what the player
    /// recognises.
    /// </summary>
    private void ShowGardenModules()
    {
        var box = OpenModal(Tr("UI_LAND_TITLE"), new Vector2(520, 292), ShowGardenBuild, ScreenIcons.Land);
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

    /// <summary>Biomes first, then plain ground, then what is still in storage.</summary>
    private IEnumerable<GardenModuleData> LandPieces()
        => _session.State.GardenModules
            .Where(module => module.Placed)
            .OrderByDescending(module => module.BiomeId.Length > 0)
            .ThenBy(module => module.HexR)
            .ThenBy(module => module.HexQ)
            .Concat(_session.State.GardenModules
                .Where(module => !module.Placed)
                .OrderBy(module => module.ShapeId, StringComparer.Ordinal));

    private Button BuildLandSlot(GardenModuleData module)
    {
        var biome = module.BiomeId.Length > 0;
        var tint = LandShapePresentation.TintForBiome(module.BiomeId);
        var button = UiFactory.CreateButton(string.Empty);
        button.Name = "Land_" + module.Id;
        button.ToggleMode = true;
        button.ButtonPressed = module.Id == _selectedModuleId;
        button.CustomMinimumSize = LandSlotSize;
        button.TooltipText = BiomePresentationCatalog.NameFor(module.BiomeId);

        var art = LandShapePresentation.CreateShapeArt(module.ShapeId, tint);
        art.Position = (LandSlotSize - art.CustomMinimumSize) * 0.5f - new Vector2(0, 4);
        // Ground still in storage is the same piece, dimmed, rather than a separate list.
        art.Modulate = new Color(1, 1, 1, module.Placed ? 1.0f : 0.5f);
        button.AddChild(art);

        if (biome)
        {
            var level = UiFactory.CreateLabel("L" + module.Level, 6);
            level.Position = new Vector2(2, 33);
            level.Size = new Vector2(54, 12);
            level.HorizontalAlignment = HorizontalAlignment.Right;
            level.MouseFilter = Control.MouseFilterEnum.Ignore;
            level.AddThemeColorOverride("font_color", PaperCard.Ink(BiomePresentationCatalog.ColorFor(module.BiomeId)));
            button.AddChild(level);
        }

        var capturedId = module.Id;
        button.Pressed += () => { _selectedModuleId = capturedId; CallDeferred(nameof(ShowGardenModules)); };
        return button;
    }

    private void BuildLandDetail(VBoxContainer detail, GardenModuleData module)
    {
        var biome = module.BiomeId.Length > 0;
        var tint = LandShapePresentation.TintForBiome(module.BiomeId);

        var artPanel = new PanelContainer { CustomMinimumSize = new Vector2(0, 54) };
        artPanel.AddThemeStyleboxOverride("panel", UiSkin.Well());
        var center = new CenterContainer();
        center.AddChild(LandShapePresentation.CreateShapeArt(module.ShapeId, tint));
        artPanel.AddChild(center);
        detail.AddChild(artPanel);

        var name = UiFactory.CreateLabel(BiomePresentationCatalog.NameFor(module.BiomeId).ToUpperInvariant(), 9);
        name.Name = "LandName";
        name.HorizontalAlignment = HorizontalAlignment.Center;
        name.AddThemeColorOverride("font_color", PaperCard.Ink(biome ? BiomePresentationCatalog.ColorFor(module.BiomeId) : Color.FromHtml("#6B8F5E")));
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

        if (biome)
        {
            detail.AddChild(PaperCard.StarRating(module.Level, _session.BiomeTileMaxStars, 15.0f));
            var rate = UiFactory.CreateLabel(
                string.Format(Tr("UI_LAND_RATE"), GameRules.GardenModuleRules.PointsPerMinuteForLevel(module.Level).ToString("0.#")), 7);
            rate.HorizontalAlignment = HorizontalAlignment.Center;
            detail.AddChild(rate);
            detail.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });
        }

        var actions = new VBoxContainer { Name = "LandAction" };
        actions.AddThemeConstantOverride("separation", 3);
        detail.AddChild(actions);
        AddBiomeTileActions(actions, module, 152, () => CallDeferred(nameof(ShowGardenModules)));
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

        var biome = module.BiomeId.Length > 0;
        var inspector = UiFactory.CreateWindowPanel(new Vector2(162, 230));
        inspector.Name = "LandInspector";
        inspector.Position = new Vector2(468, 82);
        inspector.Size = new Vector2(162, 230);
        inspector.ZIndex = 18;
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 5);
        inspector.AddChild(box);

        var heading = new HBoxContainer();
        var title = UiFactory.CreateLabel(biome
            ? BiomePresentationCatalog.NameFor(module.BiomeId)
            : Tr("UI_LAND_HEX_EMPTY_TITLE"), 9);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        if (biome)
            title.AddThemeColorOverride("font_color", BiomePresentationCatalog.ColorFor(module.BiomeId).Darkened(0.35f));
        heading.AddChild(title);
        var close = UiFactory.CreateButton(string.Empty);
        close.Name = "CloseLandInspector";
        UiSkin.ApplyIconButton(close, UiSkin.IconGlyph.SmallClose);
        close.TooltipText = Tr("UI_COMMON_CLOSE");
        close.Pressed += CloseLandInspector;
        heading.AddChild(close);
        box.AddChild(heading);

        if (!biome)
        {
            var intro = UiFactory.CreateLabel(string.Format(Tr("UI_LAND_BUILD_PROMPT"), GameRules.GardenModuleRules.BiomeTilePrice), 7);
            intro.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            intro.CustomMinimumSize = new Vector2(138, 42);
            box.AddChild(intro);
        }
        else
        {
            box.AddChild(PaperCard.StarRating(module.Level, _session.BiomeTileMaxStars, 13.0f));
            var rate = GameRules.GardenModuleRules.PointsPerMinuteForLevel(module.Level);
            var residents = _session.State.Voidlings
                .Where(creature => string.Equals(creature.PassiveTrainingModuleId, moduleId, StringComparison.Ordinal))
                .Select(creature => creature.Name)
                .ToList();
            var detail = UiFactory.CreateLabel(
                string.Format(Tr("UI_LAND_BIOME_TRAINS"), StatPresentationCatalog.NameFor(module.StatId), rate.ToString("0.#")), 7);
            detail.AddThemeColorOverride("font_color", StatPresentationCatalog.ColorFor(module.StatId).Darkened(0.4f));
            box.AddChild(detail);
            var occupancy = UiFactory.CreateLabel(
                residents.Count > 0
                    ? string.Format(Tr("UI_LAND_HEX_RESIDENT"), string.Join(", ", residents))
                    : Tr("UI_LAND_HEX_VACANT"),
                7);
            occupancy.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            occupancy.CustomMinimumSize = new Vector2(138, 30);
            box.AddChild(occupancy);
        }

        AddBiomeTileActions(box, module, 138, () => CallDeferred(nameof(ShowLandHexMenu), moduleId));
        _landInspector = inspector;
        _uiRoot.AddChild(inspector);
        UiMotion.Appear(inspector, 0.0, UiMotion.Normal, 0.08f);
    }

    /// <summary>
    /// What a hex can do with biome tiles. Plain ground: build any biome for the tile price, or put
    /// down a tile you own. A biome: stack a matching tile on top for the next star, or pick the tile
    /// back up (four-star tiles stay put).
    /// </summary>
    private void AddBiomeTileActions(VBoxContainer box, GardenModuleData module, float width, Action refresh)
    {
        var moduleId = module.Id;
        if (module.BiomeId.Length == 0)
        {
            var price = GameRules.GardenModuleRules.BiomeTilePrice;
            var grid = new GridContainer { Columns = 2, Name = "BuildBiome" };
            grid.AddThemeConstantOverride("h_separation", 4);
            grid.AddThemeConstantOverride("v_separation", 3);
            box.AddChild(grid);
            foreach (var definition in BiomeCatalog.Biomes)
            {
                var biomeId = definition.Id;
                var build = UiFactory.CreateButton(BiomePresentationCatalog.NameFor(biomeId).ToUpperInvariant());
                build.Name = "Build_" + biomeId;
                build.TooltipText = string.Format(Tr("UI_LAND_BUILD_BIOME_TOOLTIP"),
                    BiomePresentationCatalog.NameFor(biomeId), StatPresentationCatalog.NameFor(definition.StatId), price);
                build.CustomMinimumSize = new Vector2((width - 4) / 2, 24);
                UiFactory.ApplyPixelFont(build, 6);
                build.AddThemeColorOverride("font_color", BiomePresentationCatalog.ColorFor(biomeId).Darkened(0.45f));
                build.Disabled = _session.State.Coins < price;
                build.Pressed += () =>
                {
                    var origin = build.GetGlobalRect().GetCenter();
                    if (!_session.BuildBiome(moduleId, biomeId)) return;
                    Celebrate(origin);
                    refresh();
                };
                grid.AddChild(build);
            }

            foreach (var stack in _session.State.BiomeTiles.Where(stack => stack.Count > 0)
                         .OrderBy(stack => stack.BiomeId, StringComparer.Ordinal).ThenBy(stack => stack.Stars).Take(3))
            {
                var biomeId = stack.BiomeId;
                var stars = stack.Stars;
                var place = UiFactory.CreateButton(string.Format(Tr("UI_LAND_PLACE_TILE"),
                    BiomePresentationCatalog.NameFor(biomeId).ToUpperInvariant(), stars, stack.Count));
                place.Name = $"PlaceTile_{biomeId}_{stars}";
                place.CustomMinimumSize = new Vector2(width, 22);
                UiFactory.ApplyPixelFont(place, 6);
                place.Pressed += () =>
                {
                    var origin = place.GetGlobalRect().GetCenter();
                    if (!_session.PlaceBiomeTile(moduleId, biomeId, stars)) return;
                    Celebrate(origin);
                    refresh();
                };
                box.AddChild(place);
            }
            return;
        }

        var baseBiome = BiomeCatalog.BaseOf(module.BiomeId);
        var maxStars = _session.BiomeTileMaxStars;
        if (module.Level < maxStars)
        {
            var owned = BiomeTileUseCase.OwnedCount(_session.State, baseBiome, module.Level);
            var stack = UiFactory.CreateButton(string.Format(Tr("UI_LAND_STACK_TILE"), module.Level, owned));
            stack.Name = "StackTile";
            stack.TooltipText = Tr("UI_LAND_STACK_TOOLTIP");
            stack.CustomMinimumSize = new Vector2(width, 26);
            UiFactory.ApplyPrimaryStyle(stack);
            stack.Disabled = owned <= 0;
            var stars = module.Level;
            stack.Pressed += () =>
            {
                var origin = stack.GetGlobalRect().GetCenter();
                if (!_session.PlaceBiomeTile(moduleId, baseBiome, stars)) return;
                Celebrate(origin);
                refresh();
            };
            box.AddChild(stack);

            var pickUp = UiFactory.CreateButton(Tr("UI_LAND_PICK_UP_TILE"));
            pickUp.Name = "PickUpTile";
            pickUp.CustomMinimumSize = new Vector2(width, 22);
            UiFactory.ApplyPixelFont(pickUp, 6);
            pickUp.Pressed += () => { if (_session.PickUpBiomeTile(moduleId)) refresh(); };
            box.AddChild(pickUp);
        }
        else
        {
            var top = UiFactory.CreateLabel(Tr("UI_LAND_TOP_STAR"), 6);
            top.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            top.CustomMinimumSize = new Vector2(width, 20);
            box.AddChild(top);
        }
    }

    /// <summary>
    /// A failed egg lying on the island, in the same non-blocking right inspector a hex uses. It is
    /// worth nothing either way, so the only choice is whether to keep looking at it.
    /// </summary>
    private void ShowFailedEggMenu(string eggId)
    {
        var egg = _session.State.OwnedEggs.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, eggId, StringComparison.Ordinal) && candidate.State == EggState.Failed);
        if (egg == null)
            return;

        CloseLandInspector();
        _selectedId = string.Empty;
        _garden.ClearSelection();
        _garden.StopFollowing();
        RebuildDetailsPanel();

        var inspector = UiFactory.CreateWindowPanel(new Vector2(162, 150));
        inspector.Name = "FailedEggInspector";
        inspector.Position = new Vector2(468, 82);
        inspector.Size = new Vector2(162, 150);
        inspector.ZIndex = 18;
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 5);
        inspector.AddChild(box);

        var heading = new HBoxContainer();
        var title = UiFactory.CreateLabel(Tr("UI_GARDEN_EGG_FAILED_TITLE"), 9);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        title.AddThemeColorOverride("font_color", Color.FromHtml("#9C514B"));
        heading.AddChild(title);
        var close = UiFactory.CreateButton(string.Empty);
        close.Name = "CloseLandInspector";
        UiSkin.ApplyIconButton(close, UiSkin.IconGlyph.SmallClose);
        close.TooltipText = Tr("UI_COMMON_CLOSE");
        close.Pressed += CloseLandInspector;
        heading.AddChild(close);
        box.AddChild(heading);

        var detail = UiFactory.CreateLabel(Tr("UI_INVENTORY_EGG_FAILED"), 7);
        detail.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        detail.CustomMinimumSize = new Vector2(138, 34);
        box.AddChild(detail);

        var stow = UiFactory.CreateButton(Tr("UI_INVENTORY_RETURN"));
        stow.Name = "StowFailedEgg";
        stow.CustomMinimumSize = new Vector2(138, 26);
        UiFactory.ApplyPixelFont(stow, 7);
        stow.Pressed += () => { if (_session.StowFailedEgg(eggId)) CloseLandInspector(); };
        box.AddChild(stow);

        var discard = UiFactory.CreateButton(Tr("UI_INVENTORY_DISCARD"));
        discard.Name = "DiscardFailedEgg";
        discard.CustomMinimumSize = new Vector2(138, 24);
        UiFactory.ApplyPixelFont(discard, 7);
        UiFactory.ApplyDangerStyle(discard);
        discard.Pressed += () => { _session.DiscardFailedEgg(eggId); CloseLandInspector(); };
        box.AddChild(discard);

        _landInspector = inspector;
        _uiRoot.AddChild(inspector);
        UiMotion.Appear(inspector, 0.0, UiMotion.Normal, 0.08f);
    }

    private void CloseLandInspector()
    {
        if (_landInspector == null || !GodotObject.IsInstanceValid(_landInspector)) return;
        _landInspector.Visible = false;
        _landInspector.QueueFree();
        _landInspector = null;
    }
}
