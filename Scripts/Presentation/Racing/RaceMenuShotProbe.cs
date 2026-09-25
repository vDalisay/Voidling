using System;
using System.Linq;
using Godot;
using Voidling.Domain.Racing;
using Voidling.Presentation.UI.Common;
using Voidling.Presentation.UI.Racing;
using Voidling.Presentation.Voidlings;
using VoidlingGame;

namespace Voidling.Presentation.Racing;

/// <summary>
/// Development probe that opens the race entry flow on a synthetic roster and saves a screenshot of
/// every step, so the course-select screens get the same visual review as the track itself.
///
/// Enable with <c>-- --voidling-race-menu-shots</c>. Images land in <c>res://.godot/race-shots/</c>.
/// </summary>
public partial class RaceMenuShotProbe : Node
{
    private const string OutputDirectory = "res://.godot/race-shots";

    public override async void _Ready()
    {
        try
        {
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(OutputDirectory));
            GetTree().CurrentScene?.QueueFree();

            var layer = new CanvasLayer { Layer = 10 };
            AddChild(layer);
            var backdrop = new ColorRect { Color = Color.FromHtml("#6E8F5E") };
            backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            layer.AddChild(backdrop);

            // Stand-in for the full-screen modal the Garden opens the flow in.
            var panel = UiFactory.CreatePanel(new Vector2(640, 360));
            panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            layer.AddChild(panel);
            var margin = UiFactory.Pad(new VBoxContainer(), 8);
            panel.AddChild(margin);
            var host = (VBoxContainer)margin.GetChild(0);

            var screen = new RaceEntryScreen();
            screen.Configure(BuildState());
            host.AddChild(screen);

            await Capture("menu-1-course");
            Press(screen, "EntryPrimary");
            await Capture("menu-2-racer");
            Press(screen, "EntryPrimary");
            await Capture("menu-3-confirm");

            GD.Print("[race-shots] RACE_MENU_SHOTS_SUCCESS");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PrintErr($"[race-shots] RACE_MENU_SHOTS_FAILED: {exception}");
            GetTree().Quit(9);
        }
    }

    private async System.Threading.Tasks.Task Capture(string name)
    {
        for (var frame = 0; frame < 20; frame++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var path = $"{OutputDirectory}/{name}.png";
        GetViewport().GetTexture().GetImage().SavePng(path);
        GD.Print($"[race-shots] wrote {ProjectSettings.GlobalizePath(path)}");
    }

    private static void Press(Node root, string name)
    {
        if (root.FindChild(name, true, false) is not Button button)
            throw new InvalidOperationException($"Race entry has no '{name}' button.");
        button.EmitSignal(BaseButton.SignalName.Pressed);
    }

    private RacePickerScreenState BuildState()
    {
        var typeId = VoidlingVisualFactory.VisualTypeIds.First();
        var names = new[] { "Pebble", "Clover", "Puck", "Shot", "Mossy" };
        var statIds = new[] { "run", "swim", "fly", "power", "stamina" };
        var voidlings = names.Select((name, index) => new RacePickerVoidlingViewState(
                $"shot-{index}",
                name,
                new VoidlingVisualAppearance(typeId, index * 0.19f, Array.Empty<string>(), "#FFFFFF"),
                index == 1,
                index % 2,
                string.Empty,
                statIds.Select((statId, stat) => new RacePickerStatViewState(
                    statId,
                    StatPresentationCatalog.NameFor(statId),
                    StatPresentationCatalog.ColorFor(statId),
                    "C",
                    3 + (index + stat) % 5,
                    0.2f + 0.15f * ((index + stat) % 5),
                    120 + 37 * (index + stat))).ToArray()))
            .ToArray();

        var courses = RaceCourseCatalog.All.Select(definition =>
        {
            var (nameKey, summaryKey) = RaceCoursePresentationCatalog.KeysFor(definition.Id);
            var course = definition.Course;
            return new RacePickerCourseViewState(
                definition.Id,
                definition.Version,
                Tr(nameKey),
                Tr(summaryKey),
                course.Segments.Select(segment => segment.Kind).Distinct()
                    .Select(kind => Tr(RaceCoursePresentationCatalog.SectionKeyFor(kind))).ToArray(),
                (int)(course.EndX - course.StartX),
                course.StartX,
                course.EndX,
                course.Segments.Select(segment =>
                    new CourseMinimapSegment(segment.Kind.ToString(), segment.StartX, segment.EndX)).ToArray(),
                course.Obstacles.ToArray());
        }).ToArray();

        var records = new System.Collections.Generic.Dictionary<string, RaceCourseRecordViewState>(StringComparer.Ordinal)
        {
            [RaceEntryScreen.RecordKey(courses[0].Id, courses[0].Version, 1)] = new("Pebble", "0:42.18")
        };

        return new RacePickerScreenState(voidlings, "shot-0", courses, courses[0].Id, courses[0].Version, 1, records);
    }
}
