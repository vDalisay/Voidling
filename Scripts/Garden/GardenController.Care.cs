using Godot;

namespace VoidlingGame;

public partial class GardenController
{
    private const ulong PetDoubleClickWindowMilliseconds = 360;
    private const float ThrowGestureSpeedThreshold = 900.0f;
    private const ulong ThrowGestureFreshnessMilliseconds = 140;

    private string _lastCompletedClickId = string.Empty;
    private ulong _lastCompletedClickMilliseconds;
    private string _throwGestureCreatureId = string.Empty;
    private float _throwGestureSpeed;
    private ulong _lastThrowMotionMilliseconds;

    public override void _Input(InputEvent inputEvent)
    {
        if (!_inputEnabled || _draggedId.Length == 0)
        {
            ResetThrowGesture();
            return;
        }

        if (!string.Equals(_throwGestureCreatureId, _draggedId, System.StringComparison.Ordinal))
        {
            _throwGestureCreatureId = _draggedId;
            _throwGestureSpeed = 0.0f;
            _lastThrowMotionMilliseconds = 0;
        }

        if (inputEvent is InputEventMouseMotion motion)
        {
            _throwGestureSpeed = motion.Velocity.Length();
            _lastThrowMotionMilliseconds = Time.GetTicksMsec();
            return;
        }

        if (inputEvent is not InputEventMouseButton mouse ||
            mouse.ButtonIndex != MouseButton.Left ||
            mouse.Pressed)
        {
            return;
        }

        var creatureId = _draggedId;
        var now = Time.GetTicksMsec();
        var fresh = _lastThrowMotionMilliseconds > 0 &&
                    now >= _lastThrowMotionMilliseconds &&
                    now - _lastThrowMotionMilliseconds <= ThrowGestureFreshnessMilliseconds;
        var wasThrown = fresh && _throwGestureSpeed >= ThrowGestureSpeedThreshold;
        ResetThrowGesture();

        if (!wasThrown || !_session.MistreatVoidling(creatureId) || !_actors.TryGetValue(creatureId, out var actor))
            return;

        PlayMistreatmentReaction(actor);
    }

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

    private void ResetThrowGesture()
    {
        _throwGestureCreatureId = string.Empty;
        _throwGestureSpeed = 0.0f;
        _lastThrowMotionMilliseconds = 0;
    }

    private void PlayMistreatmentReaction(VoidlingActor actor)
    {
        var marker = UiFactory.CreateLabel("!", 10);
        marker.Position = new Vector2(-3, -31);
        marker.AddThemeColorOverride("font_color", Color.FromHtml("#C64F55"));
        marker.ZIndex = 65;
        actor.AddChild(marker);

        var tween = CreateTween().SetParallel(true);
        tween.TweenProperty(marker, "position", marker.Position + new Vector2(0, -10), 0.36)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(marker, "modulate:a", 0.0f, 0.20).SetDelay(0.28);
        tween.Finished += marker.QueueFree;
    }
}
