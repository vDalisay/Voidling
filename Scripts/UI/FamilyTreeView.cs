using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace VoidlingGame;

public partial class FamilyTreeView : Control
{
    public event Action<string>? MemberSelected;

    internal const float MinZoom = 0.15f;
    internal const float MaxZoom = 2.0f;
    private const float ZoomStep = 1.15f;
    private const int FlowChevronsPerPath = 3;
    private const float FlowTravelPerSecond = 0.16f;
    private const float FlowChevronHalfWidth = 3.2f;
    private const float FlowChevronDepth = 3.6f;

    private static readonly Vector2 ViewportSize = new(425, 252);
    private readonly Dictionary<string, Rect2> _baseCardRects = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PanelContainer> _cardPanels = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Label> _cardNameLabels = new(StringComparer.Ordinal);
    private readonly Dictionary<string, VoidlingData> _membersById = new(StringComparer.Ordinal);
    private readonly HashSet<string> _departedIds = new(StringComparer.Ordinal);
    private readonly List<string> _visibleMemberIds = new();

    private Control _worldLayer = null!;
    private string _selectedId = "";
    private string _highlightedConnectionKey = "";
    private Vector2 _panOffset;
    private Vector2 _contentSize;
    private float _zoom = 1.0f;
    private bool _panning;

    internal readonly struct ConnectionSegment
    {
        public ConnectionSegment(Vector2 from, Vector2 to)
        {
            From = from;
            To = to;
        }

        public Vector2 From { get; }
        public Vector2 To { get; }
    }

    internal sealed class ConnectionPath
    {
        public string Key { get; init; } = "";
        public string ParentId { get; init; } = "";
        public string ChildId { get; init; } = "";
        public List<ConnectionSegment> Segments { get; } = new();
    }

    public FamilyTreeView()
    {
        CustomMinimumSize = ViewportSize;
        Size = ViewportSize;
        ClipContents = true;
        MouseFilter = MouseFilterEnum.Stop;
    }

    public void Build(
        string selectedId,
        IReadOnlyList<VoidlingData> activeVoidlings,
        IReadOnlyList<VoidlingData> departedVoidlings)
    {
        foreach (var child in GetChildren())
            child.QueueFree();

        _worldLayer = new Control { MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_worldLayer);

        _baseCardRects.Clear();
        _cardPanels.Clear();
        _cardNameLabels.Clear();
        _membersById.Clear();
        _departedIds.Clear();
        _visibleMemberIds.Clear();
        _selectedId = selectedId;
        _highlightedConnectionKey = "";

        foreach (var member in activeVoidlings)
            _membersById[member.Id] = member;
        foreach (var member in departedVoidlings)
        {
            _membersById[member.Id] = member;
            _departedIds.Add(member.Id);
        }

        if (!_membersById.ContainsKey(selectedId))
            return;

        var allMembers = _membersById.Values.ToList();
        var connectedIds = CollectConnectedFamily(selectedId, allMembers, _membersById);
        var members = connectedIds.Select(id => _membersById[id]).ToList();
        _visibleMemberIds.AddRange(connectedIds);

        var groups = members
            .GroupBy(v => v.FamilyGeneration)
            .OrderBy(g => g.Key)
            .ToList();

        const float cardWidth = 108.0f;
        const float cardHeight = 82.0f;
        const float horizontalGap = 34.0f;
        const float verticalGap = 50.0f;
        const float margin = 28.0f;

        var widest = groups.Count == 0 ? 1 : groups.Max(g => g.Count());
        var contentWidth = Math.Max(ViewportSize.X, margin * 2 + widest * cardWidth + Math.Max(0, widest - 1) * horizontalGap);
        var contentHeight = Math.Max(ViewportSize.Y, margin * 2 + groups.Count * cardHeight + Math.Max(0, groups.Count - 1) * verticalGap);
        _contentSize = new Vector2(contentWidth, contentHeight);

        for (var generationIndex = 0; generationIndex < groups.Count; generationIndex++)
        {
            var generationMembers = groups[generationIndex].ToList();
            generationMembers.Sort((left, right) =>
            {
                var leftAnchor = ParentAnchor(left);
                var rightAnchor = ParentAnchor(right);
                var comparison = leftAnchor.CompareTo(rightAnchor);
                return comparison != 0
                    ? comparison
                    : string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
            });

            var rowWidth = generationMembers.Count * cardWidth + Math.Max(0, generationMembers.Count - 1) * horizontalGap;
            var startX = (contentWidth - rowWidth) * 0.5f;
            var y = margin + generationIndex * (cardHeight + verticalGap);

            for (var i = 0; i < generationMembers.Count; i++)
            {
                var member = generationMembers[i];
                var rect = new Rect2(
                    new Vector2(startX + i * (cardWidth + horizontalGap), y),
                    new Vector2(cardWidth, cardHeight));
                _baseCardRects[member.Id] = rect;
                AddCard(member, rect, _departedIds.Contains(member.Id));
            }
        }

        CenterOnSelected();
        ApplyView();
        ApplySelectionState();
        QueueRedraw();
    }

