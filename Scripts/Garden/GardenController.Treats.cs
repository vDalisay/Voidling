using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Voidling.Presentation.UI.Common;

namespace VoidlingGame;

/// <summary>
/// Food on the ground. Putting a treat down wakes every Voidling within smelling distance; they
/// run for it at their own Run speed and the first one to reach it eats it. The treat is only
/// spent when it is actually eaten, so the session stays the authority on what the player owns.
/// </summary>
public partial class GardenController
{
    /// <summary>How far a treat can be smelled from, in world pixels.</summary>
    private const float TreatSmellRadius = 150.0f;

    /// <summary>Close enough to count as a mouthful.</summary>
    private const float TreatReachRadius = 9.0f;

    /// <summary>The whole eating beat, squish and shrink together.</summary>
    public const float TreatEatingSeconds = 3.0f;

    private static readonly Texture2D TreatAtlas =
        GD.Load<Texture2D>(StatPresentationCatalog.TreatAtlasPath);

    /// <summary>Raised when treat placement is armed or cleared so the HUD can show its own hint.</summary>
    public event Action<bool>? TreatPlacementModeChanged;

    /// <summary>Creature ID and stat ID of a treat that was actually eaten.</summary>
    public event Action<string, string>? TreatEaten;

    private sealed class TreatVisual
    {
        public required string StatId { get; init; }
        public required Node2D Holder { get; init; }
        public required Sprite2D Sprite { get; init; }
    }

    /// <summary>Drops the session still owns, keyed by drop ID; an eaten one leaves this at once.</summary>
    private readonly Dictionary<string, TreatVisual> _treatVisuals = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Tween> _eatingTweens = new(StringComparer.Ordinal);
    private string _placingTreatStatId = "";
    private Node2D? _treatGhost;

    public bool IsPlacingTreat => _placingTreatStatId.Length > 0;

    /// <summary>
    /// Arms "click the garden to put this treat down". Nothing is spent until a Voidling eats it,
    /// so a cancelled placement leaves the treat in the satchel.
    /// </summary>
    public void BeginTreatPlacement(string statId)
    {
        if (string.IsNullOrWhiteSpace(statId))
            return;

        CancelTreatPlacement();
        _placingTreatStatId = statId;

        _treatGhost = new Node2D { ZIndex = 9, Modulate = new Color(1.0f, 1.0f, 1.0f, 0.62f) };
        _treatGhost.AddChild(CreateTreatSprite(statId));
        _eggsRoot.AddChild(_treatGhost);
        TreatPlacementModeChanged?.Invoke(true);
    }

    public void CancelTreatPlacement()
    {
        if (_treatGhost != null && GodotObject.IsInstanceValid(_treatGhost))
            _treatGhost.QueueFree();
        _treatGhost = null;

        if (_placingTreatStatId.Length == 0)
            return;

        _placingTreatStatId = "";
        TreatPlacementModeChanged?.Invoke(false);
    }

    private void UpdateTreatGhost()
    {
        if (_treatGhost == null || !GodotObject.IsInstanceValid(_treatGhost))
            return;

        _treatGhost.Position = ClampToGarden(_eggsRoot.ToLocal(GetGlobalMousePosition()));
    }

    // The treat lands where the click actually happened rather than wherever the cached pointer
    // position last settled, matching how an egg is put down.
    private bool TryCompleteTreatPlacement(Vector2 viewportPosition)
    {
        if (_placingTreatStatId.Length == 0)
            return false;

        var statId = _placingTreatStatId;
        var position = ClampToGarden(_eggsRoot.ToLocal(GetCanvasTransform().AffineInverse() * viewportPosition));
        CancelTreatPlacement();
        // The session owns what is on the ground, so a drop survives a quit exactly like an egg.
        if (_session.DropTreat(statId, position.X, position.Y) != null)
            WakeVoidlingsFor(position);
        return true;
    }

