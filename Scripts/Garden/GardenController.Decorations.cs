using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Voidling.Application.Garden;
using Voidling.Presentation.Garden;

namespace VoidlingGame;

/// <summary>
/// Player-placed cosmetic Garden objects. They are intentionally presentation-only: no decoration
/// owns training, stat, race, care, or economy behavior.
/// </summary>
public partial class GardenController
{
    public event Action<bool>? DecorationPlacementModeChanged;

    private static readonly Texture2D DecorationAtlas = GD.Load<Texture2D>(GardenDecorationCatalog.TexturePath);
    private readonly Dictionary<string, Node2D> _decorationVisuals = new(StringComparer.Ordinal);
    private Node2D? _playerDecorationsRoot;
    private Sprite2D? _decorationGhost;
    private GardenDecorationInputDriver? _decorationInputDriver;
    private string _placingDecorationTypeId = string.Empty;
    private string _movingDecorationId = string.Empty;
    private bool _decorationPresentationInstalled;

    public bool IsPlacingDecoration => _placingDecorationTypeId.Length > 0;

    private void InstallDecorationPresentation()
    {
        if (_decorationPresentationInstalled || _session == null || !GodotObject.IsInstanceValid(_session))
            return;

        var authoredRoot = GetNodeOrNull<Node2D>("Decorations");
        if (authoredRoot == null)
            return;

        _playerDecorationsRoot = new Node2D
        {
            Name = "PlayerDecorations",
            YSortEnabled = true
        };
        authoredRoot.AddChild(_playerDecorationsRoot);

        _decorationInputDriver = new GardenDecorationInputDriver { Name = "DecorationInputDriver" };
        _decorationInputDriver.Configure(this);
        AddChild(_decorationInputDriver);

        _session.StateChanged += RefreshDecorations;
        TreeExiting += DetachDecorationPresentation;
        _decorationPresentationInstalled = true;
        RefreshDecorations();
    }

    private void DetachDecorationPresentation()
    {
        if (!_decorationPresentationInstalled)
            return;

        CancelDecorationPlacement();
        if (_session != null && GodotObject.IsInstanceValid(_session))
            _session.StateChanged -= RefreshDecorations;
        TreeExiting -= DetachDecorationPresentation;
        _decorationPresentationInstalled = false;
    }

    public void BeginDecorationPlacement(string typeId, string existingDecorationId = "")
    {
        if (!_decorationPresentationInstalled || _playerDecorationsRoot == null ||
            !GardenDecorationCatalog.TryGet(typeId, out var definition))
        {
            return;
        }

        CancelDecorationPlacement();
        _placingDecorationTypeId = definition.TypeId;
        _movingDecorationId = existingDecorationId ?? string.Empty;
        _decorationGhost = CreateDecorationSprite(definition, ghost: true);
        _playerDecorationsRoot.AddChild(_decorationGhost);
        DecorationPlacementModeChanged?.Invoke(true);
    }

    public void CancelDecorationPlacement()
    {
        if (_decorationGhost != null && GodotObject.IsInstanceValid(_decorationGhost))
            _decorationGhost.QueueFree();
        _decorationGhost = null;

        if (_placingDecorationTypeId.Length == 0)
            return;

        _placingDecorationTypeId = string.Empty;
        _movingDecorationId = string.Empty;
        DecorationPlacementModeChanged?.Invoke(false);
    }

    public void UpdateDecorationPlacementGhost()
    {
        if (_decorationGhost == null || _playerDecorationsRoot == null || !GodotObject.IsInstanceValid(_decorationGhost))
            return;

        _decorationGhost.Position = ClampDecorationPosition(_playerDecorationsRoot.ToLocal(GetGlobalMousePosition()));
    }

    public void HandleDecorationPlacementInput(InputEvent inputEvent)
    {
        if (!IsPlacingDecoration || inputEvent is not InputEventMouseButton mouse || !mouse.Pressed ||
            mouse.ButtonIndex is not (MouseButton.Left or MouseButton.Right))
        {
            return;
        }

        if (mouse.ButtonIndex == MouseButton.Right)
        {
            CancelDecorationPlacement();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (_playerDecorationsRoot == null)
            return;

        var position = ClampDecorationPosition(_playerDecorationsRoot.ToLocal(GetGlobalMousePosition()));
        var succeeded = _movingDecorationId.Length > 0
            ? _session.MoveGardenDecoration(_movingDecorationId, position.X, position.Y)
            : _session.PlaceGardenDecoration(_placingDecorationTypeId, position.X, position.Y);
        if (succeeded)
            CancelDecorationPlacement();
        GetViewport().SetInputAsHandled();
    }

    private Vector2 ClampDecorationPosition(Vector2 position)
        => new(
            Mathf.Clamp(position.X, _wanderBounds.Position.X, _wanderBounds.End.X),
            Mathf.Clamp(position.Y, _wanderBounds.Position.Y, _wanderBounds.End.Y));

    private void RefreshDecorations()
    {
        if (_playerDecorationsRoot == null || !GodotObject.IsInstanceValid(_playerDecorationsRoot))
            return;

        var valid = _session.State.GardenDecorations
            .Where(data => data != null && !string.IsNullOrWhiteSpace(data.Id) &&
                           float.IsFinite(data.X) && float.IsFinite(data.Y) &&
                           GardenDecorationCatalog.TryGet(data.TypeId, out _))
            .ToDictionary(data => data.Id, StringComparer.Ordinal);

        foreach (var staleId in _decorationVisuals.Keys.Where(id => !valid.ContainsKey(id)).ToArray())
        {
            _decorationVisuals[staleId].QueueFree();
            _decorationVisuals.Remove(staleId);
        }

        foreach (var (id, data) in valid)
        {
            if (!GardenDecorationCatalog.TryGet(data.TypeId, out var definition))
                continue;

            var position = ClampDecorationPosition(new Vector2(data.X, data.Y));
            if (_decorationVisuals.TryGetValue(id, out var existing) && GodotObject.IsInstanceValid(existing))
            {
                existing.Position = position;
                continue;
            }

            var holder = new Node2D { Name = $"Decoration_{id}", Position = position };
            holder.AddChild(CreateDecorationSprite(definition, ghost: false));
            _playerDecorationsRoot.AddChild(holder);
            _decorationVisuals[id] = holder;
        }
    }

    private static Sprite2D CreateDecorationSprite(GardenDecorationDefinition definition, bool ghost)
    {
        var texture = new AtlasTexture { Atlas = DecorationAtlas, Region = definition.AtlasRegion };
        return new Sprite2D
        {
            Texture = texture,
            Scale = Vector2.One * definition.Scale,
            Position = new Vector2(0, -definition.AtlasRegion.Size.Y * definition.Scale * 0.42f),
            Modulate = ghost ? new Color(1, 1, 1, 0.58f) : Colors.White,
            ZIndex = 1
        };
    }
}
