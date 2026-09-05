using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Voidling.Application.Garden;
using Voidling.Application.Training;
using Voidling.Domain.Garden;
using Voidling.Presentation.UI.Common;

namespace VoidlingGame;

public partial class GardenController
{
    /// <summary>Raised when land placement is armed or cleared so the HUD can show its own hint.</summary>
    public event Action<bool>? LandPlacementModeChanged;

    /// <summary>Raised when the player clicks a hex of the island, to open its ground menu.</summary>
    public event Action<string>? LandHexSelected;

    /// <summary>Leaves a margin inside the hex so a roaming trainee never overhangs its edge.</summary>
    private const float TileResidentRoamFraction = 0.62f;

    private const string GroundSheetPath =
        "res://Assets/Sprout Lands - Sprites - premium pack/Tilesets/ground tiles/New tiles/Grass_tiles_v2.png";

    private static readonly Texture2D GroundSheet = GD.Load<Texture2D>(GroundSheetPath);

    /// <summary>
    /// The plain middle tile of the premium grass set, cut out as its own texture so it can repeat
    /// across a hex. An atlas region cannot tile, and the "simple cutout" files are greyscale masks.
    /// </summary>
    private static readonly Texture2D GroundTexture = CutGroundTile(1, 1);

    /// <summary>Grass details from the same sheet, scattered so no two hexes look stamped.</summary>
    private static readonly Texture2D[] GroundDetailTextures =
    {
        GroundCell(0, 5), GroundCell(1, 5), GroundCell(2, 5), GroundCell(3, 5),
        GroundCell(4, 5), GroundCell(5, 5), GroundCell(0, 6), GroundCell(3, 6), GroundCell(5, 6)
    };

    private static readonly Texture2D TreeTexture = new AtlasTexture
    {
        Atlas = GD.Load<Texture2D>("res://Assets/Sprout Lands - Sprites - premium pack/Objects/Trees, stumps and bushes.png"),
        Region = new Rect2(144, 48, 48, 50)
    };

    private static readonly Texture2D SignTexture = new AtlasTexture
    {
        Atlas = GD.Load<Texture2D>("res://Assets/Sprout Lands - Sprites - premium pack/Objects/signs.png"),
        Region = new Rect2(0, 0, 16, 16)
    };

    private const int GroundCellSize = 16;

    private static Texture2D GroundCell(int column, int row)
        => new AtlasTexture
        {
            Atlas = GroundSheet,
            Region = new Rect2(column * GroundCellSize, row * GroundCellSize, GroundCellSize, GroundCellSize)
        };

    private static Texture2D CutGroundTile(int column, int row)
    {
        var sheet = GroundSheet.GetImage();
        if (sheet.IsCompressed())
            sheet.Decompress();

        var tile = Image.CreateEmpty(GroundCellSize, GroundCellSize, false, sheet.GetFormat());
        tile.BlitRect(
            sheet,
            new Rect2I(column * GroundCellSize, row * GroundCellSize, GroundCellSize, GroundCellSize),
            Vector2I.Zero);
        return ImageTexture.CreateFromImage(tile);
    }

    private static readonly Color CoastSand = Color.FromHtml("#E4C58C");
    private static readonly Color CoastShadow = Color.FromHtml("#8A6A46");
    private static readonly Color GrassEdge = Color.FromHtml("#5E9455");

    /// <summary>
    /// Hex edges paired with the neighbour they face. <see cref="HexShape.Corners"/> starts at the
    /// top-left corner and runs clockwise, so edge i spans corners i and i+1.
    /// </summary>
    private static readonly (int Q, int R)[] EdgeNeighbours =
        { (0, -1), (1, -1), (1, 0), (0, 1), (-1, 1), (-1, 0) };

    private readonly Dictionary<string, LandVisual> _landVisuals = new(StringComparer.Ordinal);
    private readonly List<Node2D> _ghostCells = new();

    private Node2D _landRoot = null!;
    private string _placingModuleId = "";
    private GardenTileShape _placingShape = GardenTileShape.Single;
    private int _placingRotation;
    private Node2D? _landGhost;
    private Node2D? _snapGrid;
    private string _hoveredModuleId = "";
    private string _landSignature = "";
    private Rect2 _landBounds = new(new Vector2(311, 150), new Vector2(210, 180));

    public bool IsPlacingLand => _placingModuleId.Length > 0;

