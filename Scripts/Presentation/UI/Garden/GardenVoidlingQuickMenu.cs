using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Voidling.Presentation.Voidlings;
using VoidlingGame;

namespace Voidling.Presentation.UI.Garden;

public readonly record struct QuickMenuVoidlingViewState(
    string Id,
    string Name,
    string ColorName,
    VoidlingVisualAppearance Appearance,
    bool HasAngelMutation,
    int OtherMutationCount);

/// <summary>
/// Bottom-right roster shortcut. The button toggles a compact sidebar of every Voidling in the
/// Garden; picking one asks the owner to select and track it. Filtering is presentation-only text
/// matching over the projected name and colour name.
/// </summary>
public partial class GardenVoidlingQuickMenu : Control
{
    public event Action<string>? VoidlingPicked;

    private static readonly Vector2 PanelSize = new(196, 178);

    private readonly List<QuickMenuVoidlingViewState> _voidlings = new();
    private PanelContainer _panel = null!;
    private LineEdit _search = null!;
    private VBoxContainer _list = null!;
    private Label _empty = null!;
    private Button _toggle = null!;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Size = new Vector2(PanelSize.X, PanelSize.Y + 26.0f);

        _panel = UiFactory.CreatePanel(PanelSize);
        _panel.Position = Vector2.Zero;
        _panel.Size = PanelSize;
        _panel.Visible = false;
        AddChild(_panel);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 4);
        _panel.AddChild(column);

        column.AddChild(UiFactory.CreateLabel(Tr("UI_GARDEN_VOIDLINGS"), 8));

        _search = new LineEdit
        {
            PlaceholderText = Tr("UI_GARDEN_VOIDLINGS_SEARCH"),
            CustomMinimumSize = new Vector2(176, 22),
            ClearButtonEnabled = true
        };
        UiFactory.ApplyPixelFont(_search, 7);
        _search.TextChanged += _ => RebuildList();
        column.AddChild(_search);

        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(176, 112),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        column.AddChild(scroll);

        _list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _list.AddThemeConstantOverride("separation", 2);
        scroll.AddChild(_list);

        _empty = UiFactory.CreateLabel(Tr("UI_GARDEN_VOIDLINGS_EMPTY"), 6);
        _empty.Visible = false;
        column.AddChild(_empty);

        _toggle = UiFactory.CreateButton(Tr("UI_GARDEN_VOIDLINGS"));
        _toggle.CustomMinimumSize = new Vector2(PanelSize.X, 24);
        UiFactory.ApplyPixelFont(_toggle, 8);
        _toggle.Position = new Vector2(0, PanelSize.Y + 2.0f);
        _toggle.Size = new Vector2(PanelSize.X, 24);
        _toggle.Pressed += () =>
        {
            _panel.Visible = !_panel.Visible;
            if (_panel.Visible)
                RebuildList();
        };
        AddChild(_toggle);
    }

    public void SetVoidlings(IReadOnlyList<QuickMenuVoidlingViewState> voidlings)
    {
        _voidlings.Clear();
        _voidlings.AddRange(voidlings);
        if (_panel != null && GodotObject.IsInstanceValid(_panel) && _panel.Visible)
            RebuildList();
    }

    public void Close()
    {
        if (_panel != null && GodotObject.IsInstanceValid(_panel))
            _panel.Visible = false;
    }

    public bool IsOpen => _panel != null && GodotObject.IsInstanceValid(_panel) && _panel.Visible;

    private void RebuildList()
    {
        foreach (var child in _list.GetChildren())
            child.QueueFree();

        var filter = _search.Text.Trim();
        var matches = _voidlings.Where(candidate => Matches(candidate, filter)).ToList();
        _empty.Visible = matches.Count == 0;

        foreach (var candidate in matches)
            _list.AddChild(CreateRow(candidate));
    }

    private static bool Matches(QuickMenuVoidlingViewState candidate, string filter)
        => filter.Length == 0 ||
           candidate.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
           candidate.ColorName.Contains(filter, StringComparison.OrdinalIgnoreCase);

    private Control CreateRow(QuickMenuVoidlingViewState candidate)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 4);

        var portrait = UiFactory.CreatePortrait(
            candidate.Appearance,
            candidate.HasAngelMutation,
            candidate.OtherMutationCount,
            new Vector2(20, 20));
        portrait.MouseFilter = MouseFilterEnum.Ignore;
        row.AddChild(portrait);

        var pick = UiFactory.CreateButton($"{candidate.Name}  ({candidate.ColorName})");
        pick.CustomMinimumSize = new Vector2(148, 22);
        pick.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        UiFactory.ApplyPixelFont(pick, 7);
        pick.Pressed += () => VoidlingPicked?.Invoke(candidate.Id);
        row.AddChild(pick);

        return row;
    }
}
