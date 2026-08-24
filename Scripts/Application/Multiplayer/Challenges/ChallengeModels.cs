using System;
using System.Linq;
using Voidling.Application.Ports.Multiplayer;

namespace Voidling.Application.Multiplayer.Challenges;

public enum ChallengeKind
{
    Race,
    AutoBattle
}

public enum ChallengePhase
{
    Offered,
    Forming,
    Running,
    Completed,
    Cancelled
}

/// <summary>
/// Canonical transient state for one activity offered inside a connected Garden. It is never
/// persisted into GameStateData and does not imply that non-participants leave the Garden session.
/// </summary>
public sealed record ChallengeSnapshot(
    string ChallengeId,
    ulong LobbyId,
    ChallengeKind Kind,
    PlatformUserId CreatorId,
    int MaxParticipants,
    ChallengePhase Phase,
    PlatformUserId[] Participants,
    byte[] StartPayload)
{
    public bool Contains(PlatformUserId userId)
        => Participants?.Contains(userId) == true;
}

public sealed record ChallengeOperationResult(bool Success, string? ChallengeId, string? Error)
{
    public static ChallengeOperationResult Succeeded(string challengeId)
        => new(true, challengeId, null);

    public static ChallengeOperationResult Failed(string error)
        => new(false, null, error);
}

public static class ChallengeValidation
{
    public const int MaxParticipants = 4;
    public const int MaxStartPayloadBytes = 48 * 1024;

    public static bool IsValidSnapshot(ChallengeSnapshot? snapshot)
    {
        if (snapshot == null ||
            !IsValidChallengeId(snapshot.ChallengeId) ||
            snapshot.LobbyId == 0 ||
            snapshot.CreatorId.Value == 0 ||
            !Enum.IsDefined(snapshot.Kind) ||
            !Enum.IsDefined(snapshot.Phase) ||
            snapshot.MaxParticipants is < 2 or > MaxParticipants)
        {
            return false;
        }

        var participants = snapshot.Participants ?? Array.Empty<PlatformUserId>();
        if (participants.Length < 1 || participants.Length > snapshot.MaxParticipants)
            return false;
        if (participants.Any(user => user.Value == 0) || participants.Distinct().Count() != participants.Length)
            return false;
        if (!participants.Contains(snapshot.CreatorId))
            return false;

        var payload = snapshot.StartPayload ?? Array.Empty<byte>();
        if (payload.Length > MaxStartPayloadBytes)
            return false;
        if (snapshot.Phase != ChallengePhase.Running && payload.Length != 0)
            return false;

        return true;
    }

    public static bool IsValidChallengeId(string challengeId)
        => !string.IsNullOrWhiteSpace(challengeId) && Guid.TryParse(challengeId, out _);
}
