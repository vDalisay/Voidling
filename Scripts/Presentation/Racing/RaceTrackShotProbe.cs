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
            race.Configure(entry, autoFinish: true);
            AddChild(race);

            // The menu scene would otherwise sit on top of the track.
            GetTree().CurrentScene?.QueueFree();

            // Run the intro out at speed, then freeze the screen so the camera stays where it is put.
            Engine.TimeScale = 12.0;
            var deadline = Time.GetTicksMsec() + 20000;
            while (race.GetChildren().OfType<CanvasLayer>().Any(layer => layer.Layer == 100) &&
                   Time.GetTicksMsec() < deadline)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            Engine.TimeScale = 1.0;
            race.ProcessMode = ProcessModeEnum.Disabled;

            var camera = race.GetChildren().OfType<Camera2D>().Single();

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

            // Second pass: let the race run and capture the racers actually crossing each feature,
            // which is the only way to check that sprites, shadows and terrain agree in motion.
            race.ProcessMode = ProcessModeEnum.Inherit;
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
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                var actionPath = $"{OutputDirectory}/{prefix}-{hit.Name}.png";
                GetViewport().GetTexture().GetImage().SavePng(actionPath);
                GD.Print($"[race-shots] wrote {ProjectSettings.GlobalizePath(actionPath)}");
            }

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

        return shots.OrderBy(shot => shot.Item2).ToArray();
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
