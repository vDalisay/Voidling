using System;
using System.Linq;
using Godot;

namespace VoidlingGame;

public partial class MainController : Node
{
    private const float ScreenWidth = 640.0f;
    private const float ScreenHeight = 360.0f;

    private GardenController _garden = null!;
    private CanvasLayer _uiLayer = null!;
    private Control _uiRoot = null!;
    private Label _coinsLabel = null!;
    private PanelContainer? _detailsPanel;
    private PanelContainer? _eggsPanel;
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

        _uiRoot = new Control { MouseFilter = Control.MouseFilterEnum.Pass };
        _uiRoot.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _uiLayer.AddChild(_uiRoot);

        BuildTopBar();
        BuildToast();

        GameSession.Instance.StateChanged += RefreshUi;
        GameSession.Instance.ToastRequested += ShowToast;
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
        var panel = UiFactory.CreatePanel(new Vector2(616, 42));
        panel.Position = new Vector2(12, 8);
        panel.Size = new Vector2(616, 42);
        _uiRoot.AddChild(panel);

        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 8);
        panel.AddChild(row);

        _coinsLabel = UiFactory.CreateLabel("Sprouts: 0", 11);
        _coinsLabel.CustomMinimumSize = new Vector2(125, 22);
        row.AddChild(_coinsLabel);

        var shop = UiFactory.CreateButton("Shop", 0);
        shop.Pressed += ShowShop;
        row.AddChild(shop);

        var breed = UiFactory.CreateButton("Breed", 6);
        breed.Pressed += ShowBreeding;
        row.AddChild(breed);

        var race = UiFactory.CreateButton("Race", 12);
        race.Pressed += ShowRaceConfirm;
        row.AddChild(race);

        var center = UiFactory.CreateButton("Center");
        center.Pressed += _garden.ResetCamera;
        row.AddChild(center);

        var reset = UiFactory.CreateButton("Reset");
        reset.Pressed += ShowResetConfirm;
        row.AddChild(reset);
    }

    private void BuildToast()
    {
        _toastLabel = UiFactory.CreateLabel("", 10);
        _toastLabel.Position = new Vector2(20, 329);
        _toastLabel.Size = new Vector2(380, 16);
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

        if (_selectedId.Length > 0 && GameSession.Instance.FindVoidling(_selectedId) == null)
            _selectedId = "";

        _garden.Select(_selectedId);
        RebuildDetailsPanel();
        RebuildEggsPanel();
    }

    private void RebuildDetailsPanel()
    {
        if (_detailsPanel != null && GodotObject.IsInstanceValid(_detailsPanel))
            _detailsPanel.QueueFree();
        _detailsPanel = null;

        var data = GameSession.Instance.FindVoidling(_selectedId);
        if (data == null)
            return;

        _detailsPanel = UiFactory.CreatePanel(new Vector2(198, 262));
        _detailsPanel.Position = new Vector2(430, 58);
        _detailsPanel.Size = new Vector2(198, 262);
        _uiRoot.AddChild(_detailsPanel);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 4);
        _detailsPanel.AddChild(box);

        var heading = new HBoxContainer();
        heading.AddThemeConstantOverride("separation", 5);
        var title = UiFactory.CreateTitle(data.Name);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        heading.AddChild(title);
        var close = UiFactory.CreateButton("X");
        close.CustomMinimumSize = new Vector2(27, 23);
        close.Pressed += DeselectVoidling;
        heading.AddChild(close);
        box.AddChild(heading);

        var stage = data.Stage == LifeStage.Adult
            ? "Adult"
            : $"Child • {Math.Max(0, (int)Math.Ceiling(GameRules.ChildToAdultSeconds - data.AgeSeconds))}s to adult";
        box.AddChild(UiFactory.CreateLabel(stage, 8));

        foreach (var statId in GameRules.StatIds)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 4);

            var gene = GameRules.GetGene(data, statId);
            var effective = (int)Math.Round(GameRules.EffectiveStat(data, statId));
            var count = GameSession.Instance.State.TrainingItems.TryGetValue(statId, out var owned) ? owned : 0;

            var label = UiFactory.CreateLabel(
                $"{GameRules.StatDisplayNames[statId],-7} {GameRules.GradeName(gene.ExpressedValue)}  {effective:00}", 8);
            label.CustomMinimumSize = new Vector2(112, 18);
            label.TooltipText = $"DNA {GameRules.GradeName(gene.AlleleA)}/{GameRules.GradeName(gene.AlleleB)} • training {GameRules.GetTrainingPoints(data, statId)}";
            row.AddChild(label);

            var use = UiFactory.CreateButton($"+ ({count})");
            use.CustomMinimumSize = new Vector2(52, 20);
            UiFactory.ApplyPixelFont(use, 7);
            use.Disabled = count <= 0;
            var capturedStat = statId;
            use.Pressed += () => GameSession.Instance.UseTrainingItem(_selectedId, capturedStat);
            row.AddChild(use);
            box.AddChild(row);
        }

        var profileButtons = new HBoxContainer();
        profileButtons.AddThemeConstantOverride("separation", 6);
        var dna = UiFactory.CreateButton("DNA");
        dna.CustomMinimumSize = new Vector2(76, 22);
        dna.Pressed += ShowDnaProfile;
        profileButtons.AddChild(dna);
        var visual = UiFactory.CreateButton("Visual");
        visual.CustomMinimumSize = new Vector2(76, 22);
        visual.Pressed += ShowVisualProfile;
        profileButtons.AddChild(visual);
        box.AddChild(profileButtons);

        var familyRow = new HBoxContainer();
        familyRow.AddThemeConstantOverride("separation", 4);
        var parentText = data.ParentAId.Length > 0
            ? $"Parents: {GameSession.Instance.NameFor(data.ParentAId)} + {GameSession.Instance.NameFor(data.ParentBId)}"
            : "Parents: starter/store line";
        var parents = UiFactory.CreateLabel(parentText, 7);
        parents.CustomMinimumSize = new Vector2(105, 28);
        parents.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        familyRow.AddChild(parents);
        var familyTree = UiFactory.CreateButton("Family tree");
        familyTree.CustomMinimumSize = new Vector2(67, 25);
        UiFactory.ApplyPixelFont(familyTree, 6);
        familyTree.Pressed += ShowFamilyTree;
        familyRow.AddChild(familyTree);
        box.AddChild(familyRow);

        if (data.InbreedingHistoryFlag || data.InbreedingBurdenLevel > 0)
        {
            var inbred = UiFactory.CreateLabel($"INBRED history • burden {data.InbreedingBurdenLevel}", 7);
            inbred.AddThemeColorOverride("font_color", Color.FromHtml("#A75D55"));
            box.AddChild(inbred);
        }

        if (data.RareTraits.Count > 0)
        {
            var traits = string.Join(", ", data.RareTraits.Select(t =>
                $"{t.TraitId} G{t.GenerationFromFounder}{(t.CanTransmit ? "" : " terminal")}"));
            var rare = UiFactory.CreateLabel($"Rare: {traits}", 7);
            rare.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            box.AddChild(rare);
        }
    }

    private void RebuildEggsPanel()
    {
        if (_eggsPanel != null && GodotObject.IsInstanceValid(_eggsPanel))
            _eggsPanel.QueueFree();

        _eggsPanel = UiFactory.CreatePanel(new Vector2(400, 54));
        _eggsPanel.Position = new Vector2(12, 294);
        _eggsPanel.Size = new Vector2(400, 54);
        _uiRoot.AddChild(_eggsPanel);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        _eggsPanel.AddChild(row);

        var eggs = GameSession.Instance.State.OwnedEggs;
        var text = new VBoxContainer { CustomMinimumSize = new Vector2(310, 32) };
        text.AddChild(UiFactory.CreateLabel($"Eggs on island: {eggs.Count}", 9));

        if (eggs.Count == 0)
            text.AddChild(UiFactory.CreateLabel("Buy or breed an egg. It will appear in the garden.", 7));
        else
        {
            var summaries = eggs.Take(5).Select(egg => egg.State == EggState.Failed
                ? "FAILED"
                : $"{Math.Max(0, (int)Math.Ceiling(egg.RequiredIncubationSeconds - egg.IncubationSeconds))}s");
            text.AddChild(UiFactory.CreateLabel(string.Join("  •  ", summaries), 7));
        }
        row.AddChild(text);

        var failed = eggs.FirstOrDefault(e => e.State == EggState.Failed);
        if (failed != null)
        {
            var discard = UiFactory.CreateButton("Discard");
            discard.CustomMinimumSize = new Vector2(60, 22);
            discard.Pressed += () => GameSession.Instance.DiscardFailedEgg(failed.Id);
            row.AddChild(discard);
        }
    }

    private void ShowShop()
    {
        var box = OpenModal("SPROUT SHOP", new Vector2(430, 300));
        box.AddChild(UiFactory.CreateLabel("Training treats", 11));

        foreach (var statId in GameRules.StatIds)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            var owned = GameSession.Instance.State.TrainingItems.TryGetValue(statId, out var count) ? count : 0;
            var label = UiFactory.CreateLabel($"{GameRules.StatDisplayNames[statId]} treat  • owned {owned}", 8);
            label.CustomMinimumSize = new Vector2(250, 22);
            row.AddChild(label);
            var buy = UiFactory.CreateButton($"{GameRules.TrainingItemPrice} sprouts");
            buy.CustomMinimumSize = new Vector2(105, 22);
            var capturedStat = statId;
            buy.Pressed += () =>
            {
                GameSession.Instance.BuyTrainingItem(capturedStat);
                ShowShop();
            };
            row.AddChild(buy);
            box.AddChild(row);
        }

        box.AddChild(UiFactory.CreateLabel("Mystery eggs — genetics lock when stocked", 10));

        for (var i = 0; i < GameSession.Instance.State.StoreEggs.Count; i++)
        {
            var egg = GameSession.Instance.State.StoreEggs[i];
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            row.AddChild(new ColorRect
            {
                Color = GameRules.TintColor(egg.TintHex),
                CustomMinimumSize = new Vector2(20, 20)
            });
            var label = UiFactory.CreateLabel($"Egg {i + 1} • hidden stats already fixed", 8);
            label.CustomMinimumSize = new Vector2(225, 22);
            row.AddChild(label);
            var buy = UiFactory.CreateButton($"{GameRules.StoreEggPrice} sprouts");
            buy.CustomMinimumSize = new Vector2(105, 22);
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
        var adults = GameSession.Instance.State.Voidlings.Where(v => v.Stage == LifeStage.Adult).ToList();
        var box = OpenModal("BREEDING NEST", new Vector2(400, 220));

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
        parentB.Selected = 1;

        var selectors = new HBoxContainer();
        selectors.AddThemeConstantOverride("separation", 8);
        selectors.AddChild(parentA);
        selectors.AddChild(UiFactory.CreateLabel(" + ", 12));
        selectors.AddChild(parentB);
        box.AddChild(selectors);

        var preview = UiFactory.CreateLabel("", 8);
        preview.CustomMinimumSize = new Vector2(350, 44);
        preview.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(preview);

        void UpdatePreview() => preview.Text = GameSession.Instance.GetBreedingPreview(
            adults[parentA.Selected].Id, adults[parentB.Selected].Id);

        parentA.ItemSelected += _ => UpdatePreview();
        parentB.ItemSelected += _ => UpdatePreview();
        UpdatePreview();

        var breed = UiFactory.CreateButton("Breed");
        breed.CustomMinimumSize = new Vector2(110, 26);
        breed.Pressed += () =>
        {
            var a = adults[parentA.Selected];
            var b = adults[parentB.Selected];
            var check = GameSession.Instance.GetBreedingPreview(a.Id, b.Id);
            if (a.Id == b.Id || a.Stage != LifeStage.Adult || b.Stage != LifeStage.Adult ||
                a.BreedCooldownSeconds > 0.0f || b.BreedCooldownSeconds > 0.0f)
            {
                preview.Text = check;
                return;
            }

            CloseModal();
            _garden.PlayBreedingAnimation(a.Id, b.Id, eggPosition =>
            {
                GameSession.Instance.TryBreed(a.Id, b.Id, eggPosition);
            });
        };
        box.AddChild(breed);
        box.AddChild(UiFactory.CreateLabel("The parents approach, a heart appears, then their egg is placed between them.", 7));
    }

    private void ShowRaceConfirm()
    {
        var selected = GameSession.Instance.FindVoidling(_selectedId);
        selected ??= GameSession.Instance.State.Voidlings.FirstOrDefault();
        if (selected == null)
            return;

        var box = OpenModal("AUTOMATED RACE", new Vector2(330, 165));
        var run = (int)Math.Round(GameRules.EffectiveStat(selected, "run"));
        box.AddChild(UiFactory.CreateLabel($"Enter {selected.Name}?  • Run {run}", 10));
        var info = UiFactory.CreateLabel("Only this owned Voidling enters. The other three racers are generated CPU opponents.", 8);
        info.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(info);
        var start = UiFactory.CreateButton("Start Race");
        start.Pressed += () =>
        {
            CloseModal();
            StartRace(selected);
        };
        box.AddChild(start);
    }

    private void ShowDnaProfile()
    {
        var data = GameSession.Instance.FindVoidling(_selectedId);
        if (data == null)
            return;

        var box = OpenModal($"{data.Name.ToUpperInvariant()} — DNA", new Vector2(510, 300));
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 12);
        header.AddChild(UiFactory.CreatePortrait(data, new Vector2(70, 70)));
        var summary = new VBoxContainer();
        summary.AddChild(UiFactory.CreateLabel($"Generation {data.FamilyGeneration}", 9));
        summary.AddChild(UiFactory.CreateLabel($"Inbreeding burden: {data.InbreedingBurdenLevel}", 8));
        summary.AddChild(UiFactory.CreateLabel("Each stat has two inherited alleles; SHOWS is the expressed one.", 8));
        header.AddChild(summary);
        box.AddChild(header);

        var grid = new GridContainer { Columns = 5 };
        foreach (var heading in new[] { "STAT", "DNA A", "DNA B", "SHOWS", "CURRENT" })
            grid.AddChild(UiFactory.CreateLabel(heading, 8));

        foreach (var statId in GameRules.StatIds)
        {
            var gene = GameRules.GetGene(data, statId);
            grid.AddChild(UiFactory.CreateLabel(GameRules.StatDisplayNames[statId], 8));
            grid.AddChild(UiFactory.CreateLabel(GameRules.GradeName(gene.AlleleA), 8));
            grid.AddChild(UiFactory.CreateLabel(GameRules.GradeName(gene.AlleleB), 8));
            grid.AddChild(UiFactory.CreateLabel(GameRules.GradeName(gene.ExpressedValue), 8));
            grid.AddChild(UiFactory.CreateLabel(((int)Math.Round(GameRules.EffectiveStat(data, statId))).ToString(), 8));
        }
        box.AddChild(grid);

        var expressedColor = data.Genome.ExpressedColorIndex == 0 ? data.Genome.ColorAlleleA : data.Genome.ColorAlleleB;
        box.AddChild(UiFactory.CreateLabel(
            $"Color DNA: #{data.Genome.ColorAlleleA} / #{data.Genome.ColorAlleleB}  • expressed #{expressedColor}", 8));

        if (data.RareTraits.Count == 0)
            box.AddChild(UiFactory.CreateLabel("Rare appearance DNA: none", 8));
        else
        {
            foreach (var trait in data.RareTraits)
            {
                var founderName = GameSession.Instance.NameFor(trait.FounderCreatureId);
                box.AddChild(UiFactory.CreateLabel(
                    $"Rare DNA: {trait.TraitId} • founder {founderName} • G{trait.GenerationFromFounder} • {(trait.CanTransmit ? "transmissible" : "terminal")}", 7));
            }
        }
    }

    private void ShowVisualProfile()
    {
        var data = GameSession.Instance.FindVoidling(_selectedId);
        if (data == null)
            return;

        var box = OpenModal($"{data.Name.ToUpperInvariant()} — VISUAL", new Vector2(390, 250));
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 18);
        row.AddChild(UiFactory.CreatePortrait(data, new Vector2(130, 130)));

        var info = new VBoxContainer();
        info.AddThemeConstantOverride("separation", 7);
        info.AddChild(UiFactory.CreateLabel("Current color", 9));
        info.AddChild(new ColorRect
        {
            Color = GameRules.TintColor(data.TintHex),
            CustomMinimumSize = new Vector2(95, 28)
        });
        var expressedColor = data.Genome.ExpressedColorIndex == 0 ? data.Genome.ColorAlleleA : data.Genome.ColorAlleleB;
        info.AddChild(UiFactory.CreateLabel($"Expressed color gene: #{expressedColor}", 8));
        info.AddChild(UiFactory.CreateLabel($"Color alleles: #{data.Genome.ColorAlleleA} / #{data.Genome.ColorAlleleB}", 8));
        row.AddChild(info);
        box.AddChild(row);

        if (data.RareTraits.Count == 0)
            box.AddChild(UiFactory.CreateLabel("No shiny-level appearance trait.", 8));
        else
        {
            var rare = string.Join(" • ", data.RareTraits.Select(t => $"{t.TraitId} G{t.GenerationFromFounder}"));
            box.AddChild(UiFactory.CreateLabel($"Rare appearance: {rare}", 8));
        }
    }

    private void ShowFamilyTree()
    {
        var data = GameSession.Instance.FindVoidling(_selectedId);
        if (data == null)
            return;

        var box = OpenModal($"{data.Name.ToUpperInvariant()} — FAMILY TREE", new Vector2(604, 324));
        var note = UiFactory.CreateLabel("Parents are above descendants. Historical inbreeding marks remain visible after the active burden is cleansed.", 7);
        note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(note);

        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(565, 245),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        var tree = new FamilyTreeView();
        tree.Build(data.Id, GameSession.Instance.State.Voidlings);
        scroll.AddChild(tree);
        box.AddChild(scroll);
    }

    private void ShowResetConfirm()
    {
        var box = OpenModal("RESET DEMO?", new Vector2(300, 145));
        var label = UiFactory.CreateLabel("Clears this local MVP save and restores the starter Voidlings.", 8);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(label);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        var cancel = UiFactory.CreateButton("Cancel");
        cancel.Pressed += CloseModal;
        row.AddChild(cancel);
        var reset = UiFactory.CreateButton("Reset");
        reset.Pressed += () =>
        {
            CloseModal();
            DeselectVoidling();
            _garden.ResetCamera();
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
            Size = new Vector2(ScreenWidth, ScreenHeight)
        };
        _uiRoot.AddChild(overlay);
        _modal = overlay;

        var shade = new ColorRect
        {
            Color = new Color(0.16f, 0.24f, 0.20f, 0.48f),
            Position = Vector2.Zero,
            Size = new Vector2(ScreenWidth, ScreenHeight),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        overlay.AddChild(shade);

        var panel = UiFactory.CreatePanel(size);
        panel.Position = new Vector2((ScreenWidth - size.X) * 0.5f, (ScreenHeight - size.Y) * 0.5f);
        panel.Size = size;
        overlay.AddChild(panel);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 6);
        panel.AddChild(box);

        var heading = new HBoxContainer();
        var titleLabel = UiFactory.CreateTitle(title);
        titleLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        heading.AddChild(titleLabel);
        var close = UiFactory.CreateButton("X");
        close.CustomMinimumSize = new Vector2(28, 22);
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

    private void DeselectVoidling()
    {
        _selectedId = "";
        _garden.ClearSelection();
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
        option.CustomMinimumSize = new Vector2(155, 24);
        UiFactory.ApplyPixelFont(option, 8);
        option.AddThemeColorOverride("font_color", Color.FromHtml("#465247"));
    }
}
