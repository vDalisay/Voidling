using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace VoidlingGame;

public partial class FamilyTreeView : Control
{
    public event Action<string>? MemberSelected;

    private readonly Dictionary<string, Rect2> _cardRects = new(StringComparer.Ordinal);
    private readonly Dictionary<string, VoidlingData> _membersById = new(StringComparer.Ordinal);
    private readonly HashSet<string> _departedIds = new(StringComparer.Ordinal);
    private readonly List<string> _visibleMemberIds = new();
    private string _selectedId = "";

    public void Build(
        string selectedId,
        IReadOnlyList<VoidlingData> activeVoidlings,
        IReadOnlyList<VoidlingData> departedVoidlings)
    {
        foreach (var child in GetChildren())
            child.QueueFree();

        _cardRects.Clear();
        _membersById.Clear();
        _departedIds.Clear();
        _visibleMemberIds.Clear();
        _selectedId = selectedId;

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
        var contentWidth = Math.Max(610.0f, margin * 2 + widest * cardWidth + Math.Max(0, widest - 1) * horizontalGap);
        var contentHeight = Math.Max(240.0f, margin * 2 + groups.Count * cardHeight + Math.Max(0, groups.Count - 1) * verticalGap);
        CustomMinimumSize = new Vector2(contentWidth, contentHeight);
        Size = CustomMinimumSize;

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
                _cardRects[member.Id] = rect;
                AddCard(member, rect, _departedIds.Contains(member.Id));
            }
        }

        QueueRedraw();
    }

    public void SetSelectedMember(string memberId)
    {
        _selectedId = memberId;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var lineColor = Color.FromHtml("#E9E5CD");

        // Draw each child's complete parent connection as one family unit. This avoids
        // the dangling half-branches produced by drawing each parent link independently.
        foreach (var childId in _visibleMemberIds)
        {
            if (!_membersById.TryGetValue(childId, out var child) ||
                !_cardRects.TryGetValue(childId, out var childRect))
                continue;

            var parentRects = new List<Rect2>();
            if (!string.IsNullOrWhiteSpace(child.ParentAId) && _cardRects.TryGetValue(child.ParentAId, out var parentA))
                parentRects.Add(parentA);
            if (!string.IsNullOrWhiteSpace(child.ParentBId) && _cardRects.TryGetValue(child.ParentBId, out var parentB))
                parentRects.Add(parentB);

            if (parentRects.Count == 0)
                continue;

            var childTop = new Vector2(childRect.GetCenter().X, childRect.Position.Y);
            var highestParentBottom = parentRects.Max(rect => rect.End.Y);
            var junctionY = highestParentBottom + Math.Max(12.0f, (childTop.Y - highestParentBottom) * 0.46f);

            if (parentRects.Count == 1)
            {
                var parentBottom = new Vector2(parentRects[0].GetCenter().X, parentRects[0].End.Y);
                DrawLine(parentBottom, new Vector2(parentBottom.X, junctionY), lineColor, 2.0f);
                DrawLine(new Vector2(parentBottom.X, junctionY), new Vector2(childTop.X, junctionY), lineColor, 2.0f);
                DrawLine(new Vector2(childTop.X, junctionY), childTop, lineColor, 2.0f);
                continue;
            }

            var leftParent = parentRects.OrderBy(rect => rect.GetCenter().X).First();
            var rightParent = parentRects.OrderBy(rect => rect.GetCenter().X).Last();
            var leftBottom = new Vector2(leftParent.GetCenter().X, leftParent.End.Y);
            var rightBottom = new Vector2(rightParent.GetCenter().X, rightParent.End.Y);

            DrawLine(leftBottom, new Vector2(leftBottom.X, junctionY), lineColor, 2.0f);
            DrawLine(rightBottom, new Vector2(rightBottom.X, junctionY), lineColor, 2.0f);
            DrawLine(new Vector2(leftBottom.X, junctionY), new Vector2(rightBottom.X, junctionY), lineColor, 2.0f);

            var coupleX = (leftBottom.X + rightBottom.X) * 0.5f;
            var branchY = Math.Min(childTop.Y - 10.0f, junctionY + 16.0f);
            DrawLine(new Vector2(coupleX, junctionY), new Vector2(coupleX, branchY), lineColor, 2.0f);
            DrawLine(new Vector2(coupleX, branchY), new Vector2(childTop.X, branchY), lineColor, 2.0f);
            DrawLine(new Vector2(childTop.X, branchY), childTop, lineColor, 2.0f);
        }

        if (_cardRects.TryGetValue(_selectedId, out var selected))
            DrawRect(selected.Grow(3.0f), Color.FromHtml("#FFF0A6"), false, 2.0f);
    }

    private float ParentAnchor(VoidlingData member)
    {
        var anchors = new List<float>();
        if (!string.IsNullOrWhiteSpace(member.ParentAId) && _cardRects.TryGetValue(member.ParentAId, out var a))
            anchors.Add(a.GetCenter().X);
        if (!string.IsNullOrWhiteSpace(member.ParentBId) && _cardRects.TryGetValue(member.ParentBId, out var b))
            anchors.Add(b.GetCenter().X);

        return anchors.Count == 0 ? float.MaxValue : anchors.Average();
    }

    private void AddCard(VoidlingData member, Rect2 rect, bool departed)
    {
        var panel = UiFactory.CreatePanel(rect.Size);
        panel.Position = rect.Position;
        panel.Size = rect.Size;
        panel.MouseFilter = MouseFilterEnum.Pass;
        AddChild(panel);

        var column = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        column.AddThemeConstantOverride("separation", 0);
        panel.AddChild(column);

        var portrait = UiFactory.CreatePortrait(member, new Vector2(38, 38));
        if (departed)
            portrait.SelfModulate = new Color(0.55f, 0.55f, 0.55f, 0.72f);
        column.AddChild(portrait);

        var name = UiFactory.CreateLabel(member.Name, 8);
        name.HorizontalAlignment = HorizontalAlignment.Center;
        name.CustomMinimumSize = new Vector2(82, 13);
        name.MouseFilter = MouseFilterEnum.Ignore;
        column.AddChild(name);

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
