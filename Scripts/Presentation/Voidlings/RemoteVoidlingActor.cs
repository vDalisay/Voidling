using System;
using System.Linq;
using Godot;
using Voidling.Application.Multiplayer;
using VoidlingGame;

namespace Voidling.Presentation.Voidlings;

/// <summary>
/// Non-interactive presentation of another player's shared Voidling. It owns no save object and has
/// no collision/selection hooks; reliable snapshots provide identity/cosmetics while lossy transforms
/// only move the visual target.
/// </summary>
public partial class RemoteVoidlingActor : Node2D
{
    private static readonly Texture2D CharacterTexture = GD.Load<Texture2D>(
        "res://Assets/Sprout Lands - Sprites - Basic pack/Characters/Basic Charakter Spritesheet.png");

    private AnimatedSprite2D _sprite = null!;
    private MutationAdornment2D _mutationAdornment = null!;
    private Label _label = null!;
    private Vector2 _targetPosition;
    private float _baseScale;

    public SharedVoidlingKey Key { get; private set; }

    public void Setup(SharedVoidlingSnapshot snapshot, string ownerDisplayName)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        Key = snapshot.Key;
        Position = new Vector2(snapshot.ZoneX, snapshot.ZoneY);
        _targetPosition = Position;

        _sprite = new AnimatedSprite2D
        {
            SpriteFrames = BuildSpriteFrames(),
            ZIndex = 2
        };
        AddChild(_sprite);

        _mutationAdornment = new MutationAdornment2D();
        AddChild(_mutationAdornment);

        _label = UiFactory.CreateLabel(string.Empty, 6);
        _label.HorizontalAlignment = HorizontalAlignment.Center;
        _label.CustomMinimumSize = new Vector2(120, 14);
        _label.Position = new Vector2(-60, -34);
        _label.MouseFilter = Control.MouseFilterEnum.Ignore;
        _label.AddThemeColorOverride("font_shadow_color", Color.FromHtml("#F9F4D8"));
        _label.AddThemeConstantOverride("shadow_offset_x", 1);
        _label.AddThemeConstantOverride("shadow_offset_y", 1);
        AddChild(_label);

        ApplySnapshot(snapshot, ownerDisplayName);
        _sprite.Play("walk_down");
        QueueRedraw();
    }

    public void ApplySnapshot(SharedVoidlingSnapshot snapshot, string ownerDisplayName)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Key != Key)
            throw new InvalidOperationException("A remote actor cannot change connected-zone identity.");

        _baseScale = snapshot.Stage == LifeStage.Adult ? 0.62f : 0.31f;
        _sprite.Scale = Vector2.One * _baseScale;
        _sprite.Position = new Vector2(0, VoidlingGroundVisualMetrics.SpriteCenterYOffset(_baseScale));

        try
        {
            _sprite.Modulate = Color.FromHtml(snapshot.TintHex);
        }
        catch (Exception)
        {
            _sprite.Modulate = Colors.White;
        }

        var rareTraits = snapshot.RareTraitIds ?? Array.Empty<string>();
        var hasAngel = rareTraits.Any(id =>
            string.Equals(id, "Angel", StringComparison.OrdinalIgnoreCase));
        var otherMutations = rareTraits.Count(id =>
            !string.Equals(id, "Angel", StringComparison.OrdinalIgnoreCase));
        _mutationAdornment.Setup(hasAngel, otherMutations, _sprite);

        var owner = string.IsNullOrWhiteSpace(ownerDisplayName) ? "Friend" : ownerDisplayName.Trim();
        _label.Text = $"{snapshot.DisplayName} · {owner}";
        _label.Position = new Vector2(-60, snapshot.Stage == LifeStage.Adult ? -37 : -25);
        QueueRedraw();
    }

    public void ApplyTransform(SharedVoidlingTransform transform)
    {
        if (transform.Key != Key)
            return;

        _targetPosition = new Vector2(transform.ZoneX, transform.ZoneY);
        var animation = string.IsNullOrWhiteSpace(transform.AnimationState)
            ? "idle"
            : transform.AnimationState;

        if (string.Equals(animation, "idle", StringComparison.OrdinalIgnoreCase))
        {
            _sprite.Stop();
            return;
        }

        if (_sprite.SpriteFrames.HasAnimation(animation))
            _sprite.Play(animation);
    }

    public override void _Process(double delta)
    {
        var distance = Position.DistanceTo(_targetPosition);
        if (distance > 96.0f)
        {
            Position = _targetPosition;
            return;
        }

        var blend = 1.0f - Mathf.Exp(-12.0f * (float)delta);
        Position = Position.Lerp(_targetPosition, blend);
    }

    public override void _Draw()
    {
        var shadowRadii = VoidlingGroundVisualMetrics.ShadowRadii(_baseScale);
        DrawEllipse(
            new Vector2(0, VoidlingGroundVisualMetrics.ShadowCenterYOffset),
            shadowRadii,
            new Color(0.20f, 0.24f, 0.20f, 0.16f));
    }

    private static SpriteFrames BuildSpriteFrames()
    {
        var frames = new SpriteFrames();
        frames.RemoveAnimation("default");
        AddDirection(frames, "walk_down", 0);
        AddDirection(frames, "walk_up", 1);
        AddDirection(frames, "walk_left", 2);
        AddDirection(frames, "walk_right", 3);
        return frames;
    }

    private static void AddDirection(SpriteFrames frames, string name, int row)
    {
        frames.AddAnimation(name);
        frames.SetAnimationLoop(name, true);
        frames.SetAnimationSpeed(name, 6.0);

        for (var column = 0; column < 4; column++)
        {
            var atlas = new AtlasTexture
            {
                Atlas = CharacterTexture,
                Region = new Rect2(column * 48, row * 48, 48, 48)
            };
            frames.AddFrame(name, atlas);
        }
    }

    private void DrawEllipse(Vector2 center, Vector2 radii, Color color, int points = 20)
    {
        var polygon = new Vector2[points];
        for (var i = 0; i < points; i++)
        {
            var angle = Mathf.Tau * i / points;
            polygon[i] = center + new Vector2(Mathf.Cos(angle) * radii.X, Mathf.Sin(angle) * radii.Y);
        }
        DrawColoredPolygon(polygon, color);
    }
}
