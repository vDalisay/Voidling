using Voidling.Domain.Shared;

namespace Voidling.Domain.Hatching;

/// <summary>The rare appearance is fixed by the egg seed at creation, independently of other rolls.</summary>
public static class RainbowEggRoll
{
    public const string VisualTypeId = "rainbow";
    public const int Odds = 8192;

    public static bool IsRainbow(ulong eggSeed)
        => StableRandom.Create(eggSeed, "egg:rainbow-appearance").Next(Odds) == 0;
}
