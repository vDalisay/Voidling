using Voidling.Application.Breeding;

namespace VoidlingGame;

public partial class MainController
{
    // DetailsScreen currently receives localized/display-ready text. Keep the translation boundary
    // in Presentation even when English is the only shipped catalog entry for these qualitative bands.
    private static string LineageRiskTranslationKey(LineageRiskBand risk)
        => LineageRiskDisplayName(risk);
}
