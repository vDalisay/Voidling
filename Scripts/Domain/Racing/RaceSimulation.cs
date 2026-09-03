using System;
using System.Collections.Generic;
using System.Linq;
using Voidling.Domain.Rules;
using Voidling.Domain.Shared;

namespace Voidling.Domain.Racing;

public abstract record RaceSimulationEvent(string ParticipantId);

public sealed record RaceObstacleResolvedEvent(
    string ParticipantId,
    int ObstacleIndex,
    bool Avoided) : RaceSimulationEvent(ParticipantId);

public sealed record RaceParticipantFinishedEvent(
    string ParticipantId,
    int Placement,
    int FixedStep = 0) : RaceSimulationEvent(ParticipantId);

public readonly record struct RaceParticipantStateSnapshot(
    RaceParticipantSnapshot Participant,
    float X,
    float MaxStamina,
    float CurrentStamina,
    float DelaySeconds,
    float CheerSeconds,
    bool GlideResolved,
    bool GlideFailed,
    float GlideEndX,
    int NextObstacleIndex,
    bool Finished,
    RaceTerrain Terrain);

/// <summary>
/// Result-affecting state exposed strictly for deterministic diagnostics/replay verification. The
/// random generator itself stays encapsulated; RandomDrawCount plus the immutable race seed and
/// participant identity identifies the same deterministic point in that participant's RNG stream.
/// </summary>
public readonly record struct RaceParticipantDeterministicState(
    string ParticipantId,
    float X,
    float MaxStamina,
    float CurrentStamina,
    float DelaySeconds,
    float CheerSeconds,
    bool GlideResolved,
    bool GlideFailed,
    float GlideEndX,
    bool Finished,
    int NextObstacleIndex,
    int RandomDrawCount);

public sealed record RaceDeterministicStateSnapshot(
    IReadOnlyList<RaceParticipantDeterministicState> Participants,
    IReadOnlyList<string> FinishOrder);

/// <summary>
/// Non-authoritative balancing diagnostics accumulated while the deterministic simulation runs.
/// These values are never consulted by movement, RNG, finish order, replay checksums, or rewards.
/// </summary>
public readonly record struct RaceParticipantTelemetrySnapshot(
    string ParticipantId,
    float MaxObservedSpeed,
    float MinimumObservedStamina,
    int ObstacleAvoids,
    int ObstacleFailures,
    int CheerActivations,
    int FinishFixedStep,
    int Placement);

public sealed record RaceSimulationTelemetrySnapshot(
    int FixedStepCount,
    IReadOnlyList<RaceParticipantTelemetrySnapshot> Participants);

/// <summary>
/// Deterministic fixed-step race simulation for the current demo course. It owns every state
/// transition that can affect race results; Godot frame rate, sprites, camera, VFX, and animation
/// cannot consume its random stream or alter finish order.
/// </summary>
public sealed class RaceSimulation
{
    public const double FixedStepSeconds = 1.0 / 60.0;
    private const float GlideFailureTolerance = 1.0f;

    // RaceScreen intentionally places the visible hurdle 18px ahead of the course marker. Keep the
    // obstacle event 14px before that visible center so the jump animation peaks at the hurdle,
    // rather than resolving ~32px before the sprite as it did previously.
    private const float ObstacleTriggerLead = -4.0f;

    private readonly RaceCourse _course;
    private readonly RacePerformanceModel _performance;
    private readonly List<ParticipantState> _participants;
    private readonly Dictionary<string, ParticipantState> _participantsById;
    private readonly List<string> _finishOrder = new();
    private readonly IReadOnlyList<string> _finishOrderView;
    private double _accumulator;
    private int _fixedStepCount;

    private sealed class ParticipantState
    {
        public RaceParticipantSnapshot Participant { get; init; } = null!;
        public Random Random { get; init; } = null!;
        public float X { get; set; }
        public int NextObstacleIndex { get; set; }
        public int RandomDrawCount { get; set; }
        public bool ObstacleRetryPending { get; set; }
        public float DelaySeconds { get; set; }
        public float CheerSeconds { get; set; }
        public float MaxStamina { get; init; }
        public float CurrentStamina { get; set; }
        public bool GlideResolved { get; set; }
        public bool GlideFailed { get; set; }
        public float GlideEndX { get; set; }
        public bool Finished { get; set; }
        public int FinishFixedStep { get; set; }

