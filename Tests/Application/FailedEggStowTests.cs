using Voidling.Application.Roster;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Application;

/// <summary>
/// Picking a failed egg up off the island. It stays owned so the satchel still lists it, and the
/// flag is additive, so a save written before it keeps every failed egg exactly where it lay.
/// </summary>
public sealed class FailedEggStowTests
{
    [Fact]
    public void Stow_TakesAFailedEggOffTheIslandWithoutDiscardingIt()
    {
        var state = WithEgg(EggState.Failed, 120.0f, 64.0f);
        var roster = new VoidlingRosterUseCase();

        Assert.True(roster.StowFailedEgg(state, "egg"));

        var egg = Assert.Single(state.OwnedEggs);
        Assert.True(egg.Stowed);
        Assert.Equal(0.0f, egg.WorldX);
        Assert.Equal(0.0f, egg.WorldY);
        Assert.Equal(EggState.Failed, egg.State);
    }

    [Fact]
    public void Stow_RefusesAnEggThatDidNotFail()
    {
        var state = WithEgg(EggState.Incubating, 10.0f, 10.0f);
        var roster = new VoidlingRosterUseCase();

        Assert.False(roster.StowFailedEgg(state, "egg"));
        Assert.False(state.OwnedEggs[0].Stowed);
        Assert.Equal(10.0f, state.OwnedEggs[0].WorldX);
    }

    [Fact]
    public void Stow_IsIdempotentAndStillDiscardable()
    {
        var state = WithEgg(EggState.Failed, 5.0f, 5.0f);
        var roster = new VoidlingRosterUseCase();

        Assert.True(roster.StowFailedEgg(state, "egg"));
        Assert.False(roster.StowFailedEgg(state, "egg"));
        Assert.True(roster.DiscardFailedEgg(state, "egg"));
        Assert.Empty(state.OwnedEggs);
    }

    [Fact]
    public void ExistingSavesLoadWithFailedEggsStillOnTheIsland()
    {
        var state = WithEgg(EggState.Failed, 30.0f, 40.0f);

        Assert.False(state.OwnedEggs[0].Stowed);
    }

    private static GameStateData WithEgg(EggState eggState, float x, float y)
    {
        var state = new GameStateData();
        state.OwnedEggs.Add(new EggData { Id = "egg", State = eggState, WorldX = x, WorldY = y });
        return state;
    }
}
