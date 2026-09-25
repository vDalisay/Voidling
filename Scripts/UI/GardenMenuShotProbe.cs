using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Voidling.Application.Garden;
using Voidling.Domain.Creatures;
using Voidling.Domain.Genetics;
using Voidling.Domain.Hatching;
using Voidling.Domain.Stats;

namespace VoidlingGame;

/// <summary>
/// Development probe that opens every menu reachable from the Garden on a seeded, deterministic
/// save and photographs it, so the Garden UI gets the same visual review as the world and the race.
///
/// Run with <c>-- --voidling-garden-menu-shots --voidling-dev-profile=garden_menu_shots</c> and a
/// scratch APPDATA: it edits the profile's save. Add <c>--voidling-garden-hour=12.5
/// --voidling-garden-weather=clear</c> to pin the sky. Images land in
/// <c>res://.godot/garden-menu-shots/</c>. With <c>--voidling-garden-menu-showcase</c> it instead
/// plays a paced tour of the menu animations for a <c>--write-movie</c> recording.
/// </summary>
public partial class GardenMenuShotProbe : Node
{
    private const string OutputDirectory = "res://.godot/garden-menu-shots";
    private static readonly DateTime PinnedClock = DateTime.Today.AddHours(12.5);

    private const string CursorRoot = "res://Assets/Sprout Lands - UI Pack - Premium pack/UI Sprites/Mouse sprites/";

    private MainController _main = null!;
    private GameSession _session = null!;
    private readonly List<string> _failures = new();
    private bool _showcase;
    private Vector2 _pointer = new(636, 356);
    private TextureRect? _cursor;
    private Texture2D? _openPaw;
    private Texture2D? _holdingPaw;

