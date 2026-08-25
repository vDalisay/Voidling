using System;
using System.Collections.Generic;
using System.Linq;
using Voidling.Application.Ports.Multiplayer;
using Voidling.Application.Racing;
using Voidling.Domain.Rules;
using VoidlingGame;

namespace Voidling.Application.Multiplayer.Racing;

public sealed record MultiplayerRacePlacement(
    PlatformUserId OwnerId,
    string ParticipantId,
    int Place);

public sealed record MultiplayerRaceResult(
    string ChallengeId,
    long FinalTick,
    string FinalChecksum,
    MultiplayerRacePlacement[] Placements);

public sealed record MultiplayerRaceRewardResult(
    bool Success,
    bool AlreadyApplied,
    int Place,
    int CoinReward,
    bool Won,
    int MultiplayerWins,
    string? Error)
{
    public static MultiplayerRaceRewardResult Failed(string error)
        => new(false, false, 0, 0, false, 0, error);
}

public static class MultiplayerRaceResultValidation
{
    public static bool IsValid(MultiplayerRaceResult? result, out string? error)
    {
        error = null;
        if (result == null ||
            !MultiplayerRaceValidation.IsValidChallengeId(result.ChallengeId) ||
            result.FinalTick <= 0 ||
            !IsSha256(result.FinalChecksum) ||
            result.Placements == null ||
            result.Placements.Length is < 2 or > 4)
        {
            error = "Multiplayer race result metadata is invalid.";
            return false;
        }

        if (result.Placements.Any(value =>
                value == null ||
                value.OwnerId.Value == 0 ||
                string.IsNullOrWhiteSpace(value.ParticipantId) ||
                value.ParticipantId.Length > 160 ||
                value.Place < 1 ||
                value.Place > result.Placements.Length))
        {
            error = "Multiplayer race placement data is invalid.";
            return false;
        }

        if (result.Placements.Select(value => value.OwnerId).Distinct().Count() != result.Placements.Length ||
            result.Placements.Select(value => value.ParticipantId).Distinct(StringComparer.Ordinal).Count() != result.Placements.Length ||
            !result.Placements.Select(value => value.Place).OrderBy(value => value)
                .SequenceEqual(Enumerable.Range(1, result.Placements.Length)))
        {
            error = "Multiplayer race placements must contain one unique contiguous place per participant.";
            return false;
        }

        return true;
    }

    private static bool IsSha256(string value)
        => value is { Length: 64 } && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');
}

/// <summary>
/// Applies only the local player's validated multiplayer result. Network coordinators never mutate
/// coins or progress directly. Challenge IDs provide idempotency so duplicate/replayed result packets
/// cannot award twice; the bounded history is sufficient because only an active connected challenge
/// can deliver an accepted result.
/// </summary>
public sealed class MultiplayerRaceResultUseCase
{
    public const int MaxAppliedRaceIds = 256;

    private readonly RaceResultUseCase _raceRewards;

    public MultiplayerRaceResultUseCase(GameBalanceRules rules)
        => _raceRewards = new RaceResultUseCase(
            rules ?? throw new ArgumentNullException(nameof(rules)));

    public MultiplayerRaceRewardResult Apply(
        GameStateData state,
        PlatformUserId localUserId,
        MultiplayerRaceResult result)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (localUserId.Value == 0)
            return MultiplayerRaceRewardResult.Failed("Local multiplayer identity is invalid.");
        if (!MultiplayerRaceResultValidation.IsValid(result, out var error))
            return MultiplayerRaceRewardResult.Failed(error!);

        state.AppliedMultiplayerRaceIds ??= new List<string>();
        if (state.AppliedMultiplayerRaceIds.Contains(result.ChallengeId, StringComparer.Ordinal))
        {
            var existingPlacement = result.Placements.SingleOrDefault(value => value.OwnerId == localUserId);
            return new MultiplayerRaceRewardResult(
                true,
                true,
                existingPlacement?.Place ?? 0,
                0,
                existingPlacement?.Place == 1,
                Math.Max(0, state.MultiplayerWins),
                null);
        }

        var placement = result.Placements.SingleOrDefault(value => value.OwnerId == localUserId);
        if (placement == null)
            return MultiplayerRaceRewardResult.Failed("Validated multiplayer result does not contain the local player.");

        var reward = _raceRewards.AwardPlacement(state, placement.Place);
        var won = placement.Place == 1;
        if (won)
            state.MultiplayerWins = Math.Max(0, state.MultiplayerWins) + 1;
        else
            state.MultiplayerWins = Math.Max(0, state.MultiplayerWins);

        state.AppliedMultiplayerRaceIds.Add(result.ChallengeId);
        if (state.AppliedMultiplayerRaceIds.Count > MaxAppliedRaceIds)
        {
            state.AppliedMultiplayerRaceIds.RemoveRange(
                0,
                state.AppliedMultiplayerRaceIds.Count - MaxAppliedRaceIds);
        }

        return new MultiplayerRaceRewardResult(
            true,
            false,
            placement.Place,
            reward.Reward,
            won,
            state.MultiplayerWins,
            null);
    }
}