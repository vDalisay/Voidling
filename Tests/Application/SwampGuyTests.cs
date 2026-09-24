using System.Linq;
using Voidling.Application.Breeding;
using Voidling.Application.Collection;
using Voidling.Application.Multiplayer.Trading;
using Voidling.Application.Persistence;
using Voidling.Application.Roster;
using Voidling.Application.Shop;
using Voidling.Application.Simulation;
using Voidling.Application.Training;
using Voidling.Domain.Creatures;
using Voidling.Domain.Evolution;
using Voidling.Domain.Garden;
using Voidling.Domain.Genetics;
using Voidling.Domain.Lifecycle;
using Voidling.Domain.Rules;
using VoidlingGame;
using Xunit;

namespace Voidling.Tests.Application;

/// <summary>
/// The Swamp guy: the first breeding of two Water adults while an unused Swamp exists lays his egg,
/// which only incubates on a Swamp that never hatched one. He has S/S Swim, keeps his look for life,
/// cannot be traded, and after death or Goodbye a respawn egg is sold for a new Swamp.
/// </summary>
public sealed class SwampGuyTests
{
    private static readonly GameBalanceRules Rules = GameBalanceRules.DemoDefaults;
    private const string SwampHexId = TrainingUseCase.StarterHexId;

    [Fact]
    public void TwoWaterAdultsWithASwamp_LayTheSwampGuysEgg()
    {
        var state = GardenWithSwamp();
        AddParents(state, EvolutionSpecialization.Swim, EvolutionSpecialization.Swim);

        var result = Breed(state, "swamp-egg");

        Assert.True(result.Succeeded);
        var egg = result.Egg!;
        Assert.Equal(SpecialVariantCatalog.SwampGuyId, egg.SpecialVariantId);
        Assert.Equal(SpecialVariantCatalog.SwampGuyId, result.SpecialVariantId);
        Assert.True(egg.IsViable);
        Assert.Equal((5, 5, 5), (egg.Genome.AbilityGenes["swim"].AlleleA, egg.Genome.AbilityGenes["swim"].AlleleB, egg.Genome.AbilityGenes["swim"].ExpressedValue));
        Assert.Equal(SpecialVariantCatalog.SwampGuy.VisualTypeId, egg.Appearance.VisualTypeId);
        Assert.True(egg.RequiredIncubationSeconds >= Rules.Hatching.IncubationSeconds + Rules.Hatching.SpecialVariantSeconds);
        var entry = SpecialVariantTracker.Find(state, SpecialVariantCatalog.SwampGuyId)!;
        Assert.Equal(SpecialVariantStatus.Egg, entry.Status);
        Assert.True(entry.BreedingSpawnUsed);
    }

    [Theory]
    [InlineData(false, EvolutionSpecialization.Swim, EvolutionSpecialization.Swim)]
    [InlineData(true, EvolutionSpecialization.Swim, EvolutionSpecialization.Generalist)]
    [InlineData(true, EvolutionSpecialization.Run, EvolutionSpecialization.Run)]
    public void WithoutASwampOrTwoWaterParents_TheEggIsOrdinary(bool ownsSwamp, EvolutionSpecialization a, EvolutionSpecialization b)
    {
        var state = ownsSwamp ? GardenWithSwamp() : GardenWithBiome(BiomeCatalog.Water, 3);
        AddParents(state, a, b);

        var result = Breed(state, "plain-egg");

        Assert.Equal(string.Empty, result.Egg!.SpecialVariantId);
        Assert.Null(SpecialVariantTracker.Find(state, SpecialVariantCatalog.SwampGuyId));
    }

    [Fact]
    public void TheBreedingSpawnHappensOnce()
    {
        var state = GardenWithSwamp();
        AddParents(state, EvolutionSpecialization.Swim, EvolutionSpecialization.Swim);
        Breed(state, "first");
        foreach (var parent in state.Voidlings) parent.BreedCooldownSeconds = 0;

        var second = Breed(state, "second");

        Assert.Equal(string.Empty, second.Egg!.SpecialVariantId);
    }

    [Fact]
    public void TheEggOnlyIncubatesOnAnUnusedSwamp()
    {
        var state = GardenWithSwamp();
        var plain = new Voidling.Application.Garden.GardenModuleData { Id = "plain", Placed = true, HexQ = 1, HexR = 0 };
        state.GardenModules.Add(plain);
        AddParents(state, EvolutionSpecialization.Swim, EvolutionSpecialization.Swim);
        var egg = Breed(state, "swamp-egg").Egg!;
        var simulation = new AdvanceSimulationUseCase(Rules);

        PutEggOn(egg, plain);
        simulation.Advance(state, 30.0f);
        Assert.Equal(0.0f, egg.IncubationSeconds);

        PutEggOn(egg, state.GardenModules.Single(module => module.Id == SwampHexId));
        simulation.Advance(state, 30.0f);
        Assert.Equal(30.0f, egg.IncubationSeconds);
    }

