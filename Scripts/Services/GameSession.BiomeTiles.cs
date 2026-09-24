using System;
using Voidling.Application.Garden;
using Voidling.Domain.Rules;
using Voidling.Presentation.UI.Common;

namespace VoidlingGame;

/// <summary>Biome tiles: buying them, putting them on the island, stacking and picking them up.</summary>
public partial class GameSession
{
    private BiomeTileUseCase? _biomeTiles;

    public void ConfigureBiomeTiles(BiomeTileUseCase biomeTiles)
    {
        if (IsInsideTree())
            throw new InvalidOperationException("Biome tile dependencies must be configured before GameSession enters the scene tree.");

        _biomeTiles = biomeTiles ?? throw new ArgumentNullException(nameof(biomeTiles));
    }

    public int BiomeTileMaxStars => _biomeTiles!.MaxStars;

    public bool BuyBiomeTile(string biomeId)
    {
        var result = _biomeTiles!.BuyBiomeTile(State, biomeId);
        if (!result.Succeeded)
        {
            ToastRequested?.Invoke(PlayerActionFailureText.ForBiomeTile(result.Failure));
            return false;
        }

        RecordDailyMissionEvent(DailyMissionEventKind.PurchaseShopItem);
        SaveAndNotify(string.Format(Tr("LOG_BIOME_TILE_BOUGHT"), BiomePresentationCatalog.NameFor(biomeId)));
        return true;
    }

    /// <summary>Buys a one-star tile and puts it on a plain hex in one step.</summary>
    public bool BuildBiome(string moduleId, string biomeId)
    {
        var result = _biomeTiles!.BuildBiome(State, moduleId, biomeId);
        if (!result.Succeeded)
        {
            ToastRequested?.Invoke(PlayerActionFailureText.ForBiomeTile(result.Failure));
            return false;
        }

        RecordDailyMissionEvent(DailyMissionEventKind.PurchaseShopItem);
        var message = string.Format(Tr("LOG_BIOME_BUILT"), BiomePresentationCatalog.NameFor(result.BiomeId));
        SaveAndNotify(message);
        RaiseGardenEvent(message);
        return true;
    }

    public bool CanPlaceBiomeTile(string moduleId, string biomeId, int stars)
        => _biomeTiles!.ValidatePlacement(State, moduleId, biomeId, stars) == BiomeTileFailure.None;

    /// <summary>Puts an owned tile on a hex: onto plain ground, or stacked on a matching tile.</summary>
    public bool PlaceBiomeTile(string moduleId, string biomeId, int stars)
    {
        var result = _biomeTiles!.PlaceBiomeTile(State, moduleId, biomeId, stars);
        if (!result.Succeeded)
        {
            ToastRequested?.Invoke(PlayerActionFailureText.ForBiomeTile(result.Failure));
            return false;
        }

        var name = BiomePresentationCatalog.NameFor(result.BiomeId);
        var message = result.Stacked
            ? string.Format(Tr("LOG_BIOME_STACKED"), name, result.Stars)
            : string.Format(Tr("LOG_BIOME_BUILT"), name);
        SaveAndNotify(message);
        RaiseGardenEvent(message);
        return true;
    }

    public bool PickUpBiomeTile(string moduleId)
    {
        var result = _biomeTiles!.PickUpBiomeTile(State, moduleId);
        if (!result.Succeeded)
        {
            ToastRequested?.Invoke(PlayerActionFailureText.ForBiomeTile(result.Failure));
            return false;
        }

        SaveAndNotify(string.Format(Tr("LOG_BIOME_PICKED_UP"), BiomePresentationCatalog.NameFor(result.BiomeId), result.Stars));
        return true;
    }
}
