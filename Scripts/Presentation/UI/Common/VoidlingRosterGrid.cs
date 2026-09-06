using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Voidling.Presentation.Voidlings;
using VoidlingGame;

namespace Voidling.Presentation.UI.Common;

/// <summary>One Voidling as a roster slot: enough to draw the portrait card and mark it.</summary>
public readonly record struct RosterEntry(
    string Id,
    string Name,
    VoidlingVisualAppearance Appearance,
    bool HasAngelMutation,
    int OtherMutationCount);

/// <summary>
/// The paged 3x3 portrait roster shared by race entry and breeding. Race entry marks one slot,
/// breeding marks two, so the caller says which mark a slot carries rather than the grid tracking
/// a selection of its own.
/// </summary>
public sealed class VoidlingRosterGrid
{
    public const int Columns = 3;
    public const int Rows = 3;
    public const int PerPage = Columns * Rows;

    private static readonly Vector2 SlotSize = new(84, 70);

    /// <summary>
    /// Builds the arrows/grid row for one page. <paramref name="mark"/> returns the badge text for
    /// a slot — empty for unpicked, "*" for a plain star, or "A"/"B" for breeding's two parents.
    /// </summary>
    public static Control Build(
        IReadOnlyList<RosterEntry> roster,
        int page,
        Func<RosterEntry, string> mark,
        Action<RosterEntry> picked,
        Action<int> pageChanged,
        out int pages)
    {
        pages = Mathf.Max(1, (roster.Count + PerPage - 1) / PerPage);
        var clamped = Mathf.Clamp(page, 0, pages - 1);
        var pageCount = pages;

        var row = new HBoxContainer { Name = "RosterRow" };
        row.AddThemeConstantOverride("separation", 4);
        row.AddChild(PageArrow("RosterPrev", -1, clamped, pageCount, pageChanged));

        var grid = new GridContainer { Name = "RosterGrid", Columns = Columns };
        grid.AddThemeConstantOverride("h_separation", 3);
        grid.AddThemeConstantOverride("v_separation", 1);

        var visible = roster.Skip(clamped * PerPage).Take(PerPage).ToArray();
        foreach (var creature in visible)
        {
            var captured = creature;
            var entry = UiFactory.CreateVoidlingCard(
                creature.Name,
                creature.Appearance,
                creature.HasAngelMutation,
                creature.OtherMutationCount,
                pressed => { if (pressed) picked(captured); },
                out var card);
            card.Name = "Racer_" + creature.Id;
            var badge = mark(creature);
            card.SetPressedNoSignal(badge.Length > 0);
            entry.CustomMinimumSize = new Vector2(84, 72);
            if (badge.Length > 0) card.AddChild(SlotBadge(badge));
            grid.AddChild(entry);
        }
        for (var filler = visible.Length; filler < PerPage; filler++)
            grid.AddChild(PaperCard.EmptySlot(SlotSize));

        row.AddChild(grid);
        row.AddChild(PageArrow("RosterNext", 1, clamped, pageCount, pageChanged));
        return row;
    }

    /// <summary>
    /// The wooden star for a single pick; breeding's A/B ride on the same star so two parents are
    /// told apart without a second piece of art.
    /// </summary>
    private static Control SlotBadge(string badge)
    {
        var star = PaperCard.Star(new Vector2(60, 1));
        if (badge == "*") return star;
        var label = UiFactory.CreateLabel(badge, 7);
        label.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.VerticalAlignment = VerticalAlignment.Center;
        label.AddThemeColorOverride("font_color", Color.FromHtml("#5A3A18"));
        label.MouseFilter = Control.MouseFilterEnum.Ignore;
        star.AddChild(label);
        return star;
    }

    private static Button PageArrow(string name, int delta, int page, int pages, Action<int> pageChanged)
    {
        var button = UiFactory.CreateButton(string.Empty);
        button.Name = name;
        button.CustomMinimumSize = new Vector2(24, 36);
        button.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        button.Disabled = pages <= 1;
        button.AddChild(new TextureRect
        {
            Texture = UiFactory.CreateGardenIcon(13, 3),
            FlipH = delta < 0,
            CustomMinimumSize = new Vector2(24, 36),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = new Color(1, 1, 1, pages <= 1 ? 0.35f : 1.0f)
        });
        button.Pressed += () => pageChanged((page + delta + pages) % pages);
        return button;
    }
}
