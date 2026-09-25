using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Voidling.Application.Garden;
using Voidling.Presentation.Garden.Atmosphere;

namespace VoidlingGame;

/// <summary>
/// The Garden's sea, light, weather and wildlife are one composed presentation component; this
/// partial only feeds it what it cannot know by itself: the island's shape, the Voidlings and trees
/// to reflect, and the clock.
/// </summary>
public partial class GardenController
{
    private const float IslandFieldMargin = 170.0f;
    private const float IslandFieldCellSize = 2.0f;
    private const float IslandFieldRange = 128.0f;

    private GardenAtmosphere _atmosphere = null!;

    /// <summary>The Garden's atmosphere, for review probes that pin the hour or the weather.</summary>
    internal GardenAtmosphere Atmosphere => _atmosphere;

    private void InstallAtmosphere()
    {
        _atmosphere = new GardenAtmosphere { Name = "Atmosphere" };
        AddChild(_atmosphere);
        _atmosphere.UseCamera(_camera);
        _atmosphere.SetClock(DateTime.Now, _session.State.GardenTint);
    }

    /// <summary>
    /// Rebuilds everything the atmosphere derives from the island: the shared shoreline field, where
    /// the glowing mushrooms grow and which trees the sea reflects.
    /// </summary>
    private void RefreshAtmosphereIsland(IReadOnlyList<GardenModuleData> placed, IReadOnlySet<(int Q, int R)> occupied)
    {
        bool OnLand(float x, float y)
        {
            // The cliff under a south-facing edge stands in the water too, so the sea meets its foot.
            return occupied.Contains(Hex.At(x, y)) || occupied.Contains(Hex.At(x, y - GardenCoast.CliffHeight));
        }

        var bounds = _landBounds.Grow(IslandFieldMargin);
        var field = GardenIslandField.Build(
            OnLand,
            bounds.Position.X,
            bounds.Position.Y,
            bounds.End.X,
            bounds.End.Y,
            IslandFieldCellSize,
            IslandFieldRange);
        var hexCenters = placed
            .Select(module => Hex.CenterOf(module.HexQ, module.HexR))
            .Select(center => new Vector2(center.X, center.Y))
            .ToArray();
        // The boat ties up under the lowest stretch of south coast.
        Vector2? southShore = placed.Count == 0
            ? null
            : placed
                .Select(module => Hex.CenterOf(module.HexQ, module.HexR))
                .Select(center => new Vector2(center.X, center.Y + Hex.Height * 0.5f))
                .OrderByDescending(edge => edge.Y)
                .ThenBy(edge => edge.X)
                .First();
        _atmosphere.SetIsland(
            field,
            _landBounds,
            hexCenters,
            Hex.InnerRadius,
            southShore,
            spot => !_treeTrunks.Any(trunk => trunk.DistanceTo(spot) < 24.0f));
        _atmosphere.SetTrees(_actorsRoot, _treeTrunks.Select(trunk => trunk + new Vector2(0.0f, -TreeSpriteRise - 10.0f)));
        _atmosphere.SetMushrooms(_actorsRoot, MushroomSpots(placed));
        _atmosphere.SetScenery(_treeProps
            .Where(GodotObject.IsInstanceValid)
            .Select(tree => (TreeTexture, tree.Position + new Vector2(0.0f, -TreeSpriteRise), Vector2.One, tree.Position.Y)));
    }

    /// <summary>
    /// A few clusters of glowing mushrooms on plain ground, seeded by the hex so they grow in the
    /// same places every time, and kept off the trunks and the middle of the hex.
    /// </summary>
    private IEnumerable<Vector2> MushroomSpots(IEnumerable<GardenModuleData> placed)
    {
        foreach (var module in placed)
        {
            if (module.BiomeId.Length > 0)
                continue;

            var rng = new RandomNumberGenerator
            {
                Seed = unchecked((ulong)(module.HexQ * 92821L ^ module.HexR * 68917L ^ 0x6C0FFEEL))
            };
            var (x, y) = Hex.CenterOf(module.HexQ, module.HexR);
            var center = new Vector2(x, y);
            var clusters = rng.RandiRange(0, 2);
            for (var i = 0; i < clusters; i++)
            {
                var spot = center + Vector2.Right.Rotated(rng.RandfRange(0.0f, Mathf.Tau)) *
                           Hex.InnerRadius * rng.RandfRange(0.40f, 0.72f);
                if (_treeTrunks.Any(trunk => trunk.DistanceTo(spot) < 22.0f))
                    continue;
                yield return spot.Round();
            }
        }
    }

    private void TrackAtmosphereVoidling(VoidlingActor actor, VoidlingData data)
        => _atmosphere.TrackVoidling(
            actor.CreatureId,
            actor.Body,
            actor.VisualAppearance,
            () => GodotObject.IsInstanceValid(actor) ? actor.GlobalPosition : Vector2.Zero,
            actor.BodyHeight,
            GameRules.HasMutation(data, GameRules.AngelMutationId));

    private void UntrackAtmosphereVoidling(string creatureId) => _atmosphere.UntrackVoidling(creatureId);

    /// <summary>Stands the Voidlings still on the given spots, for review screenshots.</summary>
    internal void PinVoidlingsForReview((Vector2 Center, Vector2[] Spots) shore)
    {
        var index = 0;
        foreach (var actor in _actors.Values.OrderBy(actor => actor.CreatureId, StringComparer.Ordinal))
        {
            if (index >= shore.Spots.Length)
                break;
            actor.Position = shore.Spots[index++];
            actor.SetInteractionLocked(true);
            actor.PlayWalk(Vector2.Down);
            actor.PlayIdle();
        }
    }

    /// <summary>Where the island's trees stand, for review screenshots that frame one.</summary>
    internal IReadOnlyList<Vector2> TreeTrunksForReview => _treeTrunks;

    /// <summary>Grows glowing mushrooms at chosen spots, for review screenshots of their light.</summary>
    internal void GrowMushroomsForReview(IEnumerable<Vector2> spots) => _atmosphere.SetMushrooms(_actorsRoot, spots);

    /// <summary>Points the camera somewhere and holds it there, for review screenshots.</summary>
    internal void FrameCameraForReview(Vector2 center, float zoom)
    {
        StopFollowing();
        _zoomTarget = zoom;
        _camera.Zoom = new Vector2(zoom, zoom);
        _camera.Position = center;
        _camera.ResetSmoothing();
    }
}
