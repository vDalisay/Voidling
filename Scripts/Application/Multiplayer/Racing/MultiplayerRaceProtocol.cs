using System;
using System.Linq;
using Voidling.Application.Multiplayer.Challenges;
using Voidling.Application.Ports.Multiplayer;

namespace Voidling.Application.Multiplayer.Racing;

public static class MultiplayerRaceProtocol
{
    public const string SelectionCommandType = "race.selection.command";
    public const string StartRequestType = "race.start.request";
    public const string StartProposalType = "race.start.proposal";
    public const string StartAckType = "race.start.ack";

    private sealed record SelectionPayload(
        string ChallengeId,
        MultiplayerRaceEntrant Entrant);

    private sealed record StartRequestPayload(string ChallengeId);

    private sealed record StartProposalPayload(
        string ChallengeId,
        string StartHash,
        byte[] StartPayload);

    private sealed record StartAckPayload(
        string ChallengeId,
        string StartHash,
        bool Success,
        string? Error);

    public static byte[] EncodeSelection(
        PlatformUser sender,
        string challengeId,
        MultiplayerRaceEntrant entrant)
        => MultiplayerProtocol.EncodeMessage(
            SelectionCommandType,
            sender,
            new SelectionPayload(challengeId, entrant));

    public static bool TryDecodeSelection(
        ReadOnlySpan<byte> bytes,
        PlatformUserId transportSender,
        out Guid messageId,
        out string challengeId,
        out MultiplayerRaceEntrant entrant)
    {
        challengeId = string.Empty;
        entrant = default!;
        if (!MultiplayerProtocol.TryDecodeMessage(
                bytes,
                transportSender,
                SelectionCommandType,
                out messageId,
                out SelectionPayload? payload) ||
            payload == null ||
            !ChallengeValidation.IsValidChallengeId(payload.ChallengeId) ||
            !MultiplayerRaceValidation.IsValidEntrant(payload.Entrant, out _))
        {
            return false;
        }

        challengeId = payload.ChallengeId;
        entrant = payload.Entrant;
        return true;
    }

    public static byte[] EncodeStartRequest(PlatformUser sender, string challengeId)
        => MultiplayerProtocol.EncodeMessage(
            StartRequestType,
            sender,
            new StartRequestPayload(challengeId));

    public static bool TryDecodeStartRequest(
        ReadOnlySpan<byte> bytes,
        PlatformUserId transportSender,
        out Guid messageId,
        out string challengeId)
    {
        challengeId = string.Empty;
        if (!MultiplayerProtocol.TryDecodeMessage(
                bytes,
                transportSender,
                StartRequestType,
                out messageId,
                out StartRequestPayload? payload) ||
            payload == null ||
            !ChallengeValidation.IsValidChallengeId(payload.ChallengeId))
        {
            return false;
        }

        challengeId = payload.ChallengeId;
        return true;
    }

    public static byte[] EncodeStartProposal(
        PlatformUser sender,
        string challengeId,
        string startHash,
        byte[] startPayload)
        => MultiplayerProtocol.EncodeMessage(
            StartProposalType,
            sender,
            new StartProposalPayload(
                challengeId,
                startHash,
                startPayload ?? Array.Empty<byte>()));

    public static bool TryDecodeStartProposal(
        ReadOnlySpan<byte> bytes,
        PlatformUserId transportSender,
        out Guid messageId,
        out string challengeId,
        out string startHash,
        out byte[] startPayload)
    {
        challengeId = string.Empty;
        startHash = string.Empty;
        startPayload = Array.Empty<byte>();
        if (!MultiplayerProtocol.TryDecodeMessage(
                bytes,
                transportSender,
                StartProposalType,
                out messageId,
                out StartProposalPayload? payload) ||
            payload == null ||
            !ChallengeValidation.IsValidChallengeId(payload.ChallengeId) ||
            !IsSha256(payload.StartHash) ||
            payload.StartPayload == null ||
            payload.StartPayload.Length == 0 ||
            payload.StartPayload.Length > ChallengeValidation.MaxStartPayloadBytes)
        {
            return false;
        }

        challengeId = payload.ChallengeId;
        startHash = payload.StartHash;
        startPayload = payload.StartPayload;
        return true;
    }

    public static byte[] EncodeStartAck(
        PlatformUser sender,
        string challengeId,
        string startHash,
        bool success,
        string? error)
        => MultiplayerProtocol.EncodeMessage(
            StartAckType,
            sender,
            new StartAckPayload(challengeId, startHash, success, NormalizeError(error)));

    public static bool TryDecodeStartAck(
        ReadOnlySpan<byte> bytes,
        PlatformUserId transportSender,
        out Guid messageId,
        out string challengeId,
        out string startHash,
        out bool success,
        out string? error)
    {
        challengeId = string.Empty;
        startHash = string.Empty;
        success = false;
        error = null;
        if (!MultiplayerProtocol.TryDecodeMessage(
                bytes,
                transportSender,
                StartAckType,
                out messageId,
                out StartAckPayload? payload) ||
            payload == null ||
            !ChallengeValidation.IsValidChallengeId(payload.ChallengeId) ||
            !IsSha256(payload.StartHash) ||
            (payload.Error?.Length ?? 0) > 256)
        {
            return false;
        }

        challengeId = payload.ChallengeId;
        startHash = payload.StartHash;
        success = payload.Success;
        error = payload.Error;
        return true;
    }

    private static bool IsSha256(string value)
        => value is { Length: 64 } && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static string? NormalizeError(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
            return null;
        var trimmed = error.Trim();
        return trimmed.Length <= 256 ? trimmed : trimmed[..256];
    }
}