    [Fact]
    public void HatchingOnTheSwamp_UsesItUpAndTheSwampGuyKeepsHisLookForLife()
    {
        var state = GardenWithSwamp();
        AddParents(state, EvolutionSpecialization.Swim, EvolutionSpecialization.Swim);
        var egg = Breed(state, "swamp-egg").Egg!;
        var swamp = state.GardenModules.Single(module => module.Id == SwampHexId);
        PutEggOn(egg, swamp);
        state.Voidlings.RemoveAll(parent => parent.Id.StartsWith("parent"));

        var result = new AdvanceSimulationUseCase(Rules).Advance(state, egg.RequiredIncubationSeconds + 1.0f);

        var hatched = Assert.Single(result.Events.OfType<CreatureHatchedEvent>());
        Assert.Equal(SpecialVariantCatalog.SwampGuyId, hatched.SpecialVariantId);
        var swampGuy = state.Voidlings.Single(creature => creature.Id == "swamp-egg");
        Assert.True(swamp.SpecialVariantHatched);
        Assert.Equal(SpecialVariantStatus.Alive, SpecialVariantTracker.StatusOf(state, SpecialVariantCatalog.SwampGuyId));

        EvolutionService.ResolveFirstEvolution(swampGuy, Rules);
        Assert.Equal(SpecialVariantCatalog.SwampGuy.VisualTypeId, swampGuy.Appearance.VisualTypeId);
        swampGuy.Stage = LifeStage.Adult;
        new ReincarnationService().ApplyReincarnation(swampGuy, Rules.Reincarnation, Rules.Stats);
        Assert.Equal(SpecialVariantCatalog.SwampGuy.VisualTypeId, swampGuy.Appearance.VisualTypeId);
    }

    [Fact]
    public void AfterGoodbye_ARespawnEggNeedsANewSwamp()
    {
        var state = GardenWithSwamp();
        state.Coins = 1000;
        var swamp = state.GardenModules.Single(module => module.Id == SwampHexId);
        swamp.SpecialVariantHatched = true;
        var swampGuy = new VoidlingData { Id = "swamp-guy", Name = "Swampy", SpecialVariantId = SpecialVariantCatalog.SwampGuyId };
        state.Voidlings.Add(swampGuy);
        state.SpecialVariants.Add(new SpecialVariantStateData
        {
            VariantId = SpecialVariantCatalog.SwampGuyId,
            BreedingSpawnUsed = true,
            Status = SpecialVariantStatus.Alive,
            CreatureId = swampGuy.Id
        });
        var shop = new ShopUseCase(Rules);

        Assert.False(shop.BuySpecialVariantEgg(state, SpecialVariantCatalog.SwampGuyId, "too-early", 7UL).Succeeded);
        new VoidlingRosterUseCase().SayGoodbye(state, swampGuy.Id);
        Assert.Equal(SpecialVariantStatus.Departed, SpecialVariantTracker.StatusOf(state, SpecialVariantCatalog.SwampGuyId));

        var bought = shop.BuySpecialVariantEgg(state, SpecialVariantCatalog.SwampGuyId, "respawn", 7UL);
        Assert.True(bought.Succeeded);
        var egg = bought.PurchasedEgg!;
        Assert.Equal(1000 - Rules.Shop.SpecialVariantEggPrice, state.Coins);
        Assert.Equal(EggState.Stored, egg.State);
        Assert.Equal(5, egg.Genome.AbilityGenes["swim"].ExpressedValue);
        Assert.False(shop.BuySpecialVariantEgg(state, SpecialVariantCatalog.SwampGuyId, "second", 8UL).Succeeded);

        // The old Swamp already hatched one, so the egg sits still there…
        shop.PlaceStoredEgg(state, egg.Id, 0, 0);
        PutEggOn(egg, swamp);
        var simulation = new AdvanceSimulationUseCase(Rules);
        simulation.Advance(state, 20.0f);
        Assert.Equal(0.0f, egg.IncubationSeconds);

        // …but a new Swamp works.
        var newSwamp = new Voidling.Application.Garden.GardenModuleData
        {
            Id = "new-swamp", Placed = true, HexQ = 1, HexR = 0,
            BiomeId = BiomeCatalog.Swamp, StatId = "swim", Level = 4
        };
        state.GardenModules.Add(newSwamp);
        PutEggOn(egg, newSwamp);
        simulation.Advance(state, 20.0f);
        Assert.Equal(20.0f, egg.IncubationSeconds);
    }

