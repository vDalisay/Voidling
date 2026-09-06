using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Voidling.Application.Racing;
using Voidling.Domain.Racing;
using Voidling.Presentation.Racing;
using Voidling.Presentation.UI.Common;
using Voidling.Presentation.UI.Racing;

namespace VoidlingGame;

public partial class MainController
{
    private int _raceLevel = RaceEntryScreen.MinLevel;
    private string _raceCourseId = RaceCourseCatalog.Demo.Id;
    private int _raceCourseVersion = RaceCourseCatalog.Demo.Version;

    // The finished race's identity, kept so its time can be filed under the same course and level
    // that were entered rather than whatever is selected on the entry screen afterwards.
    private string _activeRaceCourseId = string.Empty;
    private int _activeRaceCourseVersion;
    private int _activeRaceLevel;
    private string _activeRaceCreatureName = string.Empty;

    /// <summary>Opens the full-screen race entry flow: course and level, then racer, then confirm.</summary>
    private void ShowRacePickerWithCourses()
    {
        var owned = _session.State.Voidlings.ToArray();
        var selectedId = owned.Any(v => v.Id == _selectedId)
            ? _selectedId
            : owned.FirstOrDefault()?.Id ?? string.Empty;

        var box = OpenFullScreenModal(Tr("UI_RACE_PICKER_TITLE"));
        var screen = new RaceEntryScreen();
        screen.Configure(new RacePickerScreenState(
            owned.Select(CreateRacePickerView).ToArray(),
            selectedId,
            RaceCourseCatalog.All.Select(CreateRacePickerCourseView).ToArray(),
            _raceCourseId,
            _raceCourseVersion,
            _raceLevel,
            CreateCourseRecordViews()));
        screen.SelectionChanged += (creatureId, courseId, courseVersion, level) =>
        {
            _selectedId = creatureId;
            _raceCourseId = courseId;
            _raceCourseVersion = courseVersion;
            _raceLevel = level;
        };
        screen.Dismissed += CloseModal;
        screen.RaceRequested += (creatureId, courseId, courseVersion, level) =>
        {
            if (_session.FindVoidling(creatureId) == null)
                return;

            CloseModal();
            StartRaceWithCourse(creatureId, courseId, courseVersion, level);
        };
        box.AddChild(screen);
        Callable.From(screen.FocusSelection).CallDeferred();
    }

    private IReadOnlyDictionary<string, RaceCourseRecordViewState> CreateCourseRecordViews()
    {
        var records = new Dictionary<string, RaceCourseRecordViewState>(StringComparer.Ordinal);
        foreach (var record in _session.State.CourseRecords)
        {
            records[RaceEntryScreen.RecordKey(record.CourseId, record.CourseVersion, record.Level)] =
                new RaceCourseRecordViewState(record.CreatureName, FormatRaceMilliseconds(record.Milliseconds));
        }
        return records;
    }

    // The section list and the minimap are both read off the authored course instead of being
    // written by hand, so a course that gains or loses a stretch cannot advertise the wrong thing.
    private RacePickerCourseViewState CreateRacePickerCourseView(RaceCourseDefinition definition)
    {
        var sections = definition.Course.Segments
            .Select(segment => segment.Kind)
            .Distinct()
            .Select(SectionName)
            .ToArray();

        var (nameKey, summaryKey) = RaceCoursePresentationCatalog.KeysFor(definition.Id);
        return new RacePickerCourseViewState(
            definition.Id,
            definition.Version,
            Tr(nameKey),
            Tr(summaryKey),
            sections,
            (int)(definition.Course.EndX - definition.Course.StartX),
            definition.Course.StartX,
            definition.Course.EndX,
            definition.Course.Segments
                .Select(segment => new CourseMinimapSegment(segment.Kind.ToString(), segment.StartX, segment.EndX))
                .ToArray(),
            definition.Course.Obstacles.ToArray());
    }

    private string SectionName(RaceSegmentKind kind)
        => Tr(RaceCoursePresentationCatalog.SectionKeyFor(kind));

    private void StartRaceWithCourse(string creatureId, string courseId, int courseVersion)
        => StartRaceWithCourse(creatureId, courseId, courseVersion, RaceDifficulty.Easiest);

    private void StartRaceWithCourse(string creatureId, string courseId, int courseVersion, int level)
    {
        var entry = _session.CreateRaceEntryFor(creatureId, courseId, courseVersion, level);
        var autoFinish = _session.State.AutoFinishRaces;

        _activeRaceCourseId = courseId;
        _activeRaceCourseVersion = courseVersion;
        _activeRaceLevel = RaceDifficulty.Clamp(level);
        _activeRaceCreatureName = _session.FindVoidling(creatureId)?.Name ?? string.Empty;

        _garden.SetGameplayActive(false);
        _garden.Visible = false;
        _uiRoot.Visible = false;

        var race = new RaceScreen();
        race.Configure(entry, autoFinish);
        race.RaceCompleted += OnRaceCompleted;
        race.ReturnRequested += EndRace;
        _race = race;
        AddChild(race);
    }

    /// <summary>Files a finished standard race under the course and level it was entered at.</summary>
    private void RecordCourseFinish(int finishedMilliseconds)
    {
        if (string.IsNullOrEmpty(_activeRaceCourseId))
            return;

        if (_session.RecordCourseFinish(
                _activeRaceCourseId,
                _activeRaceCourseVersion,
                _activeRaceLevel,
                finishedMilliseconds,
                _activeRaceCreatureName))
        {
            _gardenEventLog.Append(string.Format(
                Tr("UI_GARDEN_LOG_COURSE_RECORD"),
                _activeRaceCreatureName,
                FormatRaceMilliseconds(finishedMilliseconds)));
        }
    }
}
