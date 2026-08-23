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
    private Node2D _actorsRoot = null!;
    private Node2D _eggsRoot = null!;
    private string _selectedId = "";
    private int _spawnIndex;

    private readonly Rect2 _wanderBounds = new(new Vector2(58, 63), new Vector2(364, 145));

    public override void _Ready()
    {
        _actorsRoot = GetNode<Node2D>("Actors");
        _eggsRoot = GetNode<Node2D>("Eggs");

        GameSession.Instance.StateChanged += Refresh;
        Refresh();
    }

    public override void _ExitTree()
    {
        if (GameSession.Instance != null)
            GameSession.Instance.StateChanged -= Refresh;
    }

    public void Select(string creatureId)
    {
        _selectedId = creatureId;
        foreach (var pair in _actors)
            pair.Value.SetSelected(pair.Key == creatureId);
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

            var actor = new VoidlingActor();
            actor.Setup(data, _wanderBounds, NextSpawnPosition());
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

        var eggs = GameSession.Instance.State.OwnedEggs;
        for (var i = 0; i < eggs.Count; i++)
        {
            var egg = eggs[i];
            var holder = new Node2D
            {
                Position = new Vector2(82 + (i % 8) * 42, 225 + (i / 8) * 18)
            };

            var sprite = new Sprite2D
            {
                Texture = EggTexture,
                Scale = new Vector2(1.25f, 1.25f),
                Modulate = egg.State == EggState.Failed
                    ? new Color(0.55f, 0.55f, 0.55f, 1.0f)
                    : GameRules.TintColor(egg.TintHex)
            };
            holder.AddChild(sprite);

            var remaining = Math.Max(0, (int)Math.Ceiling(egg.RequiredIncubationSeconds - egg.IncubationSeconds));
            var label = new Label
            {
                Text = egg.State == EggState.Failed ? "X" : $"{remaining}s",
                Position = new Vector2(-9, 8)
            };
            label.AddThemeFontSizeOverride("font_size", 8);
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
            new Vector2(220, 130),
            new Vector2(270, 142),
            new Vector2(180, 150),
            new Vector2(315, 115),
            new Vector2(125, 125),
            new Vector2(350, 165),
            new Vector2(245, 85),
            new Vector2(165, 92)
        };

        var position = preset[_spawnIndex % preset.Length];
        _spawnIndex++;
        return position;
    }

    private void OnActorClicked(string creatureId)
    {
        Select(creatureId);
        VoidlingSelected?.Invoke(creatureId);
    }
}
