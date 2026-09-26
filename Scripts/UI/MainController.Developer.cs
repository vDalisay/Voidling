using System;
using System.Linq;
using Godot;
using Voidling.Domain.Garden;
using Voidling.Domain.Shop;
using Voidling.Presentation.UI.Common;

namespace VoidlingGame;

public partial class MainController
{
    private bool _developerMenuOpen;

    private void RunDeveloperMenuSmoke()
    {
        _Input(new InputEventKey { Keycode = Key.Quoteleft, PhysicalKeycode = Key.Quoteleft, Pressed = true });
        if (!_developerMenuOpen || !_modalHost.IsOpen)
            GD.PushError("Developer menu did not open.");
        else
            GD.Print("DEV_MENU_SMOKE_OK");
        GetTree().Quit();
    }

    private void ShowDeveloperMenu()
    {
        var box = OpenModal(Tr("UI_DEV_TITLE"), new Vector2(500, 338));
        _developerMenuOpen = true;
        box.AddChild(UiFactory.CreateLabel(Tr("UI_DEV_HINT"), 8));
        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        box.AddChild(scroll);
        var actions = new VBoxContainer();
        actions.AddThemeConstantOverride("separation", 7);
        scroll.AddChild(actions);

        var forms = new[] { "normal", "neutral", "run", "water", "fly", "power", "rainbow", "swamp-variant" };
        var form = DeveloperChoice(forms, id => VoidlingFormPresentationCatalog.NameFor(id));
        AddDeveloperRow(actions, "UI_DEV_VOIDLING", form, null, "UI_DEV_SPAWN", () =>
        {
            if (!_session.DeveloperSpawnVoidling(forms[form.Selected]))
                ShowToast(Tr("UI_DEV_GARDEN_FULL"));
        });

        var tiles = BiomeCatalog.Biomes.SelectMany(biome =>
                Enumerable.Range(1, BiomeCatalog.MaxStars).Select(stars => (Kind: "biome", Id: biome.Id, Stars: stars)))
            .Concat(GardenTileShape.Catalog.Select(shape => (Kind: "land", Id: shape.Id, Stars: 0)))
            .ToArray();
        var tileLabels = tiles.Select(tile => tile.Kind == "land"
            ? LandShapePresentation.NameFor(tile.Id)
            : BiomePresentationCatalog.NameFor(BiomeCatalog.IdAtStars(tile.Id, tile.Stars)) + $" {tile.Stars}★")
            .ToArray();
        var biome = DeveloperChoice(tileLabels, label => label);
        var tileCount = DeveloperNumber(1, 999, 1);
        AddDeveloperRow(actions, "UI_DEV_TILE", biome, tileCount, "UI_DEV_ADD", () =>
        {
            var selected = tiles[biome.Selected];
            if (selected.Kind == "land") _session.DeveloperGiveLandPiece(selected.Id, (int)tileCount.Value);
            else _session.DeveloperGiveTile(selected.Id, selected.Stars, (int)tileCount.Value);
        });

        var coins = DeveloperNumber(1, 1000000, 1000);
        AddDeveloperRow(actions, "UI_DEV_SPROUTS", coins, null, "UI_DEV_ADD", () =>
            _session.DeveloperGiveCoins((int)coins.Value));

        var itemIds = GameRules.StatIds.Concat(new[] { ShopItemIds.FullIncubationSkip, "mystery-egg" }).ToArray();
        var item = DeveloperChoice(itemIds, id => id switch
        {
            ShopItemIds.FullIncubationSkip => Tr("UI_DEV_INCUBATION_SKIP"),
            "mystery-egg" => Tr("UI_DEV_MYSTERY_EGG"),
            _ => StatPresentationCatalog.NameFor(id) + " " + Tr("UI_DEV_TREAT")
        });
        var itemCount = DeveloperNumber(1, 99, 1);
        AddDeveloperRow(actions, "UI_DEV_ITEM", item, itemCount, "UI_DEV_ADD", () =>
            _session.DeveloperGiveItem(itemIds[item.Selected], (int)itemCount.Value));

        var eggs = _session.State.OwnedEggs.Where(egg => egg.State != EggState.Failed).ToArray();
        var eggIds = eggs.Select(egg => egg.Id).ToArray();
        var egg = DeveloperChoice(eggIds, id => Tr("UI_DEV_EGG") + " " + (Array.IndexOf(eggIds, id) + 1));
        AddDeveloperRow(actions, "UI_DEV_HATCH", egg, null, "UI_DEV_HATCH", () =>
        {
            if (egg.Selected < 0 || !_session.DeveloperHatchEgg(eggIds[egg.Selected]))
                ShowToast(Tr("UI_DEV_HATCH_FAILED"));
            else ShowDeveloperMenu();
        });

        var cap = DeveloperNumber(GameRules.GardenMaxPopulation, 256,
            Math.Max(GameRules.GardenMaxPopulation, _session.State.GardenPopulationCapOverride));
        AddDeveloperRow(actions, "UI_DEV_CAP", cap, null, "UI_DEV_SET", () =>
            _session.DeveloperRaisePopulationCap((int)cap.Value));

        var speeds = new[] { 1, 2, 5, 10, 50, 100 };
        var speed = DeveloperChoice(speeds.Select(value => value.ToString()).ToArray(), id => id + "×");
        speed.Selected = Array.IndexOf(speeds, (int)_session.DeveloperTimeScale);
        AddDeveloperRow(actions, "UI_DEV_SPEED", speed, null, "UI_DEV_SET", () =>
        {
            _session.SetDeveloperTimeScale(speeds[speed.Selected]);
            _garden.RefreshDeveloperEnvironment();
        });

        var hours = DeveloperNumber(0.25, 24, 3, 0.25);
        AddDeveloperRow(actions, "UI_DEV_HOURS", hours, null, "UI_DEV_ADVANCE", () =>
        {
            _session.DeveloperAdvanceHours(hours.Value);
            _garden.RefreshDeveloperEnvironment();
        });
    }

    private static OptionButton DeveloperChoice(string[] ids, Func<string, string> nameFor)
    {
        var choice = new OptionButton { CustomMinimumSize = new Vector2(172, 25),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        UiFactory.ApplyButtonChrome(choice);
        UiFactory.ApplyPixelFont(choice, 9);
        foreach (var id in ids) choice.AddItem(nameFor(id));
        if (ids.Length > 0) choice.Selected = 0;
        else choice.Disabled = true;
        return choice;
    }

    private static SpinBox DeveloperNumber(double minimum, double maximum, double value, double step = 1)
    {
        var number = new SpinBox { MinValue = minimum, MaxValue = maximum, Step = step, Value = value,
            CustomMinimumSize = new Vector2(74, 25), SelectAllOnFocus = true };
        number.GetLineEdit().AddThemeStyleboxOverride("normal", UiSkin.Paper());
        UiFactory.ApplyPixelFont(number.GetLineEdit(), 9);
        return number;
    }

    private static void AddDeveloperRow(VBoxContainer parent, string labelKey, Control input,
        Control? amount, string buttonKey, Action pressed)
    {
        parent.AddChild(UiFactory.CreateLabel(TranslationServer.Translate(labelKey), 9));
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 5);
        row.AddChild(input);
        if (amount != null) row.AddChild(amount);
        var button = UiFactory.CreateButton(TranslationServer.Translate(buttonKey));
        button.Pressed += pressed;
        row.AddChild(button);
        parent.AddChild(row);
    }
}
