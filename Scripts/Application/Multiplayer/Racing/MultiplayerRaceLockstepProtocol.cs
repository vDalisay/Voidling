using System;
using System.Linq;
using Voidling.Application.Multiplayer.Challenges;
using Voidling.Application.Ports.Multiplayer;

namespace Voidling.Application.Multiplayer.Racing;

public static class MultiplayerRaceLockstepProtocol
{
    // Keep lockstep traffic outside the generic race.* start-handshake namespace. Both coordinators
    // observe NetworkChannel.Challenge, so a distinct prefix prevents valid lockstep packets from
    // being misreported as malformed race-start traffic by the start coordinator.
    public const string CheerRequestType = "lockstep.race.cheer.request";
    public const string ScheduledCommandType = "lockstep.race.command.scheduled";
    public const string ChecksumType = "lockstep.race.checksum";

    private sealed record CheerRequestPayload(string ChallengeId, long InputSequence);
    private sealed record ScheduledCommandPayload(ScheduledRaceCommand Command);
    private sealed record ChecksumPayload(string ChallengeId, long Tick, string Checksum);

    public static byte[] EncodeCheerRequest(
        PlatformUser sender,
        string challengeId,
        long inputSequence)
        => MultiplayerProtocol.EncodeMessage(
            CheerRequestType,
            sender,
            new CheerRequestPayload(challengeId, inputSequence));

    public static bool TryDecodeCheerRequest(
        ReadOnlySpan<byte> bytes,
        PlatformUserId transportSender,
        out Guid messageId,
        out string challengeId,
        out long inputSequence)
    {
        challengeId = string.Empty;
        inputSequence = 0;
        if (!MultiplayerProtocol.TryDecodeMessage(
                bytes,
                transportSender,
                CheerRequestType,
                out messageId,
                out CheerRequestPayload? payload) ||
            payload == null ||
            !ChallengeValidation.IsValidChallengeId(payload.ChallengeId) ||
            payload.InputSequence <= 0)
        {
            return false;
        }

        challengeId = payload.ChallengeId;
        inputSequence = payload.InputSequence;
        return true;
    }

    public static byte[] EncodeScheduledCommand(
        PlatformUser sender,
        ScheduledRaceCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return MultiplayerProtocol.EncodeMessage(
            ScheduledCommandType,
            sender,
            new ScheduledCommandPayload(command));
    }

    public static bool TryDecodeScheduledCommand(
        ReadOnlySpan<byte> bytes,
        PlatformUserId transportSender,
        out Guid messageId,
        out ScheduledRaceCommand command)
    {
        command = default!;
        if (!MultiplayerProtocol.TryDecodeMessage(
                bytes,
                transportSender,
                ScheduledCommandType,
                out messageId,
                out ScheduledCommandPayload? payload) ||
            payload?.Command == null ||
            !IsStructurallyValid(payload.Command))
        {
            return false;
        }

        command = payload.Command;
        return true;
    }

    public static byte[] EncodeChecksum(
        PlatformUser sender,
        string challengeId,
        long tick,
        string checksum)
        => MultiplayerProtocol.EncodeMessage(
            ChecksumType,
            sender,
            new ChecksumPayload(challengeId, tick, checksum));

    public static bool TryDecodeChecksum(
        ReadOnlySpan<byte> bytes,
        PlatformUserId transportSender,
        out Guid messageId,
        out string challengeId,
        out long tick,
        out string checksum)
    {
        challengeId = string.Empty;
        tick = 0;
        checksum = string.Empty;
        if (!MultiplayerProtocol.TryDecodeMessage(
                bytes,
                transportSender,
                ChecksumType,
                out messageId,
                out ChecksumPayload? payload) ||
            payload == null ||
            !ChallengeValidation.IsValidChallengeId(payload.ChallengeId) ||
            payload.Tick < 0 ||
            !IsSha256(payload.Checksum))
        {
            return false;
        }

        challengeId = payload.ChallengeId;
        tick = payload.Tick;
        checksum = payload.Checksum;
        return true;
    }

    private static bool IsStructurallyValid(ScheduledRaceCommand command)
        => ChallengeValidation.IsValidChallengeId(command.ChallengeId) &&
           command.Tick >= 0 &&
           command.Sequence > 0 &&
           command.OwnerId.Value > 0 &&
           !string.IsNullOrWhiteSpace(command.ParticipantId) &&
           command.ParticipantId.Length <= 160 &&
           Enum.IsDefined(command.Kind);

    private static bool IsSha256(string value)
        => value is { Length: 64 } && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');
}