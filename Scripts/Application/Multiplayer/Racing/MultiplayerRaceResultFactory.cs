using System;
using System.Linq;

namespace Voidling.Application.Multiplayer.Racing;

/// <summary>
/// Converts a completed deterministic lockstep session into the canonical host result. Ownership is
/// resolved from the immutable start payload, never from mutable save state or presentation objects.
/// </summary>
public sealed class MultiplayerRaceResultFactory
{
    public MultiplayerRaceResult Create(
        ResolvedMultiplayerRace race,
        MultiplayerRaceLockstepSession session)
    {
        ArgumentNullException.ThrowIfNull(race);
        ArgumentNullException.ThrowIfNull(session);
        if (!string.Equals(race.Start.ChallengeId, session.ChallengeId, StringComparison.Ordinal))
            throw new ArgumentException("Race and lockstep session belong to different challenges.", nameof(session));
        if (!session.IsComplete)
            throw new InvalidOperationException("A multiplayer race result can only be created after simulation completes.");

        var ownerByParticipant = race.Start.Entrants.ToDictionary(
            entrant => entrant.Participant.CreatureId,
            entrant => entrant.OwnerId,
            StringComparer.Ordinal);
        var finishOrder = session.Simulation.FinishOrder.ToArray();
        if (finishOrder.Length != ownerByParticipant.Count ||
            finishOrder.Any(participantId => !ownerByParticipant.ContainsKey(participantId)))
        {
            throw new InvalidOperationException("Completed race finish order does not match the immutable entrants.");
        }

        var placements = finishOrder
            .Select((participantId, index) => new MultiplayerRacePlacement(
                ownerByParticipant[participantId],
                participantId,
                index + 1))
            .ToArray();
        var result = new MultiplayerRaceResult(
            race.Start.ChallengeId,
            session.CurrentTick,
            session.ComputeDeterministicChecksum(),
            placements);
        if (!MultiplayerRaceResultValidation.IsValid(result, out var error))
            throw new InvalidOperationException(error ?? "Host created an invalid multiplayer race result.");
        return result;
    }
}