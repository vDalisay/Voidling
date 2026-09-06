using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Voidling.Domain.Shop;
using Voidling.Presentation.UI.Common;
using Voidling.Presentation.UI.Inventory;
using Voidling.Presentation.UI.Shop;

namespace VoidlingGame;

public partial class MainController : Node
{
    private void RebuildDetailsPanel()
    {
        var data = _session.FindVoidling(_selectedId);
        var profile = data == null ? null : _session.CreateCreatureProfileProjection(data.Id);
        if (data == null || profile == null)
        {
            if (_detailsPanel != null && GodotObject.IsInstanceValid(_detailsPanel))
            {
                _detailsPanel.Visible = false;
                _detailsPanel.QueueFree();
            }
            _detailsPanel = null;
            return;
        }
        if (_detailsPanel == null || !GodotObject.IsInstanceValid(_detailsPanel) || _detailsPanel.CreatureId != data.Id)
        {
            if (_detailsPanel != null && GodotObject.IsInstanceValid(_detailsPanel))
            {
                _detailsPanel.Visible = false;
                _detailsPanel.QueueFree();
            }
            var inspector = new Voidling.Presentation.UI.Garden.GardenInspector
            {
                Position = new Vector2(468, 82), Size = new Vector2(162, 230)
            };
            inspector.Build(profile);
            var creatureId = data.Id;
            inspector.RenameRequested += name =>
            {
                if (name != data.Name) _session.RenameVoidling(creatureId, name);
            };
            inspector.CloseRequested += DeselectVoidling;
            inspector.TreatRequested += ShowTreatChooser;
            inspector.DetailsRequested += ShowDetails;
            inspector.FamilyRequested += ShowFamilyTree;
            inspector.FollowRequested += () =>
            {
                _garden.ToggleFollowVoidling(creatureId);
                RebuildDetailsPanel();
            };
            _detailsPanel = inspector;
            _uiRoot.AddChild(inspector);
        }
        _detailsPanel.Render(profile, data.PassiveTrainingStatId, _garden.IsFollowing(data.Id));
        _detailsPanel.Visible = true;
    }

    private void ShowTreatChooser()
    {
        var creatureId = _selectedId;
        var profile = _session.CreateCreatureProfileProjection(creatureId);
        if (profile == null) return;
        var box = OpenModal(Tr("UI_PROFILE_GIVE_TREAT"), new Vector2(320, 230));
        var target = UiFactory.CreateLabel(profile.Name, 12);
        target.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        box.AddChild(target);
        foreach (var stat in profile.Stats)
        {
            var count = _session.State.TrainingItems.TryGetValue(stat.StatId, out var owned) ? owned : 0;
            var row = new HBoxContainer();
            var label = UiFactory.CreateLabel(string.Format(Tr("UI_PROFILE_TREAT_STOCK"),
                StatPresentationCatalog.NameFor(stat.StatId), count), 9);
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row.AddChild(label);
            var give = UiFactory.CreateButton(Tr("UI_PROFILE_GIVE"));
            give.Name = "Give_" + stat.StatId;
            give.Disabled = count <= 0;
            give.TooltipText = TrainingItemEffectPresentation.ProfileTooltip(StatPresentationCatalog.NameFor(stat.StatId), count);
            give.CustomMinimumSize = new Vector2(60, 22);
            var capturedStatId = stat.StatId;
            // The chooser deliberately stays open. Pressing again restarts the eating beat rather
            // than queueing a second one, so the player can spam a hungry Voidling.
            give.Pressed += () =>
            {
                _session.UseTrainingItem(creatureId, capturedStatId);
                _garden.PlayTreatEating(creatureId, capturedStatId, spawnFood: true);
                var remaining = _session.State.TrainingItems.TryGetValue(capturedStatId, out var left) ? left : 0;
                label.Text = string.Format(Tr("UI_PROFILE_TREAT_STOCK"),
                    StatPresentationCatalog.NameFor(capturedStatId), remaining);
                give.Disabled = remaining <= 0;
            };
            row.AddChild(give);
            box.AddChild(row);
        }
    }

