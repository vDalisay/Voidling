using System;
using System.Collections.Generic;
using System.Linq;
using Voidling.Domain.Genetics;
using Voidling.Domain.Racing;
using Voidling.Domain.Rules;
using VoidlingGame;

namespace Voidling.Application.Racing;

public sealed record RaceEntrant(
    RaceParticipantSnapshot Participant,
    bool HasAngelMutation,
    int OtherMutationCount);

public sealed record RaceEntry(
    ulong SimulationSeed,
    RaceRules Rules,
    IReadOnlyList<RaceEntrant> Entrants)
{
    public RaceCourseDefinition CourseDefinition { get; init; } = RaceCourseCatalog.Demo;

    /// <summary>Authored CPU difficulty tier this race was created at. See <see cref="RaceDifficulty"/>.</summary>
    public int DifficultyLevel { get; init; } = RaceDifficulty.Easiest;
}

/// <summary>
/// Fixed CPU difficulty tiers for an authored course, like a beginner/intermediate/expert race.
/// A tier is a share of the training-point cap the generated opponents are given, so it does not
/// rubber-band to the player and a recorded time stays comparable between attempts.
/// </summary>
public static class RaceDifficulty
{
    public const int Easiest = 1;
    public const int Hardest = 3;

    private static readonly float[] TrainedShareByLevel = { 0.0f, 0.4f, 0.8f };

    public static int Clamp(int level) => Math.Clamp(level, Easiest, Hardest);

    public static float TrainedShare(int level) => TrainedShareByLevel[Clamp(level) - 1];
}

/// <summary>
/// Creates the complete immutable race entry before presentation starts. The live race therefore
/// never reads mutable owned-creature state and CPU generation cannot depend on Godot frame/VFX
/// behavior. Semantic appearance remains frozen inside each RaceParticipantSnapshot.
/// </summary>
public sealed class RaceEntryFactory
{
    private static readonly string[] CpuNames = { "Fern", "Moss", "Puck", "Clover", "Pebble", "Dew" };

    private readonly GameBalanceRules _rules;
    private readonly RaceParticipantSnapshotFactory _snapshotFactory;
    private readonly GenomeFactory _genomeFactory;
    private readonly ColorPhenotypeResolver _colorResolver;

    public RaceEntryFactory(GameBalanceRules rules)
    {
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        _snapshotFactory = new RaceParticipantSnapshotFactory(rules);
        _genomeFactory = new GenomeFactory(rules.Genetics);
        _colorResolver = new ColorPhenotypeResolver(rules.Appearance);
    }

    public RaceEntry Create(VoidlingData selected, ulong simulationSeed)
        => Create(selected, simulationSeed, RaceCourseCatalog.Demo);

    public RaceEntry Create(
        VoidlingData selected,
        ulong simulationSeed,
        RaceCourseDefinition courseDefinition)
        => Create(selected, simulationSeed, courseDefinition, RaceDifficulty.Easiest);

    public RaceEntry Create(
        VoidlingData selected,
        ulong simulationSeed,
        RaceCourseDefinition courseDefinition,
        int difficultyLevel)
    {
        ArgumentNullException.ThrowIfNull(selected);
        return Create(CreateOwnedEntrant(selected), simulationSeed, courseDefinition, difficultyLevel);
    }

    /// <summary>
    /// Rebuilds a race from an already-frozen owned entrant. This is used by resumable/daily races:
    /// CPUs remain deterministic from the seed while later Garden training cannot change the entrant
    /// that was committed when the attempt began.
    /// </summary>
    public RaceEntry Create(RaceEntrant selected, ulong simulationSeed)
        => Create(selected, simulationSeed, RaceCourseCatalog.Demo);

    public RaceEntry Create(
        RaceEntrant selected,
        ulong simulationSeed,
        RaceCourseDefinition courseDefinition)
        => Create(selected, simulationSeed, courseDefinition, RaceDifficulty.Easiest);

    public RaceEntry Create(
        RaceEntrant selected,
        ulong simulationSeed,
        RaceCourseDefinition courseDefinition,
        int difficultyLevel)
    {
        ArgumentNullException.ThrowIfNull(selected);
        ArgumentNullException.ThrowIfNull(selected.Participant);
        ArgumentNullException.ThrowIfNull(courseDefinition);

        var entrants = new List<RaceEntrant>(4)
        {
            selected
        };
        var level = RaceDifficulty.Clamp(difficultyLevel);
        var cpuTrainingPoints = (int)MathF.Round(_rules.Stats.MaxTrainingPoints * RaceDifficulty.TrainedShare(level));

        for (var cpuIndex = 0; cpuIndex < 3; cpuIndex++)
        {
            var cpuSeed = simulationSeed + (ulong)(100 + cpuIndex * 17);
            var genome = _genomeFactory.CreateRandom(cpuSeed);
            var paletteHue = _colorResolver.ResolvePaletteHue(genome);
            var cpu = new VoidlingData
            {
                Id = $"cpu-{cpuIndex}-{cpuSeed}",
                Name = CpuNames[(int)(cpuSeed % (ulong)CpuNames.Length)],
                Genome = genome,
                Stage = LifeStage.Adult,
                TintHex = _colorResolver.ResolveTint(genome),
                Appearance = new VoidlingAppearanceData
                {
                    VisualTypeId = VoidlingAppearanceData.DefaultVisualTypeId,
                    PaletteHue = paletteHue
                },
                TrainingPoints = _rules.Genetics.StatIds.ToDictionary(id => id, _ => cpuTrainingPoints)
            };
            entrants.Add(new RaceEntrant(_snapshotFactory.Create(cpu), false, 0));
        }

        return new RaceEntry(simulationSeed, _rules.Racing, entrants.AsReadOnly())
        {
            CourseDefinition = courseDefinition,
            DifficultyLevel = level
        };
    }

    public RaceEntrant CreateOwnedEntrant(VoidlingData selected)
    {
        ArgumentNullException.ThrowIfNull(selected);

        var hasAngel = selected.RareTraits.Exists(trait =>
            string.Equals(trait.TraitId, "Angel", StringComparison.OrdinalIgnoreCase));
        var otherMutationCount = selected.RareTraits.Count(trait =>
            !string.Equals(trait.TraitId, "Angel", StringComparison.OrdinalIgnoreCase));

        return new RaceEntrant(
            _snapshotFactory.Create(selected),
            hasAngel,
            otherMutationCount);
    }
}
