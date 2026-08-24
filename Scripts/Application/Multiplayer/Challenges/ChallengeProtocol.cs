using System;
using System.Linq;
using Voidling.Application.Ports.Multiplayer;

namespace Voidling.Application.Multiplayer.Challenges;

public static class ChallengeProtocol
{
    public const string OfferCommandType = "challenge.offer.command";
    public const string JoinCommandType = "challenge.join.command";
    public const string LeaveCommandType = "challenge.leave.command";
    public const string StartCommandType = "challenge.start.command";
    public const string CancelCommandType = "challenge.cancel.command";
    public const string StateType = "challenge.state";
    public const string SyncRequestType = "challenge.sync.request";
    public const string SyncStateType = "challenge.sync.state";

    private sealed record OfferCommandPayload(
        string ChallengeId,
        ulong LobbyId,
        ChallengeKind Kind,
        int MaxParticipants);

    private sealed record IdPayload(string ChallengeId);
    private sealed record StartCommandPayload(string ChallengeId, byte[] StartPayload);
    private sealed record StatePayload(ChallengeSnapshot Snapshot);
    private sealed record SyncRequestPayload(ulong LobbyId);
    private sealed record SyncStatePayload(ulong LobbyId, ChallengeSnapshot[] Challenges);

    public static byte[] EncodeOfferCommand(
        PlatformUser sender,
        string challengeId,
        ulong lobbyId,
        ChallengeKind kind,
        int maxParticipants)
        => MultiplayerProtocol.EncodeMessage(
            OfferCommandType,
            sender,
            new OfferCommandPayload(challengeId, lobbyId, kind, maxParticipants));

    public static bool TryDecodeOfferCommand(
        ReadOnlySpan<byte> bytes,
        PlatformUserId transportSender,
        out Guid messageId,
        out string challengeId,
        out ulong lobbyId,
        out ChallengeKind kind,
        out int maxParticipants)
    {
        challengeId = string.Empty;
        lobbyId = 0;
        kind = default;
        maxParticipants = 0;
        if (!MultiplayerProtocol.TryDecodeMessage(
                bytes,
                transportSender,
                OfferCommandType,
                out messageId,
                out OfferCommandPayload? payload) ||
            payload == null ||
            !ChallengeValidation.IsValidChallengeId(payload.ChallengeId) ||
            payload.LobbyId == 0 ||
            !Enum.IsDefined(payload.Kind) ||
            payload.MaxParticipants is < 2 or > ChallengeValidation.MaxParticipants)
        {
            return false;
        }

        challengeId = payload.ChallengeId;
        lobbyId = payload.LobbyId;
        kind = payload.Kind;
        maxParticipants = payload.MaxParticipants;
        return true;
    }

    public static byte[] EncodeJoinCommand(PlatformUser sender, string challengeId)
        => EncodeIdCommand(JoinCommandType, sender, challengeId);

    public static bool TryDecodeJoinCommand(
        ReadOnlySpan<byte> bytes,
        PlatformUserId transportSender,
        out Guid messageId,
        out string challengeId)
        => TryDecodeIdCommand(bytes, transportSender, JoinCommandType, out messageId, out challengeId);

    public static byte[] EncodeLeaveCommand(PlatformUser sender, string challengeId)
        => EncodeIdCommand(LeaveCommandType, sender, challengeId);

    public static bool TryDecodeLeaveCommand(
        ReadOnlySpan<byte> bytes,
        PlatformUserId transportSender,
        out Guid messageId,
        out string challengeId)
        => TryDecodeIdCommand(bytes, transportSender, LeaveCommandType, out messageId, out challengeId);

    public static byte[] EncodeCancelCommand(PlatformUser sender, string challengeId)
        => EncodeIdCommand(CancelCommandType, sender, challengeId);

    public static bool TryDecodeCancelCommand(
        ReadOnlySpan<byte> bytes,
        PlatformUserId transportSender,
        out Guid messageId,
        out string challengeId)
        => TryDecodeIdCommand(bytes, transportSender, CancelCommandType, out messageId, out challengeId);

    public static byte[] EncodeStartCommand(
        PlatformUser sender,
        string challengeId,
        byte[] startPayload)
        => MultiplayerProtocol.EncodeMessage(
            StartCommandType,
            sender,
            new StartCommandPayload(challengeId, startPayload ?? Array.Empty<byte>()));