    /// <summary>
    /// Mirrors the eggs: build a visual for every drop the save holds and free the ones it no
    /// longer does. A treat being eaten has already left the state, and its visual is freed by the
    /// eating animation instead.
    /// </summary>
    private void RefreshTreats()
    {
        var drops = _session.State.DroppedTreats;
        foreach (var staleId in _treatVisuals.Keys
                     .Where(id => drops.All(drop => !string.Equals(drop.Id, id, StringComparison.Ordinal)))
                     .ToArray())
        {
            if (GodotObject.IsInstanceValid(_treatVisuals[staleId].Holder))
                _treatVisuals[staleId].Holder.QueueFree();
            _treatVisuals.Remove(staleId);
        }

        foreach (var drop in drops)
        {
            if (_treatVisuals.TryGetValue(drop.Id, out var existing))
            {
                existing.Holder.Position = new Vector2(drop.X, drop.Y);
                continue;
            }

            var holder = new Node2D { Position = new Vector2(drop.X, drop.Y), ZIndex = 6 };
            var sprite = CreateTreatSprite(drop.StatId);
            holder.AddChild(sprite);
            _eggsRoot.AddChild(holder);
            _treatVisuals[drop.Id] = new TreatVisual { StatId = drop.StatId, Holder = holder, Sprite = sprite };

            // A small hop, but only for a treat put down during play. Ones restored from a save
            // were already lying there and should not re-announce themselves.
            if (!_initialRefreshComplete)
                continue;
            holder.Scale = Vector2.Zero;
            var pop = CreateTween();
            pop.TweenProperty(holder, "scale", Vector2.One, 0.32)
                .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        }
    }

    /// <summary>
    /// Everything within smelling distance drops what it is doing and runs for the food. Speed is
    /// the Voidling's own roaming speed, which already scales with its Run stat, so a fast one
    /// genuinely gets there first.
    /// </summary>
    private void WakeVoidlingsFor(Vector2 position)
    {
        foreach (var actor in _actors.Values)
        {
            if (!GodotObject.IsInstanceValid(actor)) continue;
            if (actor.Position.DistanceTo(position) > TreatSmellRadius) continue;
            actor.ChaseTreat(position);
        }
    }

    /// <summary>Runs every frame from the Garden's own process, beside the egg pulse.</summary>
    private void UpdateTreatDrops()
    {
        if (_treatVisuals.Count == 0)
            return;

        foreach (var (dropId, visual) in _treatVisuals.ToArray())
        {
            if (!GodotObject.IsInstanceValid(visual.Holder))
            {
                _treatVisuals.Remove(dropId);
                continue;
            }

            var eater = NearestChaserWithinReach(visual.Holder.Position);
            if (eater == null)
                continue;

            // Hand the visual to the eating animation before the state changes, so the refresh
            // that follows the claim does not free the food mid-bite.
            _treatVisuals.Remove(dropId);
            ReleaseChasers(visual.Holder.Position);
            PlayEatingSquish(eater);
            PlayFoodShrink(visual.Holder, visual.Sprite);
            _session.ClaimDroppedTreat(dropId, eater.CreatureId);
        }
    }

    private VoidlingActor? NearestChaserWithinReach(Vector2 position)
    {
        VoidlingActor? best = null;
        var bestDistance = TreatReachRadius;
        foreach (var actor in _actors.Values)
        {
            if (!GodotObject.IsInstanceValid(actor) || !actor.IsChasingTreat) continue;
            var distance = actor.Position.DistanceTo(position);
            if (distance > bestDistance) continue;
            bestDistance = distance;
            best = actor;
        }
        return best;
    }

    private void ReleaseChasers(Vector2 position)
    {
        foreach (var actor in _actors.Values)
        {
            if (!GodotObject.IsInstanceValid(actor) || !actor.IsChasingTreat) continue;
            if (actor.TreatTarget.DistanceTo(position) > 0.01f) continue;
            actor.StopChasingTreat();
        }
    }

