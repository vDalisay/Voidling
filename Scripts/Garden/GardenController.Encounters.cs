using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Voidling.Presentation.Voidlings;

namespace VoidlingGame;

/// <summary>
/// Chance encounters between two free-roaming Voidlings. Every so often a pair that happens to be
/// walking near each other is pulled out of its own roaming, plays a short scripted beat, and is
/// handed straight back. Purely presentation: nothing here touches training, breeding, persistence
/// or the race simulation, and a Voidling that is training, eating, held or already in an encounter
/// is never picked.
/// </summary>
public partial class GardenController
{
    /// <summary>How close two wanderers must be to notice each other, in world pixels.</summary>
    private const float EncounterNoticeRadius = 70.0f;

    /// <summary>How long an encounter runs at most before both Voidlings are released anyway.</summary>
    private const float EncounterTimeoutSeconds = 8.0f;

    private float _encounterCooldownSeconds = 5.0f;
    private readonly RandomNumberGenerator _encounterRng = new();

    /// <summary>Runs every frame from the Garden's own process, beside the treat drops.</summary>
    private void UpdateEncounters(float step)
    {
        _encounterCooldownSeconds -= step;
        if (_encounterCooldownSeconds > 0.0f)
            return;

        _encounterCooldownSeconds = _encounterRng.RandfRange(7.0f, 16.0f);

        var pair = PickEncounterPair();
        if (pair == null)
            return;

        _ = PlayEncounter(pair.Value.First, pair.Value.Second, GardenEncounterRoll.Pick(_encounterRng.Randf()));
    }

    /// <summary>
    /// A random pair of wanderers close enough to notice each other. All candidate pairs are
    /// gathered rather than only the closest, so the same two neighbours do not monopolise the
    /// Garden's encounters.
    /// </summary>
    private (VoidlingActor First, VoidlingActor Second)? PickEncounterPair()
    {
        var candidates = _actors.Values
            .Where(actor => GodotObject.IsInstanceValid(actor) && actor.IsAvailableForEncounter)
            .ToList();
        if (candidates.Count < 2)
            return null;

        var pairs = new List<(VoidlingActor, VoidlingActor)>();
        for (var i = 0; i < candidates.Count; i++)
        {
            for (var j = i + 1; j < candidates.Count; j++)
            {
                if (candidates[i].Position.DistanceTo(candidates[j].Position) <= EncounterNoticeRadius)
                    pairs.Add((candidates[i], candidates[j]));
            }
        }

        if (pairs.Count == 0)
            return null;

        // Either one can be the instigator, so the roll picks the order too.
        var (a, b) = pairs[_encounterRng.RandiRange(0, pairs.Count - 1)];
        return _encounterRng.Randf() < 0.5f ? (a, b) : (b, a);
    }

    private async Task PlayEncounter(VoidlingActor instigator, VoidlingActor other, GardenEncounterKind kind)
    {
        try
        {
            switch (kind)
            {
                case GardenEncounterKind.Greet:
                    await PlayGreetingEncounter(instigator, other);
                    break;
                case GardenEncounterKind.Chase:
                    await PlayChaseEncounter(instigator, other);
                    break;
                default:
                    await PlayPounceEncounter(instigator, other);
                    break;
            }
        }
        finally
        {
            ReleaseFromEncounter(instigator);
            ReleaseFromEncounter(other);
        }
    }

    private static void ReleaseFromEncounter(VoidlingActor actor)
    {
        if (GodotObject.IsInstanceValid(actor))
            actor.EndScripted();
    }

    /// <summary>Both walk to the midpoint, stand facing each other, and hearts rise between them.</summary>
    private async Task PlayGreetingEncounter(VoidlingActor a, VoidlingActor b)
    {
        var midpoint = (a.Position + b.Position) * 0.5f;
        var facing = (b.Position - a.Position).Normalized();
        if (facing.LengthSquared() < 0.01f)
            facing = Vector2.Right;

        a.BeginScriptedRun(midpoint - facing * 11.0f, 1.3f, dust: false);
        b.BeginScriptedRun(midpoint + facing * 11.0f, 1.3f, dust: false);
        if (!await AwaitArrival(a, b))
            return;

        a.HoldScripted(b.Position);
        b.HoldScripted(a.Position);

        var between = (a.Position + b.Position) * 0.5f;
        for (var i = 0; i < 3; i++)
            SpawnHeartAt(between, -3.0f + i * 3.0f, i * 0.18);

        await AwaitSeconds(1.4f);
    }

