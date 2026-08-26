using System;
using Voidling.Application.Multiplayer.Challenges;
using Voidling.Application.Ports.Multiplayer;

namespace Voidling.Application.Multiplayer.Racing;

public static class MultiplayerRaceResultProtocol
{
    // Separate namespace from race.start.* and lockstep.race.* because all three share the reliable
    // Challenge transport channel but have different coordinators and lifecycle responsibilities.
    public const string FinalResultType = "result.race.final";
    public const string ResultAckType = "result.race.ack";

    private sealed record FinalResultPayload(MultiplayerRaceResult Result);
    private sealed record ResultAckPayload(string ChallengeId, bool Accepted, string? Error);

    public static byte[] EncodeFinalResult(PlatformUser sender, MultiplayerRaceResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return MultiplayerProtocol.EncodeMessage(
            FinalResultType,
            sender,
            new FinalResultPayload(result));
    }

    public static bool TryDecodeFinalResult(
        ReadOnlySpan<byte> bytes,
        PlatformUserId transportSender,
        out Guid messageId,
        out MultiplayerRaceResult result)
    {
        result = default!;
        if (!MultiplayerProtocol.TryDecodeMessage(
                bytes,
                transportSender,
                FinalResultType,
                out messageId,
                out FinalResultPayload? payload) ||
            payload?.Result == null ||
            !MultiplayerRaceResultValidation.IsValid(payload.Result, out _))
        {
            return false;
        }

        result = payload.Result;
        return true;
    }

    public static byte[] EncodeResultAck(
        PlatformUser sender,
        string challengeId,
        bool accepted,
        string? error)
        => MultiplayerProtocol.EncodeMessage(
            ResultAckType,
            sender,
            new ResultAckPayload(challengeId, accepted, NormalizeError(error)));

    public static bool TryDecodeResultAck(
        ReadOnlySpan<byte> bytes,
        PlatformUserId transportSender,
        out Guid messageId,
        out string challengeId,
        out bool accepted,
        out string? error)
    {
        challengeId = string.Empty;
        accepted = false;
        error = null;
        if (!MultiplayerProtocol.TryDecodeMessage(
                bytes,
                transportSender,
                ResultAckType,
                out messageId,
                out ResultAckPayload? payload) ||
            payload == null ||
            !ChallengeValidation.IsValidChallengeId(payload.ChallengeId) ||
            (payload.Error?.Length ?? 0) > 256)
        {
            return false;
        }

        challengeId = payload.ChallengeId;
        accepted = payload.Accepted;
        error = payload.Error;
        return true;
    }

    private static string? NormalizeError(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
            return null;
        var trimmed = error.Trim();
        return trimmed.Length <= 256 ? trimmed : trimmed[..256];
    }
}