using System;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using Voidling.Application.Multiplayer.Leaderboards;
using Voidling.Application.Ports.Multiplayer;

namespace Voidling.Presentation.UI.Multiplayer;

/// <summary>
/// Godot-facing bridge for the local-authoritative daily race. It exposes application results but
/// contains no Steam/networking implementation details and remains usable when Steam is unavailable.
/// </summary>
public partial class DailyFriendRacePresentationBridge : Node
{
    private DailyFriendRaceFacade? _facade;

    public MultiplayerAvailability LeaderboardAvailability => RequireFacade().LeaderboardAvailability;

    public void Configure(DailyFriendRaceFacade facade)
    {
        if (_facade != null)
            throw new InvalidOperationException("Daily race presentation bridge is already configured.");
        _facade = facade ?? throw new ArgumentNullException(nameof(facade));
    }

    public DailyFriendRaceStatus GetToday(DateTimeOffset utcNow)
        => RequireFacade().GetToday(utcNow);

    public DailyFriendRaceLaunchResult BeginOrResume(string creatureId, DateTimeOffset utcNow)
        => RequireFacade().BeginOrResume(creatureId, utcNow);

    public DailyFriendRaceCompleteResult Complete(string dailyKey, int finishedMilliseconds)
        => RequireFacade().Complete(dailyKey, finishedMilliseconds);

    public Task<LeaderboardOperationResult> ProjectAsync(
        string dailyKey,
        CancellationToken cancellationToken = default)
        => RequireFacade().ProjectAsync(dailyKey, cancellationToken);

    private DailyFriendRaceFacade RequireFacade()
        => _facade ?? throw new InvalidOperationException("Daily race presentation bridge is not configured.");
}