    /// <summary>One hops, gives chase, and the other bolts for somewhere else on the island.</summary>
    private async Task PlayChaseEncounter(VoidlingActor chaser, VoidlingActor runner)
    {
        chaser.BeginScriptedRun(chaser.Position, 1.0f, dust: false);
        chaser.HoldScripted(runner.Position);
        chaser.PlayHop();
        await AwaitSeconds(0.36f);
        if (!GodotObject.IsInstanceValid(chaser) || !GodotObject.IsInstanceValid(runner))
            return;

        for (var leg = 0; leg < 3; leg++)
        {
            if (!GodotObject.IsInstanceValid(chaser) || !GodotObject.IsInstanceValid(runner))
                return;

            runner.BeginScriptedRun(FleeTargetFrom(runner.Position, chaser.Position), 2.4f, dust: true);
            chaser.BeginScriptedRun(runner.Position, 2.1f, dust: true);
            await AwaitSeconds(0.9f);

            // The chaser aims where the runner is now rather than where it set off from, so the
            // pursuit stays on its heels instead of trailing to an abandoned spot.
            if (GodotObject.IsInstanceValid(chaser) && GodotObject.IsInstanceValid(runner))
                chaser.BeginScriptedRun(runner.Position, 2.1f, dust: true);
        }
    }

    /// <summary>One pounces onto the other, who is startled, jumps and turns tail.</summary>
    private async Task PlayPounceEncounter(VoidlingActor pouncer, VoidlingActor target)
    {
        pouncer.BeginScriptedRun(target.Position, 2.2f, dust: true);
        target.BeginScriptedRun(target.Position, 1.0f, dust: false);
        target.HoldScripted(pouncer.Position);
        if (!await AwaitArrival(pouncer, target))
            return;

        pouncer.HoldScripted(target.Position);
        pouncer.PlayHop(18.0f, 0.42);
        await AwaitSeconds(0.30f);
        if (!GodotObject.IsInstanceValid(pouncer) || !GodotObject.IsInstanceValid(target))
            return;

        SpawnDust(target.Position + new Vector2(0, 4));
        target.PlayHop(16.0f, 0.40);
        target.FaceAwayFrom(pouncer.Position);
        await AwaitSeconds(0.42f);
        if (!GodotObject.IsInstanceValid(pouncer) || !GodotObject.IsInstanceValid(target))
            return;

        target.BeginScriptedRun(FleeTargetFrom(target.Position, pouncer.Position), 2.3f, dust: true);
        await AwaitSeconds(1.1f);
    }

    /// <summary>Somewhere on the island directly away from whatever just startled this Voidling.</summary>
    private Vector2 FleeTargetFrom(Vector2 position, Vector2 threat)
    {
        var away = position - threat;
        if (away.LengthSquared() < 0.01f)
            away = Vector2.Right.Rotated(_encounterRng.RandfRange(0.0f, Mathf.Tau));
        away = away.Normalized().Rotated(_encounterRng.RandfRange(-0.7f, 0.7f));
        return ClampToLand(position + away * _encounterRng.RandfRange(70.0f, 130.0f));
    }

    /// <summary>
    /// Waits for both Voidlings to reach their scripted destinations. Returns false when the
    /// encounter should be abandoned: one of them was freed, or something else - food, the player's
    /// hand - took it out of the encounter before it got there.
    /// </summary>
    private async Task<bool> AwaitArrival(VoidlingActor a, VoidlingActor b)
    {
        var elapsed = 0.0f;
        while (elapsed < EncounterTimeoutSeconds)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!IsInsideTree())
                return false;
            if (!GodotObject.IsInstanceValid(a) || !GodotObject.IsInstanceValid(b))
                return false;
            if (!a.IsScripted || !b.IsScripted)
                return false;
            if (a.ScriptedArrived && b.ScriptedArrived)
                return true;
            elapsed += (float)GetProcessDeltaTime();
        }

        return false;
    }

    private async Task AwaitSeconds(float seconds)
        => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

    // ---- probe seam -------------------------------------------------------------------------

    /// <summary>
    /// Stands two free-roaming Voidlings next to each other and starts an encounter this frame, so
    /// the CI smoke does not have to wait for the pair and the cooldown to line up on their own.
    /// Returns false when the Garden has fewer than two Voidlings free to take part.
    /// </summary>
    internal bool StartEncounterForProbe()
    {
        var free = _actors.Values
            .Where(actor => GodotObject.IsInstanceValid(actor) && actor.IsAvailableForEncounter)
            .Take(2)
            .ToList();
        if (free.Count < 2)
            return false;

        free[1].Position = ClampToLand(free[0].Position + new Vector2(24.0f, 0.0f));
        _encounterCooldownSeconds = 0.0f;
        UpdateEncounters(0.0f);
        return true;
    }

    internal int ScriptedCountForProbe()
        => _actors.Values.Count(actor => GodotObject.IsInstanceValid(actor) && actor.IsScripted);
}
