using System;
using System.Linq;
using Godot;

namespace VoidlingGame;

/// <summary>
/// Reconciles the useful Garden-roaming portion of the old implementation-plan branch with the
/// current Garden/hex-training architecture. Effective Run changes free-roam speed and Stamina
/// changes how long a free-roaming Voidling pauses between destinations. Training-tile activity
/// loops remain continuous.
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

            // The old branch also caught a real lifecycle presentation bug: an actor that matures
            // must be rebuilt so adult scale, hitbox, movement baseline and current appearance apply.
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
            actor.ApplyAmbientStats(run, stamina);
        }

        // A rebuilt actor must immediately rejoin its authored training tile rather than waiting for
        // the next unrelated state change. This preserves the current hex-land behavior.
        if (replacedStageActor)
            RefreshTileResidents();
    }
}