    public void SetSelectedMember(string memberId)
    {
        _selectedId = memberId;
        _highlightedConnectionKey = "";
        ApplySelectionState();
        QueueRedraw();
    }

    public override void _GuiInput(InputEvent inputEvent)
    {
        if (inputEvent is InputEventMouseButton mouse)
        {
            if (mouse.ButtonIndex == MouseButton.Left)
            {
                if (mouse.Pressed)
                {
                    if (TryHighlightConnectionAt(mouse.Position))
                    {
                        _panning = false;
                        AcceptEvent();
                        return;
                    }

                    _panning = true;
                }
                else
                {
                    _panning = false;
                }

                AcceptEvent();
                return;
            }

            if (mouse.ButtonIndex == MouseButton.Middle)
            {
                _panning = mouse.Pressed;
                AcceptEvent();
                return;
            }

            if (mouse.Pressed &&
                mouse.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
            {
                ZoomAt(mouse.Position, mouse.ButtonIndex == MouseButton.WheelUp ? ZoomStep : 1.0f / ZoomStep);
                AcceptEvent();
                return;
            }
        }

        if (inputEvent is InputEventMouseMotion motion && _panning)
        {
            _panOffset += motion.Relative;
            ClampPan();
            ApplyView();
            QueueRedraw();
            AcceptEvent();
        }
    }

    public override void _Draw()
    {
        // Connections are authored in content space; one transform keeps them locked to the cards
        // at any zoom. Strokes divide it back out so hairlines stay readable when zoomed far out.
        DrawSetTransform(_panOffset, 0.0f, Vector2.One * _zoom);
        var stroke = 1.0f / Math.Max(_zoom, 0.0001f);

        var paths = BuildConnectionPaths();
        var lineColor = Color.FromHtml("#E0DDC5");

        // Paint the neutral genealogy first, then selected routes last. Drawing the
        // highlight last with a dark outline keeps it legible at crossings/overlaps.
        foreach (var path in paths)
            DrawConnectionPath(path, lineColor, 2.0f * stroke);

        foreach (var path in paths.Where(IsConnectionHighlighted))
        {
            DrawConnectionPath(path, Color.FromHtml("#6A5841"), 5.0f * stroke);
            DrawConnectionPath(path, Color.FromHtml("#FFD96A"), 3.0f * stroke);
        }

        // Only the selected lineage carries flow, and it drifts slowly parent to child so the
        // direction of inheritance reads without the whole tree shimmering.
        foreach (var path in paths.Where(IsConnectionHighlighted))
            DrawConnectionFlow(path, stroke);
    }

    // A slow procession of chevrons riding the highlighted lane, each fading in and out across its
    // travel so nothing pops at the ends.
    private void DrawConnectionFlow(ConnectionPath path, float stroke)
    {
        var total = path.Segments.Sum(segment => segment.From.DistanceTo(segment.To));
        if (total <= 1.0f)
            return;

        var phase = (float)Time.GetTicksMsec() / 1000.0f * FlowTravelPerSecond;
        for (var i = 0; i < FlowChevronsPerPath; i++)
        {
            var fraction = Mathf.PosMod(phase + (float)i / FlowChevronsPerPath, 1.0f);
            if (!TryPointAlongPath(path, fraction * total, out var point) ||
                !TryPointAlongPath(path, Math.Min(total, fraction * total + 2.0f), out var ahead))
            {
                continue;
            }

            var heading = ahead - point;
            if (heading.LengthSquared() < 0.0001f)
                continue;

            var alpha = Mathf.Sin(fraction * Mathf.Pi) * 0.85f;
            DrawChevron(point, heading.Normalized(), stroke, ChevronColor(fraction, alpha));
        }
    }

    // Warm amber shifting toward gold along the lane, matching the highlighted route it rides.
    private static Color ChevronColor(float fraction, float alpha)
    {
        var color = Color.FromHtml("#F09A3C").Lerp(Color.FromHtml("#FFE07A"), Mathf.Sin(fraction * Mathf.Pi));
        color.A = alpha;
        return color;
    }

    private void DrawChevron(Vector2 center, Vector2 heading, float stroke, Color color)
    {
        var side = new Vector2(-heading.Y, heading.X);
        var tip = center + heading * (FlowChevronDepth * 0.5f) * stroke;
        var left = center - heading * (FlowChevronDepth * 0.5f) * stroke + side * FlowChevronHalfWidth * stroke;
        var right = center - heading * (FlowChevronDepth * 0.5f) * stroke - side * FlowChevronHalfWidth * stroke;

        DrawLine(left, tip, color, 2.0f * stroke, true);
        DrawLine(right, tip, color, 2.0f * stroke, true);
    }

    internal static bool TryPointAlongPath(ConnectionPath path, float distance, out Vector2 point)
    {
        foreach (var segment in path.Segments)
        {
            var length = segment.From.DistanceTo(segment.To);
            if (length <= 0.0001f)
                continue;
            if (distance <= length)
            {
                point = segment.From.Lerp(segment.To, distance / length);
                return true;
            }
            distance -= length;
        }

        point = Vector2.Zero;
        return false;
    }

    private void ZoomAt(Vector2 viewportPoint, float factor)
    {
        var next = Mathf.Clamp(_zoom * factor, MinZoom, MaxZoom);
        if (Mathf.IsEqualApprox(next, _zoom))
            return;

        var anchored = ToContentSpace(viewportPoint);
        _zoom = next;
        _panOffset = viewportPoint - anchored * _zoom;
        ClampPan();
        ApplyView();
        QueueRedraw();
    }

    private Vector2 ToContentSpace(Vector2 viewportPoint)
        => (viewportPoint - _panOffset) / Math.Max(_zoom, 0.0001f);

    private List<ConnectionPath> BuildConnectionPaths()
    {
        var paths = new List<ConnectionPath>();

        foreach (var childId in _visibleMemberIds)
        {
            if (!_membersById.TryGetValue(childId, out var child) ||
                !_baseCardRects.TryGetValue(childId, out var childBase))
                continue;

            var parents = new List<(string Id, Rect2 Rect)>();
            if (!string.IsNullOrWhiteSpace(child.ParentAId) && _baseCardRects.TryGetValue(child.ParentAId, out var parentA))
                parents.Add((child.ParentAId, parentA));
            if (!string.IsNullOrWhiteSpace(child.ParentBId) && _baseCardRects.TryGetValue(child.ParentBId, out var parentB))
                parents.Add((child.ParentBId, parentB));

            if (parents.Count == 0)
                continue;

            var childRect = childBase;
            var childTop = new Vector2(childRect.GetCenter().X, childRect.Position.Y);
            var highestParentBottom = parents.Max(parent => parent.Rect.End.Y);
            var junctionY = highestParentBottom + Math.Max(12.0f, (childTop.Y - highestParentBottom) * 0.46f);

            if (parents.Count == 1)
            {
                var parent = parents[0];
                var parentBottom = new Vector2(parent.Rect.GetCenter().X, parent.Rect.End.Y);
                var path = NewPath(parent.Id, childId);
                path.Segments.Add(new ConnectionSegment(parentBottom, new Vector2(parentBottom.X, junctionY)));
                path.Segments.Add(new ConnectionSegment(new Vector2(parentBottom.X, junctionY), new Vector2(childTop.X, junctionY)));
                path.Segments.Add(new ConnectionSegment(new Vector2(childTop.X, junctionY), childTop));
                paths.Add(path);
                continue;
            }

            var ordered = parents.OrderBy(parent => parent.Rect.GetCenter().X).ToList();
            var leftX = ordered.First().Rect.GetCenter().X;
            var rightX = ordered.Last().Rect.GetCenter().X;
            var coupleX = (leftX + rightX) * 0.5f;
            var branchY = Math.Min(childTop.Y - 10.0f, junctionY + 16.0f);

            foreach (var parent in ordered)
            {
                var parentBottom = new Vector2(parent.Rect.GetCenter().X, parent.Rect.End.Y);
                var path = NewPath(parent.Id, childId);
                path.Segments.Add(new ConnectionSegment(parentBottom, new Vector2(parentBottom.X, junctionY)));
                path.Segments.Add(new ConnectionSegment(new Vector2(parentBottom.X, junctionY), new Vector2(coupleX, junctionY)));
                path.Segments.Add(new ConnectionSegment(new Vector2(coupleX, junctionY), new Vector2(coupleX, branchY)));
                path.Segments.Add(new ConnectionSegment(new Vector2(coupleX, branchY), new Vector2(childTop.X, branchY)));
                path.Segments.Add(new ConnectionSegment(new Vector2(childTop.X, branchY), childTop));
                paths.Add(path);
            }
        }

        return paths;
    }

    private static ConnectionPath NewPath(string parentId, string childId)
        => new()
        {
            Key = $"{parentId}>{childId}",
            ParentId = parentId,
            ChildId = childId
        };

    private void DrawConnectionPath(ConnectionPath path, Color color, float width)
    {
        foreach (var segment in path.Segments)
            DrawLine(segment.From, segment.To, color, width, true);
    }

    private bool IsConnectionHighlighted(ConnectionPath path)
    {
        if (_highlightedConnectionKey.Length > 0)
            return path.Key == _highlightedConnectionKey;

        return path.ParentId == _selectedId || path.ChildId == _selectedId;
    }

    private bool TryHighlightConnectionAt(Vector2 mousePosition)
    {
        var hitRadius = 7.0f / Math.Max(_zoom, 0.0001f);
        mousePosition = ToContentSpace(mousePosition);
        ConnectionPath? nearest = null;
        var nearestDistance = float.MaxValue;

        foreach (var path in BuildConnectionPaths())
        {
            foreach (var segment in path.Segments)
            {
                var distance = DistanceToSegment(mousePosition, segment.From, segment.To);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = path;
                }
            }
        }

        if (nearest == null || nearestDistance > hitRadius)
            return false;

        _highlightedConnectionKey = nearest.Key;
        QueueRedraw();
        return true;
    }

