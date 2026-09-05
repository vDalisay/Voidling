using System;
using System.Linq;
using Godot;

namespace VoidlingGame;

/// <summary>
/// Reconciles the useful Garden-roaming portion of the old implementation-plan branch with the
/// current Garden/land architecture. Effective Run changes free-roam speed, Stamina changes pause
/// duration, and Swim subtly biases free-roam destinations toward the island shoreline. Training
/// activity loops remain continuous and none of this presentation state feeds race outcomes.
/// </summary>
public partial class GardenController
{
    private bool _ambientBehaviorInstalled;

    private void EnsureAmbientBehaviorInstalled()
    {
        if (_ambientBehaviorInstalled ||
            _session == null ||
            !GodotObject.IsInstanceValid(_session) ||
            _actorsRoot == null ||
            !GodotObject.IsInstanceValid(_actorsRoot))
        {
            return;
        }

        _ambientBehaviorInstalled = true;
        _session.StateChanged += RefreshAmbientBehavior;
        TreeExiting += DetachAmbientBehavior;
        RefreshAmbientBehavior();
    }

    private void DetachAmbientBehavior()
    {
        if (!_ambientBehaviorInstalled)
            return;

        _ambientBehaviorInstalled = false;
        if (_session != null && GodotObject.IsInstanceValid(_session))
            _session.StateChanged -= RefreshAmbientBehavior;
        TreeExiting -= DetachAmbientBehavior;
    }

    private void RefreshAmbientBehavior()
    {
        if (_session == null || !GodotObject.IsInstanceValid(_session))
            return;

        var replacedStageActor = false;
        foreach (var data in _session.State.Voidlings)
        {
            if (!_actors.TryGetValue(data.Id, out var actor) || !GodotObject.IsInstanceValid(actor))
                continue;

            // A stage change rebuilds the actor so its adult/child scale, hitbox and appearance refresh.
            if (actor.Stage != data.Stage)
            {
                var position = actor.Position;
                var pickedUp = string.Equals(_draggedId, data.Id, StringComparison.Ordinal);
                actor.QueueFree();

                actor = new VoidlingActor();
                actor.Setup(data, _landBounds, position);
                actor.LandClamp = ClampToLand;
                actor.Clicked += OnActorPressed;
                _actorsRoot.AddChild(actor);
                _actors[data.Id] = actor;

                actor.SetSelected(string.Equals(_selectedId, data.Id, StringComparison.Ordinal));
                if (pickedUp)
                    actor.SetPickedUp(true);
                if (string.Equals(_followId, data.Id, StringComparison.Ordinal))
                    _camera.Position = actor.Position;

                replacedStageActor = true;
            }

            var profile = _session.CreateCreatureProfileProjection(data.Id);
            if (profile == null)
                continue;

            var run = profile.Stats.FirstOrDefault(stat =>
                string.Equals(stat.StatId, "run", StringComparison.Ordinal))?.EffectiveValue ?? 0;
            var stamina = profile.Stats.FirstOrDefault(stat =>
                string.Equals(stat.StatId, "stamina", StringComparison.Ordinal))?.EffectiveValue ?? 0;
            var swim = profile.Stats.FirstOrDefault(stat =>
                string.Equals(stat.StatId, "swim", StringComparison.Ordinal))?.EffectiveValue ?? 0;
            actor.ApplyAmbientStats(run, stamina, swim);
        }

        // A rebuilt actor must immediately rejoin its authored training tile rather than waiting for
        // the next unrelated state change. This preserves the current land-training behavior.
        if (replacedStageActor)
            RefreshTileResidents();
    }
}
