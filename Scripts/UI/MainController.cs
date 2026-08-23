using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace VoidlingGame;

public partial class MainController : Node
{
    private GardenController _garden = null!;
    private CanvasLayer _uiLayer = null!;
    private Control _uiRoot = null!;
    private Label _coinsLabel = null!;
    private PanelContainer _detailsPanel = null!;
    private PanelContainer _eggsPanel = null!;
    private Control? _modal;
    private Label _toastLabel = null!;
    private float _toastSeconds;
    private string _selectedId = "";
    private RaceController? _race;

    public override void _Ready()
    {
        _garden = GetNode<GardenController>("Garden");
        _garden.VoidlingSelected += OnVoidlingSelected;

        _uiLayer = new CanvasLayer { Layer = 10 };
        AddChild(_uiLayer);

        _uiRoot = new Control
        {
            MouseFilter = Control.MouseFilterEnum.Pass
        };
        _uiRoot.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _uiLayer.AddChild(_uiRoot);

        BuildTopBar();
        BuildToast();

        GameSession.Instance.StateChanged += RefreshUi;
        GameSession.Instance.ToastRequested += ShowToast;

        var first = GameSession.Instance.State.Voidlings.FirstOrDefault();
        if (first != null)
            _selectedId = first.Id;

        RefreshUi();
    }

    public override void _ExitTree()
    {
        if (GameSession.Instance != null)
        {
            GameSession.Instance.StateChanged -= RefreshUi;
            GameSession.Instance.ToastRequested -= ShowToast;
        }
    }

    public override void _Process(double delta)
    {
        if (_toastSeconds <= 0.0f)
            return;

        _toastSeconds -= (float)delta;
        if (_toastSeconds <= 0.0f)
            _toastLabel.Visible = false;
    }

    private void BuildTopBar()
    {
        var panel = UiFactory.CreatePanel(new Vector2(464, 34));
        panel.Position = new Vector2(8, 6);
        panel.Size = new Vector2(464, 34);
        _uiRoot.AddChild(panel);

        var row = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center
        };
        panel.AddChild(row);

        _coinsLabel = UiFactory.CreateLabel("Sprouts: 0", 11);
        _coinsLabel.CustomMinimumSize = new Vector2(100, 20);
        row.AddChild(_coinsLabel);

        var shop = UiFactory.CreateButton("Shop", 0);
        shop.Pressed += ShowShop;
        row.AddChild(shop);

        var breed = UiFactory.CreateButton("Breed", 6);
        breed.Pressed += ShowBreeding;
        row.AddChild(breed);

        var race = UiFactory.CreateButton("Race", 12);
        race.Pressed += ShowRacePicker;
        row.AddChild(race);

