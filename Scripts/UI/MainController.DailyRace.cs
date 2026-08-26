using System;
using System.Linq;
using Godot;
using Voidling.Application.Multiplayer.Leaderboards;
using Voidling.Domain.Racing;
using Voidling.Presentation.Racing;
using Voidling.Presentation.UI.Multiplayer;
using Voidling.Presentation.UI.Racing;

namespace VoidlingGame;

public partial class MainController
{
    private DailyFriendRacePresentationBridge? _dailyRaceBridge;
    private string _activeDailyRaceKey = string.Empty;
    private bool _dailyRaceResultHandled;

    private DailyFriendRacePresentationBridge DailyRaceBridge
        => _dailyRaceBridge ??= GetNode<DailyFriendRacePresentationBridge>(
            "/root/GameBootstrap/DailyFriendRacePresentationBridge");

    private void ShowDailyRace()
    {
        var status = DailyRaceBridge.GetToday(DateTimeOffset.UtcNow);
        var box = OpenOnlineModal(Tr("UI_DAILY_TITLE"), new Vector2(572, 330), ShowConnectedZone);

        var date = UiFactory.CreateLabel(
            string.Format(Tr("UI_DAILY_DATE"), status.DailyKey),
            7);
        box.AddChild(date);

        if (!string.IsNullOrWhiteSpace(status.Error))
        {
            var error = UiFactory.CreateLabel(
                string.Format(Tr("UI_DAILY_INVALID"), status.Error),
                8);
            error.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            error.AddThemeColorOverride("font_color", Color.FromHtml("#9C514B"));
            box.AddChild(error);
            return;
        }

        if (status.Completed)
        {
            var time = status.FinishedMilliseconds.HasValue
                ? FormatRaceMilliseconds(status.FinishedMilliseconds.Value)
                : "--:--.---";
            box.AddChild(UiFactory.CreateLabel(
                string.Format(Tr("UI_DAILY_COMPLETE"), status.SelectedDisplayName, time),
                9));
            box.AddChild(UiFactory.CreateLabel(Tr("UI_DAILY_ONCE_PER_DAY"), 7));

            var boards = UiFactory.CreateButton(Tr("UI_ONLINE_FRIEND_BOARDS"));
            boards.CustomMinimumSize = new Vector2(175, 25);
            boards.Disabled = !_friendsLeaderboardBridge.Availability.IsAvailable;
            boards.Pressed += () => ShowFriendsLeaderboards(ShowDailyRace);
            box.AddChild(boards);

            if (!DailyRaceBridge.LeaderboardAvailability.IsAvailable)
                box.AddChild(UiFactory.CreateLabel(Tr("UI_DAILY_SAVED_OFFLINE"), 7));
            return;
        }

        if (status.CanResume)
        {
            box.AddChild(UiFactory.CreateLabel(
                string.Format(Tr("UI_DAILY_RESUME_HINT"), status.SelectedDisplayName),
                9));
            box.AddChild(UiFactory.CreateLabel(Tr("UI_DAILY_RESUME_SAME"), 7));

            var resume = UiFactory.CreateButton(Tr("UI_DAILY_RESUME"));
            resume.CustomMinimumSize = new Vector2(180, 26);
            resume.Pressed += () => BeginOrResumeDailyRace(status.SelectedCreatureId);
            box.AddChild(resume);
            return;
        }

        box.AddChild(UiFactory.CreateLabel(Tr("UI_DAILY_PICK_HINT"), 7));
        box.AddChild(UiFactory.CreateLabel(Tr("UI_DAILY_ONCE_PER_DAY"), 7));

        var owned = _session.State.Voidlings.ToArray();
        var selectedId = owned.Any(value => value.Id == _selectedId)
            ? _selectedId
            : owned.FirstOrDefault()?.Id ?? string.Empty;
        var dailyCourse = new RacePickerCourseViewState(
            RaceCourseCatalog.Demo.Id,
            RaceCourseCatalog.Demo.Version,
            Tr("UI_RACE_COURSE_DEMO_NAME"),
            Tr("UI_RACE_COURSE_DEMO_SUMMARY"));
        var picker = new RacePickerScreen();
        picker.Configure(new RacePickerScreenState(
            owned.Select(CreateRacePickerView).ToArray(),
            selectedId,
            new[] { dailyCourse },
            dailyCourse.Id,
            dailyCourse.Version));
        // Daily race identity remains the existing canonical demo course. The general race picker
        // can select authored standard courses, but daily attempts must resume the same exact course.
        picker.RaceRequested += (creatureId, _, _) => BeginOrResumeDailyRace(creatureId);
        box.AddChild(picker);
    }

