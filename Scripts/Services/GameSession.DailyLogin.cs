using System;
using Voidling.Application.Daily;

namespace VoidlingGame;

public partial class GameSession
{
    private readonly DailyLoginUseCase _dailyLogin = new();

    public DailyLoginStatus GetDailyLoginStatus()
        => _dailyLogin.GetStatus(State, LocalDayNumber(), GameRules.DailyLoginCoinRewards);

    public bool ClaimDailyLogin()
    {
        var result = _dailyLogin.Claim(State, LocalDayNumber(), GameRules.DailyLoginCoinRewards);
        if (!result.Claimed)
            return false;

        var message = $"Daily check-in: +{result.CoinsAwarded} sprouts.";
        SaveAndNotify(message);
        RaiseGardenEvent(message);
        return true;
    }

    private static int LocalDayNumber()
        => DateOnly.FromDateTime(DateTime.Now).DayNumber;
}
