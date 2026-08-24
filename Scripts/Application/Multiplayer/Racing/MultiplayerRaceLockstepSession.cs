using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Voidling.Application.Ports.Multiplayer;
using Voidling.Domain.Racing;

namespace Voidling.Application.Multiplayer.Racing;

public enum MultiplayerRaceCommandKind
{
    Cheer
}

public sealed record ScheduledRaceCommand(
    string ChallengeId,
    long Tick,
    long Sequence,
    PlatformUserId OwnerId,
    string ParticipantId,
    MultiplayerRaceCommandKind Kind);

public enum RaceCommandScheduleResult
{
    Scheduled,
    Duplicate,
    TooLate,
    Invalid
}

public sealed record RaceCommandApplication(
    ScheduledRaceCommand Command,
    bool Applied);

public sealed record RaceLockstepAdvanceResult(
    IReadOnlyList<RaceSimulationEvent> SimulationEvents,
    IReadOnlyList<RaceCommandApplication> CommandApplications);

/// <summary>
/// Advances the existing deterministic RaceSimulation one fixed tick at a time and applies all
/// host-scheduled result-affecting commands immediately before their canonical tick. There is no
/// rollback: a command that arrives after its scheduled tick is rejected and surfaced as a sync issue.
/// </summary>
public sealed class MultiplayerRaceLockstepSession
{
    public const int DefaultInputDelayTicks = 12; // 200 ms at 60 Hz; intentionally tuneable later.
    public const int MaxFutureCommandTicks = 600; // Guard against malformed 10+ second schedules.

    private readonly string _challengeId;
    private readonly RaceSimulation _simulation;
    private readonly Dictionary<PlatformUserId, string> _participantByOwner;
    private readonly SortedDictionary<long, List<ScheduledRaceCommand>> _scheduled = new();
    private readonly HashSet<(ulong Owner, long Sequence)> _knownSequences = new();

    public MultiplayerRaceLockstepSession(ResolvedMultiplayerRace race)
    {
        ArgumentNullException.ThrowIfNull(race);
        _challengeId = race.Start.ChallengeId;
        _participantByOwner = race.Start.Entrants.ToDictionary(
            entrant => entrant.OwnerId,
            entrant => entrant.Participant.CreatureId);
        _simulation = new RaceSimulation(
            race.Course,
            race.Entry.Rules,
            race.Entry.Entrants.Select(entrant => entrant.Participant).ToArray(),
            race.Entry.SimulationSeed);
    }

    public string ChallengeId => _challengeId;
    public long CurrentTick { get; private set; }
    public RaceSimulation Simulation => _simulation;
    public bool IsComplete => _simulation.IsComplete;

    public RaceCommandScheduleResult Schedule(ScheduledRaceCommand command)
    {
        if (!IsValidCommand(command))
            return RaceCommandScheduleResult.Invalid;
        if (command.Tick < CurrentTick)
            return RaceCommandScheduleResult.TooLate;
        if (command.Tick > CurrentTick + MaxFutureCommandTicks)
            return RaceCommandScheduleResult.Invalid;

        var sequenceKey = (command.OwnerId.Value, command.Sequence);
        if (!_knownSequences.Add(sequenceKey))
            return RaceCommandScheduleResult.Duplicate;

        if (!_scheduled.TryGetValue(command.Tick, out var commands))
        {
            commands = new List<ScheduledRaceCommand>();
            _scheduled.Add(command.Tick, commands);
        }
        commands.Add(command);
        return RaceCommandScheduleResult.Scheduled;
    }

    public RaceLockstepAdvanceResult AdvanceFixedSteps(int stepCount)
    {
        if (stepCount <= 0 || IsComplete)
            return new RaceLockstepAdvanceResult(
                Array.Empty<RaceSimulationEvent>(),
                Array.Empty<RaceCommandApplication>());

        var simulationEvents = new List<RaceSimulationEvent>();
        var commandApplications = new List<RaceCommandApplication>();

        for (var i = 0; i < stepCount && !IsComplete; i++)
        {
            ApplyCommandsAtCurrentTick(commandApplications);
            simulationEvents.AddRange(_simulation.AdvanceFixedSteps(1));
            CurrentTick++;
        }

        return new RaceLockstepAdvanceResult(
            simulationEvents.AsReadOnly(),
            commandApplications.AsReadOnly());
    }