    private void ShowInventory()
    {
        var state = _session.State;
        var items = GameRules.StatIds
            .Select((statId, index) => new InventoryItemViewState(
                string.Format(Tr("UI_INVENTORY_TREAT"), StatPresentationCatalog.NameFor(statId)),
                string.Format(
                    Tr("UI_INVENTORY_TREAT_EFFECT"),
                    StatPresentationCatalog.NameFor(statId),
                    GameRules.TrainingItemRules.MinGain,
                    GameRules.TrainingItemRules.MaxGain),
                statId,
                state.TrainingItems.TryGetValue(statId, out var owned) ? owned : 0,
                18 + index))
            .ToList();
        var storedEggs = state.OwnedEggs
            .Where(egg => egg.State == EggState.Stored)
            .Select((egg, index) => new StoredEggViewState(
                egg.Id,
                string.Format(Tr("UI_INVENTORY_STORED_EGG"), index + 1),
                // What the player already knows about an unhatched egg: where it came from. Its
                // genome stays hidden until it hatches.
                Tr(egg.Source == EggSource.Bred ? "UI_INVENTORY_EGG_BRED" : "UI_INVENTORY_EGG_COMMON"),
                GameRules.TintColor(egg.TintHex)))
            .ToList();

        var storedLand = state.GardenModules
            .Where(module => !module.Placed)
            .OrderBy(module => module.StatId, StringComparer.Ordinal)
            .ThenBy(module => module.ShapeId, StringComparer.Ordinal)
            .ThenBy(module => module.Id, StringComparer.Ordinal)
            .Select(module => new StoredLandViewState(
                module.Id,
                LandShapePresentation.DescribeStoredPiece(module.ShapeId, module.StatId, module.Level),
                module.ShapeId,
                LandShapePresentation.TintFor(module.StatId)))
            .ToList();

        var failedEggs = state.OwnedEggs
            .Where(egg => egg.State == EggState.Failed)
            .Select((egg, index) => new FailedEggViewState(egg.Id, string.Format(Tr("UI_INVENTORY_FAILED_EGG"), index + 1)))
            .ToList();
        var eggShells = state.EggShells
            .Select((shell, index) => new EggShellViewState(shell.Id, string.Format(Tr("UI_INVENTORY_SHELL"), index + 1), GameRules.EggShellSalePrice))
            .ToList();
        var incubationSkipCount = state.UtilityItems.TryGetValue(ShopItemIds.FullIncubationSkip, out var ownedSkips) ? Math.Max(0, ownedSkips) : 0;
        var incubatingEggs = state.OwnedEggs
            .Where(egg => egg.State == EggState.Incubating && egg.IncubationSeconds < egg.RequiredIncubationSeconds)
            .Select((egg, index) => new IncubatingEggViewState(egg.Id, string.Format(Tr("UI_INVENTORY_EGG"), index + 1), Math.Max(0, (int)Math.Ceiling(egg.RequiredIncubationSeconds - egg.IncubationSeconds))))
            .ToList();

        var box = OpenModal(Tr("UI_INVENTORY_TITLE"), new Vector2(520, 292));
        var screen = new InventoryScreen();
        screen.Configure(new InventoryScreenState(items, failedEggs, eggShells, incubationSkipCount, incubatingEggs, storedEggs, storedLand));
        screen.PlaceStoredEggRequested += egg =>
        {
            CloseModal();
            _garden.BeginEggPlacement(egg.EggId, egg.TintColor);
        };
        screen.PlaceStoredLandRequested += land =>
        {
            CloseModal();
            _garden.BeginLandPlacement(land.ModuleId, land.ShapeId);
        };
        screen.PlaceTreatRequested += statId =>
        {
            // A treat is only spent when something eats it, so the ground must never hold more of
            // one than the satchel actually has.
            var owned = _session.State.TrainingItems.TryGetValue(statId, out var stock) ? stock : 0;
            if (owned <= _garden.DroppedTreatCount(statId))
                return;
            CloseModal();
            _garden.BeginTreatPlacement(statId);
        };
        screen.DiscardFailedEggRequested += eggId => { _session.DiscardFailedEgg(eggId); CallDeferred(nameof(ShowInventory)); };
        screen.SellEggShellRequested += shellId => { if (_session.SellEggShell(shellId)) CallDeferred(nameof(ShowInventory)); };
        screen.UseIncubationSkipRequested += eggId => { if (_session.UseFullIncubationSkip(eggId)) CallDeferred(nameof(ShowInventory)); };
        box.AddChild(screen);
        screen.CallDeferred(InventoryScreen.MethodName.FocusSelection);
    }
}