    private void BeginOrResumeDailyRace(string creatureId)
    {
        var launch = DailyRaceBridge.BeginOrResume(creatureId, DateTimeOffset.UtcNow);
        if (!launch.Success || launch.Entry == null)
        {
            ShowToast(string.Format(
                Tr("UI_DAILY_START_FAILED"),
                launch.Error ?? "unknown error"));
            return;
        }

        CloseModal(false);
        StartDailyRace(launch);
    }

    private void StartDailyRace(DailyFriendRaceLaunchResult launch)
    {
        if (launch.Entry == null)
            return;

        _activeDailyRaceKey = launch.DailyKey;
        _dailyRaceResultHandled = false;

        _garden.SetGameplayActive(false);
        _garden.Visible = false;
        _uiRoot.Visible = false;

        var race = new RaceScreen();
        // Daily leaderboard timing must never use the normal single-player auto-finish shortcut.
        race.Configure(launch.Entry, autoFinish: false);
        race.RaceCompleted += OnDailyRaceCompleted;
        race.ReturnRequested += EndDailyRace;
        _race = race;
        AddChild(race);
    }

    private void OnDailyRaceCompleted(int placement)
    {
        if (_dailyRaceResultHandled ||
            _race == null ||
            !GodotObject.IsInstanceValid(_race) ||
            string.IsNullOrWhiteSpace(_activeDailyRaceKey))
        {
            return;
        }

        _gardenEventLog.Append(string.Format(Tr("UI_GARDEN_LOG_RACE_RESULT"), placement));

        if (!_race.TryGetPlayerFinishMilliseconds(out var finishedMilliseconds))
        {
            GD.PushWarning("Daily race completed without a deterministic player finish time; local attempt remains resumable.");
            return;
        }

        var completion = DailyRaceBridge.Complete(_activeDailyRaceKey, finishedMilliseconds);
        if (!completion.Success)
        {
            GD.PushWarning(
                "Daily race result could not be persisted; local attempt remains resumable: " +
                (completion.Error ?? "unknown error"));
            return;
        }

        _dailyRaceResultHandled = true;
        if (DailyRaceBridge.LeaderboardAvailability.IsAvailable)
            ProjectDailyRaceResult(completion.DailyKey);
    }

    private async void ProjectDailyRaceResult(string dailyKey)
    {
        string? failure = null;
        try
        {
            var result = await DailyRaceBridge.ProjectAsync(dailyKey).ConfigureAwait(false);
            if (!result.Success)
                failure = result.Error ?? "unknown leaderboard error";
        }
        catch (Exception exception)
        {
            failure = exception.Message;
        }

        if (!string.IsNullOrWhiteSpace(failure))
        {
            GD.PushWarning(
                $"Daily race {dailyKey} is saved locally but friend-board projection failed: {failure}. " +
                "It will be retried on a later Steam-capable startup.");
        }
    }

    private void EndDailyRace()
    {
        if (_race != null && GodotObject.IsInstanceValid(_race))
        {
            _race.RaceCompleted -= OnDailyRaceCompleted;
            _race.ReturnRequested -= EndDailyRace;
        }

        _activeDailyRaceKey = string.Empty;
        _dailyRaceResultHandled = false;
        EndRace();
    }

    private static string FormatRaceMilliseconds(int milliseconds)
    {
        var span = TimeSpan.FromMilliseconds(Math.Max(0, milliseconds));
        return span.TotalMinutes >= 1.0
            ? $"{(int)span.TotalMinutes}:{span.Seconds:00}.{span.Milliseconds:000}"
            : $"{span.Seconds}.{span.Milliseconds:000}s";
    }
}
