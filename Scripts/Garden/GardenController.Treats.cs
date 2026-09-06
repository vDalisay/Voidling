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

    private sealed class TreatDrop
    {
        public required string StatId { get; init; }
        public required Node2D Holder { get; init; }
        public required Sprite2D Sprite { get; init; }
        public bool Claimed { get; set; }
    }

    private readonly List<TreatDrop> _treatDrops = new();
    private readonly Dictionary<string, Tween> _eatingTweens = new(StringComparer.Ordinal);
    private string _placingTreatStatId = "";
    private Node2D? _treatGhost;

    public bool IsPlacingTreat => _placingTreatStatId.Length > 0;

    /// <summary>How many treats of one stat are already lying on the ground unclaimed.</summary>
    public int DroppedTreatCount(string statId)
        => _treatDrops.Count(drop => string.Equals(drop.StatId, statId, StringComparison.Ordinal));

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
        DropTreat(statId, position);
        return true;
    }

    private void DropTreat(string statId, Vector2 position)
    {
        var holder = new Node2D { Position = position, ZIndex = 6 };
        var sprite = CreateTreatSprite(statId);
        holder.AddChild(sprite);
        _eggsRoot.AddChild(holder);
        _treatDrops.Add(new TreatDrop { StatId = statId, Holder = holder, Sprite = sprite });

        // A small hop so the treat reads as having been dropped rather than having always been there.
        holder.Scale = Vector2.Zero;
        var drop = CreateTween();
        drop.TweenProperty(holder, "scale", Vector2.One, 0.32)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);

        WakeVoidlingsFor(holder.Position);
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
        if (_treatDrops.Count == 0)
            return;

        for (var index = _treatDrops.Count - 1; index >= 0; index--)
        {
            var drop = _treatDrops[index];
            if (!GodotObject.IsInstanceValid(drop.Holder))
            {
                _treatDrops.RemoveAt(index);
                continue;
            }
            if (drop.Claimed)
                continue;

            var eater = NearestChaserWithinReach(drop.Holder.Position);
            if (eater == null)
                continue;

            drop.Claimed = true;
            _treatDrops.RemoveAt(index);
            ReleaseChasers(drop.Holder.Position);
            EatTreat(eater, drop.StatId, drop.Holder, drop.Sprite);
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
    /// The eating beat: the Voidling squishes for three seconds while the food shrinks and fades
    /// 30% inward each pass until nothing is left. The session applies the actual training.
    /// </summary>
    private void EatTreat(VoidlingActor eater, string statId, Node2D food, Node2D sprite)
    {
        var creatureId = eater.CreatureId;
        PlayEatingSquish(eater);

        var shrink = CreateTween();
        shrink.SetParallel(true);
        // Three 30% steps read as bites rather than one smooth fade.
        var sequence = CreateTween();
        var scale = sprite.Scale;
        for (var bite = 1; bite <= 3; bite++)
        {
            scale *= 0.70f;
            sequence.TweenProperty(sprite, "scale", scale, TreatEatingSeconds / 3.0f)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        }
        shrink.TweenProperty(sprite, "modulate:a", 0.0f, TreatEatingSeconds)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
        sequence.Finished += () =>
        {
            if (GodotObject.IsInstanceValid(food)) food.QueueFree();
            TreatEaten?.Invoke(creatureId, statId);
        };
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

        var sprite = CreateTreatSprite(statId);
        sprite.Position = new Vector2(0, 6);
        sprite.ZIndex = 3;
        actor.AddChild(sprite);
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
        bites.Finished += () => { if (GodotObject.IsInstanceValid(sprite)) sprite.QueueFree(); };
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

    internal void DropTreatForProbe(string statId, Vector2 position) => DropTreat(statId, position);

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
