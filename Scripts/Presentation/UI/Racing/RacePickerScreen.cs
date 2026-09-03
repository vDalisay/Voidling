using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Voidling.Presentation.Voidlings;
using VoidlingGame;

namespace Voidling.Presentation.UI.Racing;

public readonly record struct RacePickerVoidlingViewState(
    string Id,
    string Name,
    VoidlingVisualAppearance Appearance,
    bool HasAngelMutation,
    int OtherMutationCount,
    string StatSummary);

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
/// Standalone race-selection view. Appearance remains semantic until the shared visual factory
/// composes portraits/cards; course selection likewise emits only stable semantic IDs/versions.
/// </summary>
public partial class RacePickerScreen : VBoxContainer
{
    public event Action<string, string, int>? RaceRequested;

    private RacePickerScreenState? _state;
    private string _selectedId = string.Empty;
    private string _selectedCourseId = string.Empty;
    private int _selectedCourseVersion;

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

        AddThemeConstantOverride("separation", 7);
        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

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

        AddChild(UiFactory.CreateLabel(Tr("UI_RACE_PICKER_HINT"), 7));
        BuildPicker(_state.Voidlings, _state.Courses);
    }

    // Courses are picked from cards that spell out the sections ahead, so a player can tell a
    // Climb/Power course from a Swim one before committing to the start line.
    private void BuildCourseCards(IReadOnlyList<RacePickerCourseViewState> courses)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        AddChild(row);

        var summary = UiFactory.CreateLabel(string.Empty, 6);
        summary.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        summary.CustomMinimumSize = new Vector2(500, 16);
        AddChild(summary);

        var buttons = new Dictionary<string, Button>(StringComparer.Ordinal);
        foreach (var course in courses)
        {
            var captured = course;
            var key = CourseKey(course);
            var card = new Button
            {
                ToggleMode = true,
                CustomMinimumSize = new Vector2(168, 42),
                FocusMode = Control.FocusModeEnum.None,
                ButtonPressed = key == CourseKey(_selectedCourseId, _selectedCourseVersion)
            };
            UiFactory.ApplyButtonChrome(card);
            UiFactory.ApplyPixelFont(card, 7);
            card.Text = $"{course.Name}\n{string.Join(" - ", course.Sections)}\n{course.LengthMeters} M";
            card.Pressed += () =>
            {
                _selectedCourseId = captured.Id;
                _selectedCourseVersion = captured.Version;
                summary.Text = captured.Summary;
                foreach (var pair in buttons)
                    pair.Value.ButtonPressed = pair.Key == CourseKey(captured);
            };
            buttons[key] = card;
            row.AddChild(card);
        }

        summary.Text = courses
            .First(course => CourseKey(course) == CourseKey(_selectedCourseId, _selectedCourseVersion))
            .Summary;
    }

    private static string CourseKey(RacePickerCourseViewState course) => CourseKey(course.Id, course.Version);

    private static string CourseKey(string id, int version) => $"{id}@{version}";

    private void BuildPicker(
        IReadOnlyList<RacePickerVoidlingViewState> voidlings,
        IReadOnlyList<RacePickerCourseViewState> courses)
    {
        AddChild(UiFactory.CreateLabel(Tr("UI_RACE_PICKER_COURSE"), 7));
        BuildCourseCards(courses);

        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(510, 90),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            VerticalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        var cards = new HBoxContainer();
        cards.AddThemeConstantOverride("separation", 7);
        scroll.AddChild(cards);
        AddChild(scroll);

        var selected = voidlings.First(v => v.Id == _selectedId);
        var previewRow = new HBoxContainer();
        previewRow.AddThemeConstantOverride("separation", 12);
        var previewPortrait = UiFactory.CreatePortrait(
            selected.Appearance,
            selected.HasAngelMutation,
            selected.OtherMutationCount,
            new Vector2(72, 72));
        previewRow.AddChild(previewPortrait);

        var previewText = new VBoxContainer();
        previewText.AddThemeConstantOverride("separation", 2);
        var previewName = UiFactory.CreateTitle(selected.Name);
        var previewStats = UiFactory.CreateLabel(selected.StatSummary, 7);
        previewText.AddChild(previewName);
        previewText.AddChild(previewStats);
        previewRow.AddChild(previewText);
        AddChild(previewRow);

        var cardButtons = new Dictionary<string, Button>(StringComparer.Ordinal);
        void UpdatePreview(RacePickerVoidlingViewState candidate)
        {
            _selectedId = candidate.Id;
            UiFactory.SetPortraitData(
                previewPortrait,
                candidate.Appearance,
                candidate.HasAngelMutation,
                candidate.OtherMutationCount);
            previewName.Text = candidate.Name;
            previewStats.Text = candidate.StatSummary;
            foreach (var pair in cardButtons)
                pair.Value.ButtonPressed = pair.Key == candidate.Id;
        }

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
                    if (pressed)
                        UpdatePreview(captured);
                },
                out var card);
            cardButtons[creature.Id] = card;
            cards.AddChild(entry);
        }

        UpdatePreview(selected);

        var start = UiFactory.CreateButton(Tr("UI_RACE_START"));
        start.CustomMinimumSize = new Vector2(170, 26);
        start.Pressed += () => RaceRequested?.Invoke(
            _selectedId,
            _selectedCourseId,
            _selectedCourseVersion);
        AddChild(start);
    }
}