    /// <summary>Bounding box of every hex that is down, which is as far as anything can roam.</summary>
    public Rect2 LandBounds => _landBounds;

    private static GardenHexLayout Hex => GameRules.GardenModuleRules.Hex;

    private sealed class LandVisual
    {
        public Node2D Holder { get; init; } = null!;
        public Polygon2D Overlay { get; init; } = null!;
        public Line2D Highlight { get; init; } = null!;
        public Color BaseColor { get; init; }
        public bool IsTrainingGround { get; init; }
    }

    /// <summary>Arms "click the Garden to grow the island here" for one owned piece of land.</summary>
    public void BeginLandPlacement(string moduleId, string shapeId)
    {
        if (string.IsNullOrWhiteSpace(moduleId))
            return;

        CancelLandPlacement();
        _placingModuleId = moduleId;
        _placingShape = GardenTileShape.Find(shapeId) ?? GardenTileShape.Single;
        _placingRotation = 0;

        _landGhost = new Node2D { ZIndex = -3 };
        _landRoot.AddChild(_landGhost);
        BuildGhostCells();
        BuildSnapGrid();
        LandPlacementModeChanged?.Invoke(true);
    }

    /// <summary>Turns the piece under the cursor by a sixth of a turn while it is being placed.</summary>
    public void RotateLandPlacement(int steps)
    {
        if (!IsPlacingLand || _placingShape.RotationCount <= 1)
            return;

        _placingRotation = ((_placingRotation + steps) % 6 + 6) % 6;
        BuildGhostCells();
        UpdateLandGhost();
    }

    public void CancelLandPlacement()
    {
        ClearSnapGrid();
        if (_landGhost != null && GodotObject.IsInstanceValid(_landGhost))
            _landGhost.QueueFree();
        _landGhost = null;
        _ghostCells.Clear();

        if (_placingModuleId.Length == 0)
            return;

        _placingModuleId = "";
        _placingRotation = 0;
        LandPlacementModeChanged?.Invoke(false);
    }

    private void BuildGhostCells()
    {
        if (_landGhost == null || !GodotObject.IsInstanceValid(_landGhost))
            return;

        foreach (var cell in _ghostCells)
            cell.QueueFree();
        _ghostCells.Clear();

        foreach (var (offsetQ, offsetR) in _placingShape.CellsRotated(_placingRotation))
        {
            var cell = new Node2D { Position = OffsetToLocal(offsetQ, offsetR) };
            cell.AddChild(CreateGroundFill(Vector2.Zero));
            cell.AddChild(CreateHexOutline(Colors.White, 2.0f));
            _landGhost.AddChild(cell);
            _ghostCells.Add(cell);
        }
    }

    /// <summary>Where a shape cell sits relative to the hex the cursor is on.</summary>
    private static Vector2 OffsetToLocal(int offsetQ, int offsetR)
    {
        var (anchorX, anchorY) = Hex.CenterOf(0, 0);
        var (x, y) = Hex.CenterOf(offsetQ, offsetR);
        return new Vector2(x - anchorX, y - anchorY);
    }

    private void UpdateLandGhost()
    {
        if (_landGhost == null || !GodotObject.IsInstanceValid(_landGhost))
            return;

        var pointer = _landRoot.ToLocal(GetGlobalMousePosition());
        var (q, r) = Hex.At(pointer.X, pointer.Y);
        var (x, y) = Hex.CenterOf(q, r);
        _landGhost.Position = new Vector2(x, y);

        // The whole piece answers yes or no together: a three-hex piece that only half fits does
        // not fit at all, and the player should see that before letting go.
        _landGhost.Modulate = CanPlaceShapeAt(q, r)
            ? new Color(0.75f, 1.0f, 0.75f, 0.85f)
            : new Color(1.0f, 0.55f, 0.5f, 0.55f);
    }

    private bool CanPlaceShapeAt(int q, int r)
        => GardenHexLayout.CanPlaceShape(
            _placingShape.CellsRotated(_placingRotation),
            q,
            r,
            (candidateQ, candidateR) => TrainingUseCase.IsHexOccupied(_session.State, candidateQ, candidateR));

    // The piece lands on the hex the click actually happened over rather than wherever the cached
    // pointer position last settled, so the drop point always matches what the player aimed at.
    private void TryCompleteLandPlacement(Vector2 viewportPosition)
    {
        if (_placingModuleId.Length == 0)
            return;

        var moduleId = _placingModuleId;
        var rotation = _placingRotation;
        var world = _landRoot.ToLocal(GetCanvasTransform().AffineInverse() * viewportPosition);
        var (q, r) = Hex.At(world.X, world.Y);
        // A miss keeps placement armed and lets the session explain why the piece does not fit.
        if (_session.PlaceGardenModule(moduleId, q, r, rotation))
            CancelLandPlacement();
    }

