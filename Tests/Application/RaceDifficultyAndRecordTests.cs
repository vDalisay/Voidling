using System.Linq;
using Voidling.Application.Racing;
using Voidling.Domain.Genetics;
using Voidling.Domain.Racing;
using Voidling.Domain.Rules;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Application;

public sealed class RaceDifficultyAndRecordTests
{
    private static readonly GameBalanceRules Rules = GameBalanceRules.DemoDefaults;

    private static VoidlingData CreateCreature(ulong seed = 7) => new()
    {
        Id = "owned",
        Name = "Pip",
        Stage = LifeStage.Adult,
        Genome = new GenomeFactory(Rules.Genetics).CreateRandom(seed),
        TrainingPoints = Rules.Genetics.StatIds.ToDictionary(id => id, _ => 0)
    };

    private static float CpuStrength(int level)
    {
        var entry = new RaceEntryFactory(Rules).Create(CreateCreature(), 99, RaceCourseCatalog.Demo, level);
        return entry.Entrants.Skip(1).Sum(cpu =>
            cpu.Participant.Run + cpu.Participant.Swim + cpu.Participant.Fly +
            cpu.Participant.Power + cpu.Participant.Stamina);
    }

    [Fact]
    public void HigherDifficultyLevelsProduceStrongerOpponents()
    {
        var easy = CpuStrength(1);
        var medium = CpuStrength(2);
        var hard = CpuStrength(3);

        Assert.True(medium > easy, $"level 2 ({medium}) must beat level 1 ({easy})");
        Assert.True(hard > medium, $"level 3 ({hard}) must beat level 2 ({medium})");
    }

    [Fact]
    public void DifficultyIsClampedAndRecordedOnTheEntry()
    {
        var factory = new RaceEntryFactory(Rules);

        Assert.Equal(RaceDifficulty.Easiest, factory.Create(CreateCreature(), 1, RaceCourseCatalog.Demo, -4).DifficultyLevel);
        Assert.Equal(RaceDifficulty.Hardest, factory.Create(CreateCreature(), 1, RaceCourseCatalog.Demo, 9).DifficultyLevel);
        Assert.Equal(2, factory.Create(CreateCreature(), 1, RaceCourseCatalog.Demo, 2).DifficultyLevel);
    }

    [Fact]
    public void UnspecifiedDifficultyKeepsTheUntrainedOpponentsOlderCallersExpect()
    {
        var factory = new RaceEntryFactory(Rules);
        var legacy = factory.Create(CreateCreature(), 42, RaceCourseCatalog.Demo);
        var explicitEasiest = factory.Create(CreateCreature(), 42, RaceCourseCatalog.Demo, RaceDifficulty.Easiest);

        Assert.Equal(
            explicitEasiest.Entrants.Select(e => e.Participant.Run),
            legacy.Entrants.Select(e => e.Participant.Run));
    }

    [Fact]
    public void CourseRecordsKeepTheFastestFinishPerCourseVersionAndLevel()
    {
        var state = new GameStateData();

        Assert.True(Record(state, "demo", 2, 1, 15_000, "Pip"));
        Assert.False(Record(state, "demo", 2, 1, 16_000, "Mallow"));
        Assert.True(Record(state, "demo", 2, 1, 14_000, "Mallow"));
        Assert.False(Record(state, "demo", 2, 1, 0, "Zero"));

        var best = Find(state, "demo", 2, 1);
        Assert.Equal(14_000, best!.Milliseconds);
        Assert.Equal("Mallow", best.CreatureName);

        // A different level, version or course is a separate record rather than an overwrite.
        Assert.True(Record(state, "demo", 2, 3, 30_000, "Pip"));
        Assert.True(Record(state, "demo", 3, 1, 30_000, "Pip"));
        Assert.True(Record(state, "long-standard", 2, 1, 30_000, "Pip"));
        Assert.Equal(4, state.CourseRecords.Count);
        Assert.Equal(14_000, Find(state, "demo", 2, 1)!.Milliseconds);
    }

    // Mirrors GameSession.RecordCourseFinish, which cannot be instantiated outside Godot.
    private static bool Record(GameStateData state, string courseId, int version, int level, int milliseconds, string name)
    {
        if (milliseconds <= 0) return false;
        var existing = Find(state, courseId, version, level);
        if (existing != null && existing.Milliseconds <= milliseconds) return false;
        if (existing == null)
        {
            existing = new RaceCourseRecordData { CourseId = courseId, CourseVersion = version, Level = level };
            state.CourseRecords.Add(existing);
        }
        existing.Milliseconds = milliseconds;
        existing.CreatureName = name;
        return true;
    }

    private static RaceCourseRecordData? Find(GameStateData state, string courseId, int version, int level)
        => state.CourseRecords.Find(record =>
            record.CourseId == courseId && record.CourseVersion == version && record.Level == level);
}
