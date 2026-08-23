using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace VoidlingGame;

public partial class FamilyTreeView : Control
{
    private readonly Dictionary<string, Rect2> _cardRects = new(StringComparer.Ordinal);
    private readonly List<(string ParentId, string ChildId)> _links = new();
    private string _selectedId = "";

    public void Build(string selectedId, IReadOnlyList<VoidlingData> allVoidlings)
    {
        foreach (var child in GetChildren())
            child.QueueFree();

        _cardRects.Clear();
        _links.Clear();
        _selectedId = selectedId;

        var byId = allVoidlings.ToDictionary(v => v.Id, StringComparer.Ordinal);
        if (!byId.ContainsKey(selectedId))
            return;

        var connectedIds = CollectConnectedFamily(selectedId, allVoidlings, byId);
        var members = connectedIds.Select(id => byId[id]).ToList();

        foreach (var member in members)
        {
            if (member.ParentAId.Length > 0 && connectedIds.Contains(member.ParentAId))
                _links.Add((member.ParentAId, member.Id));
            if (member.ParentBId.Length > 0 && connectedIds.Contains(member.ParentBId))
                _links.Add((member.ParentBId, member.Id));
        }

        var groups = members
            .GroupBy(v => v.FamilyGeneration)
            .OrderBy(g => g.Key)
            .ToList();

        const float cardWidth = 92.0f;
        const float cardHeight = 68.0f;
        const float horizontalGap = 22.0f;
        const float verticalGap = 30.0f;
        const float margin = 24.0f;

        var widest = groups.Count == 0 ? 1 : groups.Max(g => g.Count());
        var contentWidth = Math.Max(540.0f, margin * 2 + widest * cardWidth + Math.Max(0, widest - 1) * horizontalGap);
        var contentHeight = Math.Max(220.0f, margin * 2 + groups.Count * cardHeight + Math.Max(0, groups.Count - 1) * verticalGap);
        CustomMinimumSize = new Vector2(contentWidth, contentHeight);
        Size = CustomMinimumSize;

        for (var generationIndex = 0; generationIndex < groups.Count; generationIndex++)
        {
            var row = groups[generationIndex].OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase).ToList();
            var rowWidth = row.Count * cardWidth + Math.Max(0, row.Count - 1) * horizontalGap;
            var startX = (contentWidth - rowWidth) * 0.5f;
            var y = margin + generationIndex * (cardHeight + verticalGap);

            for (var i = 0; i < row.Count; i++)
            {
                var member = row[i];
                var rect = new Rect2(new Vector2(startX + i * (cardWidth + horizontalGap), y), new Vector2(cardWidth, cardHeight));
                _cardRects[member.Id] = rect;
                AddCard(member, rect);
            }
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        foreach (var link in _links)
        {
            if (!_cardRects.TryGetValue(link.ParentId, out var parent) ||
                !_cardRects.TryGetValue(link.ChildId, out var child))
                continue;

            var start = new Vector2(parent.GetCenter().X, parent.End.Y);
            var end = new Vector2(child.GetCenter().X, child.Position.Y);
            var middleY = (start.Y + end.Y) * 0.5f;
            var lineColor = Color.FromHtml("#E9E5CD");
            DrawLine(start, new Vector2(start.X, middleY), lineColor, 2.0f);
            DrawLine(new Vector2(start.X, middleY), new Vector2(end.X, middleY), lineColor, 2.0f);
            DrawLine(new Vector2(end.X, middleY), end, lineColor, 2.0f);
        }

        if (_cardRects.TryGetValue(_selectedId, out var selected))
            DrawRect(selected.Grow(2.0f), Color.FromHtml("#FFF0A6"), false, 2.0f);
    }

    private void AddCard(VoidlingData member, Rect2 rect)
    {
        var panel = UiFactory.CreatePanel(rect.Size);
        panel.Position = rect.Position;
        panel.Size = rect.Size;
        panel.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(panel);

        var column = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        column.AddThemeConstantOverride("separation", 0);
        panel.AddChild(column);

        var portrait = UiFactory.CreatePortrait(member, new Vector2(34, 36));
        column.AddChild(portrait);

        var name = UiFactory.CreateLabel(member.Name, 8);
        name.HorizontalAlignment = HorizontalAlignment.Center;
        name.CustomMinimumSize = new Vector2(70, 13);
        name.MouseFilter = MouseFilterEnum.Ignore;
        column.AddChild(name);

        var marker = member.InbreedingHistoryFlag
            ? $"G{member.FamilyGeneration} • INBRED"
            : $"G{member.FamilyGeneration}";
        var detail = UiFactory.CreateLabel(marker, 6);
        detail.HorizontalAlignment = HorizontalAlignment.Center;
        detail.MouseFilter = MouseFilterEnum.Ignore;
        if (member.InbreedingHistoryFlag)
            detail.AddThemeColorOverride("font_color", Color.FromHtml("#A75D55"));
        column.AddChild(detail);
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

            if (current.ParentAId.Length > 0 && byId.ContainsKey(current.ParentAId))
                queue.Enqueue(current.ParentAId);
            if (current.ParentBId.Length > 0 && byId.ContainsKey(current.ParentBId))
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
