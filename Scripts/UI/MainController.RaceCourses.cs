using System.Linq;
using Godot;
using Voidling.Domain.Racing;
using Voidling.Presentation.Racing;
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
        var courses = new[]
        {
            new RacePickerCourseViewState(
                RaceCourseCatalog.Demo.Id,
                RaceCourseCatalog.Demo.Version,
                Tr("UI_RACE_COURSE_DEMO_NAME"),
                Tr("UI_RACE_COURSE_DEMO_SUMMARY")),
            new RacePickerCourseViewState(
                RaceCourseCatalog.LongStandard.Id,
                RaceCourseCatalog.LongStandard.Version,
                Tr("UI_RACE_COURSE_LONG_NAME"),
                Tr("UI_RACE_COURSE_LONG_SUMMARY"))
        };

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
