using System;
using Voidling.Domain.Rules;
using VoidlingGame;

namespace Voidling.Domain.Genetics;

internal static class AbilityGeneExpression
{
    public static GenePairData CreatePair(int alleleA, int alleleB, Random random, GeneticsRules rules)
    {
        var expressedIndex = 0;
        if (alleleA != alleleB)
        {
            var higherIndex = alleleA > alleleB ? 0 : 1;
            var lowerIndex = higherIndex == 0 ? 1 : 0;
            expressedIndex = random.NextDouble() < rules.HigherAlleleExpressionChance
                ? higherIndex
                : lowerIndex;
        }

        return new GenePairData
        {
            AlleleA = alleleA,
            AlleleB = alleleB,
            ExpressedAlleleIndex = expressedIndex
        };
    }

    public static int PickAllele(GenePairData gene, Random random)
        => random.NextDouble() < 0.5 ? gene.AlleleA : gene.AlleleB;
}
