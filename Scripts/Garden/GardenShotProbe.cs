using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Voidling.Application.Garden;
using Voidling.Domain.Genetics;
using Voidling.Domain.Rules;
using Voidling.Presentation.Garden.Atmosphere;

namespace VoidlingGame;

/// <summary>
/// Development probe that grows a small island, stands Voidlings along its shore and saves the
/// Garden at several hours and in every kind of weather, so water, light and weather work can be
/// reviewed without waiting for the real clock or sky.
///
/// Run with <c>-- --voidling-garden-shots --voidling-dev-profile=garden_shots</c> and a scratch
/// APPDATA: it edits the profile's save. Images land in <c>res://.godot/garden-shots/</c>.
/// </summary>
public partial class GardenShotProbe : Node
{
    private const string OutputDirectory = "res://.godot/garden-shots";

    private enum Focus
    {
        Island,
        Shore,
        Tree
    }

    private static readonly (string Name, float Hour, GardenWeatherKind Weather, float Zoom, Focus Focus)[] Scenes =
    {
        // The same close-ups with the Garden tint (and so all lighting) switched off, for comparison.
        ("00-golden-shore-unlit", 18.4f, GardenWeatherKind.Clear, 3.0f, Focus.Shore),
        ("00b-golden-tree-unlit", 18.4f, GardenWeatherKind.Clear, 3.0f, Focus.Tree),
        ("01-day", 12.5f, GardenWeatherKind.Clear, 0.72f, Focus.Island),
        ("02-day-shore", 12.5f, GardenWeatherKind.Clear, 2.2f, Focus.Shore),
        ("02b-morning-shore", 8.0f, GardenWeatherKind.Clear, 3.0f, Focus.Shore),
        ("03-golden", 18.6f, GardenWeatherKind.Clear, 0.72f, Focus.Island),
        ("03b-golden-shore", 18.4f, GardenWeatherKind.Clear, 3.0f, Focus.Shore),
        ("03c-golden-tree", 18.4f, GardenWeatherKind.Clear, 3.0f, Focus.Tree),
        ("04-dawn-mist", 6.3f, GardenWeatherKind.Clear, 0.72f, Focus.Island),
        ("05-night", 23.0f, GardenWeatherKind.Clear, 0.72f, Focus.Island),
        ("06-night-shore", 23.0f, GardenWeatherKind.Clear, 2.2f, Focus.Shore),
        ("07-overcast", 14.0f, GardenWeatherKind.Overcast, 0.72f, Focus.Island),
        ("08-rain", 14.0f, GardenWeatherKind.Rain, 0.72f, Focus.Island),
        ("09-rain-shore", 14.0f, GardenWeatherKind.Rain, 2.2f, Focus.Shore),
        ("10-storm-night", 22.0f, GardenWeatherKind.Storm, 0.72f, Focus.Island),
        // Glowing mushrooms grown between two Voidlings, lighting them from the side.
        ("11-night-mushrooms", 23.0f, GardenWeatherKind.Clear, 3.0f, Focus.Shore)
    };

