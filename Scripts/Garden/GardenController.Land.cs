using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Voidling.Application.Training;
using Voidling.Domain.Garden;
using Voidling.Presentation.UI.Common;

namespace VoidlingGame;

public partial class GardenController
{
    /// <summary>Raised when land placement is armed or cleared so the HUD can show its own hint.</summary>
    public event Action<bool>? LandPlacementModeChanged;

    private const float LandFillAlpha = 0.62f;

    /// <summary>Leaves a margin inside the tile so a roaming trainee never overhangs its edge.</summary>
    private const float TileResidentRoamFraction = 0.62f;

    private readonly Dictionary<string, LandVisual> _landVisuals = new(StringComparer.Ordinal);

    private Node2D _landRoot = null!;
    private string _placingModuleId = "";
    private Node2D? _landGhost;
    private Polygon2D? _landGhostFill;
    private Node2D? _snapGrid;
    private string _hoveredModuleId = "";

    public bool IsPlacingLand => _placingModuleId.Length > 0;

    private static GardenHexLayout Hex => GameRules.GardenModuleRules.Hex;

    private sealed class LandVisual
    {
        public Node2D Holder { get; init; } = null!;
        public Polygon2D Fill { get; init; } = null!;
        public Line2D Outline { get; init; } = null!;
        public Color BaseColor { get; init; }
    }

    /// <summary>Arms "click the Garden to grow the island here" for one owned land tile.</summary>
    public void BeginLandPlacement(string moduleId, Color statColor)
    {
        if (string.IsNullOrWhiteSpace(moduleId))
            return;

        CancelLandPlacement();
        _placingModuleId = moduleId;

        _landGhost = new Node2D { ZIndex = -3 };
        _landGhostFill = CreateHexFill(statColor);
        _landGhost.AddChild(_landGhostFill);
        _landGhost.AddChild(CreateHexOutline(statColor.Darkened(0.35f)));
        _landRoot.AddChild(_landGhost);
        BuildSnapGrid();
        LandPlacementModeChanged?.Invoke(true);
    }

    public void CancelLandPlacement()
    {
        ClearSnapGrid();
        if (_landGhost != null && GodotObject.IsInstanceValid(_landGhost))
            _landGhost.QueueFree();
        _landGhost = null;
        _landGhostFill = null;

        if (_placingModuleId.Length == 0)
            return;

        _placingModuleId = "";
        LandPlacementModeChanged?.Invoke(false);
    }

    private void UpdateLandGhost()
    {
        if (_landGhost == null || !GodotObject.IsInstanceValid(_landGhost) || _landGhostFill == null)
            return;

        var pointer = _landRoot.ToLocal(GetGlobalMousePosition());
        var (q, r) = Hex.At(pointer.X, pointer.Y);
        var (x, y) = Hex.CenterOf(q, r);
        _landGhost.Position = new Vector2(x, y);
        _landGhostFill.Color = _landGhostFill.Color with { A = CanPlaceAt(q, r) ? LandFillAlpha : 0.2f };
    }

    private bool CanPlaceAt(int q, int r)
        => Hex.CanPlace(q, r, (candidateQ, candidateR) =>
            TrainingUseCase.IsHexOccupied(_session.State, candidateQ, candidateR));

    // The tile lands on the hex the click actually happened over rather than wherever the cached
    // pointer position last settled, so the drop point always matches what the player aimed at.
    private void TryCompleteLandPlacement(Vector2 viewportPosition)
    {
        if (_placingModuleId.Length == 0)
            return;

        var moduleId = _placingModuleId;
        var world = _landRoot.ToLocal(GetCanvasTransform().AffineInverse() * viewportPosition);
        var (q, r) = Hex.At(world.X, world.Y);
        // A miss keeps placement armed and lets the session explain why the tile does not fit.
        if (_session.PlaceGardenModule(moduleId, q, r))
            CancelLandPlacement();
    }

    private void RefreshLand()
    {
        var placed = _session.State.GardenModules.Where(module => module.Placed).ToList();
        var placedById = placed.ToDictionary(module => module.Id, StringComparer.Ordinal);

        foreach (var staleId in _landVisuals.Keys.Where(id => !placedById.ContainsKey(id)).ToArray())
        {
            _landVisuals[staleId].Holder.QueueFree();
            _landVisuals.Remove(staleId);
        }

        foreach (var module in placed)
        {
            if (_landVisuals.ContainsKey(module.Id))
                continue;

            var color = StatPresentationCatalog.ColorFor(module.StatId);
            var (x, y) = Hex.CenterOf(module.HexQ, module.HexR);
            var holder = new Node2D { Position = new Vector2(x, y), ZIndex = -4 };
            var fill = CreateHexFill(color);
            holder.AddChild(fill);
            var outline = CreateHexOutline(color.Darkened(0.35f));
            holder.AddChild(outline);

            var label = UiFactory.CreateLabel(StatPresentationCatalog.NameFor(module.StatId).ToUpperInvariant(), 6);
            label.Position = new Vector2(-Hex.TopEdgeWidth * 0.62f, -7);
            label.CustomMinimumSize = new Vector2(Hex.TopEdgeWidth * 1.24f, 14);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.AddThemeColorOverride("font_color", color.Darkened(0.55f));
            holder.AddChild(label);

            _landRoot.AddChild(holder);
            _landVisuals[module.Id] = new LandVisual
            {
                Holder = holder,
                Fill = fill,
                Outline = outline,
                BaseColor = color
            };

            if (_initialRefreshComplete)
            {
                holder.Scale = Vector2.Zero;
                CreateTween().TweenProperty(holder, "scale", Vector2.One, 0.4)
                    .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            }
        }
    }

