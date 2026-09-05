using Voidling.Application.Racing;
using Voidling.Domain.Racing;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Application;

public sealed class CupProgressionServiceTests
{
    [Fact]
    public void Project_StartsFirstCupUnlockedAndPrerequisiteCupLocked()
    {
        var service = new CupProgressionService();
        var state = new GameStateData();

        var projected = service.Project(state);

        Assert.True(projected[0].IsUnlocked);
        Assert.False(projected[0].IsCompleted);
        Assert.False(projected[1].IsUnlocked);
        Assert.False(projected[1].IsCompleted);
    }

    [Fact]
    public void RecordVictory_UnlocksDependentCupAndIsIdempotent()
    {
        var service = new CupProgressionService();
        var state = new GameStateData();

        var first = service.RecordVictory(state, CupCatalog.FirstCup.Id);
        var duplicate = service.RecordVictory(state, CupCatalog.FirstCup.Id);
        var projected = service.Project(state);

        Assert.True(first.Succeeded);
        Assert.True(first.Changed);
        Assert.True(duplicate.Succeeded);
        Assert.False(duplicate.Changed);
        Assert.Single(state.CompletedCupIds);
        Assert.True(projected[1].IsUnlocked);
    }

    [Fact]
    public void RecordVictory_RejectsLockedAndUnknownCupsWithoutMutation()
    {
        var service = new CupProgressionService();
        var state = new GameStateData();

        var locked = service.RecordVictory(state, CupCatalog.LongCup.Id);
        var unknown = service.RecordVictory(state, "missing-cup");

        Assert.Equal(CupProgressionFailure.Locked, locked.Failure);
        Assert.Equal(CupProgressionFailure.UnknownCup, unknown.Failure);
        Assert.Empty(state.CompletedCupIds);
    }

    [Fact]
    public void CompletedCupIds_RemainsNonNullForLegacyDeserializationShape()
    {
        var state = new GameStateData { CompletedCupIds = null! };
        Assert.NotNull(state.CompletedCupIds);
        Assert.Empty(state.CompletedCupIds);
    }
}
