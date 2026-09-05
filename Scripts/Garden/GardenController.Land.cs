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
    private static readonly Color RefusedGround = Color.FromHtml("#9C514B");

    /// <summary>
    /// Hex edges paired with the neighbour they face. <see cref="HexShape.Corners"/> starts at the
    /// top-left corner and runs clockwise, so edge i spans corners i and i+1.
    /// </summary>
    private static readonly (int Q, int R)[] EdgeNeighbours =
        { (0, -1), (1, -1), (1, 0), (0, 1), (-1, 1), (-1, 0) };

    private readonly Dictionary<string, LandVisual> _landVisuals = new(StringComparer.Ordinal);
    private readonly List<Node2D> _ghostCells = new();

    /// <summary>
    /// Trees live with the actors rather than in their hex, so a Voidling standing further back
    /// than a trunk is drawn behind its leaves. Their trunks are the only solid thing on the island.
    /// </summary>
    private readonly List<Node2D> _treeProps = new();
    private readonly List<Vector2> _treeTrunks = new();

    /// <summary>Roughly the width of the drawn trunk: leaves are walk-through, wood is not.</summary>
    private const float TrunkRadius = 9.0f;

    /// <summary>How far the canopy sits above the trunk it stands on.</summary>
    private const float TreeSpriteRise = 22.0f;

    private Node2D _landRoot = null!;
    private Node2D? _coastRoot;
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

        /// <summary>Grass and clutter only. Tinting this leaves the Voidlings standing on it alone.</summary>
        public Node2D Ground { get; init; } = null!;
        public Line2D Highlight { get; init; } = null!;
        public Color BaseColor { get; init; }
        public Color IdleTint { get; init; }
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

        foreach (var tree in _treeProps)
        {
            if (GodotObject.IsInstanceValid(tree))
                tree.QueueFree();
        }
        _treeProps.Clear();
        _treeTrunks.Clear();

        var occupied = placed.Select(module => (module.HexQ, module.HexR)).ToHashSet();
        BuildCoastline(placed, occupied);
        foreach (var module in placed)
        {
            var visual = BuildHexVisual(module);
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

    private LandVisual BuildHexVisual(GardenModuleData module)
    {
        var trainingGround = module.StatId.Length > 0;
        var identity = trainingGround ? StatPresentationCatalog.ColorFor(module.StatId) : GrassEdge;
        var (x, y) = Hex.CenterOf(module.HexQ, module.HexR);
        var holder = new Node2D { Position = new Vector2(x, y), ZIndex = -4 };

        // Training ground wears its stat as a wash over the grass so it reads at a glance. The wash
        // is a modulate on the ground itself, never a polygon over the hex: an overlay would also
        // paint every Voidling standing on it.
        var idleTint = trainingGround ? Colors.White.Lerp(identity, 0.30f) : Colors.White;
        var ground = new Node2D { Modulate = idleTint };
        ground.AddChild(CreateGroundFill(new Vector2(x, y)));
        AddDecorations(ground, module, trainingGround, new Vector2(x, y));
        holder.AddChild(ground);

        var highlight = CreateHexOutline(Colors.White, 3.0f);
        highlight.Visible = false;
        holder.AddChild(highlight);

        if (trainingGround)
            AddTrainingSign(holder, module);

        _landRoot.AddChild(holder);
        return new LandVisual
        {
            Holder = holder,
            Ground = ground,
            Highlight = highlight,
            BaseColor = identity,
            IdleTint = idleTint,
            IsTrainingGround = trainingGround
        };
    }

    /// <summary>The same polygon pushed out from its centre, so neighbouring hexes overlap.</summary>
    private static Vector2[] Grown(Vector2[] polygon, float amount)
        => polygon.Select(point => point + point.Normalized() * amount).ToArray();

    /// <summary>
    /// The premium 16px ground tile, repeated across the hex. Offsetting the texture by the hex's
    /// own world position keeps the grass continuous from one hex to the next.
    /// </summary>
    private static Polygon2D CreateGroundFill(Vector2 worldPosition)
        => new()
        {
            Polygon = Grown(HexShape.Corners(Hex.TopEdgeWidth, Hex.Height), 1.5f),
            Texture = GroundTexture,
            TextureOffset = -worldPosition,
            TextureRepeat = CanvasItem.TextureRepeatEnum.Enabled
        };

    /// <summary>
    /// The shore is traced around the island as a whole, not per hex: every edge facing open water
    /// is chained into closed loops and drawn as one line. Drawing it per hex leaves a notch at
    /// every corner where two hexes meet, because each hex's line stops at its own corner.
    /// </summary>
    private void BuildCoastline(IReadOnlyList<GardenModuleData> placed, IReadOnlySet<(int Q, int R)> occupied)
    {
        if (_coastRoot != null && GodotObject.IsInstanceValid(_coastRoot))
            _coastRoot.QueueFree();
        _coastRoot = new Node2D { ZIndex = -3 };
        _landRoot.AddChild(_coastRoot);

        var corners = HexShape.Corners(Hex.TopEdgeWidth, Hex.Height);
        var open = new Dictionary<(int X, int Y), List<CoastSegment>>();
        foreach (var module in placed)
        {
            var (centerX, centerY) = Hex.CenterOf(module.HexQ, module.HexR);
            var center = new Vector2(centerX, centerY);
            for (var edge = 0; edge < EdgeNeighbours.Length; edge++)
            {
                var neighbour = (module.HexQ + EdgeNeighbours[edge].Q, module.HexR + EdgeNeighbours[edge].R);
                if (occupied.Contains(neighbour))
                    continue;

                // Corners run clockwise, so every hex hands its shore on in the same direction and
                // the segments chain into one loop around the landmass.
                var from = center + corners[edge];
                var to = center + corners[(edge + 1) % corners.Length];
                var segment = new CoastSegment(from, to, (center - (from + to) * 0.5f).Normalized());
                if (!open.TryGetValue(PointKey(from), out var starting))
                    open[PointKey(from)] = starting = new List<CoastSegment>();
                starting.Add(segment);
            }
        }

        while (open.Count > 0)
        {
            var entry = open.First();
            var loop = new List<Vector2>();
            var inwards = new List<Vector2>();
            var cursor = entry.Key;
            while (open.TryGetValue(cursor, out var candidates) && candidates.Count > 0)
            {
                var segment = candidates[0];
                candidates.RemoveAt(0);
                if (candidates.Count == 0)
                    open.Remove(cursor);

                loop.Add(segment.From);
                inwards.Add(segment.Inward);
                cursor = PointKey(segment.To);
                if (cursor == entry.Key)
                    break;
            }

            if (loop.Count > 1)
                AddCoastLoop(loop, inwards);
        }
    }

    private readonly record struct CoastSegment(Vector2 From, Vector2 To, Vector2 Inward);

    /// <summary>Corner keys are quantised so the same corner reached from two hexes matches.</summary>
    private static (int X, int Y) PointKey(Vector2 point)
        => ((int)MathF.Round(point.X * 4.0f), (int)MathF.Round(point.Y * 4.0f));

    /// <summary>Dark shore around the loop with a sand line just inside it, both continuous.</summary>
    private void AddCoastLoop(IReadOnlyList<Vector2> loop, IReadOnlyList<Vector2> inwards)
    {
        var ring = new Vector2[loop.Count + 1];
        var sand = new Vector2[loop.Count + 1];
        for (var i = 0; i < loop.Count; i++)
        {
            // A corner belongs to the segment that ends there and the one that starts there, so the
            // sand line follows the average of both, which keeps it inside on convex and concave turns.
            var previous = inwards[(i + inwards.Count - 1) % inwards.Count];
            ring[i] = loop[i];
            sand[i] = loop[i] + (previous + inwards[i]).Normalized() * 3.0f;
        }

        ring[^1] = ring[0];
        sand[^1] = sand[0];

        _coastRoot!.AddChild(new Line2D
        {
            Points = ring,
            DefaultColor = CoastShadow,
            Width = 7.0f,
            JointMode = Line2D.LineJointMode.Round,
            Closed = true
        });
        _coastRoot.AddChild(new Line2D
        {
            Points = sand,
            DefaultColor = CoastSand,
            Width = 5.0f,
            JointMode = Line2D.LineJointMode.Round,
            Closed = true,
            ZIndex = 1
        });
    }

    /// <summary>
    /// Ground clutter from the premium pack, seeded by the hex coordinate so a hex looks the same
    /// every time the Garden opens. Training ground stays clear of trees: a Voidling lives there.
    /// </summary>
    private void AddDecorations(Node2D holder, GardenModuleData module, bool trainingGround, Vector2 hexCenter)
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
        // where a Voidling would be dropped. The sprite is offset so the node itself sits at the
        // foot of the trunk, which is both what the y-sort compares and what blocks walking.
        var trunk = hexCenter + new Vector2(
            rng.RandfRange(-58.0f, 58.0f),
            -Hex.Height * 0.22f + rng.RandfRange(-8.0f, 8.0f));
        // The canopy has to be able to cover a Voidling, and z-index is checked before the y-sort:
        // a tree left on the default layer would always lose to the actor sprites on layer 2. So the
        // tree is built like an actor - a node at its feet carrying a sprite raised on that layer -
        // and the two then sort against each other by how far down the island they stand.
        var tree = new Node2D { Position = trunk };
        tree.AddChild(new Sprite2D
        {
            Texture = TreeTexture,
            Position = new Vector2(0.0f, -TreeSpriteRise),
            ZIndex = 2
        });
        _actorsRoot.AddChild(tree);
        _treeProps.Add(tree);
        _treeTrunks.Add(trunk);
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

    /// <summary>
    /// Keeps a wanderer on the island and out of the tree trunks: an off-land spot falls back to
    /// the nearest hex, and a step into a trunk is pushed back out to its edge, which reads as
    /// walking around the tree. Only the trunk is solid, so a Voidling can still stand in the leaves.
    /// </summary>
    private Vector2 ClampToLand(Vector2 position)
    {
        var (q, r) = Hex.At(position.X, position.Y);
        return TrainingUseCase.IsHexOccupied(_session.State, q, r)
            ? PushOutOfTrunks(position)
            : PushOutOfTrunks(NearestLandPoint(position));
    }

    private Vector2 PushOutOfTrunks(Vector2 position)
    {
        foreach (var trunk in _treeTrunks)
        {
            var delta = position - trunk;
            var distance = delta.Length();
            if (distance >= TrunkRadius)
                continue;

            position = trunk + (distance > 0.01f ? delta / distance : Vector2.Right) * TrunkRadius;
        }

        return position;
    }

    private Vector2 NearestLandPoint(Vector2 position)
    {
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
                visual.Ground.Modulate = visual.IdleTint;
                continue;
            }

            // Plain ground and a full hex both turn the drop away, and say so in the same colour.
            var welcome = visual.IsTrainingGround && TileHasRoomFor(moduleId, _draggedId);
            visual.Highlight.Visible = true;
            visual.Highlight.DefaultColor = welcome ? Colors.White : RefusedGround;
            visual.Ground.Modulate = welcome
                ? Colors.White.Lerp(visual.BaseColor, 0.55f)
                : Colors.White.Lerp(RefusedGround, 0.40f);
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