        // Diagnostic-only counters. No authoritative code reads these fields.
        public float MaxObservedSpeed { get; set; }
        public float MinimumObservedStamina { get; set; }
        public int ObstacleAvoids { get; set; }
        public int ObstacleFailures { get; set; }
        public int CheerActivations { get; set; }
    }

    public RaceSimulation(
        RaceCourse course,
        RaceRules rules,
        IReadOnlyList<RaceParticipantSnapshot> participants,
        ulong seed)
    {
        _course = course ?? throw new ArgumentNullException(nameof(course));
        _performance = new RacePerformanceModel(rules ?? throw new ArgumentNullException(nameof(rules)));
        ArgumentNullException.ThrowIfNull(participants);
        if (participants.Count == 0)
            throw new ArgumentException("A race requires at least one participant.", nameof(participants));
        if (participants.Select(p => p.CreatureId).Distinct(StringComparer.Ordinal).Count() != participants.Count)
            throw new ArgumentException("Race participant IDs must be unique.", nameof(participants));

        _participants = new List<ParticipantState>(participants.Count);
        _participantsById = new Dictionary<string, ParticipantState>(participants.Count, StringComparer.Ordinal);
        for (var i = 0; i < participants.Count; i++)
        {
            var participant = participants[i];
            var maxStamina = _performance.GetMaxStamina(participant);
            var state = new ParticipantState
            {
                Participant = participant,
                Random = StableRandom.Create(seed, $"race:{participant.CreatureId}:{i}"),
                X = _course.StartX,
                MaxStamina = maxStamina,
                CurrentStamina = maxStamina,
                MinimumObservedStamina = maxStamina,
                GlideEndX = _course.HasGlideSegment ? _course.GlideSegment.EndX : _course.EndX
            };
            _participants.Add(state);
            _participantsById.Add(participant.CreatureId, state);
        }

        _finishOrderView = _finishOrder.AsReadOnly();
    }

    public IReadOnlyList<string> FinishOrder => _finishOrderView;
    public bool IsComplete => _finishOrder.Count == _participants.Count;
    public int ParticipantCount => _participants.Count;
    public int FixedStepCount => _fixedStepCount;

    public RaceParticipantStateSnapshot GetState(string participantId)
    {
        if (!_participantsById.TryGetValue(participantId, out var state))
            throw new KeyNotFoundException($"Unknown race participant '{participantId}'.");
        return Snapshot(state);
    }

    public bool TryGetFinishFixedStep(string participantId, out int fixedStep)
    {
        fixedStep = 0;
        if (!_participantsById.TryGetValue(participantId, out var state) ||
            !state.Finished ||
            state.FinishFixedStep <= 0)
        {
            return false;
        }

        fixedStep = state.FinishFixedStep;
        return true;
    }

    public RaceDeterministicStateSnapshot GetDeterministicStateSnapshot()
    {
        var participants = _participants
            .Select(state => new RaceParticipantDeterministicState(
                ParticipantId: state.Participant.CreatureId,
                X: state.X,
                MaxStamina: state.MaxStamina,
                CurrentStamina: state.CurrentStamina,
                DelaySeconds: state.DelaySeconds,
                CheerSeconds: state.CheerSeconds,
                GlideResolved: state.GlideResolved,
                GlideFailed: state.GlideFailed,
                GlideEndX: state.GlideEndX,
                Finished: state.Finished,
                NextObstacleIndex: state.NextObstacleIndex,
                RandomDrawCount: state.RandomDrawCount))
            .ToArray();

        return new RaceDeterministicStateSnapshot(
            Array.AsReadOnly(participants),
            Array.AsReadOnly(_finishOrder.ToArray()));
    }

