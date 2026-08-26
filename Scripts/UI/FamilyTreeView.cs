using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Voidling.Application.Breeding;

namespace VoidlingGame;

public partial class FamilyTreeView : Control
{
    public event Action<string>? MemberSelected;

    private static readonly Vector2 ViewportSize = new(425, 252);
    private readonly Dictionary<string, Rect2> _baseCardRects = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PanelContainer> _cardPanels = new(StringComparer.Ordinal);
    private readonly Dictionary<string, LineageMemberProjection> _membersById = new(StringComparer.Ordinal);
    private readonly List<string> _visibleMemberIds = new();

    private string _selectedId = string.Empty;
    private string _highlightedConnectionKey = string.Empty;
    private Vector2 _panOffset;
    private Vector2 _contentSize;
    private bool _panning;

    private readonly struct ConnectionSegment
    {
        public ConnectionSegment(Vector2 from, Vector2 to)
        {
            From = from;
            To = to;
        }

        public Vector2 From { get; }
        public Vector2 To { get; }
    }

    private sealed class ConnectionPath
    {
        public string Key { get; init; } = string.Empty;
        public string ParentId { get; init; } = string.Empty;
        public string ChildId { get; init; } = string.Empty;
        public List<ConnectionSegment> Segments { get; } = new();
    }

    public FamilyTreeView()
    {
        CustomMinimumSize = ViewportSize;
        Size = ViewportSize;
        ClipContents = true;
        MouseFilter = MouseFilterEnum.Stop;
    }