    private static float DistanceToSegment(Vector2 point, Vector2 from, Vector2 to)
    {
        var segment = to - from;
        var lengthSquared = segment.LengthSquared();
        if (lengthSquared <= 0.0001f)
            return point.DistanceTo(from);

        var t = Mathf.Clamp((point - from).Dot(segment) / lengthSquared, 0.0f, 1.0f);
        return point.DistanceTo(from + segment * t);
    }

    private void CenterOnSelected()
    {
        if (!_baseCardRects.TryGetValue(_selectedId, out var selected))
        {
            _panOffset = Vector2.Zero;
            return;
        }

        _panOffset = ViewportSize * 0.5f - selected.GetCenter() * _zoom;
        ClampPan();
    }

    private void ClampPan()
    {
        var scaled = _contentSize * _zoom;
        _panOffset.X = ClampAxis(_panOffset.X, ViewportSize.X, scaled.X);
        _panOffset.Y = ClampAxis(_panOffset.Y, ViewportSize.Y, scaled.Y);
    }

    // Content smaller than the viewport, which zooming out always reaches, is centred instead of
    // pinned to the top-left corner.
    internal static float ClampAxis(float offset, float viewport, float content)
        => content <= viewport
            ? (viewport - content) * 0.5f
            : Mathf.Clamp(offset, viewport - content, 0.0f);

