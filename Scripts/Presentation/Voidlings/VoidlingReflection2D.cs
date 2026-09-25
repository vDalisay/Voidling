using Godot;

namespace Voidling.Presentation.Voidlings;

/// <summary>
/// A Voidling's reflection: a second body built through <see cref="VoidlingVisualFactory"/>, so it
/// wears the same palette and accessory layers, that copies the source sprite's pose every frame and
/// stands upside down about a water line.
///
/// It only draws the mirrored body. Where it shows, how it ripples and how much of it the water lets
/// through belong to whatever renders it (the Garden sea reads it from its own buffer).
/// </summary>
public partial class VoidlingReflection2D : Node2D
{
    private AnimatedSprite2D? _source;
    private AnimatedSprite2D _mirror = null!;
    private VoidlingVisualAppearance _appearance;
    private bool _usesRaceFrames;

    public void Setup(AnimatedSprite2D source, VoidlingVisualAppearance appearance)
    {
        _source = source;
        _appearance = appearance;
        _mirror = new AnimatedSprite2D { Name = "MirroredBody", Centered = source.Centered };
        AddChild(_mirror);
        ApplyFrames(IsOnRaceFrames(source));
    }

    /// <summary>True while the source sprite still exists.</summary>
    public bool HasSource => _source != null && GodotObject.IsInstanceValid(_source);

    /// <summary>World position of the source body's centre, for visibility checks.</summary>
    public Vector2 SourceCenter => HasSource ? _source!.GlobalPosition : GlobalPosition;

    /// <summary>
    /// Copies the source's current frame and facing and stands the copy flipped about
    /// <paramref name="waterLineY"/>, in the same world coordinates as the source.
    /// </summary>
    public void Follow(float waterLineY)
    {
        if (!HasSource)
            return;

        var source = _source!;
        var onRaceFrames = IsOnRaceFrames(source);
        if (onRaceFrames != _usesRaceFrames)
            ApplyFrames(onRaceFrames);

        if (_mirror.Animation != source.Animation)
            _mirror.Animation = source.Animation;
        _mirror.Frame = source.Frame;
        _mirror.FlipH = source.FlipH;
        _mirror.Offset = source.Offset;

        var transform = source.GlobalTransform;
        var scale = transform.Scale;
        Position = new Vector2(transform.Origin.X, 2.0f * waterLineY - transform.Origin.Y);
        Rotation = -transform.Rotation;
        Scale = new Vector2(scale.X, -scale.Y);
    }

    private void ApplyFrames(bool raceFrames)
    {
        _usesRaceFrames = raceFrames;
        VoidlingVisualFactory.ApplyAppearance(_mirror, _appearance, race: raceFrames);
        _mirror.Stop();
    }

    // The Garden swaps a trainee onto the race frames for its activity loop; the copy follows.
    private bool IsOnRaceFrames(AnimatedSprite2D source)
        => source.SpriteFrames == VoidlingVisualFactory.GetRaceFrames(_appearance.VisualTypeId);
}