        var reset = UiFactory.CreateButton("Reset");
        reset.CustomMinimumSize = new Vector2(54, 22);
        reset.Pressed += ShowResetConfirm;
        row.AddChild(reset);
    }

    private void BuildToast()
    {
        _toastLabel = UiFactory.CreateLabel("", 10);
        _toastLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _toastLabel.Position = new Vector2(90, 241);
        _toastLabel.Size = new Vector2(300, 22);
        _toastLabel.AddThemeColorOverride("font_color", Color.FromHtml("#F9F4D8"));
        _toastLabel.AddThemeColorOverride("font_shadow_color", Color.FromHtml("#465247"));
        _toastLabel.AddThemeConstantOverride("shadow_offset_x", 1);
        _toastLabel.AddThemeConstantOverride("shadow_offset_y", 1);
        _toastLabel.Visible = false;
        _uiRoot.AddChild(_toastLabel);
    }

    private void RefreshUi()
    {
        _coinsLabel.Text = $"Sprouts: {GameSession.Instance.State.Coins}";

        if (GameSession.Instance.FindVoidling(_selectedId) == null)
        {
            _selectedId = GameSession.Instance.State.Voidlings.FirstOrDefault()?.Id ?? "";
        }

        _garden.Select(_selectedId);
        RebuildDetailsPanel();
        RebuildEggsPanel();
    }

    private void RebuildDetailsPanel()
    {
        if (_detailsPanel != null && GodotObject.IsInstanceValid(_detailsPanel))
            _detailsPanel.QueueFree();

        _detailsPanel = UiFactory.CreatePanel(new Vector2(154, 191));
        _detailsPanel.Position = new Vector2(318, 45);
        _detailsPanel.Size = new Vector2(154, 191);
        _uiRoot.AddChild(_detailsPanel);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 2);
        _detailsPanel.AddChild(box);

        var data = GameSession.Instance.FindVoidling(_selectedId);
        if (data == null)
        {
            box.AddChild(UiFactory.CreateTitle("No Voidlings"));
            return;
        }

        var title = UiFactory.CreateTitle(data.Name);
        box.AddChild(title);

        var stage = data.Stage == LifeStage.Adult
            ? "Adult"
            : $"Child • {Math.Max(0, (int)Math.Ceiling(GameRules.ChildToAdultSeconds - data.AgeSeconds))}s to adult";
        box.AddChild(UiFactory.CreateLabel(stage, 9));

        foreach (var statId in GameRules.StatIds)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 2);

            var gene = GameRules.GetGene(data, statId);
            var training = GameRules.GetTrainingPoints(data, statId);
            var effective = (int)Math.Round(GameRules.EffectiveStat(data, statId));
            var count = GameSession.Instance.State.TrainingItems.TryGetValue(statId, out var owned) ? owned : 0;

            var label = UiFactory.CreateLabel(
                $"{GameRules.StatDisplayNames[statId],-7} {GameRules.GradeName(gene.ExpressedValue)}  {effective:00}",
                9);
            label.CustomMinimumSize = new Vector2(88, 18);
            label.TooltipText = $"DNA {GameRules.GradeName(gene.AlleleA)}/{GameRules.GradeName(gene.AlleleB)} • training {training}";
            row.AddChild(label);

            var use = UiFactory.CreateButton($"+ ({count})");
            use.CustomMinimumSize = new Vector2(43, 18);
            use.AddThemeFontSizeOverride("font_size", 8);
            use.Disabled = count <= 0;
            var capturedStat = statId;
            use.Pressed += () => GameSession.Instance.UseTrainingItem(_selectedId, capturedStat);
            row.AddChild(use);

            box.AddChild(row);
        }

        var parentText = data.ParentAId.Length > 0
            ? $"Parents: {GameSession.Instance.NameFor(data.ParentAId)} + {GameSession.Instance.NameFor(data.ParentBId)}"
            : "Parents: starter/store line";
        box.AddChild(UiFactory.CreateLabel(parentText, 8));

        if (data.InbreedingHistoryFlag || data.InbreedingBurdenLevel > 0)
        {
            var inbred = UiFactory.CreateLabel(
                $"Family mark: INBRED • burden {data.InbreedingBurdenLevel}",
                8);
            inbred.AddThemeColorOverride("font_color", Color.FromHtml("#A75D55"));
            box.AddChild(inbred);
        }

        if (data.RareTraits.Count > 0)
        {
            var traits = string.Join(", ", data.RareTraits.Select(t =>
                $"{t.TraitId} G{t.GenerationFromFounder}{(t.CanTransmit ? "" : " terminal")}"));
            var rare = UiFactory.CreateLabel($"Rare: {traits}", 8);
            rare.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            box.AddChild(rare);
        }
    }

    private void RebuildEggsPanel()
    {
        if (_eggsPanel != null && GodotObject.IsInstanceValid(_eggsPanel))
            _eggsPanel.QueueFree();

        _eggsPanel = UiFactory.CreatePanel(new Vector2(303, 49));
        _eggsPanel.Position = new Vector2(8, 213);
        _eggsPanel.Size = new Vector2(303, 49);
        _uiRoot.AddChild(_eggsPanel);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 4);
        _eggsPanel.AddChild(row);

        var eggs = GameSession.Instance.State.OwnedEggs;
        var text = new VBoxContainer();
        text.CustomMinimumSize = new Vector2(220, 30);
        text.AddChild(UiFactory.CreateLabel($"Eggs: {eggs.Count}", 10));

        if (eggs.Count == 0)
        {
            text.AddChild(UiFactory.CreateLabel("Buy or breed an egg to hatch a new Voidling.", 8));
        }
        else
        {
            var summaries = eggs.Take(4).Select(egg =>
            {
                if (egg.State == EggState.Failed)
                    return "FAILED";
                var remaining = Math.Max(0, (int)Math.Ceiling(egg.RequiredIncubationSeconds - egg.IncubationSeconds));
                return $"{remaining}s";
            });
            text.AddChild(UiFactory.CreateLabel(string.Join("  •  ", summaries), 8));
        }

        row.AddChild(text);

        var failed = eggs.FirstOrDefault(e => e.State == EggState.Failed);
        if (failed != null)
        {
            var discard = UiFactory.CreateButton("Discard");
            discard.CustomMinimumSize = new Vector2(58, 20);
            discard.Pressed += () => GameSession.Instance.DiscardFailedEgg(failed.Id);
            row.AddChild(discard);
        }
    }

    private void ShowShop()
    {
        var box = OpenModal("SPROUT SHOP", new Vector2(360, 228));

        box.AddChild(UiFactory.CreateLabel("Training treats", 11));

        foreach (var statId in GameRules.StatIds)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 4);

            var owned = GameSession.Instance.State.TrainingItems.TryGetValue(statId, out var count) ? count : 0;
            var label = UiFactory.CreateLabel($"{GameRules.StatDisplayNames[statId]} treat  • owned {owned}", 9);
            label.CustomMinimumSize = new Vector2(205, 20);
            row.AddChild(label);

            var buy = UiFactory.CreateButton($"{GameRules.TrainingItemPrice} sprouts");
            var capturedStat = statId;
            buy.Pressed += () =>
            {
                GameSession.Instance.BuyTrainingItem(capturedStat);
                ShowShop();
            };
            row.AddChild(buy);
            box.AddChild(row);
        }

        box.AddChild(UiFactory.CreateLabel("Mystery eggs — genetics lock when stocked", 11));

        for (var i = 0; i < GameSession.Instance.State.StoreEggs.Count; i++)
        {
            var egg = GameSession.Instance.State.StoreEggs[i];
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 4);

            var swatch = new ColorRect
            {
                Color = GameRules.TintColor(egg.TintHex),
                CustomMinimumSize = new Vector2(18, 18)
            };
            row.AddChild(swatch);

            var label = UiFactory.CreateLabel($"Egg {i + 1} • hidden stats fixed", 9);
            label.CustomMinimumSize = new Vector2(182, 20);
            row.AddChild(label);

            var buy = UiFactory.CreateButton($"{GameRules.StoreEggPrice} sprouts");
            var eggId = egg.Id;
            buy.Pressed += () =>
            {
                GameSession.Instance.BuyStoreEgg(eggId);
                ShowShop();
            };
            row.AddChild(buy);

            box.AddChild(row);
        }
    }

    private void ShowBreeding()
    {
        var adults = GameSession.Instance.State.Voidlings
            .Where(v => v.Stage == LifeStage.Adult)
            .ToList();

        var box = OpenModal("BREEDING NEST", new Vector2(342, 190));

        if (adults.Count < 2)
        {
            box.AddChild(UiFactory.CreateLabel("You need two adult Voidlings.", 10));
            return;
        }

        var parentA = new OptionButton();
        var parentB = new OptionButton();
        StyleOption(parentA);
        StyleOption(parentB);

        foreach (var adult in adults)
        {
            parentA.AddItem(adult.Name);
            parentB.AddItem(adult.Name);
        }

        parentA.Selected = 0;
        parentB.Selected = adults.Count > 1 ? 1 : 0;

        var selectors = new HBoxContainer();
        selectors.AddChild(parentA);
        selectors.AddChild(UiFactory.CreateLabel(" + ", 12));
        selectors.AddChild(parentB);
        box.AddChild(selectors);

        var preview = UiFactory.CreateLabel("", 9);
        preview.CustomMinimumSize = new Vector2(300, 42);
        preview.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(preview);

        void UpdatePreview()
        {
            preview.Text = GameSession.Instance.GetBreedingPreview(
                adults[parentA.Selected].Id,
                adults[parentB.Selected].Id);
        }

        parentA.ItemSelected += _ => UpdatePreview();
        parentB.ItemSelected += _ => UpdatePreview();
        UpdatePreview();

        var breed = UiFactory.CreateButton("Create Egg");
        breed.Pressed += () =>
        {
            if (GameSession.Instance.TryBreed(
                    adults[parentA.Selected].Id,
                    adults[parentB.Selected].Id))
            {
                CloseModal();
            }
            else
            {
                UpdatePreview();
            }
        };
        box.AddChild(breed);

        box.AddChild(UiFactory.CreateLabel(
            "Offspring inherits one DNA allele from each parent. No autonomous breeding.",
            8));
    }

    private void ShowRacePicker()
    {
        var candidates = GameSession.Instance.State.Voidlings.ToList();
        var box = OpenModal("AUTOMATED RACE", new Vector2(315, 155));

        if (candidates.Count == 0)
        {
            box.AddChild(UiFactory.CreateLabel("No Voidlings available.", 10));
            return;
        }

        box.AddChild(UiFactory.CreateLabel(
            "Run controls speed and obstacle avoidance. Stamina reduces late-race fatigue.",
            9));

        var chooser = new OptionButton();
        StyleOption(chooser);
        foreach (var creature in candidates)
        {
            var run = (int)Math.Round(GameRules.EffectiveStat(creature, "run"));
            chooser.AddItem($"{creature.Name}  • Run {run}");
        }
        box.AddChild(chooser);

        var start = UiFactory.CreateButton("Start Race");
        start.Pressed += () =>
        {
            var chosen = candidates[chooser.Selected];
            CloseModal();
            StartRace(chosen);
        };
        box.AddChild(start);
    }

    private void ShowResetConfirm()
    {
        var box = OpenModal("RESET DEMO?", new Vector2(280, 130));
        box.AddChild(UiFactory.CreateLabel(
            "Clears this local MVP save and restores the two starter Voidlings.",
            9));

        var row = new HBoxContainer();
        var cancel = UiFactory.CreateButton("Cancel");
        cancel.Pressed += CloseModal;
        row.AddChild(cancel);

        var reset = UiFactory.CreateButton("Reset");
        reset.Pressed += () =>
        {
            CloseModal();
            GameSession.Instance.ResetDemo();
        };
        row.AddChild(reset);
        box.AddChild(row);
    }

    private VBoxContainer OpenModal(string title, Vector2 size)
    {
        CloseModal();

        var overlay = new Control
        {
            MouseFilter = Control.MouseFilterEnum.Stop,
            Position = Vector2.Zero,
            Size = new Vector2(480, 270)
        };
        _uiRoot.AddChild(overlay);
        _modal = overlay;

        var shade = new ColorRect
        {
            Color = new Color(0.16f, 0.24f, 0.20f, 0.42f),
            Position = Vector2.Zero,
            Size = new Vector2(480, 270),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        overlay.AddChild(shade);

        var panel = UiFactory.CreatePanel(size);
        panel.Position = new Vector2((480 - size.X) * 0.5f, (270 - size.Y) * 0.5f);
        panel.Size = size;
        overlay.AddChild(panel);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 4);
        panel.AddChild(box);

        var heading = new HBoxContainer();
        var titleLabel = UiFactory.CreateTitle(title);
        titleLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        heading.AddChild(titleLabel);

        var close = UiFactory.CreateButton("X");
        close.CustomMinimumSize = new Vector2(24, 20);
        close.Pressed += CloseModal;
        heading.AddChild(close);
        box.AddChild(heading);

        return box;
    }

    private void CloseModal()
    {
        if (_modal != null && GodotObject.IsInstanceValid(_modal))
            _modal.QueueFree();
        _modal = null;
    }

    private void StartRace(VoidlingData selected)
    {
        _garden.Visible = false;
        _uiRoot.Visible = false;

        _race = new RaceController();
        AddChild(_race);
        _race.ReturnRequested += EndRace;
        _race.Setup(selected);
    }

    private void EndRace()
    {
        if (_race != null && GodotObject.IsInstanceValid(_race))
            _race.QueueFree();
        _race = null;

        _garden.Visible = true;
        _uiRoot.Visible = true;
        RefreshUi();
    }

    private void OnVoidlingSelected(string creatureId)
    {
        _selectedId = creatureId;
        RefreshUi();
    }

    private void ShowToast(string text)
    {
        _toastLabel.Text = text;
        _toastLabel.Visible = true;
        _toastSeconds = 3.0f;
    }

    private static void StyleOption(OptionButton option)
    {
        option.CustomMinimumSize = new Vector2(135, 22);
        option.AddThemeFontSizeOverride("font_size", 9);
        option.AddThemeColorOverride("font_color", Color.FromHtml("#465247"));
    }
}
