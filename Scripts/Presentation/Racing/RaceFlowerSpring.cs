using System;
using Godot;

namespace Voidling.Presentation.Racing;

/// <summary>
/// The authored flower's contact, compression and release poses. RaceScreen supplies the clock;
/// this component never advances itself or changes race simulation state.
/// Position is the flower's ground anchor, in the same coordinates as a racer's lane baseline.
/// </summary>
internal sealed partial class RaceFlowerSpring : Node2D
{
    internal const string SheetPath = "res://Assets/Racing/flower_spring.png";
    internal const int FrameWidth = 80;
    internal const int FrameHeight = 64;
    internal const int Columns = 4;
    internal const int Rows = 2;
    internal const int FrameCount = Columns * Rows;
    internal const float DisplayScale = 0.5f;
    internal const double LaunchDelaySeconds = 0.14;
    internal const double ResponseDurationSeconds = 0.44;

    // Ground is the lower edge of the opaque artwork. Its symmetry axis runs through source
    // pixel column 40, whose centre is x=40.5 in image-edge coordinates.
    internal static Vector2 GroundPivot => new(40.5f, 52.0f);

    // Measured from the user's edited sheet, including its revised release pose in frame 3.
    private static readonly float[] CenterY = { 35.0f, 37.5f, 39.5f, 31.5f, 31.5f, 37.5f, 36.0f, 35.0f };
    private static readonly double[] FrameEndSeconds = { 0.06, 0.14, 0.18, 0.24, 0.32, 0.44 };

    private Sprite2D? _sprite;

    public bool HasTriggered { get; private set; }

    /// <summary>
    /// Time since contact, including time after the petals have settled. The screen uses that
    /// continuing clock to finish the racer's launch and catch-up presentation.
    /// </summary>
    public double ElapsedSeconds { get; private set; }

    public int CurrentFrame => HasTriggered ? FrameAtElapsed(ElapsedSeconds) : 0;
    public bool IsAnimating => HasTriggered && CurrentFrame < FrameCount - 1;

    /// <summary>
    /// Current contact point relative to Position, already scaled into race-screen coordinates.
    /// The racer's ground pivot rests at the centre of the yellow landing pad.
    /// </summary>
    public Vector2 SupportOffset => SupportOffsetForFrame(CurrentFrame);

    public override void _Ready()
    {
        var texture = GD.Load<Texture2D>(SheetPath);
        if (texture == null || texture.GetWidth() != FrameWidth * Columns || texture.GetHeight() != FrameHeight * Rows)
            throw new InvalidOperationException("The flower spring requires its authored 320 by 128 sprite sheet.");

        TextureFilter = TextureFilterEnum.Nearest;
        _sprite = new Sprite2D
        {
            Name = "Flower",
            Texture = texture,
            Hframes = Columns,
            Vframes = Rows,
            Centered = false,
            Offset = -GroundPivot,
            Scale = Vector2.One * DisplayScale,
            TextureFilter = TextureFilterEnum.Nearest,
            Frame = CurrentFrame
        };
        AddChild(_sprite);
    }

    /// <summary>
    /// Starts on contact. A crossing detected partway through the screen's frame can pass the
    /// remaining time here, so a large delta does not delay the release by another whole frame.
    /// </summary>
    public void Trigger(double elapsedSeconds = 0.0)
    {
        if (!double.IsFinite(elapsedSeconds))
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));

        HasTriggered = true;
        ElapsedSeconds = Math.Max(0.0, elapsedSeconds);
        ApplyFrame();
    }

    /// <summary>Advances only when the owning race advances its presentation clock.</summary>
    public void Advance(double delta)
    {
        if (!double.IsFinite(delta))
            throw new ArgumentOutOfRangeException(nameof(delta));
        if (!HasTriggered || delta <= 0.0)
            return;

        ElapsedSeconds += delta;
        ApplyFrame();
    }

    public void Reset()
    {
        HasTriggered = false;
        ElapsedSeconds = 0.0;
        ApplyFrame();
    }

    /// <summary>Contact support for an authored frame, relative to the fixed ground anchor.</summary>
    public static Vector2 SupportOffsetForFrame(int frame)
    {
        if (frame < 0 || frame >= FrameCount)
            throw new ArgumentOutOfRangeException(nameof(frame));

        return new Vector2(0.0f, (CenterY[frame] - GroundPivot.Y) * DisplayScale);
    }

    /// <summary>
    /// The one-shot starts on frame 1 and holds frame 7 when complete; frame 0 is untriggered idle.
    /// The tiny tolerance keeps exact frame boundaries stable when several deltas are summed.
    /// </summary>
    internal static int FrameAtElapsed(double seconds)
    {
        for (var i = 0; i < FrameEndSeconds.Length; i++)
        {
            if (seconds + 0.000000001 < FrameEndSeconds[i])
                return i + 1;
        }

        return FrameCount - 1;
    }

    private void ApplyFrame()
    {
        if (_sprite != null && _sprite.Frame != CurrentFrame)
            _sprite.Frame = CurrentFrame;
    }
}
