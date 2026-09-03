using Voidling.Application.Persistence;
using Voidling.Domain.Rules;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Application;

public sealed class AppearanceMigrationTests
{
    [Fact]
    public void Normalize_V8SaveAddsSemanticAppearanceWithoutRerollingLegacyColor()
    {
        var state = new GameStateData { SaveVersion = 8 };
        var creature = new VoidlingData
        {
            Id = "legacy",
            TintHex = "#A8C8EC",
            Genome = new GenomeData
            {
                ColorAlleleA = 5,
                ColorAlleleB = 1,
                ExpressedColorIndex = 0
            }
        };
        state.Voidlings.Add(creature);

        new GameStateMigrationService(GameBalanceRules.DemoDefaults).Normalize(state);

        Assert.Equal(GameStateMigrationService.CurrentSaveVersion, state.SaveVersion);
        Assert.Equal("#A8C8EC", creature.TintHex);
        Assert.Equal("normal", creature.Appearance.VisualTypeId);
        Assert.True(VoidlingAppearanceData.IsValidHue(creature.Genome.PaletteHueA));
        Assert.True(VoidlingAppearanceData.IsValidHue(creature.Genome.PaletteHueB));
        Assert.True(VoidlingAppearanceData.IsValidHue(creature.Appearance.PaletteHue));
    }
}
