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
/// Headless CI probe that races a real RaceScreen to the finish line and requires the results
/// screen to appear with a working return control.
///
/// Guards the freeze where the simulation completed but the podium never rendered, leaving the
/// player stuck on a motionless track with no way back to the Garden.
/// </summary>
public partial class RaceCompletionSmokeProbe : Node
{
    // The race is fixed-step and deterministic, so the probe runs it fast rather than waiting out a
    // real 40-second race.
    private const double TimeScale = 40.0;
    private const double BudgetSeconds = 40.0;

    public override async void _Ready()
    {
        try
        {
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
            var returnRequested = false;

            var race = new RaceScreen();
            race.Configure(entry, autoFinish: true);
            race.RaceCompleted += placement => completedPlacement = placement;
            race.ReturnRequested += () => returnRequested = true;
            AddChild(race);

            Engine.TimeScale = TimeScale;
            var startedMsec = Time.GetTicksMsec();
            while (!race.ResultsShown && (Time.GetTicksMsec() - startedMsec) < BudgetSeconds * 1000.0)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Engine.TimeScale = 1.0;

            if (!race.ResultsShown)
                throw new InvalidOperationException($"Race never reached its results screen within {BudgetSeconds}s.");

            if (completedPlacement <= 0)
                throw new InvalidOperationException("Race completion never reported a placement to its owner.");

            var returnButton = FindReturnButton(race)
                ?? throw new InvalidOperationException(
                    "Race finished but the results screen has no return control, so the player is stranded on the track.");

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

    // Search only inside the results overlay so the in-race Cheer button cannot be mistaken for it.
    private static Button? FindReturnButton(Node race)
    {
        var overlay = race.GetChildren()
            .OfType<CanvasLayer>()
            .FirstOrDefault(layer => layer.Layer == RaceScreen.ResultsCanvasLayer);
        return overlay == null ? null : FirstButton(overlay);
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
}