    public override async void _Ready()
    {
        try
        {
            if (!OS.GetCmdlineUserArgs().Any(arg => arg.StartsWith("--voidling-dev-profile=", StringComparison.Ordinal)))
                throw new InvalidOperationException("The menu probe edits its save; run it with --voidling-dev-profile=.");
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(OutputDirectory));
            _main = await WaitForMain();
            _session = GetNode<GameSession>("/root/GameBootstrap/GameSession");
            _session.SetEdgePanning(false);
            await Frames(10);
            SeedSave();
            _main.PrepareMenuReview(PinnedClock);
            Park();
            await Seconds(1.0);

            _showcase = OS.GetCmdlineUserArgs().Contains("--voidling-garden-menu-showcase");
            if (_showcase)
            {
                AddPointer();
                await RunShowcase();
            }
            else
                await RunShots();

            foreach (var failure in _failures)
                GD.PrintErr($"[menu-shots] step failed: {failure}");
            GD.Print($"[menu-shots] {Engine.GetFramesPerSecond():0} fps at the end");
            GD.Print("[menu-shots] GARDEN_MENU_SHOTS_SUCCESS");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PrintErr($"[menu-shots] GARDEN_MENU_SHOTS_FAILED: {exception}");
            GetTree().Quit(9);
        }
    }

    // ---- still shots ------------------------------------------------------------------------

    private async Task RunShots()
    {
        await Shot("01-garden");
        var pip = _session.State.Voidlings[0].Id;
        _main.ReviewSelect(pip);
        await Shot("02-garden-inspector");
        _main.ReviewCloseAll();

        await Open("roster", "03-roster");
        await Open("land-inspector", "04-land-inspector");
        await Open("garden-menu", "05-garden-menu");
        await Open("settings", "06-settings");
        await Open("build", "07-build");
        await Open("land", "08-land");
        await Open("decorate", "09-decorate");
        await Open("activities", "10-activities");
        await Open("missions", "11-missions");
        await Open("journal", "12-journal");
        await Open("inventory", "13-inventory", keepOpen: true);
        await ClickNamed("CategoryShopEggs", "13b-inventory-eggs");
        _main.ReviewCloseAll();
        await Open("shop", "14-shop-treats", keepOpen: true);
        await ClickNamed("CategoryEggs", "14b-shop-eggs");
        await ClickNamed("CategoryLand", "14c-shop-land");
        await ClickNamed("CategoryBiomes", "14d-shop-biomes");
        _main.ReviewCloseAll();
        await Open("breed", "15-breed");
        await Open("race", "16-race-course", keepOpen: true);
        await ClickNamed("EntryPrimary", "16b-race-racer");
        await ClickNamed("EntryPrimary", "16c-race-confirm");
        _main.ReviewCloseAll();

        _main.ReviewSelect(pip);
        await Settle();
        await Open("details", "17-details", closeAfter: false);
        await Open("family", "18-family", closeAfter: false);
        await Open("treats", "19-treats", closeAfter: false);
        await Open("goodbye", "20-goodbye", closeAfter: false);
        _main.ReviewCloseAll();
        await Open("reset", "21-reset");

        await Open("online", "22-online");
        await Open("leaderboard", "23-leaderboard");
        await Open("daily-race", "24-daily-race");
        await Open("challenges", "25-challenges");
        await Open("trades", "26-trades");

        // Interaction states on the Garden HUD.
        await Settle();
        var rail = _main.ReviewUiRoot.GetNode<Control>("GardenRail");
        await Hover(Named<Control>(rail, "Shop"));
        await Seconds(0.5);
        await Capture("27-hover-rail");
        Park();
        Named<Control>(rail, "Inventory").GrabFocus();
        await Seconds(0.5);
        await Capture("28-focus-rail");
        _main.GetViewport().GuiReleaseFocus();
        _main.ReviewCollapseRail(true);
        await Seconds(0.6);
        await Capture("28b-rail-collapsed");
        _main.ReviewCollapseRail(false);
        await Seconds(0.6);
        _main.GetViewport().GuiReleaseFocus();
        var settings = Named<Control>(_main.ReviewUiRoot, "Settings");
        await Hover(settings);
        await Seconds(1.6);
        await Capture("29-tooltip");
        Park();

        // A purchase, photographed mid-celebration.
        await Open("shop", string.Empty, keepOpen: true);
        await ClickNamed("CategoryEggs", string.Empty);
        await Press(Named<Control>(_main.ReviewModalHost, "BuySelected"));
        await Seconds(0.25);
        await Capture("30-shop-bought");
        _main.ReviewCloseAll();
        await Seconds(0.6);

        _main.ReviewEvent("Pip found a shiny pebble.");
        await Seconds(0.3);
        await Capture("31-garden-event");

        _main.ReviewShowTutorial();
        await Seconds(0.8);
        await Capture("32-tutorial");
        _main.ReviewSkipTutorial();
        await Settle();

        // Reduce motion: the same purchase with every effect switched off must still work.
        _session.SetReduceMotion(true);
        await Open("shop", string.Empty, keepOpen: true);
        await ClickNamed("CategoryTreats", string.Empty);
        await Press(Named<Control>(_main.ReviewModalHost, "BuySelected"));
        await Seconds(0.1);
        await Capture("33-reduced-motion-shop");
        _main.ReviewCloseAll();
        _session.SetReduceMotion(false);
        await Settle();
    }

    // ---- showcase (recorded with --write-movie) -----------------------------------------------

    private async Task RunShowcase()
    {
        var root = _main.ReviewUiRoot;
        var rail = root.GetNode<Control>("GardenRail");
        await Seconds(1.0);

        // Rail: hover down the destinations, then keyboard focus.
        foreach (var name in new[] { "Voidlings", "Inventory", "Shop", "Breed", "Races", "Build", "Journal" })
        {
            await Hover(Named<Control>(rail, name));
            await Seconds(0.35);
        }
        Park();
        await Seconds(0.4);
        foreach (var name in new[] { "Online", "Journal", "Build" })
        {
            Named<Control>(rail, name).GrabFocus();
            await Seconds(0.45);
        }
        _main.GetViewport().GuiReleaseFocus();

        // Roster slides out beside the rail and staggers its rows in.
        await PressNamed(rail, "Voidlings");
        await Seconds(1.2);
        _main.ReviewCloseAll();
        await Seconds(0.4);

        // Selecting a Voidling brings in the inspector.
        _main.ReviewSelect(_session.State.Voidlings[0].Id);
        await Seconds(1.2);
        await Hover(Named<Control>(root, "GiveTreat"));
        await Seconds(0.8);
        _main.ReviewCloseAll();
        Park();
        await Seconds(0.5);

        // Shop: open, switch tabs, buy (counter roll, floating number, burst).
        await PressNamed(rail, "Shop");
        await Seconds(1.0);
        var host = _main.ReviewModalHost;
        await PressNamed(host, "BuySelected");
        await Seconds(1.2);
        await PressNamed(host, "CategoryEggs");
        await Seconds(0.8);
        await PressNamed(host, "Product_egg_*");
        await Seconds(0.5);
        await PressNamed(host, "BuySelected");
        await Seconds(2.0);
        await PressNamed(host, "CategoryBiomes");
        await Seconds(0.9);
        _main.ReviewCloseAll();
        Park();
        await Seconds(0.6);

        // Activities: claim the daily check-in and a mission.
        _main.ReviewOpen("activities");
        await Seconds(1.0);
        await PressNamed(_main.ReviewModalHost, "Claim");
        await Seconds(1.8);
        await PressNamed(_main.ReviewModalHost, "Missions");
        await Seconds(1.0);
        var claim = _main.ReviewModalHost.FindChildren("ClaimMission_*", "Button", true, false).OfType<Button>()
            .FirstOrDefault(button => !button.Disabled && button.IsVisibleInTree());
        if (claim != null)
        {
            await Press(claim);
            await Seconds(1.8);
        }
        _main.ReviewCloseAll();
        Park();
        await Seconds(0.5);

        // Inventory and the journal: grids stagger in, selection pops.
        await PressNamed(rail, "Inventory");
        await Seconds(1.0);
        // Slots rebuild when one is picked, so each is found again by name.
        var slotNames = _main.ReviewModalHost.FindChildren("Slot_*", "Button", true, false).OfType<Button>()
            .Where(button => button.IsVisibleInTree()).Skip(1).Take(3).Select(button => button.Name.ToString()).ToArray();
        foreach (var slot in slotNames)
        {
            await PressNamed(_main.ReviewModalHost, slot);
            await Seconds(0.5);
        }
        await PressNamed(_main.ReviewModalHost, "CategoryShopEggs");
        await Seconds(0.9);
        _main.ReviewCloseAll();
        Park();
        await Seconds(0.4);

        // Breeding: pick a pair and hover the breed button.
        await PressNamed(rail, "Breed");
        await Seconds(1.0);
        var racers = _main.ReviewModalHost.FindChildren("Racer_*", "Button", true, false).OfType<Button>()
            .Where(button => button.IsVisibleInTree()).Select(button => button.Name.ToString()).ToArray();
        if (racers.Length > 2)
        {
            await PressNamed(_main.ReviewModalHost, racers[2]);
            await Seconds(0.8);
        }
        await Hover(Named<Control>(_main.ReviewModalHost, "BreedAction"));
        await Seconds(1.2);
        _main.ReviewCloseAll();
        Park();
        await Seconds(0.4);

        // Race entry: course, level, then racer.
        await PressNamed(rail, "Races");
        await Seconds(1.2);
        await PressNamed(_main.ReviewModalHost, "Level_*_2");
        await Seconds(0.6);
        await PressNamed(_main.ReviewModalHost, "EntryPrimary");
        await Seconds(1.2);
        _main.ReviewCloseAll();
        Park();
        await Seconds(0.4);

        // Settings: switches and a tooltip; then the Garden menu and back.
        _main.ReviewOpen("settings");
        await Seconds(0.9);
        await PressNamed(_main.ReviewModalHost, "EdgePan");
        await Seconds(0.6);
        await PressNamed(_main.ReviewModalHost, "EdgePan");
        await Seconds(0.5);
        await Hover(Named<Control>(_main.ReviewModalHost, "GardenTint"));
        await Seconds(1.6);
        _main.ReviewCloseAll();
        Park();
        await Seconds(0.4);

        _main.ReviewEvent("Mallow found a shiny pebble.");
        await Seconds(1.5);
        _main.ReviewEvent("Brook is napping in the sun.");
        await Seconds(1.5);
    }

    // ---- helpers ----------------------------------------------------------------------------

    private async Task Open(string target, string shot, bool keepOpen = false, bool closeAfter = true)
    {
        try
        {
            if (!_main.ReviewOpen(target))
                throw new InvalidOperationException($"Unknown review target '{target}'.");
            await Settle();
            if (shot.Length > 0) await Capture(shot);
        }
        catch (Exception exception)
        {
            _failures.Add($"{target}: {exception.Message}");
        }
        if (!keepOpen && closeAfter)
        {
            _main.ReviewCloseAll();
            await Seconds(0.3);
        }
    }

    private async Task ClickNamed(string name, string shot)
    {
        try
        {
            await Press(Named<Control>(_main.ReviewModalHost, name));
            await Settle();
            if (shot.Length > 0) await Capture(shot);
        }
        catch (Exception exception)
        {
            _failures.Add($"{name}: {exception.Message}");
        }
    }

    private async Task PressNamed(Node root, string pattern)
    {
        try { await Press(Named<Control>(root, pattern)); }
        catch (Exception exception) { _failures.Add($"{pattern}: {exception.Message}"); }
    }

    private static T Named<T>(Node root, string pattern) where T : Control
        => root.FindChildren(pattern, string.Empty, true, false).OfType<T>()
               .FirstOrDefault(control => !control.IsQueuedForDeletion() && control.IsVisibleInTree())
           ?? throw new InvalidOperationException($"No visible '{pattern}' under {root.Name}.");

    private async Task Hover(Control control) => await MoveTo(control.GetGlobalRect().GetCenter());

    private async Task Press(Control control)
    {
        await MoveTo(control.GetGlobalRect().GetCenter());
        await Seconds(0.12);
        if (_cursor != null) _cursor.Texture = _holdingPaw;
        GetViewport().PushInput(new InputEventMouseButton
            { Position = _pointer, GlobalPosition = _pointer, ButtonIndex = MouseButton.Left, Pressed = true }, true);
        await Seconds(0.09);
        GetViewport().PushInput(new InputEventMouseButton
            { Position = _pointer, GlobalPosition = _pointer, ButtonIndex = MouseButton.Left, Pressed = false }, true);
        if (_cursor != null) _cursor.Texture = _openPaw;
        await Frames(2);
    }

    /// <summary>
    /// Moves the pointer. In the showcase it glides there with the pack's cat-paw cursor drawn on
    /// top (a recording shows no system cursor), passing over whatever lies on the way.
    /// </summary>
    private async Task MoveTo(Vector2 target)
    {
        var from = _pointer;
        var steps = _showcase ? 8 : 1;
        for (var step = 1; step <= steps; step++)
        {
            var t = step / (float)steps;
            var eased = t * t * (3.0f - 2.0f * t);
            _pointer = from.Lerp(target, eased).Round();
            if (_cursor != null) _cursor.Position = _pointer - new Vector2(3, 1);
            GetViewport().PushInput(new InputEventMouseMotion { Position = _pointer, GlobalPosition = _pointer }, true);
            await Frames(1);
        }
        await Frames(1);
    }

    /// <summary>The pointer goes to an empty corner so nothing wears a hover state by accident.</summary>
    private void Park()
    {
        _pointer = new Vector2(636, 356);
        if (_cursor != null) _cursor.Position = _pointer - new Vector2(3, 1);
        GetViewport().PushInput(new InputEventMouseMotion { Position = _pointer, GlobalPosition = _pointer }, true);
    }

    private void AddPointer()
    {
        _openPaw = GD.Load<Texture2D>(CursorRoot + "Catpaw Mouse icon.png");
        _holdingPaw = GD.Load<Texture2D>(CursorRoot + "Catpaw holding Mouse icon.png");
        var layer = new CanvasLayer { Layer = 120 };
        AddChild(layer);
        _cursor = new TextureRect
        {
            Texture = _openPaw,
            Size = new Vector2(16, 16),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Position = _pointer
        };
        layer.AddChild(_cursor);
    }

    private async Task Settle() => await Seconds(0.7);

    private async Task Shot(string name)
    {
        await Settle();
        await Capture(name);
    }

    private async Task Capture(string name)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var path = $"{OutputDirectory}/{name}.png";
        GetViewport().GetTexture().GetImage().SavePng(path);
        GD.Print($"[menu-shots] wrote {name} ({Engine.GetFramesPerSecond():0} fps)");
        AuditPixels(name);
    }

    /// <summary>
    /// Every visible panel, button and picture at rest has to sit on whole UI pixels: anything off
    /// the grid would draw its pixel art between pixels. Controls mid-animation (scaled) are skipped.
    /// </summary>
    private void AuditPixels(string shot)
    {
        var offGrid = new List<string>();
        foreach (var node in GetTree().Root.FindChildren("*", "Control", true, false))
        {
            if (node is not Control control || !control.IsVisibleInTree()) continue;
            if (control is not (PanelContainer or BaseButton or TextureRect or NinePatchRect or Panel)) continue;
            var transform = control.GetGlobalTransformWithCanvas();
            if (!transform.Scale.IsEqualApprox(Vector2.One) || transform.Rotation != 0) continue;
            var rect = control.GetGlobalRect();
            if (IsWhole(rect.Position.X) && IsWhole(rect.Position.Y) && IsWhole(rect.Size.X) && IsWhole(rect.Size.Y)) continue;
            offGrid.Add($"{control.GetParent()?.Name}/{control.Name} {rect}");
        }
        GD.Print($"[menu-shots] pixel audit {shot}: {offGrid.Count} off-grid" +
                 (offGrid.Count > 0 ? ": " + string.Join("; ", offGrid.Take(5)) : string.Empty));
    }

    private static bool IsWhole(float value) => Mathf.Abs(value - Mathf.Round(value)) < 0.01f;

    private async Task<MainController> WaitForMain()
    {
        for (var frame = 0; frame < 600; frame++)
        {
            await Frames(1);
            if (GetTree().Root.FindChild("Main", true, false) is MainController main && main.IsNodeReady())
                return main;
        }
        throw new InvalidOperationException("The Garden UI never appeared.");
    }

    private async Task Frames(int count)
    {
        for (var i = 0; i < count; i++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task Seconds(double seconds)
        => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

    // ---- the seeded save --------------------------------------------------------------------

    /// <summary>
    /// A lived-in, deterministic save: six named Voidlings of different forms, a biome hex, eggs,
    /// shells, treats, tiles and a wallet, so every screen has something to show and every run of
    /// the probe looks the same.
    /// </summary>
    private void SeedSave()
    {
        var state = _session.State;
        state.GardenName = "Sunny Isle";
        state.Coins = 1250;
        state.SeedCounter = 424242;

        var looks = new (string Name, string Type, float Hue, string Tint, bool Adult)[]
        {
            ("Pip", "neutral", 0.00f, "#E7A6B6", true),
            ("Mallow", "run", 0.30f, "#A9D5C0", true),
            ("Brook", "water", 0.55f, "#9CC8E6", true),
            ("Moss", "fly", 0.18f, "#B7D98B", true),
            ("Ember", "power", 0.05f, "#E8A66B", true),
            ("Tiny", "normal", 0.72f, "#D9C2F0", false)
        };
        for (var index = 0; index < looks.Length; index++)
        {
            var look = looks[index];
            var creature = index < state.Voidlings.Count ? state.Voidlings[index] : new VoidlingData();
            if (index >= state.Voidlings.Count)
            {
                creature.Id = $"review-voidling-{index}";
                creature.WorldX = 380 + (index % 3) * 36;
                creature.WorldY = 222 + (index / 3) * 40;
                state.Voidlings.Add(creature);
            }
            creature.Name = look.Name;
            creature.Genome = GeneticsService.CreateRandomGenome(7100UL + (ulong)index);
            creature.Stage = look.Adult ? LifeStage.Adult : LifeStage.Child;
            creature.AgeSeconds = look.Adult ? GameRules.ChildToAdultSeconds : 30;
            creature.TintHex = look.Tint;
            creature.Appearance = new VoidlingAppearanceData { VisualTypeId = look.Type, PaletteHue = look.Hue };
            creature.RareTraits = index == 0
                ? new List<RareTraitData> { new() { TraitId = GameRules.AngelMutationId, FounderCreatureId = creature.Id, CanTransmit = true } }
                : new List<RareTraitData>();
            creature.BreedCooldownSeconds = 0;
            creature.Stats = StatProgressionService.CreateNewborn(GameRules.StatIds);
            var statIndex = 0;
            foreach (var stat in creature.Stats.Values)
            {
                stat.Level = 1 + (index + statIndex * 2) % 5;
                stat.Progress = 10 + 17 * ((index + statIndex) % 5);
                stat.Points = 40 + 23 * ((index * 3 + statIndex) % 9);
                statIndex++;
            }
        }

        foreach (var statId in GameRules.StatIds)
            state.TrainingItems[statId] = 3;

        // Three more hexes and one of them a biome, so land and the hex inspector have content.
        var start = state.GardenModules.First(module => module.Placed);
        var offsets = new[] { (1, 0), (0, 1), (-1, 1) };
        for (var index = 0; index < offsets.Length; index++)
        {
            var (q, r) = offsets[index];
            if (state.GardenModules.Any(module => module.Placed && module.HexQ == start.HexQ + q && module.HexR == start.HexR + r))
                continue;
            state.GardenModules.Add(new GardenModuleData
            {
                Id = $"review-hex-{index}", Placed = true, HexQ = start.HexQ + q, HexR = start.HexR + r
            });
        }
        state.GardenModules.Add(new GardenModuleData { Id = "review-stored-hex", Placed = false, ShapeId = "single" });
        state.BiomeTiles.Add(new BiomeTileStackData { BiomeId = "water", Stars = 1, Count = 2 });
        state.BiomeTiles.Add(new BiomeTileStackData { BiomeId = "grove", Stars = 1, Count = 1 });

        state.OwnedEggs.Add(new EggData { Id = "review-shop-egg", Source = EggSource.Store, State = EggState.Stored, TintHex = "#F2D38B" });
        state.OwnedEggs.Add(new EggData { Id = "review-bred-egg", Source = EggSource.Bred, State = EggState.Stored, TintHex = "#9CC8E6" });
        state.EggShells.Add(new EggShellData { Id = "review-shell", Source = EggSource.Store, TintHex = "#F6F0C9" });

        _session.NotifyExternallyPersistedStateChanged();
        _session.BuildBiome("review-hex-0", "water");
    }
}
