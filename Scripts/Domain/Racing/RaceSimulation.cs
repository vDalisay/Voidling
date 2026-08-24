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
    int Placement) : RaceSimulationEvent(ParticipantId);

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
    bool Finished,
    RaceTerrain Terrain);

/// <summary>
/// Deterministic fixed-step race simulation for the current demo course. It owns every state
/// transition that can affect race results; Godot frame rate, sprites, camera, VFX, and animation
/// cannot consume its random stream or alter finish order.
/// </summary>
public sealed class RaceSimulation
{
    public const double FixedStepSeconds = 1.0 / 60.0;
    private const float GlideFailureTolerance = 1.0f;
    private const float ObstacleTriggerLead = 14.0f;

    private readonly RaceCourse _course;
    private readonly RacePerformanceModel _performance;
    private readonly List<ParticipantState> _participants;
    private readonly Dictionary<string, ParticipantState> _participantsById;
    private readonly List<string> _finishOrder = new();
    private readonly IReadOnlyList<string> _finishOrderView;
    private double _accumulator;

    private sealed class ParticipantState
    {
        public RaceParticipantSnapshot Participant { get; init; } = null!;
        public Random Random { get; init; } = null!;
        public float X { get; set; }
        public int NextObstacleIndex { get; set; }
        public float DelaySeconds { get; set; }
        public float CheerSeconds { get; set; }
        public float MaxStamina { get; init; }
        public float CurrentStamina { get; set; }
        public bool GlideResolved { get; set; }
        public bool GlideFailed { get; set; }
        public float GlideEndX { get; set; }
        public bool Finished { get; set; }
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
                GlideEndX = _course.GlideSegment.EndX
            };
            _participants.Add(state);
            _participantsById.Add(participant.CreatureId, state);
        }

        _finishOrderView = _finishOrder.AsReadOnly();
    }

    public IReadOnlyList<string> FinishOrder => _finishOrderView;
    public bool IsComplete => _finishOrder.Count == _participants.Count;
    public int ParticipantCount => _participants.Count;

    public RaceParticipantStateSnapshot GetState(string participantId)
    {
        if (!_participantsById.TryGetValue(participantId, out var state))
            throw new KeyNotFoundException($"Unknown race participant '{participantId}'.");
        return Snapshot(state);
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
        _finishOrder.Add(participantId);
        return new RaceParticipantFinishedEvent(participantId, _finishOrder.Count);
    }

    public bool TryCheer(string participantId)
    {
        if (!_participantsById.TryGetValue(participantId, out var state) || state.Finished)
            return false;
        if (state.CheerSeconds > 0.0f || state.CurrentStamina < _performance.CheerCost)
            return false;

        state.CurrentStamina -= _performance.CheerCost;
        state.CheerSeconds = _performance.CheerDurationSeconds;
        return true;
    }

    private void Step(float step, List<RaceSimulationEvent> events)
    {
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

            state.CurrentStamina = Math.Max(
                0.0f,
                state.CurrentStamina - movement.StaminaDrainPerSecond * step);
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

        var avoided = _performance.AvoidsObstacle(state.Participant, state.Random.NextDouble());
        if (!avoided)
        {
            state.DelaySeconds = _performance.GetObstacleDelaySeconds(state.Participant);
            state.X -= _performance.ObstacleRollbackDistance;
        }

        state.NextObstacleIndex++;
        events.Add(new RaceObstacleResolvedEvent(state.Participant.CreatureId, obstacleIndex, avoided));
    }

    private void ResolveFinish(ParticipantState state, List<RaceSimulationEvent> events)
    {
        if (state.X < _course.EndX)
            return;

        state.X = _course.EndX;
        state.Finished = true;
        _finishOrder.Add(state.Participant.CreatureId);
        events.Add(new RaceParticipantFinishedEvent(state.Participant.CreatureId, _finishOrder.Count));
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
            Finished: state.Finished,
            Terrain: _course.TerrainAt(state.X, state.GlideFailed));
}
