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
/// Headless CI probe that races a real RaceScreen to the finish line and requires the results card
/// to appear on screen, complete, with a working return control.
///
/// Guards the freeze where the simulation completed but the podium never rendered, leaving the
/// player stuck on a motionless track with no way back to the Garden, and guards a reward being
/// applied twice: the probe keeps the finished race processing and requires the owner to have been
/// told exactly once.
///
/// Add <c>--voidling-race-results-shot</c> with a graphical renderer to also save a review capture.
/// </summary>
public partial class RaceCompletionSmokeProbe : Node
{
    // The race is fixed-step and deterministic, so the probe runs it fast rather than waiting out a
    // real 40-second race.
    private const double TimeScale = 40.0;
    private const double BudgetSeconds = 40.0;
    private const string ShotPath = "res://.godot/ui-checks/race-results.png";

    /// <summary>Sprouts the probe pretends the owner awarded, so the reward line has to render.</summary>
    private const int ProbeReward = 12;

    public override async void _Ready()
    {
        try
        {
            var capture = Array.Exists(
                OS.GetCmdlineUserArgs(),
                arg => string.Equals(arg, "--voidling-race-results-shot", StringComparison.OrdinalIgnoreCase));

            var rules = GameBalanceRules.DemoDefaults;
            var racer = new VoidlingData
            {
                Id = "probe-racer",
                Name = "Probe",
                Stage = LifeStage.Adult,
                Genome = new GenomeFactory(rules.Genetics).CreateRandom(4242UL)
            };

            var entry = new RaceEntryFactory(rules).Create(racer, 4242UL, RaceCourseCatalog.Demo);
            var completedPlacement = 0;
            var completions = 0;
            var returnRequested = false;

            var race = new RaceScreen();
            race.Configure(entry, autoFinish: true);
            race.RaceCompleted += placement =>
            {
                completions++;
                completedPlacement = placement;

                // Stands in for MainController's one-time reward and course-record handling.
                race.PresentOutcome(ProbeReward, newCourseRecord: true);
            };
            race.ReturnRequested += () => returnRequested = true;
            AddChild(race);

            // The menu scene would otherwise sit on top of the card in a rendered capture.
            if (capture)
                GetTree().CurrentScene?.QueueFree();

            Engine.TimeScale = TimeScale;
            var startedMsec = Time.GetTicksMsec();
            while (!race.ResultsShown && (Time.GetTicksMsec() - startedMsec) < BudgetSeconds * 1000.0)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Engine.TimeScale = 1.0;

            if (!race.ResultsShown)
                throw new InvalidOperationException($"Race never reached its results screen within {BudgetSeconds}s.");

            if (completedPlacement <= 0)
                throw new InvalidOperationException("Race completion never reported a placement to its owner.");

            // Let the finished race keep processing. A second completion here would be a second
            // reward, so the probe holds the card open well past the finish before checking.
            for (var frame = 0; frame < 90; frame++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            if (completions != 1)
            {
                throw new InvalidOperationException(
                    $"Race completion was reported {completions} times; the reward must be applied exactly once.");
            }

            var overlay = race.GetChildren()
                .OfType<CanvasLayer>()
                .FirstOrDefault(layer => layer.Layer == RaceScreen.ResultsCanvasLayer)
                ?? throw new InvalidOperationException(
                    "Race finished but there is no results overlay, so the player is stranded on the track.");

            if (overlay.Name != RaceScreen.ResultsCanvasName)
                throw new InvalidOperationException($"The results overlay is named '{overlay.Name}'.");

            var card = Require<Control>(overlay, RaceScreen.ResultsCardName);
            Require<Control>(overlay, RaceScreen.ResultsHeadlineName);
            Require<Control>(overlay, RaceScreen.ResultsPodiumName);
            var placement = Require<Label>(overlay, RaceScreen.ResultsPlacementName);
            var reward = Require<Control>(overlay, RaceScreen.ResultsRewardName);
            var record = Require<Control>(overlay, RaceScreen.ResultsRecordName);
            var returnButton = Require<Button>(overlay, RaceScreen.ResultsReturnName);

            if (string.IsNullOrWhiteSpace(placement.Text) ||
                placement.Text.Contains("UI_RACE", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"The results card shows no readable placement: '{placement.Text}'.");
            }

            if (!reward.Visible)
                throw new InvalidOperationException("The awarded reward is not shown on the results card.");

            if (reward.FindChild("Text", true, false) is not Label rewardText ||
                !rewardText.Text.Contains(ProbeReward.ToString(), StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The reward line does not report the sprouts that were awarded.");
            }

            if (!record.Visible)
                throw new InvalidOperationException("A new course record was set but the results card does not say so.");

            // Bounds: the card, and every part of it the player has to read or press, stays inside
            // the viewport rather than hanging off an edge.
            var screen = new Rect2(Vector2.Zero, GetViewport().GetVisibleRect().Size);
            foreach (var control in new Control[] { card, placement, reward, record, returnButton })
            {
                var rect = control.GetGlobalRect();
                if (!screen.Encloses(rect))
                    throw new InvalidOperationException($"Results control '{control.Name}' at {rect} is off screen {screen}.");
            }

            if (capture)
            {
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(ShotPath).GetBaseDir());
                GetViewport().GetTexture().GetImage().SavePng(ShotPath);
                GD.Print($"[race-completion-smoke] wrote {ProjectSettings.GlobalizePath(ShotPath)}");
            }

            returnButton.EmitSignal(BaseButton.SignalName.Pressed);
            if (!returnRequested)
                throw new InvalidOperationException("The results return control did not ask its owner to leave the race.");

            GD.Print($"[race-completion-smoke] RACE_COMPLETION_SMOKE_SUCCESS placement={completedPlacement}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PrintErr($"[race-completion-smoke] RACE_COMPLETION_SMOKE_FAILED: {exception}");
            GetTree().Quit(8);
        }
    }

    private static T Require<T>(Node overlay, string name) where T : Node
        => overlay.FindChild(name, true, false) as T
           ?? throw new InvalidOperationException($"The results card has no {typeof(T).Name} named '{name}'.");
}
