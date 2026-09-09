using System;
using System.Collections.Generic;
using Voidling.Domain.Lifecycle;
using Voidling.Domain.Racing;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Domain;

/// <summary>
/// These seams exist for product decisions that are not locked yet. The tests pin that the shipped
/// defaults change nothing, and that the arithmetic is correct once a decision is authored.
/// </summary>
public sealed class BlockedSystemSeamTests
{
    [Fact]
    public void TrophyTransformation_WithoutAnAuthoredRecipe_NeverQualifies()
    {
        var creature = new VoidlingData { Id = "veteran", ReincarnationCount = 99 };

        Assert.False(TrophyRequirements.Undecided.IsAuthored);
        Assert.False(TrophyTransformation.Qualifies(creature, TrophyRequirements.Undecided));
    }

    [Fact]
    public void TrophyTransformation_WithAnAuthoredRecipe_GatesOnLifecycleCount()
    {
        var requirements = new TrophyRequirements(MinimumReincarnations: 3);
        var creature = new VoidlingData { Id = "veteran" };

        creature.ReincarnationCount = 2;
        Assert.False(TrophyTransformation.Qualifies(creature, requirements));

        creature.ReincarnationCount = 3;
        Assert.True(TrophyTransformation.Qualifies(creature, requirements));
    }

    [Fact]
    public void CupEconomy_DefaultsToFreeEntryAndNoRefund()
    {
        foreach (var cup in CupCatalog.All)
        {
            Assert.Equal(0, CupEconomy.EntryFee(CupEconomyRules.Free, cup.Id));
            Assert.Equal(0, CupEconomy.Refund(CupEconomyRules.Free, cup.Id, 1));
        }
    }

    [Fact]
    public void CupEconomy_ExpressesWinnerOnlyAndPlacementBasedRefunds()
    {
        var fees = new Dictionary<string, int>(StringComparer.Ordinal) { [CupCatalog.LongCup.Id] = 100 };

        var winnerOnly = new CupEconomyRules(fees, new Dictionary<int, double> { [1] = 1.0 });
        Assert.Equal(100, CupEconomy.EntryFee(winnerOnly, CupCatalog.LongCup.Id));
        Assert.Equal(100, CupEconomy.Refund(winnerOnly, CupCatalog.LongCup.Id, 1));
        Assert.Equal(0, CupEconomy.Refund(winnerOnly, CupCatalog.LongCup.Id, 2));

        var placementBased = new CupEconomyRules(fees, new Dictionary<int, double> { [1] = 1.0, [2] = 0.5 });
        Assert.Equal(50, CupEconomy.Refund(placementBased, CupCatalog.LongCup.Id, 2));
        Assert.Equal(0, CupEconomy.Refund(placementBased, CupCatalog.LongCup.Id, 3));

        // Unpriced Cups stay free even when a refund table exists.
        Assert.Equal(0, CupEconomy.EntryFee(placementBased, CupCatalog.FirstCup.Id));
        Assert.Equal(0, CupEconomy.Refund(placementBased, CupCatalog.FirstCup.Id, 1));
    }
}