    public void Build(LineageTreeProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        foreach (var child in GetChildren())
            child.QueueFree();

        _baseCardRects.Clear();
        _cardPanels.Clear();
        _membersById.Clear();
        _visibleMemberIds.Clear();
        _selectedId = projection.SelectedCreatureId;
        _highlightedConnectionKey = string.Empty;

        foreach (var member in projection.Members)
            _membersById[member.CreatureId] = member;

        if (!_membersById.ContainsKey(_selectedId))
            return;

        var connectedIds = CollectConnectedFamily(_selectedId, projection.Members, _membersById);
        var members = connectedIds.Select(id => _membersById[id]).ToList();
        _visibleMemberIds.AddRange(connectedIds);

        var groups = members
            .GroupBy(member => member.FamilyGeneration)
            .OrderBy(group => group.Key)
            .ToList();

        const float cardWidth = 108.0f;
        const float cardHeight = 82.0f;
        const float horizontalGap = 34.0f;
        const float verticalGap = 50.0f;
        const float margin = 28.0f;

        var widest = groups.Count == 0 ? 1 : groups.Max(group => group.Count());
        var contentWidth = Math.Max(
            ViewportSize.X,
            margin * 2 + widest * cardWidth + Math.Max(0, widest - 1) * horizontalGap);
        var contentHeight = Math.Max(
            ViewportSize.Y,
            margin * 2 + groups.Count * cardHeight + Math.Max(0, groups.Count - 1) * verticalGap);
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
                    : string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase);
            });

            var rowWidth = generationMembers.Count * cardWidth +
                           Math.Max(0, generationMembers.Count - 1) * horizontalGap;
            var startX = (contentWidth - rowWidth) * 0.5f;
            var y = margin + generationIndex * (cardHeight + verticalGap);

            for (var i = 0; i < generationMembers.Count; i++)
            {
                var member = generationMembers[i];
                var rect = new Rect2(
                    new Vector2(startX + i * (cardWidth + horizontalGap), y),
                    new Vector2(cardWidth, cardHeight));
                _baseCardRects[member.CreatureId] = rect;
                AddCard(member, rect);
            }
        }

        CenterOnSelected();
        ApplyPan();
        ApplySelectionState();
        QueueRedraw();
    }

    public void SetSelectedMember(string memberId)
    {
        _selectedId = memberId;
        _highlightedConnectionKey = string.Empty;
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
        }

        if (inputEvent is InputEventMouseMotion motion && _panning)
        {
            _panOffset += motion.Relative;
            ClampPan();
            ApplyPan();
            QueueRedraw();
            AcceptEvent();
        }
    }

    public override void _Draw()
    {
        var paths = BuildConnectionPaths();
        var lineColor = Color.FromHtml("#E0DDC5");

        foreach (var path in paths)
            DrawConnectionPath(path, lineColor, 2.0f);

        foreach (var path in paths.Where(IsConnectionHighlighted))
        {
            DrawConnectionPath(path, Color.FromHtml("#6A5841"), 5.0f);
            DrawConnectionPath(path, Color.FromHtml("#FFD96A"), 3.0f);
        }
    }

    private List<ConnectionPath> BuildConnectionPaths()
    {
        var paths = new List<ConnectionPath>();

        foreach (var childId in _visibleMemberIds)
        {
            if (!_membersById.TryGetValue(childId, out var child) ||
                !_baseCardRects.TryGetValue(childId, out var childBase))
            {
                continue;
            }

            var parents = new List<(string Id, Rect2 Rect)>();
            if (!string.IsNullOrWhiteSpace(child.ParentAId) &&
                _baseCardRects.TryGetValue(child.ParentAId, out var parentA))
            {
                parents.Add((child.ParentAId, Offset(parentA)));
            }
            if (!string.IsNullOrWhiteSpace(child.ParentBId) &&
                _baseCardRects.TryGetValue(child.ParentBId, out var parentB))
            {
                parents.Add((child.ParentBId, Offset(parentB)));
            }

            if (parents.Count == 0)
                continue;

            var childRect = Offset(childBase);
            var childTop = new Vector2(childRect.GetCenter().X, childRect.Position.Y);
            var highestParentBottom = parents.Max(parent => parent.Rect.End.Y);
            var junctionY = highestParentBottom +
                            Math.Max(12.0f, (childTop.Y - highestParentBottom) * 0.46f);

            if (parents.Count == 1)
            {
                var parent = parents[0];
                var parentBottom = new Vector2(parent.Rect.GetCenter().X, parent.Rect.End.Y);
                var path = NewPath(parent.Id, childId);
                path.Segments.Add(new ConnectionSegment(parentBottom, new Vector2(parentBottom.X, junctionY)));
                path.Segments.Add(new ConnectionSegment(
                    new Vector2(parentBottom.X, junctionY),
                    new Vector2(childTop.X, junctionY)));
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
                path.Segments.Add(new ConnectionSegment(
                    new Vector2(parentBottom.X, junctionY),
                    new Vector2(coupleX, junctionY)));
                path.Segments.Add(new ConnectionSegment(
                    new Vector2(coupleX, junctionY),
                    new Vector2(coupleX, branchY)));
                path.Segments.Add(new ConnectionSegment(
                    new Vector2(coupleX, branchY),
                    new Vector2(childTop.X, branchY)));
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
        const float hitRadius = 7.0f;
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

    private Rect2 Offset(Rect2 rect) => new(rect.Position + _panOffset, rect.Size);

    private void CenterOnSelected()
    {
        if (!_baseCardRects.TryGetValue(_selectedId, out var selected))
        {
            _panOffset = Vector2.Zero;
            return;
        }

        _panOffset = ViewportSize * 0.5f - selected.GetCenter();
        ClampPan();
    }

    private void ClampPan()
    {
        var minX = Math.Min(0.0f, ViewportSize.X - _contentSize.X);
        var minY = Math.Min(0.0f, ViewportSize.Y - _contentSize.Y);
        _panOffset.X = Mathf.Clamp(_panOffset.X, minX, 0.0f);
        _panOffset.Y = Mathf.Clamp(_panOffset.Y, minY, 0.0f);
    }

    private void ApplyPan()
    {
        foreach (var pair in _cardPanels)
        {
            if (_baseCardRects.TryGetValue(pair.Key, out var rect))
                pair.Value.Position = rect.Position + _panOffset;
        }
    }

    private void ApplySelectionState()
    {
        foreach (var pair in _cardPanels)
        {
            pair.Value.Modulate = pair.Key == _selectedId
                ? new Color(0.78f, 0.72f, 0.62f, 1.0f)
                : Colors.White;
        }
    }

    private float ParentAnchor(LineageMemberProjection member)
    {
        var anchors = new List<float>();
        if (!string.IsNullOrWhiteSpace(member.ParentAId) &&
            _baseCardRects.TryGetValue(member.ParentAId, out var parentA))
        {
            anchors.Add(parentA.GetCenter().X);
        }
        if (!string.IsNullOrWhiteSpace(member.ParentBId) &&
            _baseCardRects.TryGetValue(member.ParentBId, out var parentB))
        {
            anchors.Add(parentB.GetCenter().X);
        }
        return anchors.Count == 0 ? float.MaxValue : anchors.Average();
    }

    private void AddCard(LineageMemberProjection member, Rect2 rect)
    {
        var panel = UiFactory.CreatePanel(rect.Size);
        panel.Position = rect.Position;
        panel.Size = rect.Size;
        panel.MouseFilter = MouseFilterEnum.Pass;
        AddChild(panel);
        _cardPanels[member.CreatureId] = panel;

        var column = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        column.AddThemeConstantOverride("separation", 0);
        panel.AddChild(column);

        var tint = string.IsNullOrWhiteSpace(member.TintHex)
            ? Colors.White
            : Color.FromHtml(member.TintHex);
        var portrait = UiFactory.CreatePortrait(
            tint,
            member.HasAngelMutation,
            member.OtherMutationCount,
            new Vector2(38, 38));
        if (member.Presence != LineageMemberPresence.Owned)
            portrait.Modulate = new Color(0.55f, 0.55f, 0.55f, 0.72f);
        column.AddChild(portrait);

        var name = UiFactory.CreateLabel(member.DisplayName, 8);
        name.HorizontalAlignment = HorizontalAlignment.Center;
        name.CustomMinimumSize = new Vector2(82, 13);
        name.MouseFilter = MouseFilterEnum.Ignore;
        column.AddChild(name);

        var generationText = member.InbreedingHistoryFlag
            ? $"G{member.FamilyGeneration} • INBRED"
            : $"G{member.FamilyGeneration}";
        generationText += member.Presence switch
        {
            LineageMemberPresence.Departed => " • LEFT",
            LineageMemberPresence.Archived => " • ARCHIVED",
            _ => string.Empty
        };

        var detail = UiFactory.CreateLabel(generationText, 6);
        detail.HorizontalAlignment = HorizontalAlignment.Center;
        detail.MouseFilter = MouseFilterEnum.Ignore;
        if (member.InbreedingHistoryFlag)
            detail.AddThemeColorOverride("font_color", Color.FromHtml("#A75D55"));
        else if (member.Presence != LineageMemberPresence.Owned)
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
            Text = string.Empty,
            Position = Vector2.Zero,
            Size = rect.Size,
            FocusMode = FocusModeEnum.None,
            MouseFilter = MouseFilterEnum.Stop
        };
        click.Pressed += () =>
        {
            _selectedId = member.CreatureId;
            _highlightedConnectionKey = string.Empty;
            ApplySelectionState();
            QueueRedraw();
            MemberSelected?.Invoke(member.CreatureId);
        };
        panel.AddChild(click);
    }

    private string ParentSummary(LineageMemberProjection member)
    {
        if (string.IsNullOrWhiteSpace(member.ParentAId) && string.IsNullOrWhiteSpace(member.ParentBId))
            return string.Empty;

        var first = _membersById.TryGetValue(member.ParentAId, out var parentA)
            ? parentA.DisplayName
            : "?";
        var second = _membersById.TryGetValue(member.ParentBId, out var parentB)
            ? parentB.DisplayName
            : "?";
        return $"P: {first} + {second}";
    }

    private static HashSet<string> CollectConnectedFamily(
        string selectedId,
        IReadOnlyList<LineageMemberProjection> allMembers,
        IReadOnlyDictionary<string, LineageMemberProjection> byId)
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

            foreach (var child in allMembers)
            {
                if (child.ParentAId == id || child.ParentBId == id)
                    queue.Enqueue(child.CreatureId);
            }
        }

        return result;
    }
}
