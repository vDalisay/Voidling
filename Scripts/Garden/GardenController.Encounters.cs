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

    /// <summary>
    /// The sprint every Voidling in an encounter runs at, as a multiple of its own roaming speed.
    /// It is deliberately the same number for chaser and quarry: roaming speed already carries the
    /// Run stat, so who outruns whom is decided by the creatures rather than by their role here.
    /// </summary>
    private const float SprintSpeedMultiplier = 2.4f;

    /// <summary>Close enough for a chaser to land on the one it is after.</summary>
    private const float CatchRadius = 11.0f;

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

    /// <summary>
    /// Both walk up to each other and stand shoulder to shoulder, and hearts rise between them.
    /// They meet side by side on one ground line rather than at the midpoint of however they were
    /// standing, so neither ends up hovering above the other while the hearts go up between them.
    /// </summary>
    private async Task PlayGreetingEncounter(VoidlingActor a, VoidlingActor b)
    {
        var midpoint = (a.Position + b.Position) * 0.5f;
        var (left, right) = a.Position.X <= b.Position.X ? (a, b) : (b, a);

        left.BeginScriptedRun(ClampToLand(new Vector2(midpoint.X - 11.0f, midpoint.Y)), 1.3f, dust: false);
        right.BeginScriptedRun(ClampToLand(new Vector2(midpoint.X + 11.0f, midpoint.Y)), 1.3f, dust: false);
        if (!await AwaitArrival(a, b))
            return;

        // Arriving is judged within a few pixels, which is close enough for walking somewhere and
        // not close enough for standing together: settle both onto one ground line so neither is
        // left floating above the other while the hearts go up between them.
        var groundLine = (left.Position.Y + right.Position.Y) * 0.5f;
        left.Position = ClampToLand(new Vector2(left.Position.X, groundLine));
        right.Position = ClampToLand(new Vector2(right.Position.X, groundLine));

        left.HoldScripted(right.Position);
        right.HoldScripted(left.Position);

        var between = new Vector2((left.Position.X + right.Position.X) * 0.5f, groundLine);
        for (var i = 0; i < 3; i++)
            SpawnHeartAt(between, -3.0f + i * 3.0f, i * 0.18);

        await AwaitSeconds(1.4f);
    }

    /// <summary>
    /// One hops and gives chase while the other bolts. Both run at their own speed, so a faster
    /// Voidling really does run the other down; catching it turns into a pounce and the one that
    /// was caught bolts again.
    /// </summary>
    private async Task PlayChaseEncounter(VoidlingActor chaser, VoidlingActor runner)
    {
        chaser.BeginScriptedRun(chaser.Position, 1.0f, dust: false);
        chaser.HoldScripted(runner.Position);
        runner.BeginScriptedRun(runner.Position, 1.0f, dust: false);
        runner.HoldScripted(chaser.Position);
        chaser.PlayHop();
        await AwaitSeconds(0.36f);
        if (!BothValid(chaser, runner))
            return;

        // The hop is what sets the other one off.
        runner.BeginScriptedRun(FleeTargetFrom(runner.Position, chaser.Position), SprintSpeedMultiplier, dust: true);
        if (!await AwaitChase(chaser, runner, _encounterRng.RandfRange(4.0f, 9.0f)))
            return;

        await PlayPounceOn(chaser, runner);
    }

    /// <summary>One runs the other down and pounces on it; the other is startled and turns tail.</summary>
    private async Task PlayPounceEncounter(VoidlingActor pouncer, VoidlingActor target)
    {
        pouncer.BeginScriptedRun(target.Position, SprintSpeedMultiplier, dust: true);
        target.BeginScriptedRun(target.Position, 1.0f, dust: false);
        target.HoldScripted(pouncer.Position);
        if (!await AwaitArrival(pouncer, target))
            return;

        await PlayPounceOn(pouncer, target);
    }

    /// <summary>
    /// The landing itself: the pouncer jumps onto the one it reached, which is startled into a hop,
    /// turns around and bolts. Shared by the pounce encounter and by a chase that ends in a catch.
    /// </summary>
    private async Task PlayPounceOn(VoidlingActor pouncer, VoidlingActor target)
    {
        if (!BothValid(pouncer, target))
            return;

        pouncer.HoldScripted(target.Position);
        pouncer.PlayHop(18.0f, 0.42);
        await AwaitSeconds(0.30f);
        if (!BothValid(pouncer, target))
            return;

        SpawnDust(target.Position + new Vector2(0, 4));
        target.PlayHop(16.0f, 0.40);
        target.FaceAwayFrom(pouncer.Position);
        await AwaitSeconds(0.42f);
        if (!BothValid(pouncer, target))
            return;

        await AwaitFlight(target, pouncer.Position, _encounterRng.RandfRange(2.5f, 5.5f));
    }

    /// <summary>
    /// The pursuit. The chaser re-aims at where its quarry is now on every frame rather than at
    /// where it set off from, which is also what keeps its legs moving: a chaser pointed at a spot
    /// its quarry has already left arrives, stops, and stands there mid-chase.
    /// Returns true when it actually catches up.
    /// </summary>
    private async Task<bool> AwaitChase(VoidlingActor chaser, VoidlingActor runner, float seconds)
    {
        var elapsed = 0.0f;
        while (elapsed < seconds)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!IsInsideTree() || !BothValid(chaser, runner) || !chaser.IsScripted || !runner.IsScripted)
                return false;

            if (chaser.Position.DistanceTo(runner.Position) <= CatchRadius)
                return true;

            KeepFleeing(runner, chaser.Position);
            chaser.BeginScriptedRun(runner.Position, SprintSpeedMultiplier, dust: true);
            elapsed += (float)GetProcessDeltaTime();
        }

        return false;
    }

    /// <summary>Bolting away from something for a while, picking a new bolt-hole on arrival.</summary>
    private async Task AwaitFlight(VoidlingActor runner, Vector2 threat, float seconds)
    {
        runner.BeginScriptedRun(FleeTargetFrom(runner.Position, threat), SprintSpeedMultiplier, dust: true);

        var elapsed = 0.0f;
        while (elapsed < seconds)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!IsInsideTree() || !GodotObject.IsInstanceValid(runner) || !runner.IsScripted)
                return;

            KeepFleeing(runner, threat);
            elapsed += (float)GetProcessDeltaTime();
        }
    }

    /// <summary>Sends a bolting Voidling somewhere new once it reaches where it was headed.</summary>
    private void KeepFleeing(VoidlingActor runner, Vector2 threat)
    {
        if (runner.ScriptedArrived)
            runner.BeginScriptedRun(FleeTargetFrom(runner.Position, threat), SprintSpeedMultiplier, dust: true);
    }

    private static bool BothValid(VoidlingActor a, VoidlingActor b)
        => GodotObject.IsInstanceValid(a) && GodotObject.IsInstanceValid(b);

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
