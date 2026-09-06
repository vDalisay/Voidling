using System.Linq;
using Godot;
using Voidling.Presentation.UI.Common;
using Voidling.Presentation.UI.Garden;
using Voidling.Presentation.Voidlings;

namespace VoidlingGame;

public partial class MainController
{
    private GardenVoidlingQuickMenu _quickMenu = null!;
    private Label _placementHint = null!;
    private PanelContainer _landPurchaseActions = null!;
    private string _shopLandPurchaseId = string.Empty;
    private bool _cancelShopLandPurchase;

    private void BuildQuickMenu()
    {
        _quickMenu = new GardenVoidlingQuickMenu
        {
            Position = new Vector2(106, 82),
            ZIndex = 20
        };
        _quickMenu.VoidlingPicked += OnQuickMenuVoidlingPicked;
        _uiRoot.AddChild(_quickMenu);

        _placementHint = UiFactory.CreateLabel(Tr("UI_GARDEN_PLACE_EGG_HINT"), 8);
        _placementHint.Position = new Vector2(210, 66);
        _placementHint.Size = new Vector2(248, 28);
        _placementHint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _placementHint.AddThemeColorOverride("font_color", Color.FromHtml("#F9F4D8"));
        _placementHint.AddThemeColorOverride("font_outline_color", Color.FromHtml("#465247"));
        _placementHint.AddThemeConstantOverride("outline_size", 2);
        _placementHint.Visible = false;
        _uiRoot.AddChild(_placementHint);

        _landPurchaseActions = UiFactory.CreatePanel(new Vector2(258, 38));
        _landPurchaseActions.Name = "LandPurchaseActions";
        _landPurchaseActions.Position = new Vector2((ScreenWidth - 258) / 2f, 312);
        _landPurchaseActions.Size = new Vector2(258, 38);
        _landPurchaseActions.ZIndex = 25;
        _landPurchaseActions.Visible = false;
        var actions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        actions.AddThemeConstantOverride("separation", 6);
        var inventory = UiFactory.CreateButton(Tr("UI_GARDEN_PUT_IN_INVENTORY"));
        inventory.Name = "PutLandInInventory";
        inventory.CustomMinimumSize = new Vector2(145, 26);
        inventory.Pressed += _garden.CancelLandPlacement;
        actions.AddChild(inventory);
        var cancel = UiFactory.CreateButton(Tr("UI_COMMON_CANCEL"));
        cancel.Name = "CancelLandPurchase";
        cancel.CustomMinimumSize = new Vector2(86, 26);
        cancel.Pressed += () =>
        {
            _cancelShopLandPurchase = true;
            _garden.CancelLandPlacement();
        };
        actions.AddChild(cancel);
        _landPurchaseActions.AddChild(actions);
        _uiRoot.AddChild(_landPurchaseActions);

        _garden.EggPlacementModeChanged += placing => ShowPlacementHint(placing, "UI_GARDEN_PLACE_EGG_HINT");
        _garden.LandPlacementModeChanged += OnLandPlacementModeChanged;
        _garden.LandHexSelected += ShowLandHexMenu;
    }

    private void OnLandPlacementModeChanged(bool placing)
    {
        ShowPlacementHint(placing, "UI_GARDEN_PLACE_LAND_HINT");
        _landPurchaseActions.Visible = placing && _shopLandPurchaseId.Length > 0;
        if (!placing && _shopLandPurchaseId.Length > 0)
            FinishShopLandPlacement();
    }

    private void ShowPlacementHint(bool placing, string hintKey)
    {
        if (placing)
            _placementHint.Text = Tr(hintKey);
        _placementHint.Visible = placing;
    }

    // Picking from the quick menu both inspects and tracks, which is the whole point of the
    // shortcut: find a Voidling by name or colour and have the camera go to it.
    private void OnQuickMenuVoidlingPicked(string creatureId)
    {
        if (_session.FindVoidling(creatureId) == null)
            return;

        _quickMenu.Close();
        CloseLandInspector();
        _selectedId = creatureId;
        RefreshUi();
        if (!_garden.IsFollowing(creatureId))
            _garden.ToggleFollowVoidling(creatureId);
        RebuildDetailsPanel();
        _detailsPanel?.FocusCare();
    }

    private void RefreshQuickMenu()
    {
        if (_quickMenu == null || !GodotObject.IsInstanceValid(_quickMenu))
            return;

        var cornerTaken = _modalHost.IsOpen;
        _quickMenu.Visible = !cornerTaken;
        if (cornerTaken)
        {
            _quickMenu.Close();
            return;
        }

        _quickMenu.SetVoidlings(_session.State.Voidlings
            .Select(creature => new QuickMenuVoidlingViewState(
                creature.Id,
                creature.Name,
                VoidlingColorNameCatalog.NameFor(GameRules.TintColor(creature.TintHex)),
                VoidlingVisualAppearance.From(creature.Appearance, creature.TintHex),
                GameRules.HasMutation(creature, GameRules.AngelMutationId),
                creature.RareTraits?.Count(trait =>
                    !string.Equals(trait.TraitId, GameRules.AngelMutationId, System.StringComparison.OrdinalIgnoreCase)) ?? 0))
            .ToArray());
    }
}
