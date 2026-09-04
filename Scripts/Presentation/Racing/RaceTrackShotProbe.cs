using System;
using System.Linq;
using Godot;
using Voidling.Application.Racing;
using Voidling.Domain.Genetics;
using Voidling.Domain.Racing;
using Voidling.Domain.Rules;
using VoidlingGame;

namespace Voidling.Presentation.Racing;

/// <summary>
/// Development probe that runs a real race and saves viewport screenshots along the course, so
/// track art can be reviewed at the positions that matter (start, swim, climb, take-off, finish)
/// without playing through a race by hand every time.
///
/// Enable with <c>-- --voidling-race-shots</c>. Images land in <c>res://.godot/race-shots/</c>.
/// </summary>
public partial class RaceTrackShotProbe : Node
{
    private const string OutputDirectory = "res://.godot/race-shots";

    public override async void _Ready()
    {
        try
        {
            var rules = GameBalanceRules.DemoDefaults;
            var racer = new VoidlingData
            {
                Id = "shot-racer",
                Name = "Shot",
                Stage = LifeStage.Adult,
                Genome = new GenomeFactory(rules.Genetics).CreateRandom(4242UL)
            };

            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(OutputDirectory));
            var factory = new RaceEntryFactory(rules);

            // Every authored course, because course shape decides how the elevation profile behaves:
            // the demo's clifftop runs straight into the glide launch, the long course's has to drop
            // back down into a river.
            foreach (var definition in RaceCourseCatalog.All)
            {
            var entry = factory.Create(racer, 4242UL, definition);
            var course = entry.CourseDefinition.Course;
            var prefix = definition.Id;

            var race = new RaceScreen();
            race.Configure(entry, autoFinish: false);
            AddChild(race);

            // The menu scene would otherwise sit on top of the track.
            GetTree().CurrentScene?.QueueFree();

            var camera = race.GetChildren().OfType<Camera2D>().Single();

            // Run the intro out at speed, catching the opening flyover on its way past mid-course.
            Engine.TimeScale = 12.0;
            var deadline = Time.GetTicksMsec() + 20000;
            var flyoverCaught = false;
            var midCourse = (course.StartX + course.EndX) * 0.5f;
            while (race.GetChildren().OfType<CanvasLayer>().Any(layer => layer.Layer == 100) &&
                   Time.GetTicksMsec() < deadline)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                if (flyoverCaught || camera.Position.X < midCourse)
                    continue;

                flyoverCaught = true;
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                var flyoverPath = $"{OutputDirectory}/{prefix}-flyover.png";
                GetViewport().GetTexture().GetImage().SavePng(flyoverPath);
                GD.Print($"[race-shots] wrote {ProjectSettings.GlobalizePath(flyoverPath)}");
            }
            Engine.TimeScale = 1.0;
            race.ProcessMode = ProcessModeEnum.Disabled;

            foreach (var stop in Stops(course))
            {
                camera.Position = new Vector2(stop.X, 180.0f);
                for (var frame = 0; frame < 4; frame++)
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);

                var path = $"{OutputDirectory}/{prefix}-{stop.Name}.png";
                var image = GetViewport().GetTexture().GetImage();
                image.SavePng(path);
                GD.Print($"[race-shots] wrote {ProjectSettings.GlobalizePath(path)}");
            }

            // A zoomed still, so the wheel zoom's framing gets the same review as the wide shot.
            var climb = course.Segments.FirstOrDefault(segment => segment.Kind == RaceSegmentKind.Climb);
            camera.Zoom = new Vector2(2.2f, 2.2f);
            camera.Position = new Vector2(
                climb.EndX > climb.StartX ? (climb.StartX + climb.EndX) * 0.5f : course.StartX + 120.0f,
                180.0f);
            for (var frame = 0; frame < 4; frame++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            var zoomPath = $"{OutputDirectory}/{prefix}-zoomed.png";
            GetViewport().GetTexture().GetImage().SavePng(zoomPath);
            GD.Print($"[race-shots] wrote {ProjectSettings.GlobalizePath(zoomPath)}");
            camera.Zoom = Vector2.One;

            // Second pass: let the race run and capture the racers actually crossing each feature,
            // which is the only way to check that sprites, shadows and terrain agree in motion.
            race.ProcessMode = ProcessModeEnum.Inherit;
            // The camera is still parked at the last still. Let the race put it back on the player
            // before polling, or every shot behind that point fires on the first frame.
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var pending = ActionShots(course).ToList();
            var raceDeadline = Time.GetTicksMsec() + 120000;
            while (pending.Count > 0 && Time.GetTicksMsec() < raceDeadline)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                var here = camera.Position.X;
                var hit = pending.FirstOrDefault(shot => here >= shot.X);
                if (hit.Name == null)
                    continue;

                pending.Remove(hit);

                // The cheer burst only exists while the button has just been pressed, so press it.
                if (hit.Name.EndsWith("-cheer", StringComparison.Ordinal))
                {
                    var cheer = FindCheerButton(race);
                    cheer?.EmitSignal(BaseButton.SignalName.Pressed);
                    for (var frame = 0; frame < 4; frame++)
                        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                }

                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                var actionPath = $"{OutputDirectory}/{prefix}-{hit.Name}.png";
                GetViewport().GetTexture().GetImage().SavePng(actionPath);
                GD.Print($"[race-shots] wrote {ProjectSettings.GlobalizePath(actionPath)}");
            }

