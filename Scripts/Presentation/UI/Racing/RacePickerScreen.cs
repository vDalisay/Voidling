using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Voidling.Presentation.Voidlings;
using VoidlingGame;

namespace Voidling.Presentation.UI.Racing;

public readonly record struct RacePickerStatViewState(string Name, Color Color, string Rank, int Level);

public readonly record struct RacePickerVoidlingViewState(
    string Id,
    string Name,
    VoidlingVisualAppearance Appearance,
    bool HasAngelMutation,
    int OtherMutationCount,
    string StatSummary,
    IReadOnlyList<RacePickerStatViewState> Stats);

public readonly record struct RacePickerCourseViewState(
    string Id,
    int Version,
    string Name,
    string Summary,
    IReadOnlyList<string> Sections,
    int LengthMeters);

public sealed record RacePickerScreenState(
    IReadOnlyList<RacePickerVoidlingViewState> Voidlings,
    string SelectedId,
    IReadOnlyList<RacePickerCourseViewState> Courses,
    string SelectedCourseId,
    int SelectedCourseVersion);

/// <summary>
/// Race entry beside the manager's rail: course choice on the left, racer choice and the chosen
/// racer's trained stats on the right, and one Start action. Appearance remains semantic until the
/// shared visual factory composes portraits; course selection emits only stable semantic IDs/versions.
/// </summary>
public partial class RacePickerScreen : VBoxContainer
{
    public event Action<string, string, int>? RaceRequested;

    private RacePickerScreenState? _state;
    private string _selectedId = string.Empty;
    private string _selectedCourseId = string.Empty;
    private int _selectedCourseVersion;

    private readonly Dictionary<string, Button> _courseButtons = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Button> _racerButtons = new(StringComparer.Ordinal);
    private VBoxContainer _statsColumn = null!;
    private Label _footer = null!;

    public void Configure(RacePickerScreenState state)
    {
        if (IsInsideTree())
            throw new InvalidOperationException("RacePickerScreen must be configured before it enters the scene tree.");
        _state = state ?? throw new ArgumentNullException(nameof(state));
    }

    public override void _Ready()
    {
        if (_state == null)
            throw new InvalidOperationException("RacePickerScreen must be configured before AddChild.");

        Name = "RaceEntry";
        AddThemeConstantOverride("separation", 5);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;

        if (_state.Voidlings.Count == 0)
        {
            AddChild(UiFactory.CreateLabel(Tr("UI_RACE_PICKER_EMPTY"), 9));
            return;
        }
        if (_state.Courses.Count == 0)
        {
            AddChild(UiFactory.CreateLabel(Tr("UI_RACE_PICKER_NO_COURSES"), 9));
            return;
        }

        _selectedId = _state.Voidlings.Any(v => v.Id == _state.SelectedId)
            ? _state.SelectedId
            : _state.Voidlings[0].Id;
        var selectedCourse = _state.Courses.FirstOrDefault(course =>
            course.Id == _state.SelectedCourseId && course.Version == _state.SelectedCourseVersion);
        if (string.IsNullOrWhiteSpace(selectedCourse.Id))
            selectedCourse = _state.Courses[0];
        _selectedCourseId = selectedCourse.Id;
        _selectedCourseVersion = selectedCourse.Version;

        var body = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 8);
        AddChild(body);
        body.AddChild(BuildCourseColumn(_state.Courses));
        body.AddChild(BuildRacerColumn(_state.Voidlings));
        AddChild(BuildFooter());

