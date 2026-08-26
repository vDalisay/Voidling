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

public sealed class DailyFriendRaceCoordinatorTests
{
    private static readonly GameBalanceRules Rules = GameBalanceRules.DemoDefaults;
    private static readonly DateTimeOffset Today = new(2026, 8, 25, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void BeginPersistsBeforeReturningLaunchableEntry()
    {
        var state = StateWith(CreateAdult("runner", 100));
        var repository = new FakeRepository();
        var coordinator = CreateCoordinator(repository, new FakeLeaderboardService());

        var result = coordinator.BeginAndPersist(state, "runner", Today);

        Assert.True(result.Success, result.Error);
        Assert.NotNull(result.Entry);
        Assert.Equal(1, repository.SaveCalls);
        Assert.Single(state.DailyRaceAttempts);
        Assert.Same(state, repository.LastSavedState);
    }

    [Fact]
    public void FailedBeginPersistenceRollsBackAttemptAndDoesNotReturnLaunchableEntry()
    {
        var state = StateWith(CreateAdult("runner", 200));
        var repository = new FakeRepository { ThrowOnSave = true };
        var coordinator = CreateCoordinator(repository, new FakeLeaderboardService());

        var result = coordinator.BeginAndPersist(state, "runner", Today);

        Assert.False(result.Success);
        Assert.Null(result.Entry);
        Assert.Empty(state.DailyRaceAttempts);
        Assert.Equal(1, repository.SaveCalls);
    }

    [Fact]
    public void ResumeDoesNotRewriteAlreadyPersistedAttempt()
    {
        var state = StateWith(CreateAdult("runner", 300));
        var repository = new FakeRepository();
        var coordinator = CreateCoordinator(repository, new FakeLeaderboardService());
        var started = coordinator.BeginAndPersist(state, "runner", Today);
        repository.ResetCount();

        var resumed = coordinator.ResumeToday(state, Today.AddHours(4));

        Assert.True(started.Success, started.Error);
        Assert.True(resumed.Success, resumed.Error);
        Assert.True(resumed.AlreadyStarted);
        Assert.NotNull(resumed.Entry);
        Assert.Equal(0, repository.SaveCalls);
    }

    [Fact]
    public void FailedCompletionPersistenceRestoresStartedAttemptForRetry()
    {
        var state = StateWith(CreateAdult("runner", 400));
        var repository = new FakeRepository();
        var coordinator = CreateCoordinator(repository, new FakeLeaderboardService());
        var started = coordinator.BeginAndPersist(state, "runner", Today);
        repository.ThrowOnSave = true;

        var completed = coordinator.CompleteAndPersist(
            state,
            started.Attempt!.DailyKey,
            45_000);

        Assert.False(completed.Success);
        var attempt = Assert.Single(state.DailyRaceAttempts);
        Assert.Equal(DailyRaceAttemptState.Started, attempt.State);
        Assert.Null(attempt.FinishedMilliseconds);
    }

    [Fact]
    public async Task CompletedAttemptProjectsOnlyAfterLocalCompletionWasSaved()
    {
        var state = StateWith(CreateAdult("runner", 500));
        var repository = new FakeRepository();
        var leaderboards = new FakeLeaderboardService();
        var coordinator = CreateCoordinator(repository, leaderboards);
        var started = coordinator.BeginAndPersist(state, "runner", Today);
        repository.ResetCount();

        var completed = coordinator.CompleteAndPersist(
            state,
            started.Attempt!.DailyKey,
            42_500);
        var projected = await coordinator.ProjectCompletedAttemptAsync(completed.Attempt!);

        Assert.True(completed.Success, completed.Error);
        Assert.Equal(1, repository.SaveCalls);
        Assert.True(projected.Success, projected.Error);
        var upload = Assert.Single(leaderboards.Uploads);
        Assert.Equal("voidling_daily_2026-08-25_v1", upload.Definition.Name);
        Assert.Equal(42_500, upload.Score);
        Assert.True(upload.KeepBest);
    }

    [Fact]
    public async Task SteamProjectionFailureDoesNotAlterCompletedLocalAttempt()
    {
        var state = StateWith(CreateAdult("runner", 600));
        var repository = new FakeRepository();
        var leaderboards = new FakeLeaderboardService { FailUploads = true };
        var coordinator = CreateCoordinator(repository, leaderboards);
        var started = coordinator.BeginAndPersist(state, "runner", Today);
        var completed = coordinator.CompleteAndPersist(
            state,
            started.Attempt!.DailyKey,
            50_000);

        var projected = await coordinator.ProjectCompletedAttemptAsync(completed.Attempt!);

        Assert.False(projected.Success);
        var attempt = Assert.Single(state.DailyRaceAttempts);
        Assert.Equal(DailyRaceAttemptState.Completed, attempt.State);
        Assert.Equal(50_000, attempt.FinishedMilliseconds);
    }

    private static DailyFriendRaceCoordinator CreateCoordinator(
        IGameStateRepository repository,
        ILeaderboardService leaderboards)
        => new(
            new DailyFriendRaceService(new RaceEntryFactory(Rules)),
            repository,
            new LeaderboardProjectionService(leaderboards));

    private static GameStateData StateWith(VoidlingData creature)
    {
        var state = new GameStateData();
        state.Voidlings.Add(creature);
        return state;
    }

    private static VoidlingData CreateAdult(string id, ulong seed)
    {
        var creature = new VoidlingData
        {
            Id = id,
            Name = id,
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
        public bool ThrowOnSave { get; set; }
        public int SaveCalls { get; private set; }
        public GameStateData? LastSavedState { get; private set; }

        public GameStateData? Load() => null;

        public void Save(GameStateData state)
        {
            SaveCalls++;
            if (ThrowOnSave)
                throw new InvalidOperationException("simulated disk failure");
            LastSavedState = state;
        }

        public void ResetCount()
        {
            SaveCalls = 0;
            LastSavedState = null;
        }
    }

    private sealed record UploadCall(
        LeaderboardDefinition Definition,
        int Score,
        bool KeepBest);

    private sealed class FakeLeaderboardService : ILeaderboardService
    {
        public MultiplayerAvailability Availability => MultiplayerAvailability.Available;
        public bool FailUploads { get; set; }
        public List<UploadCall> Uploads { get; } = new();

        public Task<LeaderboardOperationResult> UploadScoreAsync(
            LeaderboardDefinition leaderboard,
            int score,
            bool keepBest,
            IReadOnlyList<int>? details = null,
            CancellationToken cancellationToken = default)
        {
            Uploads.Add(new UploadCall(leaderboard, score, keepBest));
            return Task.FromResult(FailUploads
                ? LeaderboardOperationResult.Failed("simulated Steam failure")
                : LeaderboardOperationResult.Succeeded);
        }

        public Task<LeaderboardEntriesResult> DownloadFriendsAsync(
            LeaderboardDefinition leaderboard,
            CancellationToken cancellationToken = default)
            => Task.FromResult(LeaderboardEntriesResult.Succeeded(Array.Empty<LeaderboardEntry>()));
    }
}
