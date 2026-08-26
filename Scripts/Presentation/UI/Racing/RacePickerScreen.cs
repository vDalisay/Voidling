using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Voidling.Domain.Genetics;
using Voidling.Presentation.Voidlings;
using VoidlingGame;

namespace Voidling.Presentation.UI.Racing;

public readonly record struct RacePickerVoidlingViewState(
    string Id,
    string Name,
    string TintHex,
    AppearancePhenotype Appearance,
    bool HasAngelMutation,
    int OtherMutationCount,
    string StatSummary);

public readonly record struct RacePickerCourseViewState(
    string Id,
    int Version,
    string Name,
    string Summary);

public sealed record RacePickerScreenState(
    IReadOnlyList<RacePickerVoidlingViewState> Voidlings,
    string SelectedId,
    IReadOnlyList<RacePickerCourseViewState> Courses,
    string SelectedCourseId,
    int SelectedCourseVersion);

/// <summary>
/// Standalone race-selection view. It renders immutable, presentation-ready racer/course snapshots
/// and emits only semantic selection IDs. It has no knowledge of GameSession, race construction,
/// persistence, balance rules, or the race simulator.
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
            course.Id == _state.SelectedCourseId &&
            course.Version == _state.SelectedCourseVersion);
        if (string.IsNullOrWhiteSpace(selectedCourse.Id))
            selectedCourse = _state.Courses[0];
        _selectedCourseId = selectedCourse.Id;
        _selectedCourseVersion = selectedCourse.Version;

        AddChild(UiFactory.CreateLabel(Tr("UI_RACE_PICKER_HINT"), 7));
        BuildPicker(_state.Voidlings, _state.Courses);
    }

    private void BuildPicker(
        IReadOnlyList<RacePickerVoidlingViewState> voidlings,
        IReadOnlyList<RacePickerCourseViewState> courses)
    {
        var courseRow = new HBoxContainer();
        courseRow.AddThemeConstantOverride("separation", 7);
        courseRow.AddChild(UiFactory.CreateLabel(Tr("UI_RACE_PICKER_COURSE"), 7));

        var courseOption = new OptionButton
        {
            CustomMinimumSize = new Vector2(185, 24)
        };
        UiFactory.ApplyPixelFont(courseOption, 7);
        UiFactory.ApplyButtonChrome(courseOption);
        var selectedCourseIndex = 0;
        for (var i = 0; i < courses.Count; i++)
        {
            courseOption.AddItem(courses[i].Name);
            if (courses[i].Id == _selectedCourseId && courses[i].Version == _selectedCourseVersion)
                selectedCourseIndex = i;
        }
        courseOption.Select(selectedCourseIndex);
        courseRow.AddChild(courseOption);
        AddChild(courseRow);

        var courseSummary = UiFactory.CreateLabel(courses[selectedCourseIndex].Summary, 6);
        courseSummary.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        AddChild(courseSummary);

        courseOption.ItemSelected += index =>
        {
            var selectedCourse = courses[(int)index];
            _selectedCourseId = selectedCourse.Id;
            _selectedCourseVersion = selectedCourse.Version;
            courseSummary.Text = selectedCourse.Summary;
        };

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
        var previewPortrait = VoidlingAppearancePresenter.CreatePortrait(
            selected.TintHex,
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
            VoidlingAppearancePresenter.ApplyPortrait(
                previewPortrait,
                candidate.TintHex,
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
            var entry = new VBoxContainer { CustomMinimumSize = new Vector2(84, 78) };
            entry.AddThemeConstantOverride("separation", 1);

            var card = UiFactory.CreateButton("");
            card.CustomMinimumSize = new Vector2(80, 58);
            card.ToggleMode = true;
            card.KeepPressedOutside = true;
            var portrait = VoidlingAppearancePresenter.CreatePortrait(
                creature.TintHex,
                creature.Appearance,
                creature.HasAngelMutation,
                creature.OtherMutationCount,
                new Vector2(48, 48));
            portrait.Position = new Vector2(16, 4);
            portrait.Size = new Vector2(48, 48);
            card.AddChild(portrait);
            card.Toggled += pressed =>
            {
                if (pressed)
                    UpdatePreview(captured);
            };
            entry.AddChild(card);

            var label = UiFactory.CreateLabel(creature.Name, 6);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            label.AddThemeColorOverride("font_color", Color.FromHtml("#2F4437"));
            entry.AddChild(label);

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