    public static bool TryDecodeStartCommand(
        ReadOnlySpan<byte> bytes,
        PlatformUserId transportSender,
        out Guid messageId,
        out string challengeId,
        out byte[] startPayload)
    {
        challengeId = string.Empty;
        startPayload = Array.Empty<byte>();
        if (!MultiplayerProtocol.TryDecodeMessage(
                bytes,
                transportSender,
                StartCommandType,
                out messageId,
                out StartCommandPayload? payload) ||
            payload == null ||
            !ChallengeValidation.IsValidChallengeId(payload.ChallengeId) ||
            payload.StartPayload == null ||
            payload.StartPayload.Length > ChallengeValidation.MaxStartPayloadBytes)
        {
            return false;
        }

        challengeId = payload.ChallengeId;
        startPayload = payload.StartPayload;
        return true;
    }

    public static byte[] EncodeState(PlatformUser sender, ChallengeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return MultiplayerProtocol.EncodeMessage(StateType, sender, new StatePayload(snapshot));
    }

    public static bool TryDecodeState(
        ReadOnlySpan<byte> bytes,
        PlatformUserId transportSender,
        out ChallengeSnapshot snapshot)
    {
        snapshot = default!;
        if (!MultiplayerProtocol.TryDecodeMessage(
                bytes,
                transportSender,
                StateType,
                out _,
                out StatePayload? payload) ||
            payload?.Snapshot == null ||
            !ChallengeValidation.IsValidSnapshot(payload.Snapshot))
        {
            return false;
        }

        snapshot = payload.Snapshot;
        return true;
    }

    public static byte[] EncodeSyncRequest(PlatformUser sender, ulong lobbyId)
        => MultiplayerProtocol.EncodeMessage(
            SyncRequestType,
            sender,
            new SyncRequestPayload(lobbyId));

    public static bool TryDecodeSyncRequest(
        ReadOnlySpan<byte> bytes,
        PlatformUserId transportSender,
        out Guid messageId,
        out ulong lobbyId)
    {
        lobbyId = 0;
        if (!MultiplayerProtocol.TryDecodeMessage(
                bytes,
                transportSender,
                SyncRequestType,
                out messageId,
                out SyncRequestPayload? payload) ||
            payload == null ||
            payload.LobbyId == 0)
        {
            return false;
        }

        lobbyId = payload.LobbyId;
        return true;
    }

    public static byte[] EncodeSyncState(
        PlatformUser sender,
        ulong lobbyId,
        ChallengeSnapshot[] challenges)
        => MultiplayerProtocol.EncodeMessage(
            SyncStateType,
            sender,
            new SyncStatePayload(lobbyId, challenges ?? Array.Empty<ChallengeSnapshot>()));

    public static bool TryDecodeSyncState(
        ReadOnlySpan<byte> bytes,
        PlatformUserId transportSender,
        out ulong lobbyId,
        out ChallengeSnapshot[] challenges)
    {
        lobbyId = 0;
        challenges = Array.Empty<ChallengeSnapshot>();
        if (!MultiplayerProtocol.TryDecodeMessage(
                bytes,
                transportSender,
                SyncStateType,
                out _,
                out SyncStatePayload? payload) ||
            payload == null ||
            payload.LobbyId == 0 ||
            payload.Challenges == null ||
            payload.Challenges.Length > ChallengeValidation.MaxChallengesPerLobby ||
            payload.Challenges.Any(snapshot =>
                !ChallengeValidation.IsValidSnapshot(snapshot) || snapshot.LobbyId != payload.LobbyId) ||
            payload.Challenges.Select(snapshot => snapshot.ChallengeId).Distinct(StringComparer.Ordinal).Count() !=
            payload.Challenges.Length)
        {
            return false;
        }

        lobbyId = payload.LobbyId;
        challenges = payload.Challenges;
        return true;
    }

    private static byte[] EncodeIdCommand(string type, PlatformUser sender, string challengeId)
        => MultiplayerProtocol.EncodeMessage(type, sender, new IdPayload(challengeId));

    private static bool TryDecodeIdCommand(
        ReadOnlySpan<byte> bytes,
        PlatformUserId transportSender,
        string type,
        out Guid messageId,
        out string challengeId)
    {
        challengeId = string.Empty;
        if (!MultiplayerProtocol.TryDecodeMessage(
                bytes,
                transportSender,
                type,
                out messageId,
                out IdPayload? payload) ||
            payload == null ||
            !ChallengeValidation.IsValidChallengeId(payload.ChallengeId))
        {
            return false;
        }

        challengeId = payload.ChallengeId;
        return true;
    }
}