    public string ComputeDeterministicChecksum()
        => RaceDeterministicChecksum.Compute(
            _challengeId,
            CurrentTick,
            _simulation.GetDeterministicStateSnapshot());

    public bool TryGetParticipantId(PlatformUserId ownerId, out string participantId)
        => _participantByOwner.TryGetValue(ownerId, out participantId!);

    private void ApplyCommandsAtCurrentTick(List<RaceCommandApplication> applications)
    {
        if (!_scheduled.Remove(CurrentTick, out var commands))
            return;

        foreach (var command in commands
                     .OrderBy(value => value.OwnerId.Value)
                     .ThenBy(value => value.Sequence))
        {
            var applied = command.Kind switch
            {
                MultiplayerRaceCommandKind.Cheer => _simulation.TryCheer(command.ParticipantId),
                _ => false
            };
            applications.Add(new RaceCommandApplication(command, applied));
        }
    }

    private bool IsValidCommand(ScheduledRaceCommand? command)
    {
        if (command == null ||
            !string.Equals(command.ChallengeId, _challengeId, StringComparison.Ordinal) ||
            command.Tick < 0 ||
            command.Sequence <= 0 ||
            command.OwnerId.Value == 0 ||
            string.IsNullOrWhiteSpace(command.ParticipantId) ||
            command.ParticipantId.Length > 160 ||
            !Enum.IsDefined(command.Kind) ||
            !_participantByOwner.TryGetValue(command.OwnerId, out var expectedParticipant))
        {
            return false;
        }

        return string.Equals(expectedParticipant, command.ParticipantId, StringComparison.Ordinal);
    }
}

public static class RaceDeterministicChecksum
{
    public static string Compute(
        string challengeId,
        long tick,
        RaceDeterministicStateSnapshot state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(challengeId);
        ArgumentNullException.ThrowIfNull(state);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendString(hash, "voidling:race-checksum:v1");
        AppendString(hash, challengeId);
        AppendInt64(hash, tick);

        var participants = state.Participants
            .OrderBy(value => value.ParticipantId, StringComparer.Ordinal)
            .ToArray();
        AppendInt32(hash, participants.Length);
        foreach (var participant in participants)
        {
            AppendString(hash, participant.ParticipantId);
            AppendSingle(hash, participant.X);
            AppendSingle(hash, participant.MaxStamina);
            AppendSingle(hash, participant.CurrentStamina);
            AppendSingle(hash, participant.DelaySeconds);
            AppendSingle(hash, participant.CheerSeconds);
            AppendBool(hash, participant.GlideResolved);
            AppendBool(hash, participant.GlideFailed);
            AppendSingle(hash, participant.GlideEndX);
            AppendBool(hash, participant.Finished);
            AppendInt32(hash, participant.NextObstacleIndex);
            AppendInt32(hash, participant.RandomDrawCount);
        }

        AppendInt32(hash, state.FinishOrder.Count);
        foreach (var participantId in state.FinishOrder)
            AppendString(hash, participantId);

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static void AppendString(IncrementalHash hash, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        AppendInt32(hash, bytes.Length);
        hash.AppendData(bytes);
    }

    private static void AppendInt64(IncrementalHash hash, long value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64LittleEndian(bytes, value);
        hash.AppendData(bytes);
    }

    private static void AppendInt32(IncrementalHash hash, int value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        hash.AppendData(bytes);
    }

    private static void AppendSingle(IncrementalHash hash, float value)
        => AppendInt32(hash, BitConverter.SingleToInt32Bits(value));

    private static void AppendBool(IncrementalHash hash, bool value)
    {
        Span<byte> bytes = stackalloc byte[1];
        bytes[0] = value ? (byte)1 : (byte)0;
        hash.AppendData(bytes);
    }
}