    /// <summary>
    /// The food half of the eating beat: three 30% bites while the whole thing fades, then gone.
    /// </summary>
    private void PlayFoodShrink(Node2D holder, Node2D sprite)
    {
        var scale = sprite.Scale;
        var bites = CreateTween();
        for (var bite = 1; bite <= 3; bite++)
        {
            scale *= 0.70f;
            bites.TweenProperty(sprite, "scale", scale, TreatEatingSeconds / 3.0f)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        }
        var fade = CreateTween();
        fade.TweenProperty(sprite, "modulate:a", 0.0f, TreatEatingSeconds)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
        bites.Finished += () => { if (GodotObject.IsInstanceValid(holder)) holder.QueueFree(); };
    }

    /// <summary>
    /// The squish, on its own so the inspector's Give treat shows the same thing. Spamming it
    /// restarts the beat rather than stacking tweens on one sprite.
    /// </summary>
    public void PlayTreatEating(string creatureId, string statId, bool spawnFood)
    {
        if (!_actors.TryGetValue(creatureId, out var actor) || !GodotObject.IsInstanceValid(actor))
            return;

        PlayEatingSquish(actor);
        if (!spawnFood)
            return;

        var holder = new Node2D { Position = new Vector2(0, 6), ZIndex = 3 };
        var sprite = CreateTreatSprite(statId);
        holder.AddChild(sprite);
        actor.AddChild(holder);
        PlayFoodShrink(holder, sprite);
    }

    private void PlayEatingSquish(VoidlingActor actor)
    {
        var creatureId = actor.CreatureId;
        if (_eatingTweens.TryGetValue(creatureId, out var running) && running is { } previous &&
            GodotObject.IsInstanceValid(previous) && previous.IsRunning())
        {
            previous.Kill();
        }

        actor.BeginEating(TreatEatingSeconds);
        var squish = CreateTween();
        squish.SetLoops(6);
        squish.TweenMethod(Callable.From<float>(actor.SetEatingSquish), 0.0f, 1.0f, TreatEatingSeconds / 12.0f)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        squish.TweenMethod(Callable.From<float>(actor.SetEatingSquish), 1.0f, 0.0f, TreatEatingSeconds / 12.0f)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        _eatingTweens[creatureId] = squish;
        squish.Finished += () =>
        {
            if (GodotObject.IsInstanceValid(actor)) actor.EndEating();
            _eatingTweens.Remove(creatureId);
        };
    }

    /// <summary>
    /// A Voidling breaking into a stride on running ground. Reuses the Garden's own dust so a
    /// training hex looks like the same world the drop and the landing do.
    /// </summary>
    private void OnRunningStride(VoidlingActor actor)
    {
        if (!GodotObject.IsInstanceValid(actor))
            return;
        SpawnDust(actor.Position + new Vector2(0, 4));
    }

    // ---- probe seams ------------------------------------------------------------------------
    // The CI smoke drives the drop directly instead of synthesising a click at a world position
    // it would have to compute itself.

    internal void DropTreatForProbe(string statId, Vector2 position)
    {
        if (_session.DropTreat(statId, position.X, position.Y) != null)
            WakeVoidlingsFor(position);
    }

    internal Vector2 ActorPositionForProbe(string creatureId)
        => _actors.TryGetValue(creatureId, out var actor) && GodotObject.IsInstanceValid(actor)
            ? actor.Position
            : Vector2.Zero;

    internal int ChasingCountForProbe()
        => _actors.Values.Count(actor => GodotObject.IsInstanceValid(actor) && actor.IsChasingTreat);

    internal bool AnyVoidlingEatingForProbe()
        => _actors.Values.Any(actor => GodotObject.IsInstanceValid(actor) && actor.IsEating);

    private static Sprite2D CreateTreatSprite(string statId) => new()
    {
        Texture = new AtlasTexture { Atlas = TreatAtlas, Region = StatPresentationCatalog.TreatRegionFor(statId) },
        Scale = Vector2.One * 0.75f,
        ZIndex = 2
    };
}