    public override async void _Ready()
    {
        try
        {
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(OutputDirectory));
            var garden = await WaitForGarden();
            var session = GetNode<GameSession>("/root/GameBootstrap/GameSession");
            HideHud();
            // Park the pointer in a corner so no hex wears its hover outline in the shots, with edge
            // panning off so the corner does not drag the camera away.
            session.SetEdgePanning(false);
            Input.WarpMouse(new Vector2(2.0f, 2.0f));

            var shore = SeedIsland(session);
            await Frames(20);
            garden.PinVoidlingsForReview(shore);
            await Frames(10);

            foreach (var scene in Scenes)
            {
                session.SetGardenTint(!scene.Name.EndsWith("-unlit", StringComparison.Ordinal));
                if (scene.Name.EndsWith("-mushrooms", StringComparison.Ordinal))
                    garden.GrowMushroomsForReview(new[] { shore.Center + new Vector2(0.0f, -10.0f) });
                garden.Atmosphere.Preview(scene.Hour, scene.Weather);
                var focus = scene.Focus switch
                {
                    Focus.Shore => shore.Center,
                    // The canopy sits above the trunk the tree stands on.
                    Focus.Tree when garden.TreeTrunksForReview.Count > 0 => garden.TreeTrunksForReview[0] + new Vector2(0.0f, -26.0f),
                    _ => garden.LandBounds.GetCenter()
                };
                garden.FrameCameraForReview(focus, scene.Zoom);
                await Seconds(2.2);
                await Capture(scene.Name);
                GD.Print($"[garden-shots] {scene.Name}: {Engine.GetFramesPerSecond():0} fps");
            }

            GD.Print("[garden-shots] GARDEN_SHOTS_SUCCESS");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PrintErr($"[garden-shots] GARDEN_SHOTS_FAILED: {exception}");
            GetTree().Quit(9);
        }
    }

    private async Task<GardenController> WaitForGarden()
    {
        for (var frame = 0; frame < 600; frame++)
        {
            await Frames(1);
            if (GetTree().Root.FindChild("Garden", true, false) is GardenController garden && garden.IsNodeReady())
                return garden;
        }

        throw new InvalidOperationException("The Garden never appeared.");
    }

    private void HideHud()
    {
        foreach (var layer in GetTree().Root.FindChildren("*", "CanvasLayer", true, false).OfType<CanvasLayer>())
            layer.Visible = false;
    }

    /// <summary>
    /// A seven-hex flower around the starting hex, plus a few Voidlings of different colours. Returns
    /// the southern shore they are lined up along.
    /// </summary>
    private static (Vector2 Center, Vector2[] Spots) SeedIsland(GameSession session)
    {
        var state = session.State;
        var start = state.GardenModules.First(module => module.Placed);
        var offsets = new[] { (1, 0), (1, -1), (0, -1), (-1, 0), (-1, 1), (0, 1), (1, 1) };
        var index = 0;
        foreach (var (q, r) in offsets)
        {
            var hexQ = start.HexQ + q;
            var hexR = start.HexR + r;
            if (state.GardenModules.Any(module => module.Placed && module.HexQ == hexQ && module.HexR == hexR))
                continue;
            state.GardenModules.Add(new GardenModuleData
            {
                Id = $"shot-hex-{index++}",
                Placed = true,
                HexQ = hexQ,
                HexR = hexR
            });
        }

        var rules = GameBalanceRules.DemoDefaults;
        var names = new[] { "Brook", "Moss", "Ember" };
        for (var i = 0; i < names.Length; i++)
        {
            state.Voidlings.Add(new VoidlingData
            {
                Id = $"shot-voidling-{i}",
                Name = names[i],
                Stage = LifeStage.Adult,
                Genome = new GenomeFactory(rules.Genetics).CreateRandom(9100UL + (ulong)i),
                Appearance = new VoidlingAppearanceData { PaletteHue = 0.12f + i * 0.27f }
            });
        }

        session.NotifyExternallyPersistedStateChanged();

        // The southern edge of the hex below the start: the shore the sea reflects best.
        var hex = GameRules.GardenModuleRules.Hex;
        var (x, y) = hex.CenterOf(start.HexQ, start.HexR + 1);
        var edgeY = y + hex.Height * 0.5f;
        // A player-placed tree at the end of the line, so placed decorations are reviewed lit too.
        session.PlaceGardenDecoration("tree", x + 92.0f, edgeY + 30.0f);
        var spots = new[]
        {
            new Vector2(x - 54.0f, edgeY - 9.0f),
            new Vector2(x - 18.0f, edgeY - 7.0f),
            new Vector2(x + 18.0f, edgeY - 10.0f),
            new Vector2(x + 54.0f, edgeY - 8.0f),
            new Vector2(x + 86.0f, edgeY - 40.0f)
        };
        return (new Vector2(x, edgeY + 6.0f), spots);
    }

    private async Task Capture(string name)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var path = $"{OutputDirectory}/{name}.png";
        GetViewport().GetTexture().GetImage().SavePng(path);
        GD.Print($"[garden-shots] wrote {ProjectSettings.GlobalizePath(path)}");
    }

    private async Task Frames(int count)
    {
        for (var i = 0; i < count; i++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task Seconds(double seconds)
        => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
}