    private void ApplyView()
    {
        if (_worldLayer == null || !GodotObject.IsInstanceValid(_worldLayer))
            return;

        _worldLayer.Position = _panOffset;
        _worldLayer.Scale = Vector2.One * _zoom;
    }

    private void ApplySelectionState()
    {
        foreach (var pair in _cardPanels)
        {
            pair.Value.Modulate = pair.Key == _selectedId
                ? new Color(0.78f, 0.72f, 0.62f, 1.0f)
                : Colors.White;
        }

        var emphasised = EmphasisedMemberIds();
        foreach (var pair in _cardNameLabels)
            UiFactory.SetLabelBold(pair.Value, emphasised.Contains(pair.Key));
    }

    /// <summary>
    /// The selected Voidling plus everyone joined to it by a highlighted lane. These read as bold so
    /// a pairing can be identified from the card names alone, not just from the lanes.
    /// </summary>
    internal HashSet<string> EmphasisedMemberIds()
    {
        var emphasised = new HashSet<string>(StringComparer.Ordinal);
        if (_selectedId.Length > 0)
            emphasised.Add(_selectedId);

        foreach (var path in BuildConnectionPaths().Where(IsConnectionHighlighted))
        {
            emphasised.Add(path.ParentId);
            emphasised.Add(path.ChildId);
        }

        return emphasised;
    }

