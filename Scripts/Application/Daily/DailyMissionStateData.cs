using System;
using System.Collections.Generic;

namespace Voidling.Application.Daily;

public sealed class DailyMissionProgressData
{
    public string MissionId { get; set; } = string.Empty;
    public int Progress { get; set; }
    public bool Claimed { get; set; }
}

/// <summary>
/// Persisted frozen mission selection/progress for one local calendar day. Mission definitions and
/// tuning remain external authorable rules; only stable IDs and player progress enter the save.
/// </summary>
public sealed class DailyMissionStateData
{
    public int DayNumber { get; set; }
    public List<DailyMissionProgressData> Missions { get; set; } = new();
}
