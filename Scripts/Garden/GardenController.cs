using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace VoidlingGame;

public partial class GardenController : Node2D
{
    public event Action<string>? VoidlingSelected;

    private const float HoldToPickUpSeconds = 0.16f;
    private const float EggBaseScale = 1.45f;

    private static readonly Texture2D EggTexture = GD.Load<Texture2D>(
        "res://Assets/Sprout Lands - Sprites - Basic pack/Objects/Egg item.png");

    private readonly Dictionary<string, VoidlingActor> _actors = new(StringComparer.Ordinal);
    private readonly Dictionary<string, EggVisual> _eggVisuals = new(StringComparer.Ordinal);
    private readonly Rect2 _wanderBounds = new(new Vector2(72, 76), new Vector2(688, 330));

    private GameSession _session = null!;
    private Node2D _actorsRoot = null!;
    private Node2D _eggsRoot = null!;
    private Camera2D _camera = null!;
    private string _selectedId = "";
    private string _followId = "";
    private string _pendingGrabId = "";
    private string _draggedId = "";
    private float _pendingGrabSeconds;
    private int _spawnIndex;
    private bool _cameraDragging;
    private bool _inputEnabled = true;
    private bool _initialRefreshComplete;
    private float _zoomTarget = 1.0f;

    private sealed class EggVisual
    {
        public Node2D Holder { get; init; } = null!;
        public Sprite2D Sprite { get; init; } = null!;
        public Label Label { get; init; } = null!;
    }