    private void RefreshLand()
    {
        var placed = _session.State.GardenModules.Where(module => module.Placed).ToList();
        var signature = string.Join(
            "|",
            placed.OrderBy(module => module.Id, StringComparer.Ordinal)
                .Select(module => $"{module.Id}:{module.HexQ},{module.HexR}:{module.StatId}:{module.Level}"));
        if (signature == _landSignature && _landVisuals.Count > 0)
            return;

        // A hex's coastline depends on its neighbours, so the island is rebuilt as a whole whenever
        // it changes. It is a handful of hexes, not a tilemap.
        var known = new HashSet<string>(_landVisuals.Keys, StringComparer.Ordinal);
        _landSignature = signature;
        foreach (var visual in _landVisuals.Values)
            visual.Holder.QueueFree();
        _landVisuals.Clear();

        var occupied = placed.Select(module => (module.HexQ, module.HexR)).ToHashSet();
        foreach (var module in placed)
        {
            var visual = BuildHexVisual(module, occupied);
            _landVisuals[module.Id] = visual;

            if (_initialRefreshComplete && !known.Contains(module.Id))
            {
                visual.Holder.Scale = Vector2.Zero;
                CreateTween().TweenProperty(visual.Holder, "scale", Vector2.One, 0.4)
                    .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            }
        }

        _landBounds = BoundsOf(placed);
        _hoveredModuleId = "";
        if (IsPlacingLand)
            BuildSnapGrid();
    }

    private LandVisual BuildHexVisual(GardenModuleData module, IReadOnlySet<(int Q, int R)> occupied)
    {
        var trainingGround = module.StatId.Length > 0;
        var identity = trainingGround ? StatPresentationCatalog.ColorFor(module.StatId) : GrassEdge;
        var (x, y) = Hex.CenterOf(module.HexQ, module.HexR);
        var holder = new Node2D { Position = new Vector2(x, y), ZIndex = -4 };

        holder.AddChild(CreateGroundFill(new Vector2(x, y)));

        // Training ground wears its stat as a wash over the grass so it reads at a glance.
        var overlay = new Polygon2D
        {
            Polygon = HexShape.Corners(Hex.TopEdgeWidth, Hex.Height),
            Color = identity with { A = trainingGround ? 0.22f : 0.0f }
        };
        holder.AddChild(overlay);

        AddDecorations(holder, module, trainingGround);
        AddCoastline(holder, module, occupied);

        var highlight = CreateHexOutline(Colors.White, 3.0f);
        highlight.Visible = false;
        holder.AddChild(highlight);

        if (trainingGround)
            AddTrainingSign(holder, module);

        _landRoot.AddChild(holder);
        return new LandVisual
        {
            Holder = holder,
            Overlay = overlay,
            Highlight = highlight,
            BaseColor = identity,
            IsTrainingGround = trainingGround
        };
    }

    /// <summary>
    /// The premium 16px ground tile, repeated across the hex. Offsetting the texture by the hex's
    /// own world position keeps the grass continuous from one hex to the next.
    /// </summary>
    private static Polygon2D CreateGroundFill(Vector2 worldPosition)
        => new()
        {
            Polygon = HexShape.Corners(Hex.TopEdgeWidth, Hex.Height),
            Texture = GroundTexture,
            TextureOffset = -worldPosition,
            TextureRepeat = CanvasItem.TextureRepeatEnum.Enabled
        };

    /// <summary>
    /// Only the edges facing open water get a coast, so a placed piece reads as one landmass
    /// instead of a pile of separate tiles.
    /// </summary>
    private static void AddCoastline(Node2D holder, GardenModuleData module, IReadOnlySet<(int Q, int R)> occupied)
    {
        var corners = HexShape.Corners(Hex.TopEdgeWidth, Hex.Height);
        for (var edge = 0; edge < EdgeNeighbours.Length; edge++)
        {
            var neighbour = (module.HexQ + EdgeNeighbours[edge].Q, module.HexR + EdgeNeighbours[edge].R);
            if (occupied.Contains(neighbour))
                continue;

            var from = corners[edge];
            var to = corners[(edge + 1) % corners.Length];
            var inward = -((from + to) * 0.5f).Normalized();

            holder.AddChild(new Line2D
            {
                Points = new[] { from, to },
                DefaultColor = CoastShadow,
                Width = 7.0f,
                ZIndex = 1
            });
            holder.AddChild(new Line2D
            {
                Points = new[] { from + inward * 3.0f, to + inward * 3.0f },
                DefaultColor = CoastSand,
                Width = 5.0f,
                ZIndex = 2
            });
        }
    }

