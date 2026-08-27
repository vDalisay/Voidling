using Voidling.Domain.Care;
using Voidling.Domain.Rules;
using Xunit;

namespace Voidling.Tests.Domain;

public sealed class CreatureNeedsServiceTests
{
    private static readonly NeedsRules Rules = GameBalanceRules.DemoDefaults.Needs;

    [Fact]
    public void Advance_OneMinuteAppliesOnlyConfiguredOpenGameDrift()
    {
        var needs = new CreatureNeedsState();
        var service = new CreatureNeedsService();

        var changed = service.Advance(needs, 60.0f, Rules);

        Assert.True(changed);
        Assert.Equal(0.75f, needs.Hunger, 3);
        Assert.Equal(99.55f, needs.Energy, 3);
        Assert.Equal(0.35f, needs.Fatigue, 3);
        Assert.Equal(0.0f, needs.Stress, 3);
        Assert.Equal(0.50f, needs.Boredom, 3);
        Assert.Equal(0.25f, needs.Loneliness, 3);
        Assert.Equal(99.60f, needs.Nourishment, 3);
        Assert.Equal(99.95f, needs.Condition, 3);
        Assert.Equal(0.0f, needs.Happiness, 3);
    }

    [Fact]
    public void Advance_EquivalentElapsedChunksProduceEquivalentState()
    {
        var single = new CreatureNeedsState { Stress = 20.0f, Happiness = 40.0f };
        var chunked = new CreatureNeedsState { Stress = 20.0f, Happiness = 40.0f };
        var service = new CreatureNeedsService();

        service.Advance(single, 120.0f, Rules);
        for (var i = 0; i < 4; i++)
            service.Advance(chunked, 30.0f, Rules);

        Assert.Equal(single.Hunger, chunked.Hunger, 4);
        Assert.Equal(single.Energy, chunked.Energy, 4);
        Assert.Equal(single.Fatigue, chunked.Fatigue, 4);
        Assert.Equal(single.Stress, chunked.Stress, 4);
        Assert.Equal(single.Boredom, chunked.Boredom, 4);
        Assert.Equal(single.Loneliness, chunked.Loneliness, 4);
        Assert.Equal(single.Nourishment, chunked.Nourishment, 4);
        Assert.Equal(single.Condition, chunked.Condition, 4);
        Assert.Equal(single.Happiness, chunked.Happiness, 4);
    }

    [Fact]
    public void Advance_ClampsEveryNeedToNormalizedRange()
    {
        var needs = new CreatureNeedsState
        {
            Hunger = 99.9f,
            Energy = 0.1f,
            Fatigue = 99.9f,
            Stress = 0.1f,
            Boredom = 99.9f,
            Loneliness = 99.9f,
            Nourishment = 0.1f,
            Condition = 0.01f,
            Happiness = 0.01f
        };

        new CreatureNeedsService().Advance(needs, 600.0f, Rules);

        Assert.InRange(needs.Hunger, 0.0f, 100.0f);
        Assert.InRange(needs.Energy, 0.0f, 100.0f);
        Assert.InRange(needs.Fatigue, 0.0f, 100.0f);
        Assert.InRange(needs.Stress, 0.0f, 100.0f);
        Assert.InRange(needs.Boredom, 0.0f, 100.0f);
        Assert.InRange(needs.Loneliness, 0.0f, 100.0f);
        Assert.InRange(needs.Nourishment, 0.0f, 100.0f);
        Assert.InRange(needs.Condition, 0.0f, 100.0f);
        Assert.InRange(needs.Happiness, 0.0f, 100.0f);
    }

    [Fact]
    public void Advance_ZeroOrInvalidElapsedTimeIsNoOp()
    {
        var needs = new CreatureNeedsState();
        var service = new CreatureNeedsService();

        Assert.False(service.Advance(needs, 0.0f, Rules));
        Assert.False(service.Advance(needs, float.NaN, Rules));
        Assert.Equal(0.0f, needs.Hunger);
        Assert.Equal(100.0f, needs.Energy);
        Assert.Equal(0.0f, needs.Happiness);
    }
}