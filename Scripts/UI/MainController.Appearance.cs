using Voidling.Application.Creatures;
using Voidling.Domain.Genetics;

namespace VoidlingGame;

public partial class MainController
{
    private static AppearancePhenotype ToAppearancePhenotype(VoidlingAppearanceProfileProjection appearance)
        => new(
            appearance.ExpressedColorAllele,
            appearance.Tone,
            appearance.PatternAllele,
            appearance.Shiny,
            appearance.CoatAllele);
}