    public override void _Ready()
    {
        _session = GetNode<GameSession>("/root/GameBootstrap/GameSession");
        _actorsRoot = GetNode<Node2D>("Actors");
        _eggsRoot = GetNode<Node2D>("Eggs");
        _camera = GetNode<Camera2D>("Camera2D");
        _zoomTarget = _camera.Zoom.X;

        _session.StateChanged += Refresh;
        Refresh();
        _initialRefreshComplete = true;
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(_session))
            _session.StateChanged -= Refresh;
        Input.SetDefaultCursorShape(Input.CursorShape.Arrow);
    }

    public override void _Process(double delta)
    {
        UpdateEggPulse();

        var zoomBlend = 1.0f - Mathf.Exp(-12.0f * (float)delta);
        var zoom = Mathf.Lerp(_camera.Zoom.X, _zoomTarget, zoomBlend);
        if (Mathf.Abs(zoom - _zoomTarget) < 0.001f)
            zoom = _zoomTarget;
        _camera.Zoom = new Vector2(zoom, zoom);

        if (!_inputEnabled)
            return;

        if (_followId.Length > 0 && _actors.TryGetValue(_followId, out var followed) && !_cameraDragging)
            _camera.Position = followed.Position;

        if (_draggedId.Length > 0)
        {
            if (!Input.IsMouseButtonPressed(MouseButton.Left))
            {
                DropGrabbedVoidling();
                return;
            }

            if (_actors.TryGetValue(_draggedId, out var dragged))
                dragged.Position = ClampToGarden(_actorsRoot.ToLocal(GetGlobalMousePosition()));
            return;
        }

        if (_pendingGrabId.Length == 0)
            return;

        if (!Input.IsMouseButtonPressed(MouseButton.Left))
        {
            ClearPendingGrab();
            return;
        }

        _pendingGrabSeconds += (float)delta;
        if (_pendingGrabSeconds >= HoldToPickUpSeconds)
            StartGrab();
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (!_inputEnabled)
            return;

        if (inputEvent is InputEventMouseButton mouse)
        {
            if (mouse.ButtonIndex == MouseButton.Left && !mouse.Pressed &&
                (_draggedId.Length > 0 || _pendingGrabId.Length > 0))
            {
                if (_draggedId.Length > 0)
                    DropGrabbedVoidling();
                else
                    ClearPendingGrab();

                GetViewport().SetInputAsHandled();
                return;
            }

            // RMB owns garden camera dragging so LMB remains free for interaction.
            if (mouse.ButtonIndex == MouseButton.Right)
            {
                _cameraDragging = mouse.Pressed;
                if (mouse.Pressed)
                    StopFollowing();
                GetViewport().SetInputAsHandled();
                return;
            }

            // Keep middle mouse as a secondary pan binding for desktop users.
            if (mouse.ButtonIndex == MouseButton.Middle)
            {
                _cameraDragging = mouse.Pressed;
                if (mouse.Pressed)
                    StopFollowing();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (mouse.Pressed && (mouse.ButtonIndex == MouseButton.WheelUp || mouse.ButtonIndex == MouseButton.WheelDown))
            {
                var factor = mouse.ButtonIndex == MouseButton.WheelUp ? 1.12f : 1.0f / 1.12f;
                _zoomTarget = Mathf.Clamp(_zoomTarget * factor, 0.70f, 2.35f);
                if (_followId.Length == 0)
                    ClampCamera();
                GetViewport().SetInputAsHandled();
                return;
            }
        }

        if (inputEvent is InputEventMouseMotion motion)
        {
            if (_draggedId.Length > 0)
            {
                if (_actors.TryGetValue(_draggedId, out var dragged))
                    dragged.Position = ClampToGarden(_actorsRoot.ToLocal(GetGlobalMousePosition()));
                GetViewport().SetInputAsHandled();
                return;
            }

            if (_cameraDragging)
            {
                _camera.Position -= motion.Relative / _camera.Zoom.X;
                ClampCamera();
                GetViewport().SetInputAsHandled();
            }
        }
    }

    public void Select(string creatureId)
    {
        _selectedId = creatureId;
        foreach (var pair in _actors)
            pair.Value.SetSelected(pair.Key == creatureId);
    }

    public void ClearSelection() => Select("");

    public void ResetCamera()
    {
        StopFollowing();
        _camera.Position = new Vector2(416, 240);
        _zoomTarget = 1.0f;
    }

    public void ToggleFollowVoidling(string creatureId)
    {
        if (_followId == creatureId)
        {
            StopFollowing();
            return;
        }

        if (!_actors.TryGetValue(creatureId, out var actor))
            return;

        _followId = creatureId;
        _camera.Position = actor.Position;
    }

    public void StopFollowing() => _followId = "";

    public bool IsFollowing(string creatureId) => _followId == creatureId;

    public void SetGameplayActive(bool active)
    {
        _inputEnabled = active;
        _cameraDragging = false;
        ClearPendingGrab();

        if (_draggedId.Length > 0)
            DropGrabbedVoidling();

        if (!active)
            StopFollowing();

        Input.SetDefaultCursorShape(Input.CursorShape.Arrow);
        _camera.Enabled = active;
    }

    public Vector2 GetActorWorldPosition(string creatureId)
        => _actors.TryGetValue(creatureId, out var actor) ? actor.Position : new Vector2(416, 240);

    public async void PlayBreedingAnimation(string parentAId, string parentBId, Action<Vector2> createEgg)
    {
        if (!_actors.TryGetValue(parentAId, out var a) || !_actors.TryGetValue(parentBId, out var b))
        {
            createEgg(new Vector2(416, 240));
            return;
        }

        a.SetInteractionLocked(true);
        b.SetInteractionLocked(true);

        var midpoint = (a.Position + b.Position) * 0.5f;
        var direction = b.Position - a.Position;
        if (direction.LengthSquared() < 0.01f)
            direction = Vector2.Right;
        direction = direction.Normalized();
        var perpendicular = new Vector2(-direction.Y, direction.X);

        var targetA = midpoint - direction * 14.0f;
        var targetB = midpoint + direction * 14.0f;

        a.PlayWalk(direction);
        b.PlayWalk(-direction);
        for (var i = 0; i < 4; i++)
        {
            SpawnHeartParticle(a, -3 + i * 2, i * 0.16);
            SpawnHeartParticle(b, 3 - i * 2, i * 0.16 + 0.07);
        }

        var approach = CreateTween().SetParallel(true);
        approach.TweenProperty(a, "position", targetA, 1.05)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        approach.TweenProperty(b, "position", targetB, 1.05)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        await ToSignal(approach, Tween.SignalName.Finished);

        for (var step = 0; step < 3; step++)
        {
            var sign = step % 2 == 0 ? 1.0f : -1.0f;
            a.PlayWalk(perpendicular * sign);
            b.PlayWalk(-perpendicular * sign);
            var dance = CreateTween().SetParallel(true);
            dance.TweenProperty(a, "position", targetA + perpendicular * 6.0f * sign, 0.18)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            dance.TweenProperty(b, "position", targetB - perpendicular * 6.0f * sign, 0.18)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            await ToSignal(dance, Tween.SignalName.Finished);
        }

        var settleDance = CreateTween().SetParallel(true);
        settleDance.TweenProperty(a, "position", targetA, 0.18);
        settleDance.TweenProperty(b, "position", targetB, 0.18);
        await ToSignal(settleDance, Tween.SignalName.Finished);
        a.PlayIdle();
        b.PlayIdle();

        var heart = UiFactory.CreateLabel("♥", 20);
        heart.Position = midpoint + new Vector2(-7, -34);
        heart.AddThemeColorOverride("font_color", Color.FromHtml("#E77B87"));
        heart.ZIndex = 50;
        AddChild(heart);

        var heartTween = CreateTween().SetParallel(true);
        heartTween.TweenProperty(heart, "position", heart.Position + new Vector2(0, -18), 0.85)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        heartTween.TweenProperty(heart, "scale", new Vector2(1.25f, 1.25f), 0.35)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        heartTween.TweenProperty(heart, "modulate:a", 0.0f, 0.85).SetDelay(0.25);
        await ToSignal(heartTween, Tween.SignalName.Finished);
        heart.QueueFree();

        var eggPosition = midpoint + new Vector2(0, 8);
        createEgg(eggPosition);

        var settle = CreateTween();
        settle.TweenInterval(0.55);
        await ToSignal(settle, Tween.SignalName.Finished);

        a.SetInteractionLocked(false);
        b.SetInteractionLocked(false);
    }

    private void Refresh()
    {
        var currentIds = _session.State.Voidlings
            .Select(v => v.Id)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var staleId in _actors.Keys.Where(id => !currentIds.Contains(id)).ToArray())
        {
            _actors[staleId].QueueFree();
            _actors.Remove(staleId);

            if (_followId == staleId)
                StopFollowing();
            if (_pendingGrabId == staleId)
                ClearPendingGrab();
            if (_draggedId == staleId)
                _draggedId = "";
        }

        foreach (var data in _session.State.Voidlings)
        {
            if (_actors.ContainsKey(data.Id))
                continue;

            var start = Math.Abs(data.WorldX) > 0.01f || Math.Abs(data.WorldY) > 0.01f
                ? new Vector2(data.WorldX, data.WorldY)
                : NextSpawnPosition();

            var actor = new VoidlingActor();
            actor.Setup(data, _wanderBounds, start);
            actor.Clicked += OnActorPressed;
            _actorsRoot.AddChild(actor);
            _actors[data.Id] = actor;

            if (_initialRefreshComplete && data.Stage == LifeStage.Child)
                actor.PlayHatchJump();
        }

        Select(_selectedId);
        RefreshEggs();
    }

    private void RefreshEggs()
    {
        var eggsById = _session.State.OwnedEggs.ToDictionary(e => e.Id, StringComparer.Ordinal);

        foreach (var staleId in _eggVisuals.Keys.Where(id => !eggsById.ContainsKey(id)).ToArray())
        {
            var visual = _eggVisuals[staleId];
            if (_initialRefreshComplete)
                SpawnEggBurst(visual.Holder.Position);
            visual.Holder.QueueFree();
            _eggVisuals.Remove(staleId);
        }

        foreach (var egg in _session.State.OwnedEggs)
        {
            if (!_eggVisuals.TryGetValue(egg.Id, out var visual))
            {
                var holder = new Node2D { Position = new Vector2(egg.WorldX, egg.WorldY), ZIndex = 5 };
                var sprite = new Sprite2D
                {
                    Texture = EggTexture,
                    Scale = Vector2.One * EggBaseScale,
                    Modulate = GameRules.TintColor(egg.TintHex),
                    ZIndex = 2
                };
                holder.AddChild(sprite);

                var label = UiFactory.CreateLabel("", 7);
                label.Position = new Vector2(-10, 9);
                label.AddThemeColorOverride("font_color", Color.FromHtml("#4F5948"));
                holder.AddChild(label);

                _eggsRoot.AddChild(holder);
                visual = new EggVisual { Holder = holder, Sprite = sprite, Label = label };
                _eggVisuals[egg.Id] = visual;

                if (_initialRefreshComplete)
                {
                    holder.Scale = Vector2.Zero;
                    var pop = CreateTween();
                    pop.TweenProperty(holder, "scale", Vector2.One, 0.46)
                        .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
                }
            }

            visual.Holder.Position = new Vector2(egg.WorldX, egg.WorldY);
            visual.Sprite.Modulate = egg.State == EggState.Failed
                ? new Color(0.55f, 0.55f, 0.55f, 1.0f)
                : GameRules.TintColor(egg.TintHex);
            var remaining = Math.Max(0, (int)Math.Ceiling(egg.RequiredIncubationSeconds - egg.IncubationSeconds));
            visual.Label.Text = egg.State == EggState.Failed ? "X" : $"{remaining}s";
        }
    }

    private void UpdateEggPulse()
    {
        var time = (float)Time.GetTicksMsec() / 1000.0f;
        foreach (var egg in _session.State.OwnedEggs)
        {
            if (!_eggVisuals.TryGetValue(egg.Id, out var visual))
                continue;

            if (egg.State == EggState.Failed || egg.RequiredIncubationSeconds <= 0.01f)
            {
                visual.Sprite.Scale = Vector2.One * EggBaseScale;
                continue;
            }

            var progress = Mathf.Clamp(egg.IncubationSeconds / egg.RequiredIncubationSeconds, 0.0f, 1.0f);
            var amplitude = 0.012f + progress * 0.065f;
            var frequency = 1.2f + progress * 5.0f;
            var pulse = 1.0f + Mathf.Sin(time * Mathf.Tau * frequency) * amplitude;
            visual.Sprite.Scale = Vector2.One * EggBaseScale * pulse;
        }
    }

    private void OnActorPressed(string creatureId)
    {
        Select(creatureId);
        VoidlingSelected?.Invoke(creatureId);
        _pendingGrabId = creatureId;
        _pendingGrabSeconds = 0.0f;
    }

    private void StartGrab()
    {
        var creatureId = _pendingGrabId;
        ClearPendingGrab();

        if (creatureId.Length == 0 || !_actors.TryGetValue(creatureId, out var actor))
            return;

        _draggedId = creatureId;
        Input.SetDefaultCursorShape(Input.CursorShape.Drag);
        actor.SetPickedUp(true);
        actor.Position = ClampToGarden(_actorsRoot.ToLocal(GetGlobalMousePosition()));
    }

    private void DropGrabbedVoidling()
    {
        var creatureId = _draggedId;
        _draggedId = "";
        Input.SetDefaultCursorShape(Input.CursorShape.Arrow);

        if (creatureId.Length == 0 || !_actors.TryGetValue(creatureId, out var actor))
            return;

        actor.Position = ClampToGarden(actor.Position);
        actor.SetPickedUp(false);
        SpawnDust(actor.Position + new Vector2(0, 4));
        _session.MoveVoidling(creatureId, actor.Position);
    }

    private void SpawnHeartParticle(VoidlingActor actor, float xOffset, double delay)
    {
        var heart = UiFactory.CreateLabel("♥", 8);
        heart.Position = new Vector2(xOffset - 3, -29);
        heart.Modulate = new Color(1, 1, 1, 0);
        heart.AddThemeColorOverride("font_color", Color.FromHtml("#EB8996"));
        heart.ZIndex = 60;
        actor.AddChild(heart);

        var tween = CreateTween().SetParallel(true);
        tween.TweenProperty(heart, "modulate:a", 1.0f, 0.08).SetDelay(delay);
        tween.TweenProperty(heart, "position", heart.Position + new Vector2(xOffset * 0.5f, -16), 0.65)
            .SetDelay(delay).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(heart, "modulate:a", 0.0f, 0.25).SetDelay(delay + 0.42);
        tween.Finished += heart.QueueFree;
    }

    private void SpawnDust(Vector2 position)
    {
        var holder = new Node2D { Position = position, ZIndex = 80 };
        AddChild(holder);
        var rng = new RandomNumberGenerator { Seed = (ulong)Time.GetTicksMsec() };

        for (var i = 0; i < 7; i++)
        {
            var puff = new Polygon2D
            {
                Polygon = new Vector2[] { new(-1.5f, -1.5f), new(1.5f, -1.5f), new(1.5f, 1.5f), new(-1.5f, 1.5f) },
                Color = Color.FromHtml(i % 2 == 0 ? "#E8D5A5" : "#C5B283"),
                Position = new Vector2(rng.RandfRange(-5, 5), rng.RandfRange(-1, 2))
            };
            holder.AddChild(puff);
            var target = puff.Position + new Vector2(rng.RandfRange(-10, 10), rng.RandfRange(-8, -3));
            var tween = CreateTween().SetParallel(true);
            tween.TweenProperty(puff, "position", target, 0.34).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(puff, "modulate:a", 0.0f, 0.34);
        }

        var cleanup = GetTree().CreateTimer(0.4);
        cleanup.Timeout += holder.QueueFree;
    }

    private void SpawnEggBurst(Vector2 position)
    {
        var holder = new Node2D { Position = position, ZIndex = 70 };
        AddChild(holder);
        var rng = new RandomNumberGenerator { Seed = (ulong)(Time.GetTicksMsec() + 91) };

        for (var i = 0; i < 10; i++)
        {
            var shard = new Polygon2D
            {
                Polygon = new Vector2[] { new(-2, -1), new(2, -1), new(1, 2), new(-1, 2) },
                Color = Color.FromHtml(i % 2 == 0 ? "#F5E7BD" : "#DAB889"),
                Position = Vector2.Zero,
                Rotation = rng.RandfRange(-1.5f, 1.5f)
            };
            holder.AddChild(shard);
            var target = new Vector2(rng.RandfRange(-18, 18), rng.RandfRange(-20, 8));
            var tween = CreateTween().SetParallel(true);
            tween.TweenProperty(shard, "position", target, 0.42).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(shard, "rotation", shard.Rotation + rng.RandfRange(-2.0f, 2.0f), 0.42);
            tween.TweenProperty(shard, "modulate:a", 0.0f, 0.42).SetDelay(0.12);
        }

        var cleanup = GetTree().CreateTimer(0.6);
        cleanup.Timeout += holder.QueueFree;
    }

    private void ClearPendingGrab()
    {
        _pendingGrabId = "";
        _pendingGrabSeconds = 0.0f;
    }

    private Vector2 ClampToGarden(Vector2 position)
        => new(
            Mathf.Clamp(position.X, _wanderBounds.Position.X, _wanderBounds.End.X),
            Mathf.Clamp(position.Y, _wanderBounds.Position.Y, _wanderBounds.End.Y));

    private Vector2 NextSpawnPosition()
    {
        var preset = new[]
        {
            new Vector2(300, 185), new Vector2(420, 210), new Vector2(250, 250),
            new Vector2(485, 160), new Vector2(360, 290), new Vector2(530, 250),
            new Vector2(215, 180), new Vector2(590, 185)
        };

        var position = preset[_spawnIndex % preset.Length];
        _spawnIndex++;
        return position;
    }

    private void ClampCamera()
    {
        _camera.Position = new Vector2(
            Mathf.Clamp(_camera.Position.X, 260.0f, 570.0f),
            Mathf.Clamp(_camera.Position.Y, 150.0f, 330.0f));
    }
}
