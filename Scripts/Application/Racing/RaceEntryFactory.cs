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
}

/// <summary>
/// Creates the complete immutable race entry before presentation starts. The live race therefore
/// never reads mutable owned-creature state and CPU generation cannot depend on Godot frame/VFX
/// behavior. CPU generation intentionally preserves the demo's existing seed/name/genome rules.
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
    {
        ArgumentNullException.ThrowIfNull(selected);
        return Create(CreateOwnedEntrant(selected), simulationSeed, courseDefinition);
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
    {
        ArgumentNullException.ThrowIfNull(selected);
        ArgumentNullException.ThrowIfNull(selected.Participant);
        ArgumentNullException.ThrowIfNull(courseDefinition);

        var entrants = new List<RaceEntrant>(4)
        {
            selected
        };

        for (var cpuIndex = 0; cpuIndex < 3; cpuIndex++)
        {
            var cpuSeed = simulationSeed + (ulong)(100 + cpuIndex * 17);
            var genome = _genomeFactory.CreateRandom(cpuSeed);
            var cpu = new VoidlingData
            {
                Id = $"cpu-{cpuIndex}-{cpuSeed}",
                Name = CpuNames[(int)(cpuSeed % (ulong)CpuNames.Length)],
                Genome = genome,
                Stage = LifeStage.Adult,
                TintHex = _colorResolver.ResolveTint(genome),
                TrainingPoints = _rules.Genetics.StatIds.ToDictionary(id => id, _ => 0)
            };
            entrants.Add(new RaceEntrant(_snapshotFactory.Create(cpu), false, 0));
        }

        return new RaceEntry(simulationSeed, _rules.Racing, entrants.AsReadOnly())
        {
            CourseDefinition = courseDefinition
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