    public RaceSimulationTelemetrySnapshot GetTelemetrySnapshot()
    {
        var participants = _participants
            .Select(state => new RaceParticipantTelemetrySnapshot(
                ParticipantId: state.Participant.CreatureId,
                MaxObservedSpeed: state.MaxObservedSpeed,
                MinimumObservedStamina: state.MinimumObservedStamina,
                ObstacleAvoids: state.ObstacleAvoids,
                ObstacleFailures: state.ObstacleFailures,
                CheerActivations: state.CheerActivations,
                FinishFixedStep: state.FinishFixedStep,
                Placement: state.Finished
                    ? _finishOrder.IndexOf(state.Participant.CreatureId) + 1
                    : 0))
            .ToArray();

        return new RaceSimulationTelemetrySnapshot(
            _fixedStepCount,
            Array.AsReadOnly(participants));
    }

    public IReadOnlyList<RaceSimulationEvent> Advance(double elapsedSeconds)
    {
        if (elapsedSeconds <= 0.0 || IsComplete)
            return Array.Empty<RaceSimulationEvent>();

        _accumulator += elapsedSeconds;
        var events = new List<RaceSimulationEvent>();
        while (_accumulator + 1e-12 >= FixedStepSeconds && !IsComplete)
        {
            Step((float)FixedStepSeconds, events);
            _accumulator -= FixedStepSeconds;
        }

        return events;
    }

    public IReadOnlyList<RaceSimulationEvent> AdvanceFixedSteps(int stepCount)
    {
        if (stepCount <= 0 || IsComplete)
            return Array.Empty<RaceSimulationEvent>();

        var events = new List<RaceSimulationEvent>();
        for (var i = 0; i < stepCount && !IsComplete; i++)
            Step((float)FixedStepSeconds, events);
        return events;
    }

    public IReadOnlyList<RaceSimulationEvent> FastForwardToFinish(int maxSteps = 120000)
    {
        if (maxSteps <= 0 || IsComplete)
            return Array.Empty<RaceSimulationEvent>();

        var events = new List<RaceSimulationEvent>();
        var steps = 0;
        while (!IsComplete && steps++ < maxSteps)
            Step((float)FixedStepSeconds, events);

        if (!IsComplete)
            throw new InvalidOperationException("Race simulation did not finish within the fast-forward guard.");

        return events;
    }

    public RaceParticipantFinishedEvent? CompleteParticipantAsLast(string participantId)
    {
        if (!_participantsById.TryGetValue(participantId, out var state) || state.Finished)
            return null;

        state.X = _course.EndX;
        state.Finished = true;
        state.FinishFixedStep = Math.Max(1, _fixedStepCount);
        _finishOrder.Add(participantId);
        return new RaceParticipantFinishedEvent(
            participantId,
            _finishOrder.Count,
            state.FinishFixedStep);
    }

    public bool TryCheer(string participantId)
    {
        if (!_participantsById.TryGetValue(participantId, out var state) || state.Finished)
            return false;
        if (state.CheerSeconds > 0.0f || state.CurrentStamina < _performance.CheerCost)
            return false;

        state.CurrentStamina -= _performance.CheerCost;
        state.MinimumObservedStamina = Math.Min(state.MinimumObservedStamina, state.CurrentStamina);
        state.CheerSeconds = _performance.CheerDurationSeconds;
        state.CheerActivations++;
        return true;
    }

    private void Step(float step, List<RaceSimulationEvent> events)
    {
        _fixedStepCount++;

        foreach (var state in _participants)
        {
            if (state.Finished)
                continue;

            state.CheerSeconds = Math.Max(0.0f, state.CheerSeconds - step);

            if (state.DelaySeconds > 0.0f)
            {
                state.DelaySeconds = Math.Max(0.0f, state.DelaySeconds - step);
                state.CurrentStamina = Math.Max(
                    0.0f,
                    state.CurrentStamina - _performance.GetDelayStaminaDrainPerSecond() * step);
                state.MinimumObservedStamina = Math.Min(state.MinimumObservedStamina, state.CurrentStamina);
                continue;
            }

            ResolveGlideState(state);
            var terrain = _course.TerrainAt(state.X, state.GlideFailed);
            var movement = _performance.GetMovement(
                state.Participant,
                terrain,
                state.CurrentStamina,
                state.MaxStamina,
                state.CheerSeconds > 0.0f);
            state.MaxObservedSpeed = Math.Max(state.MaxObservedSpeed, movement.Speed);

            state.CurrentStamina = Math.Max(
                0.0f,
                state.CurrentStamina - movement.StaminaDrainPerSecond * step);
            state.MinimumObservedStamina = Math.Min(state.MinimumObservedStamina, state.CurrentStamina);
            state.X += movement.Speed * step;

            ResolvePendingObstacle(state, events);
            ResolveFinish(state, events);
        }
    }