    [Fact]
    public void DeathOpensTheRespawnEggToo()
    {
        var state = GardenWithSwamp();
        var swampGuy = new VoidlingData
        {
            Id = "swamp-guy", Name = "Swampy", Stage = LifeStage.Adult,
            AdultAgeSeconds = Rules.Reincarnation.AdultLifespanSeconds - 1.0f,
            SpecialVariantId = SpecialVariantCatalog.SwampGuyId
        };
        state.Voidlings.Add(swampGuy);
        state.SpecialVariants.Add(new SpecialVariantStateData
        {
            VariantId = SpecialVariantCatalog.SwampGuyId, Status = SpecialVariantStatus.Alive, CreatureId = swampGuy.Id
        });

        new AdvanceSimulationUseCase(Rules).Advance(state, 5.0f);

        Assert.Contains(swampGuy, state.DepartedVoidlings);
        Assert.Equal(SpecialVariantStatus.Departed, SpecialVariantTracker.StatusOf(state, SpecialVariantCatalog.SwampGuyId));
    }

    [Fact]
    public void TheSwampGuyCannotBeTraded()
    {
        var state = new GameStateData();
        state.Voidlings.Add(new VoidlingData { Id = "swamp-guy", Name = "Swampy", SpecialVariantId = SpecialVariantCatalog.SwampGuyId });

        var built = new TradeTransferService(Rules).TryBuildTransferBundle(
            state,
            new[] { new TradeAssetReference(TradeAssetKind.Voidling, "swamp-guy") },
            out _,
            out var error);

        Assert.False(built);
        Assert.Contains("special variant", error);
    }

    [Fact]
    public void Migration_ClearsUnknownVariantsAndKeepsTheSwampGuy()
    {
        var state = new GameStateData { SaveVersion = GameStateMigrationService.CurrentSaveVersion };
        state.Voidlings.Add(new VoidlingData { Id = "real", SpecialVariantId = SpecialVariantCatalog.SwampGuyId });
        state.Voidlings.Add(new VoidlingData { Id = "made-up", SpecialVariantId = "moon-variant" });
        state.SpecialVariants.Add(new SpecialVariantStateData { VariantId = "moon-variant" });
        state.SpecialVariants.Add(new SpecialVariantStateData { VariantId = SpecialVariantCatalog.SwampGuyId, Status = SpecialVariantStatus.Alive, CreatureId = "real" });

        new GameStateMigrationService(Rules).Normalize(state);

        Assert.Equal(SpecialVariantCatalog.SwampGuyId, state.Voidlings.Single(v => v.Id == "real").SpecialVariantId);
        Assert.Equal(string.Empty, state.Voidlings.Single(v => v.Id == "made-up").SpecialVariantId);
        Assert.Equal(SpecialVariantCatalog.SwampGuyId, Assert.Single(state.SpecialVariants).VariantId);
    }

    private static BreedingResult Breed(GameStateData state, string eggId)
        => new BreedVoidlingsUseCase(Rules).Execute(state, "parent-a", "parent-b", 99UL, eggId, 0, 0);

    private static GameStateData GardenWithSwamp() => GardenWithBiome(BiomeCatalog.Swamp, 4);

    private static GameStateData GardenWithBiome(string biomeId, int level)
    {
        var state = new GameStateData();
        TrainingUseCase.EnsureStarterHex(state);
        var hex = state.GardenModules.Single(module => module.Id == SwampHexId);
        hex.BiomeId = biomeId;
        hex.StatId = BiomeCatalog.StatOf(biomeId);
        hex.Level = level;
        return state;
    }

    private static void AddParents(GameStateData state, EvolutionSpecialization formA, EvolutionSpecialization formB)
    {
        state.Voidlings.Add(Parent("parent-a", 11UL, formA));
        state.Voidlings.Add(Parent("parent-b", 12UL, formB));
    }

    private static VoidlingData Parent(string id, ulong seed, EvolutionSpecialization form) => new()
    {
        Id = id,
        Name = id,
        Stage = LifeStage.Adult,
        EvolutionSpecialization = form,
        Genome = new GenomeFactory(Rules.Genetics).CreateRandom(seed)
    };

    private static void PutEggOn(EggData egg, Voidling.Application.Garden.GardenModuleData hex)
    {
        var (x, y) = Rules.GardenModules.Hex.CenterOf(hex.HexQ, hex.HexR);
        egg.WorldX = x;
        egg.WorldY = y;
    }
}
