using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Voidling.Presentation.UI.Common;
using Voidling.Presentation.Voidlings;
using VoidlingGame;

namespace Voidling.Presentation.UI.Encyclopedia;

/// <summary>One journal entry, already worded. Undiscovered entries carry no name.</summary>
public readonly record struct EncyclopediaEntryViewState(
    string EntryId,
    bool Discovered,
    string Name,
    string HowText,
    string FoundByText,
    VoidlingVisualAppearance Appearance);

public sealed record EncyclopediaScreenState(IReadOnlyList<EncyclopediaEntryViewState> Entries, int Discovered, int Total);

/// <summary>
/// The journal: how many Voidlings there are to find, each entry's sprite once discovered and a dark
/// silhouette with "???" until then, and a card saying how to get the selected one and who found it
/// first. Silhouettes are the real composed portrait, darkened, so they follow any art change.
/// </summary>
public partial class EncyclopediaScreen : HBoxContainer
{
    private const int SlotColumns = 4;
    private static readonly Vector2 SlotSize = new(56, 62);
    private static readonly Color SilhouetteColor = new(0.16f, 0.13f, 0.2f, 0.9f);

    private EncyclopediaScreenState? _state;
    private string _selection = string.Empty;
    private GridContainer _grid = null!;
    private VBoxContainer _detail = null!;

    public void Configure(EncyclopediaScreenState state)
    {
        if (IsInsideTree()) throw new InvalidOperationException("EncyclopediaScreen must be configured before it enters the scene tree.");
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _selection = state.Entries.FirstOrDefault(entry => entry.Discovered).EntryId ?? state.Entries.FirstOrDefault().EntryId ?? string.Empty;
    }

    public override void _Ready()
    {
        if (_state == null) throw new InvalidOperationException("EncyclopediaScreen must be configured before AddChild.");
        Name = "Journal";
        AddThemeConstantOverride("separation", 6);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;

        var left = new VBoxContainer();
        left.AddThemeConstantOverride("separation", 4);
        AddChild(left);
        var count = UiFactory.CreateLabel(string.Format(Tr("UI_JOURNAL_COUNT"), _state.Discovered, _state.Total), 8);
        count.Name = "JournalCount";
        left.AddChild(count);
        var gridPanel = PaperCard.Panel(new Vector2(250, 0));
        gridPanel.Name = "JournalEntries";
        gridPanel.SizeFlagsVertical = SizeFlags.ShrinkBegin;
        left.AddChild(gridPanel);
        _grid = new GridContainer { Columns = SlotColumns };
        _grid.AddThemeConstantOverride("h_separation", 4);
        _grid.AddThemeConstantOverride("v_separation", 4);
        gridPanel.AddChild(_grid);

        var detailPanel = PaperCard.Panel(new Vector2(190, 232));
        detailPanel.Name = "JournalDetail";
        AddChild(detailPanel);
        _detail = new VBoxContainer();
        _detail.AddThemeConstantOverride("separation", 5);
        detailPanel.AddChild(_detail);

        Rebuild();
    }

    public void FocusSelection()
        => (_grid.GetChildren().OfType<Button>().FirstOrDefault(button => button.ButtonPressed)
            ?? _grid.GetChildren().OfType<Button>().FirstOrDefault())?.GrabFocus();

    private void Rebuild()
    {
        PaperCard.Clear(_grid);
        PaperCard.Clear(_detail);
        foreach (var entry in _state!.Entries)
            _grid.AddChild(BuildSlot(entry));
        var selected = _state.Entries.FirstOrDefault(entry => entry.EntryId == _selection);
        if (selected.EntryId != null)
            BuildDetail(selected);
    }

    private Button BuildSlot(EncyclopediaEntryViewState entry)
    {
        var button = UiFactory.CreateButton(string.Empty);
        button.Name = "Entry_" + entry.EntryId;
        button.ToggleMode = true;
        button.ButtonPressed = entry.EntryId == _selection;
        button.CustomMinimumSize = SlotSize;
        button.TooltipText = entry.Discovered ? entry.Name : Tr("UI_JOURNAL_UNKNOWN");

        var portrait = CreateEntryPortrait(entry, new Vector2(38, 38));
        portrait.Position = new Vector2((SlotSize.X - 38) * 0.5f, 3);
        button.AddChild(portrait);

        var label = UiFactory.CreateLabel(entry.Discovered ? entry.Name : Tr("UI_JOURNAL_UNKNOWN"), 6);
        label.Position = new Vector2(2, 44);
        label.Size = new Vector2(SlotSize.X - 4, 14);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        label.MouseFilter = MouseFilterEnum.Ignore;
        button.AddChild(label);

        var captured = entry.EntryId;
        button.Pressed += () =>
        {
            _selection = captured;
            Rebuild();
            FocusSelection();
        };
        return button;
    }

    private void BuildDetail(EncyclopediaEntryViewState entry)
    {
        var artPanel = new PanelContainer { CustomMinimumSize = new Vector2(0, 76) };
        artPanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = Color.FromHtml("#F7E5BD") });
        var center = new CenterContainer();
        center.AddChild(CreateEntryPortrait(entry, new Vector2(64, 64)));
        artPanel.AddChild(center);
        _detail.AddChild(artPanel);

        var name = UiFactory.CreateLabel(entry.Discovered ? entry.Name.ToUpperInvariant() : Tr("UI_JOURNAL_UNKNOWN"), 9);
        name.Name = "JournalEntryName";
        name.HorizontalAlignment = HorizontalAlignment.Center;
        _detail.AddChild(name);

        var how = UiFactory.CreateLabel(entry.Discovered ? entry.HowText : Tr("UI_JOURNAL_UNKNOWN_HINT"), 7);
        how.Name = "JournalHow";
        how.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        how.CustomMinimumSize = new Vector2(170, 0);
        _detail.AddChild(how);

        if (entry.Discovered && entry.FoundByText.Length > 0)
        {
            var foundBy = UiFactory.CreateLabel(entry.FoundByText, 6);
            foundBy.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            foundBy.CustomMinimumSize = new Vector2(170, 0);
            _detail.AddChild(foundBy);
        }
    }

    /// <summary>The composed portrait; darkened to a silhouette until the entry is discovered.</summary>
    private static TextureRect CreateEntryPortrait(EncyclopediaEntryViewState entry, Vector2 size)
    {
        var portrait = UiFactory.CreatePortrait(entry.Appearance, hasAngelMutation: false, otherMutationCount: 0, size);
        portrait.MouseFilter = MouseFilterEnum.Ignore;
        if (!entry.Discovered)
            portrait.Modulate = SilhouetteColor;
        return portrait;
    }
}
