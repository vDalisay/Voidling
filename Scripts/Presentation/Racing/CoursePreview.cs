using System;
using Godot;
using Voidling.Domain.Racing;

namespace Voidling.Presentation.Racing;

/// <summary>
/// A live window onto an authored course for the race entry screens, in the spirit of the preview
/// the Chao Stadium shows before a race: the real track art, water and crowd rendered into a
/// SubViewport, with a camera sweeping from the start gate to the finish arch and back.
///
/// It builds the same layers the race itself uses, so the preview can never advertise a course that
/// looks different from the one the player is about to run. The viewport renders at the window's
/// real pixel density, so the smaller world scale still lands on whole screen pixels.
/// </summary>
public partial class CoursePreview : Control
{
    private const float TrackTop = 126.0f;
    private const float TrackBottom = 244.0f;
    private const float WorldWidth = 640.0f;
    private const float WorldHeight = 360.0f;
    private const float FocusY = 168.0f;
    private const double SweepSeconds = 16.0;

    private RaceCourse? _course;
    private SubViewport? _viewport;
    private TextureRect? _display;
    private Camera2D? _camera;
    private double _elapsed;

    /// <summary>Resolves a catalogued course; returns false when there is nothing to preview.</summary>
    public bool SetCourse(string courseId, int version)
    {
        if (!RaceCourseCatalog.TryGet(courseId, version, out var definition))
            return false;

        _course = definition.Course;
        return true;
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        ClipContents = true;
        if (_course == null)
            return;

        _viewport = new SubViewport
        {
            Name = "PreviewViewport",
            Disable3D = true,
            CanvasItemDefaultTextureFilter = Viewport.DefaultCanvasItemTextureFilter.Nearest,
            RenderTargetUpdateMode = SubViewport.UpdateMode.WhenParentVisible,
            Size = new Vector2I(64, 64)
        };
        AddChild(_viewport);

        var world = new Node2D { Name = "PreviewWorld" };
        _viewport.AddChild(world);
        var layout = new RaceTrackLayout(TrackTop, TrackBottom, WorldWidth, WorldHeight, RaceTrackArt.ClimbHeight);
        RaceTrackLayers.Build(world, _course, layout);
        RaceTrackFurniture.Build(world, _course, layout);
        foreach (var obstacle in _course.Obstacles)
            RaceTrackFurniture.AddHurdle(world, obstacle + 18.0f, layout, RaceTrackArt.SurfaceLift(_course, obstacle + 18.0f));

        _camera = new Camera2D
        {
            Enabled = true,
            Position = new Vector2(_course.StartX, FocusY)
        };
        world.AddChild(_camera);

        _display = new TextureRect
        {
            Name = "PreviewImage",
            Texture = _viewport.GetTexture(),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            TextureFilter = TextureFilterEnum.Linear,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _display.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_display);
        ResizeViewport();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized)
            ResizeViewport();
    }

    /// <summary>
    /// One viewport pixel per physical screen pixel: the UI is drawn at a whole-number multiple of
    /// its 640x360 design size, so the preview renders at that multiple and is shown scaled back.
    /// </summary>
    private void ResizeViewport()
    {
        if (_viewport == null || Size.X < 1.0f || Size.Y < 1.0f)
            return;

        var density = Math.Max(1.0f, Mathf.Round(GetViewport().GetVisibleRect().Size.X > 0
            ? DisplayServer.WindowGetSize().X / GetViewport().GetVisibleRect().Size.X
            : 1.0f));
        _viewport.Size = new Vector2I((int)(Size.X * density), (int)(Size.Y * density));
        if (_camera != null)
            _camera.Zoom = Vector2.One * Math.Max(1.0f, density * 0.5f);
    }

    public override void _Process(double delta)
    {
        if (_camera == null || _course == null || _viewport == null)
            return;

        // Out to the finish and back, eased at both ends so it lingers on the gate and the arch.
        _elapsed += delta;
        var phase = Mathf.PingPong((float)(_elapsed / SweepSeconds), 1.0f);
        var eased = phase * phase * (3.0f - 2.0f * phase);
        var visibleHalf = _viewport.Size.X * 0.5f / Mathf.Max(0.01f, _camera.Zoom.X);
        var from = _course.StartX - 70.0f + visibleHalf;
        var to = Math.Max(from, _course.EndX + 70.0f - visibleHalf);
        _camera.Position = new Vector2(Mathf.Round(Mathf.Lerp(from, to, eased)), FocusY);
    }
}
