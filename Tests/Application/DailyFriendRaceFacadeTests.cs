using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Voidling.Application.Multiplayer.Leaderboards;
using Voidling.Application.Ports;
using Voidling.Application.Ports.Multiplayer;
using Voidling.Application.Racing;
using Voidling.Domain.Genetics;
using Voidling.Domain.Rules;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Application;

public sealed class DailyFriendRaceFacadeTests
{
    private static readonly GameBalanceRules Rules = GameBalanceRules.DemoDefaults;
    private static readonly DateTimeOffset Today = new(2026, 8, 25, 23, 59, 0, TimeSpan.Zero);

    [Fact]
    public void BeginPersistsOnceAndLaterCallsResumeFrozenEntrant()
    {
        var runner = CreateAdult("runner", "Runner", 100);
        var alternate = CreateAdult("alternate", "Alternate", 200);
        var state = StateWith(runner, alternate);
        var repository = new FakeRepository();
        var leaderboards = new FakeLeaderboardService();
        var changed = 0;
        var facade = CreateFacade(state, repository, leaderboards, () => changed++);

        var initial = facade.GetToday(Today);
        var started = facade.BeginOrResume("runner", Today);
        runner.Name = "Renamed after start";
        var resumed = facade.BeginOrResume("alternate", Today.AddSeconds(30));
        var status = facade.GetToday(Today.AddMinutes(1));

        Assert.True(initial.CanStart);
        Assert.True(started.Success, started.Error);
        Assert.False(started.Resumed);
        Assert.True(resumed.Success, resumed.Error);
        Assert.True(resumed.Resumed);
        Assert.Equal("runner", resumed.Entry!.Entrants[0].Participant.CreatureId);
        Assert.Equal("Runner", resumed.Entry.Entrants[0].Participant.DisplayName);
        Assert.True(status.CanResume);
        Assert.Equal("Runner", status.SelectedDisplayName);
        Assert.Equal(1, repository.SaveCalls);
        Assert.Equal(1, changed);
    }

    [Fact]
    public async Task CompletePersistsThenProjectsUsingOriginalDailyKeyAcrossMidnight()
    {
        var state = StateWith(CreateAdult("runner", "Runner", 300));
        var repository = new FakeRepository();
        var leaderboards = new FakeLeaderboardService();
        var changed = 0;
        var facade = CreateFacade(state, repository, leaderboards, () => changed++);
        var started = facade.BeginOrResume("runner", Today);

        var completed = facade.Complete(started.DailyKey, 41_250);
        var projected = await facade.ProjectAsync(started.DailyKey);
        var tomorrowStatus = facade.GetToday(Today.AddMinutes(2));

        Assert.True(completed.Success, completed.Error);
        Assert.Equal(41_250, completed.FinishedMilliseconds);
        Assert.True(projected.Success, projected.Error);
        Assert.Equal(2, repository.SaveCalls);
        Assert.Equal(2, changed);
        var upload = Assert.Single(leaderboards.Uploads);
        Assert.Equal("voidling_daily_2026-08-25_v1", upload.Definition.Name);
        Assert.Equal(41_250, upload.Score);
        Assert.True(tomorrowStatus.CanStart);
    }

    [Fact]
    public void CompletedAttemptCannotBeStartedAgainOnSameUtcDay()
    {
        var state = StateWith(CreateAdult("runner", "Runner", 400));
        var facade = CreateFacade(
            state,
            new FakeRepository(),
            new FakeLeaderboardService(),
            () => { });
        var started = facade.BeginOrResume("runner", Today);
        var completed = facade.Complete(started.DailyKey, 50_000);

        var duplicate = facade.BeginOrResume("runner", Today.AddSeconds(45));
        var status = facade.GetToday(Today.AddSeconds(45));

        Assert.True(completed.Success, completed.Error);
        Assert.False(duplicate.Success);
        Assert.True(status.Completed);
        Assert.False(status.CanResume);
        Assert.False(status.CanStart);
    }

    private static DailyFriendRaceFacade CreateFacade(
        GameStateData state,
        IGameStateRepository repository,
        ILeaderboardService leaderboards,
        Action changed)
    {
        var coordinator = new DailyFriendRaceCoordinator(
            new DailyFriendRaceService(new RaceEntryFactory(Rules)),
            repository,
            new LeaderboardProjectionService(leaderboards));
        return new DailyFriendRaceFacade(coordinator, () => state, changed);
    }

    private static GameStateData StateWith(params VoidlingData[] creatures)
    {
        var state = new GameStateData();
        state.Voidlings.AddRange(creatures);
        return state;
    }

    private static VoidlingData CreateAdult(string id, string name, ulong seed)
    {
        var creature = new VoidlingData
        {
            Id = id,
            Name = name,
            Stage = LifeStage.Adult,
            Genome = new GenomeFactory(Rules.Genetics).CreateRandom(seed),
            TintHex = "#ABCDEF"
        };
        foreach (var statId in Rules.Genetics.StatIds)
            creature.TrainingPoints[statId] = 0;
        return creature;
    }

    private sealed class FakeRepository : IGameStateRepository
    {
        public int SaveCalls { get; private set; }
        public GameStateData? Load() => null;
        public void Save(GameStateData state) => SaveCalls++;
    }

    private sealed record UploadCall(
        LeaderboardDefinition Definition,
        int Score,
        bool KeepBest);

    private sealed class FakeLeaderboardService : ILeaderboardService
    {
        public MultiplayerAvailability Availability => MultiplayerAvailability.Available;
        public List<UploadCall> Uploads { get; } = new();

        public Task<LeaderboardOperationResult> UploadScoreAsync(
            LeaderboardDefinition leaderboard,
            int score,
            bool keepBest,
            IReadOnlyList<int>? details = null,
            CancellationToken cancellationToken = default)
        {
            Uploads.Add(new UploadCall(leaderboard, score, keepBest));
            return Task.FromResult(LeaderboardOperationResult.Succeeded);
        }

        public Task<LeaderboardEntriesResult> DownloadFriendsAsync(
            LeaderboardDefinition leaderboard,
            CancellationToken cancellationToken = default)
            => Task.FromResult(LeaderboardEntriesResult.Succeeded(Array.Empty<LeaderboardEntry>()));
    }
}
