using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace VoidlingGame;

public partial class GardenController : Node2D
{
    public event Action<string>? VoidlingSelected;

    private static readonly Texture2D EggTexture = GD.Load<Texture2D>(
        "res://Assets/Sprout Lands - Sprites - Basic pack/Objects/Egg item.png");

    private readonly Dictionary<string, VoidlingActor> _actors = new(StringComparer.Ordinal);
    private readonly List<Node> _eggVisuals = new();
    private readonly Rect2 _wanderBounds = new(new Vector2(72, 76), new Vector2(688, 330));

    private Node2D _actorsRoot = null!;
    private Node2D _eggsRoot = null!;
    private Camera2D _camera = null!;
    private string _selectedId = "";
    private int _spawnIndex;
    private bool _cameraDragging;

    public override void _Ready()
    {
        _actorsRoot = GetNode<Node2D>("Actors");
        _eggsRoot = GetNode<Node2D>("Eggs");
        _camera = GetNode<Camera2D>("Camera2D");

        GameSession.Instance.StateChanged += Refresh;
        Refresh();
    }

    public override void _ExitTree()
    {
        if (GameSession.Instance != null)
            GameSession.Instance.StateChanged -= Refresh;
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (inputEvent is InputEventMouseButton mouse)
        {
            if (mouse.ButtonIndex == MouseButton.Middle)
            {
                _cameraDragging = mouse.Pressed;
                GetViewport().SetInputAsHandled();
                return;
            }

            if (mouse.Pressed && (mouse.ButtonIndex == MouseButton.WheelUp || mouse.ButtonIndex == MouseButton.WheelDown))
            {
                var factor = mouse.ButtonIndex == MouseButton.WheelUp ? 1.12f : 1.0f / 1.12f;
                var next = Mathf.Clamp(_camera.Zoom.X * factor, 0.70f, 2.35f);
                _camera.Zoom = new Vector2(next, next);
                ClampCamera();
                GetViewport().SetInputAsHandled();
                return;
            }
        }

        if (inputEvent is InputEventMouseMotion motion && _cameraDragging)
        {
            _camera.Position -= motion.Relative / _camera.Zoom.X;
            ClampCamera();
            GetViewport().SetInputAsHandled();
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
        _camera.Position = new Vector2(416, 240);
        _camera.Zoom = Vector2.One;
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

        var targetA = midpoint - direction * 13.0f;
        var targetB = midpoint + direction * 13.0f;

        var approach = CreateTween().SetParallel(true);
        approach.TweenProperty(a, "position", targetA, 0.55)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        approach.TweenProperty(b, "position", targetB, 0.55)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        await ToSignal(approach, Tween.SignalName.Finished);

        var heart = UiFactory.CreateLabel("♥", 18);
        heart.Position = midpoint + new Vector2(-7, -34);
        heart.AddThemeColorOverride("font_color", Color.FromHtml("#E77B87"));
        heart.ZIndex = 50;
        AddChild(heart);

        var heartTween = CreateTween().SetParallel(true);
        heartTween.TweenProperty(heart, "position", heart.Position + new Vector2(0, -18), 0.7)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        heartTween.TweenProperty(heart, "modulate:a", 0.0f, 0.7);
        await ToSignal(heartTween, Tween.SignalName.Finished);
        heart.QueueFree();

        var eggPosition = midpoint + new Vector2(0, 8);
        createEgg(eggPosition);

        var settle = CreateTween();
        settle.TweenInterval(0.35);
        await ToSignal(settle, Tween.SignalName.Finished);

        a.SetInteractionLocked(false);
        b.SetInteractionLocked(false);
    }

    private void Refresh()
    {
        var currentIds = GameSession.Instance.State.Voidlings
            .Select(v => v.Id)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var staleId in _actors.Keys.Where(id => !currentIds.Contains(id)).ToArray())
        {
            _actors[staleId].QueueFree();
            _actors.Remove(staleId);
        }

        foreach (var data in GameSession.Instance.State.Voidlings)
        {
            if (_actors.ContainsKey(data.Id))
                continue;

            var start = Math.Abs(data.WorldX) > 0.01f || Math.Abs(data.WorldY) > 0.01f
                ? new Vector2(data.WorldX, data.WorldY)
                : NextSpawnPosition();

            var actor = new VoidlingActor();
            actor.Setup(data, _wanderBounds, start);
            actor.Clicked += OnActorClicked;
            _actorsRoot.AddChild(actor);
            _actors[data.Id] = actor;
        }

        Select(_selectedId);
        RefreshEggs();
    }

    private void RefreshEggs()
    {
        foreach (var visual in _eggVisuals)
            visual.QueueFree();
        _eggVisuals.Clear();

        foreach (var egg in GameSession.Instance.State.OwnedEggs)
        {
            var holder = new Node2D { Position = new Vector2(egg.WorldX, egg.WorldY), ZIndex = 5 };

            var sprite = new Sprite2D
            {
                Texture = EggTexture,
                Scale = new Vector2(1.45f, 1.45f),
                Modulate = egg.State == EggState.Failed
                    ? new Color(0.55f, 0.55f, 0.55f, 1.0f)
                    : GameRules.TintColor(egg.TintHex)
            };
            holder.AddChild(sprite);

            var remaining = Math.Max(0, (int)Math.Ceiling(egg.RequiredIncubationSeconds - egg.IncubationSeconds));
            var label = UiFactory.CreateLabel(egg.State == EggState.Failed ? "X" : $"{remaining}s", 7);
            label.Position = new Vector2(-10, 9);
            label.AddThemeColorOverride("font_color", Color.FromHtml("#4F5948"));
            holder.AddChild(label);

            _eggsRoot.AddChild(holder);
            _eggVisuals.Add(holder);
        }
    }

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

    private void OnActorClicked(string creatureId)
    {
        Select(creatureId);
        VoidlingSelected?.Invoke(creatureId);
    }
}
