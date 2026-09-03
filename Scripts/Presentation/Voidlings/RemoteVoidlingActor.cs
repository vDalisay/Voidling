using System;
using System.Linq;
using Godot;
using Voidling.Application.Multiplayer;
using VoidlingGame;

namespace Voidling.Presentation.Voidlings;

/// <summary>
/// Non-interactive presentation of another player's shared Voidling. Reliable snapshots carry only
/// semantic appearance state; this client resolves the same local visual catalog as owned creatures.
/// </summary>
public partial class RemoteVoidlingActor : Node2D
{
    private AnimatedSprite2D _sprite = null!;
    private MutationAdornment2D _mutationAdornment = null!;
    private Label _label = null!;
    private Vector2 _targetPosition;
    private float _baseScale;
    private string _visualTypeId = VoidlingAppearanceData.DefaultVisualTypeId;
    private float _shadowCenterYOffset;

    public SharedVoidlingKey Key { get; private set; }

    public void Setup(SharedVoidlingSnapshot snapshot, string ownerDisplayName)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        Key = snapshot.Key;
        Position = new Vector2(snapshot.ZoneX, snapshot.ZoneY);
        _targetPosition = Position;

        _sprite = new AnimatedSprite2D { ZIndex = 2 };
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

        var appearance = new VoidlingVisualAppearance(
            snapshot.VisualTypeId,
            snapshot.PaletteHue,
            snapshot.LayerIds ?? Array.Empty<string>(),
            snapshot.TintHex);
        var definition = VoidlingVisualFactory.ResolveDefinition(snapshot.VisualTypeId);
        _visualTypeId = definition.DefinitionId;
        _shadowCenterYOffset = definition.ShadowCenterYOffset;

        var isAdult = snapshot.Stage == LifeStage.Adult;
        _baseScale = VoidlingVisualFactory.WorldScale(isAdult, _visualTypeId);
        _sprite.Scale = Vector2.One * _baseScale;
        _sprite.Position = new Vector2(
            0,
            VoidlingVisualFactory.WorldSpriteCenterYOffset(_baseScale, _visualTypeId));
        VoidlingVisualFactory.ApplyAppearance(_sprite, appearance, race: false);

        var rareTraits = snapshot.RareTraitIds ?? Array.Empty<string>();
        var hasAngel = rareTraits.Any(id => string.Equals(id, "Angel", StringComparison.OrdinalIgnoreCase));
        var otherMutations = rareTraits.Count(id => !string.Equals(id, "Angel", StringComparison.OrdinalIgnoreCase));
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
        var animation = string.IsNullOrWhiteSpace(transform.AnimationState) ? "idle" : transform.AnimationState;
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
        var shadowRadii = VoidlingVisualFactory.ShadowRadii(_baseScale, _visualTypeId);
        DrawEllipse(
            new Vector2(0, _shadowCenterYOffset),
            shadowRadii,
            new Color(0.20f, 0.24f, 0.20f, 0.16f));
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
