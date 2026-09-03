using System.Linq;
using Godot;
using Voidling.Domain.Racing;
using Voidling.Presentation.Racing;
using Voidling.Presentation.UI.Common;
using Voidling.Presentation.UI.Racing;

namespace VoidlingGame;

public partial class MainController
{
    private void ShowRacePickerWithCourses()
    {
        var owned = _session.State.Voidlings.ToArray();
        var selectedId = owned.Any(v => v.Id == _selectedId)
            ? _selectedId
            : owned.FirstOrDefault()?.Id ?? string.Empty;

        var viewState = owned.Select(CreateRacePickerView).ToArray();
        var courses = RaceCourseCatalog.All.Select(CreateRacePickerCourseView).ToArray();

        var box = OpenModal(Tr("UI_RACE_PICKER_TITLE"), new Vector2(552, 335));
        var screen = new RacePickerScreen();
        screen.Configure(new RacePickerScreenState(
            viewState,
            selectedId,
            courses,
            RaceCourseCatalog.Demo.Id,
            RaceCourseCatalog.Demo.Version));
        screen.RaceRequested += (creatureId, courseId, courseVersion) =>
        {
            if (_session.FindVoidling(creatureId) == null)
                return;

            CloseModal();
            StartRaceWithCourse(creatureId, courseId, courseVersion);
        };
        box.AddChild(screen);
    }

    // The section list is read off the authored course instead of being written by hand, so a course
    // that gains or loses a Climb/Glide stretch cannot advertise the wrong thing.
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
            (int)(definition.Course.EndX - definition.Course.StartX));
    }

    private string SectionName(RaceSegmentKind kind)
        => Tr(RaceCoursePresentationCatalog.SectionKeyFor(kind));

    private void StartRaceWithCourse(string creatureId, string courseId, int courseVersion)
    {
        var entry = _session.CreateRaceEntryFor(creatureId, courseId, courseVersion);
        var autoFinish = _session.State.AutoFinishRaces;

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
}