        UpdateCourse(selectedCourse);
        UpdateRacer(_state.Voidlings.First(v => v.Id == _selectedId));
    }

    public void FocusSelection()
        => (_courseButtons.TryGetValue(CourseKey(_selectedCourseId, _selectedCourseVersion), out var course)
            ? course
            : _courseButtons.Values.FirstOrDefault())?.GrabFocus();

    // Courses spell out the sections ahead, so a player can tell a Climb/Power course from a Swim
    // one before committing to the start line.
    private Control BuildCourseColumn(IReadOnlyList<RacePickerCourseViewState> courses)
    {
        var column = new VBoxContainer { CustomMinimumSize = new Vector2(212, 0), SizeFlagsVertical = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 4);
        column.AddChild(UiFactory.CreateLabel(Tr("UI_RACE_PICKER_COURSE"), 8));

        var scroll = new ScrollContainer
        {
            Name = "CourseList",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        UiFactory.StyleScroll(scroll);
        column.AddChild(scroll);
        var list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(list);

        foreach (var course in courses)
        {
            var captured = course;
            var card = new Button
            {
                Name = "Course_" + course.Id,
                ToggleMode = true,
                CustomMinimumSize = new Vector2(200, 62),
                FocusMode = FocusModeEnum.All,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                ButtonPressed = CourseKey(course) == CourseKey(_selectedCourseId, _selectedCourseVersion)
            };
            UiFactory.ApplyButtonChrome(card);
            var copy = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            copy.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, LayoutPresetMode.Minsize, 6);
            copy.AddThemeConstantOverride("separation", 1);
            copy.AddChild(UiFactory.CreateLabel(course.Name, 8));
            var summary = UiFactory.CreateLabel(course.Summary, 6);
            summary.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            summary.SizeFlagsVertical = SizeFlags.ExpandFill;
            copy.AddChild(summary);
            copy.AddChild(UiFactory.CreateLabel(
                $"{string.Join(" · ", course.Sections)}   {course.LengthMeters} M", 6));
            card.AddChild(copy);
            card.Pressed += () => UpdateCourse(captured);
            _courseButtons[CourseKey(course)] = card;
            list.AddChild(card);
        }

        return column;
    }

    private Control BuildRacerColumn(IReadOnlyList<RacePickerVoidlingViewState> voidlings)
    {
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 4);
        column.AddChild(UiFactory.CreateLabel(Tr("UI_RACE_PICKER_RACER"), 8));

        var scroll = new ScrollContainer
        {
            Name = "RacerList",
            CustomMinimumSize = new Vector2(250, 82),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        UiFactory.StyleScroll(scroll);
        column.AddChild(scroll);
        var cards = new HBoxContainer();
        cards.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(cards);

        foreach (var creature in voidlings)
        {
            var captured = creature;
            var entry = UiFactory.CreateVoidlingCard(
                creature.Name,
                creature.Appearance,
                creature.HasAngelMutation,
                creature.OtherMutationCount,
                pressed =>
                {
                    if (pressed) UpdateRacer(captured);
                },
                out var card);
            card.Name = "Racer_" + creature.Id;
            _racerButtons[creature.Id] = card;
            cards.AddChild(entry);
        }

        _statsColumn = new VBoxContainer { Name = "RacerStats", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _statsColumn.AddThemeConstantOverride("separation", 3);
        column.AddChild(_statsColumn);

        var hint = UiFactory.CreateLabel(Tr("UI_RACE_PICKER_HINT"), 6);
        hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        column.AddChild(hint);
        column.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
        return column;
    }

    private Control BuildFooter()
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        _footer = UiFactory.CreateLabel(string.Empty, 6);
        _footer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _footer.VerticalAlignment = VerticalAlignment.Center;
        _footer.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        row.AddChild(_footer);

        var start = UiFactory.CreateButton(Tr("UI_RACE_START"));
        start.Name = "StartRace";
        start.CustomMinimumSize = new Vector2(170, 28);
        UiFactory.ApplyPrimaryStyle(start);
        start.Pressed += () => RaceRequested?.Invoke(_selectedId, _selectedCourseId, _selectedCourseVersion);
        row.AddChild(start);
        return row;
    }

    private void UpdateCourse(RacePickerCourseViewState course)
    {
        _selectedCourseId = course.Id;
        _selectedCourseVersion = course.Version;
        foreach (var pair in _courseButtons)
            pair.Value.ButtonPressed = pair.Key == CourseKey(course);
        RefreshFooter();
    }

    private void UpdateRacer(RacePickerVoidlingViewState racer)
    {
        _selectedId = racer.Id;
        foreach (var pair in _racerButtons)
            pair.Value.SetPressedNoSignal(pair.Key == racer.Id);

        foreach (var child in _statsColumn.GetChildren())
        {
            if (child is Control control) control.MouseFilter = MouseFilterEnum.Ignore;
            child.QueueFree();
        }

        _statsColumn.AddChild(UiFactory.CreateLabel(
            string.Format(Tr("UI_RACE_PICKER_TRAINED"), racer.Name), 7));
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 4);
        foreach (var stat in racer.Stats)
        {
            var cell = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            cell.AddThemeConstantOverride("separation", 0);
            var name = UiFactory.CreateLabel(stat.Name, 6);
            name.HorizontalAlignment = HorizontalAlignment.Center;
            name.AddThemeColorOverride("font_color", PaperInk(stat.Color));
            cell.AddChild(name);
            var level = UiFactory.CreateLabel(string.Format(Tr("UI_PROFILE_LEVEL_VALUE"), stat.Level), 8);
            level.HorizontalAlignment = HorizontalAlignment.Center;
            cell.AddChild(level);
            var rank = UiFactory.CreateLabel(stat.Rank, 6);
            rank.HorizontalAlignment = HorizontalAlignment.Center;
            cell.AddChild(rank);
            row.AddChild(cell);
        }
        _statsColumn.AddChild(row);
        RefreshFooter();
    }

    private void RefreshFooter()
    {
        var course = _state!.Courses.First(c => CourseKey(c) == CourseKey(_selectedCourseId, _selectedCourseVersion));
        var racer = _state.Voidlings.First(v => v.Id == _selectedId);
        _footer.Text = $"{course.Name} · {racer.Name}";
    }

    // Stat identity colours are authored for the dark Garden inspector; on the ledger's paper the
    // pale ones (swim yellow, stamina white) vanish, so darken by however much luminance is over.
    private static Color PaperInk(Color color)
        => color.Darkened(Mathf.Clamp(color.Luminance - 0.35f, 0f, 0.6f));

    private static string CourseKey(RacePickerCourseViewState course) => CourseKey(course.Id, course.Version);

    private static string CourseKey(string id, int version) => $"{id}@{version}";
}
