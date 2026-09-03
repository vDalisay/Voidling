using Voidling.Application.Daily;
using Voidling.Domain.Rules;

namespace VoidlingGame;

public partial class GameSession
{
    private readonly DailyMissionUseCase _dailyMissions = new();

    public DailyMissionStatus GetDailyMissionStatus()
    {
        var changed = _dailyMissions.EnsureDay(State, LocalDayNumber(), GameRules.DailyMissionRules);
        if (changed)
            Save();
        return _dailyMissions.GetStatus(State, LocalDayNumber(), GameRules.DailyMissionRules);
    }

    public bool ClaimDailyMission(string missionId)
    {
        var result = _dailyMissions.Claim(State, LocalDayNumber(), GameRules.DailyMissionRules, missionId);
        if (!result.Claimed)
            return false;

        var message = $"Daily mission complete: +{result.CoinsAwarded} sprouts.";
        SaveAndNotify(message);
        RaiseGardenEvent(message);
        return true;
    }

    private bool RecordDailyMissionEvent(DailyMissionEventKind eventKind, int amount = 1)
        => _dailyMissions.RecordEvent(
            State,
            LocalDayNumber(),
            GameRules.DailyMissionRules,
            eventKind,
            amount);
}
