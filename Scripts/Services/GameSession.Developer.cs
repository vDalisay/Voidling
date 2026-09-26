using System;
using System.Linq;
using Voidling.Application.Garden;
using Voidling.Domain.Creatures;
using Voidling.Domain.Evolution;
using Voidling.Domain.Garden;
using Voidling.Domain.Shop;

namespace VoidlingGame;

/// <summary>Playtest actions. These use the same saved state and simulation as the normal game.</summary>
public partial class GameSession
{
    public double DeveloperClockOffsetSeconds { get; private set; }

    public bool DeveloperSpawnVoidling(string visualTypeId)
    {
        var form = visualTypeId switch
        {
            "normal" => EvolutionSpecialization.None,
            "neutral" => EvolutionSpecialization.Generalist,
            "run" => EvolutionSpecialization.Run,
            "water" => EvolutionSpecialization.Swim,
            "fly" => EvolutionSpecialization.Fly,
            "rainbow" => EvolutionSpecialization.None,
            "power" => EvolutionSpecialization.Power,
            "swamp-variant" => EvolutionSpecialization.Swim,
            _ => (EvolutionSpecialization?)null
        };
        if (form == null || State.Voidlings.Count >= Math.Max(GameRules.GardenMaxPopulation, State.GardenPopulationCapOverride))
            return false;
        var creature = CreateStarter($"Voidling {State.Voidlings.Count + State.DepartedVoidlings.Count + 1}",
            "#F6F0C9", StarterSpawnPosition(State.Voidlings.Count));
        creature.EvolutionSpecialization = form.Value;
        creature.Appearance.VisualTypeId = visualTypeId;
        if (form == EvolutionSpecialization.None)
        {
            creature.Stage = LifeStage.Child;
            creature.AgeSeconds = 0;
        }
        if (visualTypeId == SpecialVariantCatalog.SwampGuyId)
        {
            creature.SpecialVariantId = visualTypeId;
            SpecialVariantCatalog.ForceGenes(SpecialVariantCatalog.SwampGuy, creature.Genome);
        }
        State.Voidlings.Add(creature);
        SaveAndNotify(Tr("UI_DEV_CHANGE_SAVED"));
        return true;
    }

    public bool DeveloperGiveTile(string biomeId, int stars, int amount)
    {
        if (BiomeCatalog.FindBase(biomeId) == null || stars < 1 || stars > BiomeCatalog.MaxStars || amount < 1)
            return false;
        var stack = State.BiomeTiles.FirstOrDefault(tile => tile.BiomeId == biomeId && tile.Stars == stars);
        if (stack == null)
            State.BiomeTiles.Add(new BiomeTileStackData { BiomeId = biomeId, Stars = stars, Count = amount });
        else
            stack.Count = (int)Math.Min(int.MaxValue, (long)stack.Count + amount);
        SaveAndNotify(Tr("UI_DEV_CHANGE_SAVED"));
        return true;
    }

    public bool DeveloperGiveLandPiece(string shapeId, int amount)
    {
        if (!GardenTileShape.Catalog.Any(shape => shape.Id == shapeId) || amount < 1) return false;
        for (var i = 0; i < amount; i++)
            State.GardenModules.Add(new GardenModuleData { Id = NewId(), ShapeId = shapeId, Placed = false });
        SaveAndNotify(Tr("UI_DEV_CHANGE_SAVED"));
        return true;
    }

    public bool DeveloperGiveItem(string itemId, int amount)
    {
        if (amount < 1) return false;
        if (GameRules.StatIds.Contains(itemId))
        {
            State.TrainingItems.TryGetValue(itemId, out var count);
            State.TrainingItems[itemId] = (int)Math.Min(int.MaxValue, (long)count + amount);
        }
        else if (itemId == ShopItemIds.FullIncubationSkip)
        {
            State.UtilityItems.TryGetValue(itemId, out var count);
            State.UtilityItems[itemId] = (int)Math.Min(int.MaxValue, (long)count + amount);
        }
        else if (itemId == "mystery-egg")
        {
            for (var i = 0; i < amount; i++)
                State.OwnedEggs.Add(CreateStoreEgg());
        }
        else return false;
        SaveAndNotify(Tr("UI_DEV_CHANGE_SAVED"));
        return true;
    }

    public void DeveloperGiveCoins(int amount)
    {
        if (amount < 1) return;
        State.Coins = (int)Math.Min(int.MaxValue, (long)State.Coins + amount);
        SaveAndNotify(Tr("UI_DEV_CHANGE_SAVED"));
    }

    public void DeveloperRaisePopulationCap(int cap)
    {
        State.GardenPopulationCapOverride = Math.Max(State.GardenPopulationCapOverride,
            Math.Clamp(cap, GameRules.GardenMaxPopulation, 256));
        SaveAndNotify(Tr("UI_DEV_CHANGE_SAVED"));
    }

    public bool DeveloperHatchEgg(string eggId)
    {
        var egg = State.OwnedEggs.FirstOrDefault(candidate => candidate.Id == eggId);
        if (egg == null) return false;
        var previousX = egg.WorldX;
        var previousY = egg.WorldY;
        if (egg.State == EggState.Stored)
        {
            var position = NestPosition(State.Voidlings.Count);
            egg.WorldX = position.X;
            egg.WorldY = position.Y;
        }
        var result = _simulation!.HatchImmediately(State, eggId);
        if (!result.Changed)
        {
            egg.WorldX = previousX;
            egg.WorldY = previousY;
            return false;
        }
        PresentSimulationEvents(result);
        SaveAndNotify(Tr(egg.IsViable ? "UI_DEV_EGG_HATCHED" : "UI_DEV_EGG_RESOLVED"));
        return true;
    }

    public void DeveloperAdvanceHours(double hours)
    {
        if (!double.IsFinite(hours) || hours <= 0) return;
        var remaining = Math.Min(hours, 24) * 3600;
        while (remaining > 0)
        {
            var step = (float)Math.Min(300, remaining);
            PresentSimulationEvents(_simulation!.Advance(State, step));
            remaining -= step;
        }
        DeveloperClockOffsetSeconds += Math.Min(hours, 24) * 3600;
        SaveAndNotify(Tr("UI_DEV_TIME_ADVANCED"));
    }
}
