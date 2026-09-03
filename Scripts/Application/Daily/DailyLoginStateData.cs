namespace Voidling.Application.Daily;

/// <summary>
/// Persisted daily check-in state. Calendar interpretation stays outside this DTO; Application
/// receives an explicit local day number so tests and non-Godot callers remain deterministic.
/// </summary>
public sealed class DailyLoginStateData
{
    public int LastClaimDayNumber { get; set; }
    public int Streak { get; set; }
}
