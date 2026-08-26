using Godot;

namespace Voidling.Presentation.Voidlings;

/// <summary>
/// Designer-authored presentation contract for the base Voidling artwork.
/// Gameplay/domain state must never depend on these texture/layout values.
/// </summary>
[GlobalClass]
public partial class VoidlingVisualDefinition : Resource
{
    [Export]
    public string DefinitionId { get; set; } = "default";

    [Export]
    public Texture2D BaseAtlas { get; set; } = null!;

    [Export]
    public Texture2D SwimAtlas { get; set; } = null!;

    [Export(PropertyHint.Range, "1,512,1")]
    public int FrameWidth { get; set; } = 48;

    [Export(PropertyHint.Range, "1,512,1")]
    public int FrameHeight { get; set; } = 48;

    [Export(PropertyHint.Range, "1,32,1")]
    public int WorldFrameCount { get; set; } = 4;

    [Export]
    public int WalkDownRow { get; set; } = 0;

    [Export]
    public int WalkUpRow { get; set; } = 1;

    [Export]
    public int WalkLeftRow { get; set; } = 2;

    [Export]
    public int WalkRightRow { get; set; } = 3;

    [Export(PropertyHint.Range, "0.1,60,0.1")]
    public double WorldAnimationFps { get; set; } = 6.0;

    [Export]
    public int RaceRunRow { get; set; } = 3;

    [Export(PropertyHint.Range, "1,32,1")]
    public int RaceRunFrameCount { get; set; } = 4;

    [Export(PropertyHint.Range, "0.1,60,0.1")]
    public double RaceRunFps { get; set; } = 8.0;

    [Export]
    public int RaceSwimRow { get; set; } = 3;

    [Export(PropertyHint.Range, "1,32,1")]
    public int RaceSwimFrameCount { get; set; } = 8;

    [Export(PropertyHint.Range, "0.1,60,0.1")]
    public double RaceSwimFps { get; set; } = 10.0;

    [Export]
    public int PortraitColumn { get; set; } = 0;

    [Export]
    public int PortraitRow { get; set; } = 0;

    [Export(PropertyHint.Range, "0.01,4,0.01")]
    public float AdultWorldScale { get; set; } = 0.62f;

    [Export(PropertyHint.Range, "0.01,4,0.01")]
    public float ChildWorldScale { get; set; } = 0.31f;

    [Export(PropertyHint.Range, "0.01,4,0.01")]
    public float RaceScale { get; set; } = 0.72f;

    [Export]
    public float WorldSpriteCenterYOffsetAtScaleOne { get; set; } = -8.0f;

    [Export]
    public float RaceSpriteCenterYOffset { get; set; } = -8.0f;

    [Export]
    public Vector2 AdultHitboxSize { get; set; } = new(23.0f, 27.0f);

    [Export]
    public Vector2 ChildHitboxSize { get; set; } = new(14.0f, 16.0f);

    [Export(PropertyHint.Range, "0.1,4,0.01")]
    public float HeldScaleMultiplier { get; set; } = 1.14f;

    [Export]
    public float HeldSpriteYOffset { get; set; } = -9.0f;

    [Export]
    public Vector2 AdultShadowRadii { get; set; } = new(5.2f, 1.8f);

    [Export]
    public float ShadowCenterYOffset { get; set; } = 0.8f;
}
