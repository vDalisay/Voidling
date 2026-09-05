using System;
using System.Collections.Generic;
using Voidling.Application.Racing;
using Voidling.Domain.Racing;

namespace VoidlingGame;

public partial class GameSession
{
    private readonly CupProgressionService _cupProgression = new();

    public IReadOnlyList<CupProgressionEntry> GetCupProgression()
        => _cupProgression.Project(State);

    /// <summary>
    /// Creates a normal authoritative race entry from a Cup's authored course. Cup structure never
    /// forks simulation rules; locked/unknown Cup IDs are rejected before the race seed is allocated.
    /// </summary>
    public RaceEntry CreateCupRaceEntryFor(string selectedCreatureId, string cupId)
    {
        if (!CupCatalog.TryGet(cupId, out var cup))
            throw new InvalidOperationException($"Cannot create race entry for unknown Cup '{cupId}'.");
        if (!_cupProgression.IsUnlocked(State, cup))
            throw new InvalidOperationException($"Cannot enter locked Cup '{cupId}'.");

        return CreateRaceEntryFor(selectedCreatureId, cup.Course.Id, cup.Course.Version);
    }

    /// <summary>
    /// Persists the stable Cup completion ID and derived unlock progression. Economy is intentionally
    /// untouched until entry-fee/refund/prize rules are explicitly decided.
    /// </summary>
    public bool RecordCupVictory(string cupId)
    {
        var result = _cupProgression.RecordVictory(State, cupId);
        if (!result.Succeeded)
        {
            ToastRequested?.Invoke(result.Failure switch
            {
                CupProgressionFailure.Locked => "That Cup is still locked.",
                CupProgressionFailure.UnknownCup => "That Cup is unavailable.",
                _ => "The Cup result could not be recorded."
            });
            return false;
        }

        if (!result.Changed)
            return true;

        SaveAndNotify($"Cup completed: {result.Cup!.Id}.");
        return true;
    }
}
