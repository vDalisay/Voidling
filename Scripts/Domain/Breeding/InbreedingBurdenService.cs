using System;
using VoidlingGame;

namespace Voidling.Domain.Breeding;

public sealed class InbreedingBurdenService
{
    public int ComputeChildBurden(VoidlingData parentA, VoidlingData parentB, bool related)
    {
        ArgumentNullException.ThrowIfNull(parentA);
        ArgumentNullException.ThrowIfNull(parentB);

        if (related)
            return Math.Clamp(Math.Max(parentA.InbreedingBurdenLevel, parentB.InbreedingBurdenLevel) + 1, 1, 4);

        var first = parentA.InbreedingBurdenLevel;
        var second = parentB.InbreedingBurdenLevel;

        if (first > 0 && second == 0)
            return Math.Max(first - 1, 0);
        if (second > 0 && first == 0)
            return Math.Max(second - 1, 0);
        if (first > 0 && second > 0)
            return Math.Max(first, second);

        return 0;
    }
}
