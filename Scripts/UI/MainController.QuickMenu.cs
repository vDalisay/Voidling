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

    private void BuildQuickMenu()
    {
        _quickMenu = new GardenVoidlingQuickMenu
        {
            // Bottom-right corner, opening upward. It sits above the details panel it shares that
            // corner with, and closes on pick so the details panel is what the player ends up on.
            Position = new Vector2(ScreenWidth - 208.0f, ScreenHeight - 214.0f),
            ZIndex = 20
        };
        _quickMenu.VoidlingPicked += OnQuickMenuVoidlingPicked;
        _uiRoot.AddChild(_quickMenu);

        _placementHint = UiFactory.CreateLabel(Tr("UI_GARDEN_PLACE_EGG_HINT"), 8);
        _placementHint.Position = new Vector2(18, 62);
        _placementHint.Size = new Vector2(420, 16);
        _placementHint.AddThemeColorOverride("font_color", Color.FromHtml("#F9F4D8"));
        _placementHint.AddThemeColorOverride("font_outline_color", Color.FromHtml("#465247"));
        _placementHint.AddThemeConstantOverride("outline_size", 2);
        _placementHint.Visible = false;
        _uiRoot.AddChild(_placementHint);

        _garden.EggPlacementModeChanged += placing => ShowPlacementHint(placing, "UI_GARDEN_PLACE_EGG_HINT");
        _garden.LandPlacementModeChanged += placing => ShowPlacementHint(placing, "UI_GARDEN_PLACE_LAND_HINT");
        _garden.LandHexSelected += ShowLandHexMenu;
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
        _selectedId = creatureId;
        RefreshUi();
        if (!_garden.IsFollowing(creatureId))
            _garden.ToggleFollowVoidling(creatureId);
    }

    private void RefreshQuickMenu()
    {
        if (_quickMenu == null || !GodotObject.IsInstanceValid(_quickMenu))
            return;

        // The details side panel owns the same bottom-right corner, so the shortcut steps aside while
        // that panel is up and comes back once it closes.
        var cornerTaken = _modalHost.IsOpen ||
                          (_detailsPanel != null && GodotObject.IsInstanceValid(_detailsPanel) && _detailsPanel.Visible);
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