    private float ParentAnchor(VoidlingData member)
    {
        var anchors = new List<float>();
        if (!string.IsNullOrWhiteSpace(member.ParentAId) && _baseCardRects.TryGetValue(member.ParentAId, out var a))
            anchors.Add(a.GetCenter().X);
        if (!string.IsNullOrWhiteSpace(member.ParentBId) && _baseCardRects.TryGetValue(member.ParentBId, out var b))
            anchors.Add(b.GetCenter().X);
        return anchors.Count == 0 ? float.MaxValue : anchors.Average();
    }

    private void AddCard(VoidlingData member, Rect2 rect, bool departed)
    {
        var panel = UiFactory.CreatePanel(rect.Size);
        panel.Position = rect.Position;
        panel.Size = rect.Size;
        panel.MouseFilter = MouseFilterEnum.Pass;
        _worldLayer.AddChild(panel);
        _cardPanels[member.Id] = panel;

        var column = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        column.AddThemeConstantOverride("separation", 0);
        panel.AddChild(column);

        var portrait = UiFactory.CreatePortrait(member, new Vector2(38, 38));
        if (departed)
            portrait.Modulate = new Color(0.55f, 0.55f, 0.55f, 0.72f);
        column.AddChild(portrait);

        var name = UiFactory.CreateLabel(member.Name, 8);
        name.HorizontalAlignment = HorizontalAlignment.Center;
        name.CustomMinimumSize = new Vector2(82, 13);
        name.MouseFilter = MouseFilterEnum.Ignore;
        column.AddChild(name);
        _cardNameLabels[member.Id] = name;

        var generationText = member.InbreedingHistoryFlag
            ? $"G{member.FamilyGeneration} • INBRED"
            : $"G{member.FamilyGeneration}";
        if (departed)
            generationText += " • LEFT";

        var detail = UiFactory.CreateLabel(generationText, 6);
        detail.HorizontalAlignment = HorizontalAlignment.Center;
        detail.MouseFilter = MouseFilterEnum.Ignore;
        if (member.InbreedingHistoryFlag)
            detail.AddThemeColorOverride("font_color", Color.FromHtml("#A75D55"));
        else if (departed)
            detail.AddThemeColorOverride("font_color", Color.FromHtml("#7A7267"));
        column.AddChild(detail);

        var parentSummary = ParentSummary(member);
        if (parentSummary.Length > 0)
        {
            var parents = UiFactory.CreateLabel(parentSummary, 5);
            parents.HorizontalAlignment = HorizontalAlignment.Center;
            parents.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            parents.CustomMinimumSize = new Vector2(82, 15);
            parents.MouseFilter = MouseFilterEnum.Ignore;
            column.AddChild(parents);
        }

        var click = new Button
        {
            Flat = true,
            Text = "",
            Position = Vector2.Zero,
            Size = rect.Size,
            FocusMode = FocusModeEnum.None,
            MouseFilter = MouseFilterEnum.Stop
        };
        click.Pressed += () =>
        {
            _selectedId = member.Id;
            _highlightedConnectionKey = "";
            ApplySelectionState();
            QueueRedraw();
            MemberSelected?.Invoke(member.Id);
        };
        panel.AddChild(click);
    }

    private string ParentSummary(VoidlingData member)
    {
        if (string.IsNullOrWhiteSpace(member.ParentAId) && string.IsNullOrWhiteSpace(member.ParentBId))
            return "";

        var first = _membersById.TryGetValue(member.ParentAId, out var a) ? a.Name : "?";
        var second = _membersById.TryGetValue(member.ParentBId, out var b) ? b.Name : "?";
        return $"P: {first} + {second}";
    }

    private static HashSet<string> CollectConnectedFamily(
        string selectedId,
        IReadOnlyList<VoidlingData> allVoidlings,
        IReadOnlyDictionary<string, VoidlingData> byId)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>();
        queue.Enqueue(selectedId);

        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            if (!result.Add(id) || !byId.TryGetValue(id, out var current))
                continue;

            if (!string.IsNullOrWhiteSpace(current.ParentAId) && byId.ContainsKey(current.ParentAId))
                queue.Enqueue(current.ParentAId);
            if (!string.IsNullOrWhiteSpace(current.ParentBId) && byId.ContainsKey(current.ParentBId))
                queue.Enqueue(current.ParentBId);

            foreach (var child in allVoidlings)
            {
                if (child.ParentAId == id || child.ParentBId == id)
                    queue.Enqueue(child.Id);
            }
        }

        return result;
    }
}
