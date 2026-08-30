using Voidling.Application.Creatures;
using Voidling.Domain.Genetics;
using Voidling.Domain.Rules;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Application;

public sealed class VoidlingProfileDemeanorTests
{
    private static readonly GameBalanceRules Rules = GameBalanceRules.DemoDefaults;

    [Fact]
    public void Projection_ReportsSettledWithoutExposingHiddenCareValues()
    {
        var state = new GameStateData();
        var creature = CreateCreature();
        creature.Needs.Happiness = Rules.Reincarnation.MinimumHappiness;
        creature.Needs.Stress = Rules.Reincarnation.MaximumStress;
        state.Voidlings.Add(creature);

        var profile = new VoidlingProfileProjectionService(Rules).Create(state, creature.Id);

        Assert.NotNull(profile);
        Assert.Equal(VoidlingCareDemeanor.Settled, profile!.CareDemeanor);
    }

    [Fact]
    public void Projection_ReportsNeedsCareWhenLifecycleCareBoundaryIsUnsafe()
    {
        var state = new GameStateData();
        var creature = CreateCreature();
        creature.Needs.Happiness = Rules.Reincarnation.MinimumHappiness;
        creature.Needs.Stress = Rules.Reincarnation.MaximumStress + 1.0f;
        state.Voidlings.Add(creature);

        var profile = new VoidlingProfileProjectionService(Rules).Create(state, creature.Id);

        Assert.NotNull(profile);
        Assert.Equal(VoidlingCareDemeanor.NeedsCare, profile!.CareDemeanor);
    }

    private static VoidlingData CreateCreature()
    {
        var creature = new VoidlingData
        {
            Id = "care-profile",
            Name = "Pip",
            Stage = LifeStage.Adult,
            TintHex = "#F6F0C9"
        };

        foreach (var statId in Rules.Genetics.StatIds)
        {
            creature.Genome.AbilityGenes[statId] = new GenePairData
            {
                AlleleA = 1,
                AlleleB = 1,
                ExpressedAlleleIndex = 0
            };
            creature.TrainingPoints[statId] = 0;
        }

        return creature;
    }
}