    private void ResolveGlideState(ParticipantState state)
    {
        var glide = _course.GlideSegment;
        if (glide.Kind != RaceSegmentKind.Glide)
            return;

        if (glide.Contains(state.X))
        {
            if (!state.GlideResolved)
            {
                state.GlideResolved = true;
                state.GlideFailed = false;
                var sectionWidth = glide.EndX - glide.StartX;
                var glideDistance = _performance.GetGlideDistance(state.Participant);
                state.GlideEndX = Math.Min(
                    glide.EndX,
                    glide.StartX + Math.Min(sectionWidth, glideDistance));
            }

            if (!state.GlideFailed &&
                state.X >= state.GlideEndX &&
                state.GlideEndX < glide.EndX - GlideFailureTolerance)
            {
                state.GlideFailed = true;
            }
            return;
        }

        if (state.X >= glide.EndX)
        {
            state.GlideResolved = false;
            state.GlideFailed = false;
            state.GlideEndX = glide.EndX;
        }
    }

    private void ResolvePendingObstacle(ParticipantState state, List<RaceSimulationEvent> events)
    {
        if (state.NextObstacleIndex >= _course.Obstacles.Count)
            return;

        var obstacleIndex = state.NextObstacleIndex;
        if (state.X < _course.Obstacles[obstacleIndex] - ObstacleTriggerLead)
            return;

        // A failed hurdle attempt is the stumble/jump-in-place beat. Do not consume the
        // hurdle at that point: after the delay, the racer approaches the same fence again
        // and the retry is guaranteed to clear it so the sprite can never walk through it.
        var avoided = state.ObstacleRetryPending;
        if (state.ObstacleRetryPending)
        {
            state.ObstacleRetryPending = false;
        }
        else
        {
            var randomRoll = state.Random.NextDouble();
            state.RandomDrawCount++;
            avoided = _performance.AvoidsObstacle(state.Participant, randomRoll);
        }

        if (!avoided)
        {
            state.ObstacleFailures++;
            state.DelaySeconds = _performance.GetObstacleDelaySeconds(state.Participant);
            state.X -= _performance.ObstacleRollbackDistance;
            state.ObstacleRetryPending = true;
            events.Add(new RaceObstacleResolvedEvent(state.Participant.CreatureId, obstacleIndex, false));
            return;
        }

        state.ObstacleAvoids++;
        state.NextObstacleIndex++;
        events.Add(new RaceObstacleResolvedEvent(state.Participant.CreatureId, obstacleIndex, true));
    }

    private void ResolveFinish(ParticipantState state, List<RaceSimulationEvent> events)
    {
        if (state.X < _course.EndX)
            return;

        state.X = _course.EndX;
        state.Finished = true;
        state.FinishFixedStep = _fixedStepCount;
        _finishOrder.Add(state.Participant.CreatureId);
        events.Add(new RaceParticipantFinishedEvent(
            state.Participant.CreatureId,
            _finishOrder.Count,
            state.FinishFixedStep));
    }

    private RaceParticipantStateSnapshot Snapshot(ParticipantState state)
        => new(
            Participant: state.Participant,
            X: state.X,
            MaxStamina: state.MaxStamina,
            CurrentStamina: state.CurrentStamina,
            DelaySeconds: state.DelaySeconds,
            CheerSeconds: state.CheerSeconds,
            GlideResolved: state.GlideResolved,
            GlideFailed: state.GlideFailed,
            GlideEndX: state.GlideEndX,
            NextObstacleIndex: state.NextObstacleIndex,
            Finished: state.Finished,
            Terrain: _course.TerrainAt(state.X, state.GlideFailed));
}
