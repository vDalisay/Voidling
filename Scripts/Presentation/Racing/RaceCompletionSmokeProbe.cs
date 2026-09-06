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
///
/// It also holds the live HUD to the "creature first" layout: the four groups exist, stay inside the
/// viewport, never overlap each other, the player's portrait, place, stamina and Cheer stay together
/// in one group, and the standings actually rank the field with the player's own row marked.
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
            // Containers only resolve their rects once layout has run, so settle before measuring.
            for (var frame = 0; frame < 6; frame++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            ValidateHudLayout(race);

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

            ValidateHudReadouts(race);

            GD.Print($"[race-completion-smoke] RACE_COMPLETION_SMOKE_SUCCESS placement={completedPlacement}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PrintErr($"[race-completion-smoke] RACE_COMPLETION_SMOKE_FAILED: {exception}");
            GetTree().Quit(8);
        }
    }

    private static CanvasLayer Hud(Node race)
        => race.GetChildren().OfType<CanvasLayer>()
               .FirstOrDefault(layer => layer.Layer == RaceScreen.HudCanvasLayer)
           ?? throw new InvalidOperationException("The race has no HUD layer.");

    /// <summary>
    /// The layout contract: each named group is present, inside the viewport, and clear of every
    /// other group, with stamina and Cheer together at bottom-left and course progress bottom-right.
    /// </summary>
    private void ValidateHudLayout(RaceScreen race)
    {
        var hud = Hud(race);
        var viewport = race.GetViewport().GetVisibleRect().Size;
        var groups = RaceScreen.HudGroupNames
            .Select(name => hud.GetNodeOrNull<Control>(name)
                ?? throw new InvalidOperationException($"Race HUD group '{name}' is missing."))
            .ToList();

        foreach (var group in groups)
        {
            var rect = group.GetGlobalRect();
            if (rect.Size.X < 40.0f || rect.Size.Y < 16.0f)
                throw new InvalidOperationException($"Race HUD group '{group.Name}' has no usable size: {rect}.");
            if (rect.Position.X < -1 || rect.Position.Y < -1 ||
                rect.End.X > viewport.X + 1 || rect.End.Y > viewport.Y + 1)
            {
                throw new InvalidOperationException($"Race HUD group '{group.Name}' leaves the viewport: {rect}.");
            }
        }

        for (var first = 0; first < groups.Count; first++)
        {
            for (var second = first + 1; second < groups.Count; second++)
            {
                if (groups[first].GetGlobalRect().Intersects(groups[second].GetGlobalRect()))
                {
                    throw new InvalidOperationException(
                        $"Race HUD groups '{groups[first].Name}' and '{groups[second].Name}' overlap.");
                }
            }
        }

        var player = hud.GetNode<Control>("RaceHudPlayer");
        foreach (var part in new[] { "StaminaBar", "StaminaValue", "CheerButton", "CheerCost" })
        {
            if (player.FindChild(part, recursive: true, owned: false) == null)
                throw new InvalidOperationException($"The player's race HUD group is missing '{part}'.");
        }

        if (player.FindChild("CheerButton", recursive: true, owned: false) is not Button cheer ||
            cheer.FocusMode != Control.FocusModeEnum.All)
        {
            throw new InvalidOperationException("The Cheer action is not keyboard accessible.");
        }

        if (player.FindChild("StaminaTicks", recursive: true, owned: false) is not Control ticks ||
            ticks.GetChildCount() < 2)
        {
            throw new InvalidOperationException("The stamina bar is missing its 50-point chunk marks.");
        }

        if (hud.GetNode<Control>("RaceHudCourse").FindChild("CourseStrip", recursive: true, owned: false) == null)
            throw new InvalidOperationException("Course progress is missing its course strip.");

        if (player.GetGlobalRect().Position.X > 10.0f ||
            hud.GetNode<Control>("RaceHudCourse").GetGlobalRect().End.X < viewport.X - 10.0f)
        {
            throw new InvalidOperationException("Stamina and course progress are not anchored to opposite bottom corners.");
        }
    }

    /// <summary>
    /// The readouts the groups exist to carry: every standings row names a racer, exactly one row is
    /// the player's, and course progress states a position. All are filled by the live HUD update,
    /// so an empty one means the HUD stopped following the race.
    /// </summary>
    private void ValidateHudReadouts(RaceScreen race)
    {
        var hud = Hud(race);
        var standings = hud.GetNode<Control>("RaceHudStandings");
        var rows = standings.FindChildren("Standing*", "PanelContainer", recursive: true, owned: false)
            .OfType<PanelContainer>()
            .ToList();
        if (rows.Count == 0)
            throw new InvalidOperationException("Opponent standings have no rows.");

        var you = Tr("UI_RACE_HUD_YOU");
        var playerRows = 0;
        foreach (var row in rows)
        {
            if (row.FindChild("Racer", recursive: true, owned: false) is not Label name ||
                string.IsNullOrWhiteSpace(name.Text))
            {
                throw new InvalidOperationException("A standings row never received a racer name.");
            }

            if (row.FindChild("Status", recursive: true, owned: false) is Label status &&
                string.Equals(status.Text, you, StringComparison.Ordinal))
            {
                playerRows++;
            }
        }

        if (playerRows != 1)
            throw new InvalidOperationException($"Standings marked {playerRows} rows as the player instead of one.");

        if (hud.GetNode<Control>("RaceHudCourse").FindChild("CourseProgress", recursive: true, owned: false)
                is not Label progress || string.IsNullOrWhiteSpace(progress.Text))
        {
            throw new InvalidOperationException("Course progress never reported a position on the course.");
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
