using Godot;

namespace VoidlingGame;

public partial class GardenController
{
    private const ulong PetDoubleClickWindowMilliseconds = 360;

    private string _lastCompletedClickId = string.Empty;
    private ulong _lastCompletedClickMilliseconds;

    private void HandleCompletedVoidlingClick(string creatureId)
    {
        var now = Time.GetTicksMsec();
        var isPet = string.Equals(
                        _lastCompletedClickId,
                        creatureId,
                        System.StringComparison.Ordinal) &&
                    now >= _lastCompletedClickMilliseconds &&
                    now - _lastCompletedClickMilliseconds <= PetDoubleClickWindowMilliseconds;

        Select(creatureId);
        VoidlingSelected?.Invoke(creatureId);

        if (!isPet)
        {
            _lastCompletedClickId = creatureId;
            _lastCompletedClickMilliseconds = now;
            return;
        }

        _lastCompletedClickId = string.Empty;
        _lastCompletedClickMilliseconds = 0;
        if (!_session.PetVoidling(creatureId) || !_actors.TryGetValue(creatureId, out var actor))
            return;

        SpawnHeartParticle(actor, -4.0f, 0.0);
        SpawnHeartParticle(actor, 0.0f, 0.08);
        SpawnHeartParticle(actor, 4.0f, 0.16);
    }
}