    /// <summary>
    /// Ground clutter from the premium pack, seeded by the hex coordinate so a hex looks the same
    /// every time the Garden opens. Training ground stays clear of trees: a Voidling lives there.
    /// </summary>
    private static void AddDecorations(Node2D holder, GardenModuleData module, bool trainingGround)
    {
        var rng = new RandomNumberGenerator
        {
            Seed = unchecked((ulong)(module.HexQ * 73856093L ^ module.HexR * 19349663L ^ 0x5EEDL))
        };

        var reach = Hex.InnerRadius * 0.78f;
        var detailCount = rng.RandiRange(4, 7);
        for (var i = 0; i < detailCount; i++)
        {
            var angle = rng.RandfRange(0.0f, Mathf.Tau);
            var distance = Mathf.Sqrt(rng.Randf()) * reach;
            holder.AddChild(new Sprite2D
            {
                Texture = GroundDetailTextures[rng.RandiRange(0, GroundDetailTextures.Length - 1)],
                Position = Vector2.Right.Rotated(angle) * distance,
                ZIndex = 1
            });
        }

        if (trainingGround)
            return;

        // Plain ground gets a tree towards its back edge: it dresses the island without standing
        // where a Voidling would be dropped.
        holder.AddChild(new Sprite2D
        {
            Texture = TreeTexture,
            Position = new Vector2(
                rng.RandfRange(-58.0f, 58.0f),
                -Hex.Height * 0.22f + rng.RandfRange(-8.0f, 8.0f)),
            ZIndex = 3
        });
    }

    /// <summary>A premium signboard names what the ground trains, and its level once upgraded.</summary>
    private static void AddTrainingSign(Node2D holder, GardenModuleData module)
    {
        var color = StatPresentationCatalog.ColorFor(module.StatId);
        var sign = new Sprite2D
        {
            Texture = SignTexture,
            Position = new Vector2(0.0f, -Hex.Height * 0.30f),
            Scale = new Vector2(2.0f, 2.0f),
            ZIndex = 4
        };
        holder.AddChild(sign);

        var caption = module.Level > 1
            ? $"{StatPresentationCatalog.NameFor(module.StatId).ToUpperInvariant()} L{module.Level}"
            : StatPresentationCatalog.NameFor(module.StatId).ToUpperInvariant();
        var label = UiFactory.CreateLabel(caption, 6);
        label.Position = new Vector2(-46.0f, sign.Position.Y - 6.0f);
        label.CustomMinimumSize = new Vector2(92.0f, 12.0f);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.AddThemeColorOverride("font_color", color.Darkened(0.55f));
        label.ZIndex = 5;
        holder.AddChild(label);
    }

    private static Rect2 BoundsOf(IReadOnlyList<GardenModuleData> placed)
    {
        var bounds = new Rect2(
            new Vector2(Hex.OriginX - Hex.TopEdgeWidth, Hex.OriginY - Hex.Height * 0.5f),
            new Vector2(Hex.Width, Hex.Height));

        foreach (var module in placed)
        {
            var (x, y) = Hex.CenterOf(module.HexQ, module.HexR);
            bounds = bounds.Merge(new Rect2(
                new Vector2(x - Hex.TopEdgeWidth, y - Hex.Height * 0.5f),
                new Vector2(Hex.Width, Hex.Height)));
        }

        return bounds;
    }

    /// <summary>Keeps a wanderer on the island: an off-land spot falls back to the nearest hex.</summary>
    private Vector2 ClampToLand(Vector2 position)
    {
        var (q, r) = Hex.At(position.X, position.Y);
        if (TrainingUseCase.IsHexOccupied(_session.State, q, r))
            return position;

        var nearest = position;
        var bestDistance = float.MaxValue;
        foreach (var module in _session.State.GardenModules)
        {
            if (!module.Placed)
                continue;

            var (x, y) = Hex.CenterOf(module.HexQ, module.HexR);
            var center = new Vector2(x, y);
            var distance = center.DistanceSquaredTo(position);
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            // Aim a little way in from the centre so arrivals do not all stack on one point.
            nearest = center + (position - center).LimitLength(Hex.InnerRadius * 0.6f);
        }

        return nearest;
    }

