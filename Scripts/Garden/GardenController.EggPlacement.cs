using System;
using Godot;

namespace VoidlingGame;

public partial class GardenController
{
    /// <summary>Raised when placement mode is armed or cleared so the HUD can show its own hint.</summary>
    public event Action<bool>? EggPlacementModeChanged;

    private string _placingEggId = "";
    private Node2D? _placementGhost;

    public bool IsPlacingEgg => _placingEggId.Length > 0;

    /// <summary>
    /// Arms "click the garden to put this egg down". Incubation only starts once the click lands,
    /// so an armed placement that is cancelled leaves the egg untouched in the inventory.
    /// </summary>
    public void BeginEggPlacement(string eggId, Color tint)
    {
        if (string.IsNullOrWhiteSpace(eggId))
            return;

        CancelEggPlacement();
        _placingEggId = eggId;

        _placementGhost = new Node2D { ZIndex = 9, Modulate = new Color(1.0f, 1.0f, 1.0f, 0.62f) };
        _placementGhost.AddChild(new Sprite2D
        {
            Texture = EggTexture,
            Scale = Vector2.One * EggBaseScale,
            Modulate = tint
        });
        _eggsRoot.AddChild(_placementGhost);
        EggPlacementModeChanged?.Invoke(true);
    }

    public void CancelEggPlacement()
    {
        if (_placementGhost != null && GodotObject.IsInstanceValid(_placementGhost))
            _placementGhost.QueueFree();
        _placementGhost = null;

        if (_placingEggId.Length == 0)
            return;

        _placingEggId = "";
        EggPlacementModeChanged?.Invoke(false);
    }

    private void UpdatePlacementGhost()
    {
        if (_placementGhost == null || !GodotObject.IsInstanceValid(_placementGhost))
            return;

        _placementGhost.Position = ClampToGarden(_eggsRoot.ToLocal(GetGlobalMousePosition()));
    }

    // The egg lands where the click actually happened rather than wherever the cached pointer
    // position last settled, so the drop point always matches what the player aimed at.
    private bool TryCompleteEggPlacement(Vector2 viewportPosition)
    {
        if (_placingEggId.Length == 0)
            return false;

        var eggId = _placingEggId;
        var position = ClampToGarden(_eggsRoot.ToLocal(GetCanvasTransform().AffineInverse() * viewportPosition));
        CancelEggPlacement();
        _session.PlaceStoredEgg(eggId, position);
        return true;
    }
}