    /// <summary>
    /// A Voidling training on a tile lives on that tile: it roams inside the hex playing the
    /// activity its ground trains, and only leaves when the player carries it off.
    /// </summary>
    private void RefreshTileResidents()
    {
        var placedById = _session.State.GardenModules
            .Where(module => module.Placed)
            .ToDictionary(module => module.Id, StringComparer.Ordinal);

        foreach (var creature in _session.State.Voidlings)
        {
            if (!_actors.TryGetValue(creature.Id, out var actor))
                continue;

            if (creature.PassiveTrainingModuleId.Length > 0 &&
                placedById.TryGetValue(creature.PassiveTrainingModuleId, out var tile))
            {
                var (x, y) = Hex.CenterOf(tile.HexQ, tile.HexR);
                actor.ConfineToTile(
                    new Vector2(x, y),
                    Hex.InnerRadius * TileResidentRoamFraction,
                    ActivityAnimationFor(tile.StatId));
            }
            else
            {
                actor.ReleaseFromTile();
            }
        }
    }

    /// <summary>Swim ground gets the swim loop; everything else runs, matching the race screen.</summary>
    private static StringName ActivityAnimationFor(string statId)
        => string.Equals(statId, "swim", StringComparison.Ordinal) ? "swim" : "run";

    /// <summary>
    /// While a Voidling is held, the tile under the pointer glows to advertise that letting go
    /// there starts training. Assignment is semantic, so the pointer decides, not the actor body.
    /// </summary>
    private void UpdateLandHover()
    {
        var hovered = _draggedId.Length > 0 ? ModuleIdUnderPointer() : "";
        if (hovered == _hoveredModuleId)
            return;

        _hoveredModuleId = hovered;
        var welcome = hovered.Length == 0 || TileHasRoomFor(hovered, _draggedId);
        foreach (var (moduleId, visual) in _landVisuals)
        {
            var glowing = moduleId == hovered;
            visual.Fill.Color = visual.BaseColor with { A = glowing ? 0.92f : LandFillAlpha };
            visual.Outline.DefaultColor = glowing
                ? (welcome ? Colors.White : Color.FromHtml("#9C514B"))
                : visual.BaseColor.Darkened(0.35f);
            visual.Outline.Width = glowing ? 3.0f : 2.0f;
        }
    }

    /// <summary>A tile already carrying its Voidling turns the drop away instead of glowing yes.</summary>
    private bool TileHasRoomFor(string moduleId, string creatureId)
        => _session.HasRoomOnLand(moduleId, creatureId);

    private Vector2 TileCenterOf(string moduleId)
    {
        var tile = _session.State.GardenModules.FirstOrDefault(module =>
            module.Placed && string.Equals(module.Id, moduleId, StringComparison.Ordinal));
        if (tile == null)
            return new Vector2(Hex.OriginX, Hex.OriginY);

        var (x, y) = Hex.CenterOf(tile.HexQ, tile.HexR);
        return new Vector2(x, y);
    }

    private bool WasTrainingOnLand(string creatureId)
    {
        var creature = _session.State.Voidlings.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, creatureId, StringComparison.Ordinal));
        return creature is { PassiveTrainingModuleId.Length: > 0 };
    }

    private string ModuleIdUnderPointer()
    {
        var pointer = _landRoot.ToLocal(GetGlobalMousePosition());
        var (q, r) = Hex.At(pointer.X, pointer.Y);
        return _session.State.GardenModules
            .FirstOrDefault(module => module.Placed && module.HexQ == q && module.HexR == r)?.Id ?? "";
    }

    private static Polygon2D CreateHexFill(Color color)
        => new()
        {
            Polygon = HexShape.Corners(Hex.TopEdgeWidth, Hex.Height),
            Color = color with { A = LandFillAlpha }
        };

    private static Line2D CreateHexOutline(Color color, float width = 2.0f)
        => new()
        {
            Points = HexShape.Outline(Hex.TopEdgeWidth, Hex.Height),
            DefaultColor = color,
            Width = width,
            JointMode = Line2D.LineJointMode.Round
        };

    /// <summary>
    /// Placement runs on a visible snapping grid: every hex the island can legally grow into is
    /// outlined, so the player can see where a tile will land before committing to it.
    /// </summary>
    private void BuildSnapGrid()
    {
        ClearSnapGrid();
        _snapGrid = new Node2D { ZIndex = -4, Modulate = new Color(1.0f, 1.0f, 1.0f, 0.5f) };
        _landRoot.AddChild(_snapGrid);

        // One ring beyond the island covers every hex a tile can reach from existing land.
        var columns = (int)Math.Ceiling((Hex.IslandRight - Hex.IslandLeft) / (Hex.TopEdgeWidth * 1.5f)) + 4;
        var rows = (int)Math.Ceiling((Hex.IslandBottom - Hex.IslandTop) / Hex.Height) + 4;
        for (var q = -columns; q <= columns; q++)
        {
            for (var r = -rows; r <= rows; r++)
            {
                if (!CanPlaceAt(q, r))
                    continue;

                var (x, y) = Hex.CenterOf(q, r);
                var cell = CreateHexOutline(Color.FromHtml("#F4F0D2"), 1.0f);
                cell.Position = new Vector2(x, y);
                _snapGrid.AddChild(cell);
            }
        }
    }

    private void ClearSnapGrid()
    {
        if (_snapGrid != null && GodotObject.IsInstanceValid(_snapGrid))
            _snapGrid.QueueFree();
        _snapGrid = null;
    }
}