            // Finally the podium, which is where portrait alignment shows up.
            var resultsDeadline = Time.GetTicksMsec() + 90000;
            Engine.TimeScale = 8.0;
            while (!race.ResultsShown && Time.GetTicksMsec() < resultsDeadline)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Engine.TimeScale = 1.0;
            for (var frame = 0; frame < 40; frame++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            var podiumPath = $"{OutputDirectory}/{prefix}-results.png";
            GetViewport().GetTexture().GetImage().SavePng(podiumPath);
            GD.Print($"[race-shots] wrote {ProjectSettings.GlobalizePath(podiumPath)}");

            race.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }

            GD.Print("[race-shots] RACE_SHOTS_SUCCESS");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PrintErr($"[race-shots] RACE_SHOTS_FAILED: {exception}");
            GetTree().Quit(9);
        }
    }

    /// <summary>Points where a racer is mid-feature: on the cliff, in the water, off the ramp.</summary>
    private static (string Name, float X)[] ActionShots(RaceCourse course)
    {
        var shots = new System.Collections.Generic.List<(string, float)>();
        foreach (var segment in course.Segments)
        {
            if (segment.Kind == RaceSegmentKind.Ground)
                continue;

            shots.Add(($"live-{segment.Id}-entry", Mathf.Lerp(segment.StartX, segment.EndX, 0.2f)));
            shots.Add(($"live-{segment.Id}-mid", Mathf.Lerp(segment.StartX, segment.EndX, 0.6f)));
        }

        if (course.HasGlideSegment)
            shots.Add(("live-launch-ramp", Mathf.Lerp(course.GlideLaunchStartX, course.GlideSegment.StartX, 0.7f)));

        for (var i = 0; i < course.Obstacles.Count; i++)
            shots.Add(($"live-hurdle-{i}", course.Obstacles[i] + 14.0f));

        var firstGround = course.Segments.First(segment => segment.Kind == RaceSegmentKind.Ground);
        shots.Add(("live-running", Mathf.Lerp(firstGround.StartX, firstGround.EndX, 0.45f)));
        shots.Add(("live-cheer", Mathf.Lerp(firstGround.StartX, firstGround.EndX, 0.7f)));
        shots.Add(("live-finish", course.EndX - 90.0f));

        return shots.OrderBy(shot => shot.Item2).ToArray();
    }

    private static Button? FindCheerButton(Node race)
    {
        foreach (var layer in race.GetChildren().OfType<CanvasLayer>().Where(layer => layer.Layer == 20))
        {
            var button = FirstButton(layer);
            if (button != null)
                return button;
        }

        return null;
    }

    private static Button? FirstButton(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is Button button)
                return button;
            var nested = FirstButton(child);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private static (string Name, float X)[] Stops(RaceCourse course)
    {
        var stops = new System.Collections.Generic.List<(string, float)>
        {
            ("00-start", course.StartX + 60.0f)
        };

        var index = 1;
        foreach (var segment in course.Segments)
        {
            stops.Add(($"{index:00}-{segment.Id}", (segment.StartX + segment.EndX) * 0.5f));
            index++;
        }

        if (course.HasGlideSegment)
            stops.Add(($"{index:00}-launch-ramp", (course.GlideLaunchStartX + course.GlideSegment.StartX) * 0.5f));

        stops.Add(("99-finish", course.EndX - 40.0f));
        return stops.ToArray();
    }
}
