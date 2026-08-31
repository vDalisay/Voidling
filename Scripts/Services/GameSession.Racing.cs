using System;
using Voidling.Application.Racing;
using Voidling.Domain.Racing;
using Voidling.Domain.Rules;

namespace VoidlingGame;

public partial class GameSession
{
    private RaceEntryFactory? _raceEntryFactory;

    public void ConfigureRacing(RaceEntryFactory raceEntryFactory)
    {
        if (IsInsideTree())
            throw new InvalidOperationException("Race dependencies must be configured before GameSession enters the scene tree.");

        _raceEntryFactory = raceEntryFactory ?? throw new ArgumentNullException(nameof(raceEntryFactory));
    }

    /// <summary>
    /// Allocates one persistent race seed and snapshots the selected Voidling plus generated CPU
    /// opponents before presentation starts. The RaceScreen receives no live mutable state.
    /// </summary>
    public RaceEntry CreateRaceEntryFor(string selectedCreatureId)
        => CreateRaceEntryFor(
            selectedCreatureId,
            RaceCourseCatalog.Demo.Id,
            RaceCourseCatalog.Demo.Version);

    /// <summary>
    /// Resolves a stable authored course identity before allocating the authoritative race seed.
    /// Unknown content therefore cannot consume progression RNG state or start a partial race.
    /// </summary>
    public RaceEntry CreateRaceEntryFor(string selectedCreatureId, string courseId, int courseVersion)
    {
        var selected = FindVoidling(selectedCreatureId)
            ?? throw new InvalidOperationException($"Cannot create race entry for unknown Voidling '{selectedCreatureId}'.");
        if (_raceEntryFactory == null)
            throw new InvalidOperationException("RaceEntryFactory was not configured by Bootstrap.");
        if (!RaceCourseCatalog.TryGet(courseId, courseVersion, out var courseDefinition))
        {
            throw new InvalidOperationException(
                $"Cannot create race entry for unknown race course '{courseId}' version {courseVersion}.");
        }

        return _raceEntryFactory.Create(selected, NextSeed(), courseDefinition);
    }

    /// <summary>
    /// Applies the persistent reward after presentation reports the simulation-owned placement.
    /// </summary>
    public int ApplyRacePlacementReward(int placement)
    {
        if (_raceResults == null)
            throw new InvalidOperationException("RaceResultUseCase was not configured by Bootstrap.");

        var result = _raceResults.AwardPlacement(State, placement);
        RecordDailyMissionEvent(DailyMissionEventKind.CompleteStandardRace);
        SaveAndNotify($"Race reward: +{result.Reward} sprouts.");
        return result.Reward;
    }
}