    /// <summary>
    /// A Voidling training on a hex lives on that hex: it roams inside it playing the activity its
    /// ground trains, and only leaves when the player carries it off.
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

            actor.LandClamp = ClampToLand;
            actor.SetWanderArea(_landBounds);

            if (creature.PassiveTrainingModuleId.Length > 0 &&
                placedById.TryGetValue(creature.PassiveTrainingModuleId, out var tile) &&
                tile.StatId.Length > 0)
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
    /// While a Voidling is held, the hex under the pointer glows to advertise that letting go there
    /// starts training. Assignment is semantic, so the pointer decides, not the actor body.
    /// </summary>
    private void UpdateLandHover()
    {
        var hovered = _draggedId.Length > 0 ? ModuleIdUnderPointer() : "";
        if (hovered == _hoveredModuleId)
            return;

        _hoveredModuleId = hovered;
        foreach (var (moduleId, visual) in _landVisuals)
        {
            if (moduleId != hovered)
            {
                visual.Highlight.Visible = false;
                visual.Overlay.Color = visual.BaseColor with { A = visual.IsTrainingGround ? 0.22f : 0.0f };
                continue;
            }

            // Plain ground and a full hex both turn the drop away, and say so in the same colour.
            var welcome = visual.IsTrainingGround && TileHasRoomFor(moduleId, _draggedId);
            visual.Highlight.Visible = true;
            visual.Highlight.DefaultColor = welcome ? Colors.White : Color.FromHtml("#9C514B");
            visual.Overlay.Color = visual.BaseColor with { A = welcome ? 0.42f : 0.30f };
        }
    }

    /// <summary>Whether a placed hex has been built into training ground for a stat.</summary>
    private bool IsTrainingGround(string moduleId)
        => _session.State.GardenModules.Any(module =>
            module.Placed &&
            module.StatId.Length > 0 &&
            string.Equals(module.Id, moduleId, StringComparison.Ordinal));

    /// <summary>A hex already carrying its Voidling turns the drop away instead of glowing yes.</summary>
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

    /// <summary>A plain click on a hex opens that ground's menu instead of panning the camera.</summary>
    private void SelectLandHexUnderPointer()
    {
        var moduleId = ModuleIdUnderPointer();
        if (moduleId.Length > 0)
            LandHexSelected?.Invoke(moduleId);
    }

    private static Line2D CreateHexOutline(Color color, float width = 2.0f)
        => new()
        {
            Points = HexShape.Outline(Hex.TopEdgeWidth, Hex.Height),
            DefaultColor = color,
            Width = width,
            JointMode = Line2D.LineJointMode.Round,
            ZIndex = 6
        };

    /// <summary>
    /// Placement runs on a visible snapping grid: every hex the island can legally grow into is
    /// outlined, so the player can see where a piece will land before committing to it.
    /// </summary>
    private void BuildSnapGrid()
    {
        ClearSnapGrid();
        _snapGrid = new Node2D { ZIndex = -5, Modulate = new Color(1.0f, 1.0f, 1.0f, 0.45f) };
        _landRoot.AddChild(_snapGrid);

        // Candidates are exactly the empty neighbours of land that is already down.
        var occupied = _session.State.GardenModules
            .Where(module => module.Placed)
            .Select(module => (module.HexQ, module.HexR))
            .ToHashSet();
        var candidates = new HashSet<(int Q, int R)>();
        foreach (var (q, r) in occupied)
        {
            foreach (var neighbour in GardenHexLayout.NeighboursOf(q, r))
            {
                if (!occupied.Contains(neighbour))
                    candidates.Add(neighbour);
            }
        }

        foreach (var (q, r) in candidates)
        {
            var (x, y) = Hex.CenterOf(q, r);
            var cell = CreateHexOutline(Color.FromHtml("#F4F0D2"), 2.0f);
            cell.Position = new Vector2(x, y);
            _snapGrid.AddChild(cell);
        }
    }

    private void ClearSnapGrid()
    {
        if (_snapGrid != null && GodotObject.IsInstanceValid(_snapGrid))
            _snapGrid.QueueFree();
        _snapGrid = null;
    }
}
