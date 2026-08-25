using System;
using Godot;
using Voidling.Presentation.UI.Multiplayer;

namespace VoidlingGame;

public partial class MainController
{
    private const string DemoCourseLeaderboardId = "demo";
    private const int DemoCourseLeaderboardRulesVersion = 1;

    private FriendsLeaderboardPanel? _friendsLeaderboardPanel;
    private int _friendsLeaderboardRequestVersion;

    private void ShowFriendsLeaderboards()
    {
        var availability = _friendsLeaderboardBridge.Availability;
        var box = OpenModal(Tr("UI_LEADERBOARD_TITLE"), new Vector2(470, 318));
        var panel = new FriendsLeaderboardPanel();
        panel.Configure(new FriendsLeaderboardPanelState(
            availability.IsAvailable,
            availability.Reason ?? string.Empty,
            FriendsLeaderboardKind.MultiplayerWins,
            "Multiplayer wins",
            availability.IsAvailable,
            Array.Empty<FriendsLeaderboardRow>(),
            string.Empty));
        panel.MultiplayerWinsRequested += LoadFriendMultiplayerWins;
        panel.TodayDailyRequested += LoadTodayDailyFriendRace;
        panel.CourseBestRequested += LoadFriendDemoCourseBestTime;
        _friendsLeaderboardPanel = panel;
        box.AddChild(panel);

        if (availability.IsAvailable)
            LoadFriendMultiplayerWins();
    }

    private async void LoadFriendMultiplayerWins()
    {
        var requestVersion = BeginFriendsLeaderboardRequest(
            FriendsLeaderboardKind.MultiplayerWins,
            "Multiplayer wins");
        if (requestVersion < 0)
            return;

        FriendsLeaderboardViewResult result;
        try
        {
            result = await _friendsLeaderboardBridge.LoadMultiplayerWinsAsync();
        }
        catch (Exception exception)
        {
            result = FriendsLeaderboardViewResult.Failed(
                FriendsLeaderboardKind.MultiplayerWins,
                "Multiplayer wins",
                exception.Message);
        }

        DeferredApplyFriendsLeaderboardResult(requestVersion, result);
    }

    private async void LoadTodayDailyFriendRace()
    {
        var utcNow = DateTimeOffset.UtcNow;
        var requestVersion = BeginFriendsLeaderboardRequest(
            FriendsLeaderboardKind.DailyRace,
            utcNow.ToString("yyyy-MM-dd"));
        if (requestVersion < 0)
            return;

        FriendsLeaderboardViewResult result;
        try
        {
            result = await _friendsLeaderboardBridge.LoadTodayDailyRaceAsync(utcNow);
        }
        catch (Exception exception)
        {
            result = FriendsLeaderboardViewResult.Failed(
                FriendsLeaderboardKind.DailyRace,
                utcNow.ToString("yyyy-MM-dd"),
                exception.Message);
        }

        DeferredApplyFriendsLeaderboardResult(requestVersion, result);
    }

    private async void LoadFriendDemoCourseBestTime()
    {
        var requestVersion = BeginFriendsLeaderboardRequest(
            FriendsLeaderboardKind.CourseBestTime,
            DemoCourseLeaderboardId);
        if (requestVersion < 0)
            return;

        FriendsLeaderboardViewResult result;
        try
        {
            result = await _friendsLeaderboardBridge.LoadCourseBestTimeAsync(
                DemoCourseLeaderboardId,
                DemoCourseLeaderboardRulesVersion);
        }
        catch (Exception exception)
        {
            result = FriendsLeaderboardViewResult.Failed(
                FriendsLeaderboardKind.CourseBestTime,
                DemoCourseLeaderboardId,
                exception.Message);
        }

        DeferredApplyFriendsLeaderboardResult(requestVersion, result);
    }

    private async void ProjectSinglePlayerCourseBestTime(int finishedMilliseconds)
    {
        if (finishedMilliseconds <= 0 || !_friendsLeaderboardBridge.Availability.IsAvailable)
            return;

        try
        {
            var result = await _friendsLeaderboardBridge.UploadCourseBestTimeAsync(
                DemoCourseLeaderboardId,
                DemoCourseLeaderboardRulesVersion,
                finishedMilliseconds);
            if (!result.Success)
            {
                GD.PushWarning(
                    "Steam course-best leaderboard projection failed: " +
                    (result.Error ?? "unknown Steam leaderboard error") +
                    ". The local race result and reward remain valid.");
            }
        }
        catch (Exception exception)
        {
            GD.PushWarning(
                $"Steam course-best leaderboard projection threw: {exception.Message}. " +
                "The local race result and reward remain valid.");
        }
    }

    private int BeginFriendsLeaderboardRequest(
        FriendsLeaderboardKind kind,
        string contextLabel)
    {
        var panel = _friendsLeaderboardPanel;
        if (panel == null || !GodotObject.IsInstanceValid(panel))
            return -1;

        var availability = _friendsLeaderboardBridge.Availability;
        if (!availability.IsAvailable)
        {
            panel.Render(new FriendsLeaderboardPanelState(
                false,
                availability.Reason ?? string.Empty,
                kind,
                contextLabel,
                false,
                Array.Empty<FriendsLeaderboardRow>(),
                string.Empty));
            return -1;
        }

        var version = ++_friendsLeaderboardRequestVersion;
        panel.Render(new FriendsLeaderboardPanelState(
            true,
            string.Empty,
            kind,
            contextLabel,
            true,
            Array.Empty<FriendsLeaderboardRow>(),
            string.Empty));
        return version;
    }

    private void DeferredApplyFriendsLeaderboardResult(
        int requestVersion,
        FriendsLeaderboardViewResult result)
    {
        // Steam leaderboard callbacks may complete an async continuation off the Godot main thread.
        // Queue the actual Control mutation back onto the idle/main loop and discard stale tab results.
        Callable.From(() => ApplyFriendsLeaderboardResult(requestVersion, result)).CallDeferred();
    }

    private void ApplyFriendsLeaderboardResult(
        int requestVersion,
        FriendsLeaderboardViewResult result)
    {
        if (requestVersion != _friendsLeaderboardRequestVersion)
            return;

        var panel = _friendsLeaderboardPanel;
        if (panel == null || !GodotObject.IsInstanceValid(panel))
            return;

        panel.Render(new FriendsLeaderboardPanelState(
            true,
            string.Empty,
            result.Kind,
            result.ContextLabel,
            false,
            result.Rows,
            result.Success ? string.Empty : result.Error ?? "Leaderboard query failed."));
    }
}
