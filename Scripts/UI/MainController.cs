using System;
using System.Linq;
using Godot;

namespace VoidlingGame;

public partial class MainController : Node
{
    private const float ScreenWidth = 640.0f;
    private const float ScreenHeight = 360.0f;

    private static readonly Texture2D EggTexture = GD.Load<Texture2D>(
        "res://Assets/Sprout Lands - Sprites - Basic pack/Objects/Egg item.png");

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
        var panel = UiFactory.CreatePanel(new Vector2(624, 44));
        panel.Position = new Vector2(8, 7);
        panel.Size = new Vector2(624, 44);
        _uiRoot.AddChild(panel);

        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 5);
        panel.AddChild(row);

        _coinsLabel = UiFactory.CreateLabel("Sprouts: 0", 10);
        _coinsLabel.CustomMinimumSize = new Vector2(92, 22);
        row.AddChild(_coinsLabel);

        AddTopButton(row, "Shop", ShowShop, 0, 67);
        AddTopButton(row, "Inventory", ShowInventory, 3, 78);
        AddTopButton(row, "Breed", ShowBreeding, 6, 67);
        AddTopButton(row, "Race", ShowRacePicker, 12, 67);
        AddTopButton(row, "Center", _garden.ResetCamera, -1, 67);
        AddTopButton(row, "Reset", ShowResetConfirm, -1, 64);
    }

    private static void AddTopButton(HBoxContainer row, string text, Action action, int iconIndex, float width)
    {
        var button = UiFactory.CreateButton(text, iconIndex);
        button.CustomMinimumSize = new Vector2(width, 24);
        UiFactory.ApplyPixelFont(button, 8);
        button.Pressed += action;
        row.AddChild(button);
    }

    private void BuildToast()
    {
        _toastLabel = UiFactory.CreateLabel("", 9);
        _toastLabel.Position = new Vector2(18, 330);
        _toastLabel.Size = new Vector2(390, 16);
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

        _detailsPanel = UiFactory.CreatePanel(new Vector2(220, 288));
        _detailsPanel.Position = new Vector2(408, 57);
        _detailsPanel.Size = new Vector2(220, 288);
        _uiRoot.AddChild(_detailsPanel);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 4);
        _detailsPanel.AddChild(box);

        var heading = new HBoxContainer();
        heading.AddThemeConstantOverride("separation", 4);

        var title = UiFactory.CreateTitle(data.Name);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        title.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        heading.AddChild(title);

        var follow = UiFactory.CreateButton("◉");
        follow.CustomMinimumSize = new Vector2(28, 23);
        UiFactory.ApplyPixelFont(follow, 10);
        follow.TooltipText = "Follow this Voidling with the camera";
        follow.Pressed += () => _garden.ToggleFollowVoidling(data.Id);
        heading.AddChild(follow);

        var close = UiFactory.CreateButton("X");
        close.CustomMinimumSize = new Vector2(28, 23);
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
            row.AddThemeConstantOverride("separation", 5);

            var gene = GameRules.GetGene(data, statId);
            var effective = (int)Math.Round(GameRules.EffectiveStat(data, statId));
            var count = GameSession.Instance.State.TrainingItems.TryGetValue(statId, out var owned) ? owned : 0;

            var label = UiFactory.CreateLabel(
                $"{GameRules.StatDisplayNames[statId],-7} {GameRules.GradeName(gene.ExpressedValue)}  {effective:00}", 8);
            label.CustomMinimumSize = new Vector2(124, 20);
            label.TooltipText = $"DNA {GameRules.GradeName(gene.AlleleA)}/{GameRules.GradeName(gene.AlleleB)} • training {GameRules.GetTrainingPoints(data, statId)}";
            row.AddChild(label);

            var use = UiFactory.CreateButton($"+1 ({count})");
            use.CustomMinimumSize = new Vector2(62, 21);
            UiFactory.ApplyPixelFont(use, 7);
            use.Disabled = count <= 0;
            var capturedStat = statId;
            use.Pressed += () => GameSession.Instance.UseTrainingItem(_selectedId, capturedStat);
            row.AddChild(use);
            box.AddChild(row);
        }

        var details = UiFactory.CreateButton("Details");
        details.CustomMinimumSize = new Vector2(190, 23);
        UiFactory.ApplyPixelFont(details, 8);
        details.Pressed += ShowDetails;
        box.AddChild(details);

        var parentText = data.ParentAId.Length > 0
            ? $"Parents: {GameSession.Instance.NameFor(data.ParentAId)} + {GameSession.Instance.NameFor(data.ParentBId)}"
            : "Parents: starter/store line";
        var parents = UiFactory.CreateLabel(parentText, 7);
        parents.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        parents.CustomMinimumSize = new Vector2(190, 24);
        box.AddChild(parents);

        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 5);
        var familyTree = UiFactory.CreateButton("Family tree");
        familyTree.CustomMinimumSize = new Vector2(100, 22);
        UiFactory.ApplyPixelFont(familyTree, 7);
        familyTree.Pressed += ShowFamilyTree;
        actions.AddChild(familyTree);

        var goodbye = UiFactory.CreateButton("Goodbye");
        goodbye.CustomMinimumSize = new Vector2(84, 22);
        UiFactory.ApplyPixelFont(goodbye, 7);
        goodbye.AddThemeColorOverride("font_color", Color.FromHtml("#9C514B"));
        goodbye.Pressed += () => ShowGoodbyeFirst(data.Id);
        actions.AddChild(goodbye);
        box.AddChild(actions);

        if (data.InbreedingHistoryFlag || data.InbreedingBurdenLevel > 0)
        {
            var inbred = UiFactory.CreateLabel($"INBRED history • burden {data.InbreedingBurdenLevel}", 6);
            inbred.AddThemeColorOverride("font_color", Color.FromHtml("#A75D55"));
            box.AddChild(inbred);
        }

        if (data.RareTraits.Count > 0)
        {
            var traits = string.Join(", ", data.RareTraits.Select(t =>
                $"{t.TraitId} G{t.GenerationFromFounder}{(t.CanTransmit ? "" : " terminal")}"));
            var rare = UiFactory.CreateLabel($"Rare: {traits}", 6);
            rare.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            box.AddChild(rare);
        }
    }

    private void RebuildEggsPanel()
    {
        if (_eggsPanel != null && GodotObject.IsInstanceValid(_eggsPanel))
            _eggsPanel.QueueFree();

        _eggsPanel = UiFactory.CreatePanel(new Vector2(386, 54));
        _eggsPanel.Position = new Vector2(10, 294);
        _eggsPanel.Size = new Vector2(386, 54);
        _uiRoot.AddChild(_eggsPanel);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        _eggsPanel.AddChild(row);

        var eggs = GameSession.Instance.State.OwnedEggs;
        var text = new VBoxContainer { CustomMinimumSize = new Vector2(292, 32) };
        text.AddChild(UiFactory.CreateLabel($"Eggs on island: {eggs.Count}", 8));

        if (eggs.Count == 0)
            text.AddChild(UiFactory.CreateLabel("Buy or breed an egg to place it in the garden.", 6));
        else
        {
            var summaries = eggs.Take(5).Select(egg => egg.State == EggState.Failed
                ? "FAILED"
                : $"{Math.Max(0, (int)Math.Ceiling(egg.RequiredIncubationSeconds - egg.IncubationSeconds))}s");
            text.AddChild(UiFactory.CreateLabel(string.Join("  •  ", summaries), 6));
        }
        row.AddChild(text);

        var failed = eggs.FirstOrDefault(e => e.State == EggState.Failed);
        if (failed != null)
        {
            var discard = UiFactory.CreateButton("Discard");
            discard.CustomMinimumSize = new Vector2(66, 22);
            UiFactory.ApplyPixelFont(discard, 7);
            discard.Pressed += () => GameSession.Instance.DiscardFailedEgg(failed.Id);
            row.AddChild(discard);
        }
    }

    private void ShowInventory()
    {
        var box = OpenModal("INVENTORY", new Vector2(380, 292));
        box.AddChild(UiFactory.CreateLabel("Items you currently own", 9));

        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(340, 198),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        box.AddChild(scroll);

        var list = new VBoxContainer();
        list.AddThemeConstantOverride("separation", 5);
        scroll.AddChild(list);

        for (var i = 0; i < GameRules.StatIds.Length; i++)
        {
            var statId = GameRules.StatIds[i];
            var count = GameSession.Instance.State.TrainingItems.TryGetValue(statId, out var owned) ? owned : 0;
            list.AddChild(CreateInventoryRow(
                UiFactory.CreateIcon(18 + i),
                $"{GameRules.StatDisplayNames[statId]} Treat",
                count));
        }

        var eggAtlas = new AtlasTexture
        {
            Atlas = EggTexture,
            Region = new Rect2(0, 0, EggTexture.GetWidth(), EggTexture.GetHeight())
        };
        list.AddChild(CreateInventoryRow(eggAtlas, "Eggs on Island", GameSession.Instance.State.OwnedEggs.Count));
    }

    private static Control CreateInventoryRow(Texture2D iconTexture, string itemName, int count)
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(328, 32) };
        var style = new StyleBoxFlat
        {
            BgColor = Color.FromHtml("#F0D9A8"),
            BorderColor = Color.FromHtml("#C59670")
        };
        style.SetBorderWidthAll(1);
        style.ContentMarginLeft = 7;
        style.ContentMarginRight = 7;
        style.ContentMarginTop = 4;
        style.ContentMarginBottom = 4;
        panel.AddThemeStyleboxOverride("panel", style);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        panel.AddChild(row);

        var icon = new TextureRect
        {
            Texture = iconTexture,
            CustomMinimumSize = new Vector2(22, 22),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        row.AddChild(icon);

        var name = UiFactory.CreateLabel(itemName, 8);
        name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        name.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(name);

        var amount = UiFactory.CreateLabel($"x{count}", 9);
        amount.CustomMinimumSize = new Vector2(42, 20);
        amount.HorizontalAlignment = HorizontalAlignment.Right;
        amount.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(amount);
        return panel;
    }

    private void ShowShop()
    {
        var box = OpenModal("SPROUT SHOP", new Vector2(438, 302));
        box.AddChild(UiFactory.CreateLabel("Training treats", 10));

        foreach (var statId in GameRules.StatIds)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            var owned = GameSession.Instance.State.TrainingItems.TryGetValue(statId, out var count) ? count : 0;
            var label = UiFactory.CreateLabel($"{GameRules.StatDisplayNames[statId]} treat  • owned {owned}", 8);
            label.CustomMinimumSize = new Vector2(255, 22);
            row.AddChild(label);
            var buy = UiFactory.CreateButton($"{GameRules.TrainingItemPrice} sprouts");
            buy.CustomMinimumSize = new Vector2(112, 22);
            UiFactory.ApplyPixelFont(buy, 7);
            var capturedStat = statId;
            buy.Pressed += () =>
            {
                GameSession.Instance.BuyTrainingItem(capturedStat);
                ShowShop();
            };
            row.AddChild(buy);
            box.AddChild(row);
        }

        box.AddChild(UiFactory.CreateLabel("Mystery eggs", 10));

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
            var label = UiFactory.CreateLabel($"Egg {i + 1}", 8);
            label.CustomMinimumSize = new Vector2(225, 22);
            row.AddChild(label);
            var buy = UiFactory.CreateButton($"{GameRules.StoreEggPrice} sprouts");
            buy.CustomMinimumSize = new Vector2(112, 22);
            UiFactory.ApplyPixelFont(buy, 7);
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
        var box = OpenModal("BREEDING NEST", new Vector2(420, 225));

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
        preview.CustomMinimumSize = new Vector2(370, 45);
        preview.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(preview);

        void UpdatePreview() => preview.Text = GameSession.Instance.GetBreedingPreview(
            adults[parentA.Selected].Id, adults[parentB.Selected].Id);

        parentA.ItemSelected += _ => UpdatePreview();
        parentB.ItemSelected += _ => UpdatePreview();
        UpdatePreview();

        var breed = UiFactory.CreateButton("Breed");
        breed.CustomMinimumSize = new Vector2(120, 26);
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
        box.AddChild(UiFactory.CreateLabel("The parents approach, show a heart, then place their egg between them.", 7));
    }

    private void ShowRacePicker()
    {
        var owned = GameSession.Instance.State.Voidlings.ToList();
        var box = OpenModal("CHOOSE A RACER", new Vector2(552, 308));

        if (owned.Count == 0)
        {
            box.AddChild(UiFactory.CreateLabel("No Voidlings are currently on the farm.", 10));
            return;
        }

        var chosen = owned.FirstOrDefault(v => v.Id == _selectedId) ?? owned[0];
        box.AddChild(UiFactory.CreateLabel("Choose one owned Voidling. The other racers are generated CPU opponents.", 8));

        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(510, 90),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        var cards = new HBoxContainer();
        cards.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(cards);
        box.AddChild(scroll);

        var previewRow = new HBoxContainer();
        previewRow.AddThemeConstantOverride("separation", 14);
        var previewPortrait = UiFactory.CreatePortrait(chosen, new Vector2(76, 76));
        previewRow.AddChild(previewPortrait);
        var previewText = new VBoxContainer();
        previewText.AddThemeConstantOverride("separation", 3);
        var previewName = UiFactory.CreateTitle(chosen.Name);
        var previewStats = UiFactory.CreateLabel("", 8);
        previewText.AddChild(previewName);
        previewText.AddChild(previewStats);
        previewRow.AddChild(previewText);
        box.AddChild(previewRow);

        void UpdatePreview(VoidlingData candidate)
        {
            chosen = candidate;
            previewPortrait.Modulate = GameRules.TintColor(candidate.TintHex);
            previewName.Text = candidate.Name;
            previewStats.Text = string.Join("   ", GameRules.StatIds.Select(stat =>
                $"{GameRules.StatDisplayNames[stat]} {(int)Math.Round(GameRules.EffectiveStat(candidate, stat))}"));
        }

        foreach (var creature in owned)
        {
            var entry = new VBoxContainer { CustomMinimumSize = new Vector2(86, 78) };
            entry.AddThemeConstantOverride("separation", 1);

            var card = UiFactory.CreateButton("");
            card.CustomMinimumSize = new Vector2(82, 58);
            var portrait = UiFactory.CreatePortrait(creature, new Vector2(50, 50));
            portrait.Position = new Vector2(16, 3);
            portrait.Size = new Vector2(50, 50);
            card.AddChild(portrait);
            var captured = creature;
            card.Pressed += () => UpdatePreview(captured);
            entry.AddChild(card);

            var name = UiFactory.CreateLabel(creature.Name, 7);
            name.HorizontalAlignment = HorizontalAlignment.Center;
            name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            entry.AddChild(name);
            cards.AddChild(entry);
        }

        UpdatePreview(chosen);
        var start = UiFactory.CreateButton("Start Race");
        start.CustomMinimumSize = new Vector2(170, 27);
        start.Pressed += () =>
        {
            CloseModal();
            StartRace(chosen);
        };
        box.AddChild(start);
    }

    private void ShowDetails()
    {
        var data = GameSession.Instance.FindVoidling(_selectedId);
        if (data == null)
            return;

        var box = OpenModal($"{data.Name.ToUpperInvariant()} — DETAILS", new Vector2(536, 318));

        var tabs = new HBoxContainer();
        tabs.AddThemeConstantOverride("separation", 6);
        var dnaTab = UiFactory.CreateButton("DNA");
        dnaTab.CustomMinimumSize = new Vector2(92, 24);
        var visualTab = UiFactory.CreateButton("Visual");
        visualTab.CustomMinimumSize = new Vector2(92, 24);
        tabs.AddChild(dnaTab);
        tabs.AddChild(visualTab);
        box.AddChild(tabs);

        var body = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(492, 238),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        body.AddThemeConstantOverride("separation", 5);
        box.AddChild(body);

        void ClearBody()
        {
            foreach (var child in body.GetChildren())
            {
                body.RemoveChild(child);
                child.QueueFree();
            }
        }

        void RenderDna()
        {
            ClearBody();
            dnaTab.Disabled = true;
            visualTab.Disabled = false;

            var header = new HBoxContainer();
            header.AddThemeConstantOverride("separation", 12);
            header.AddChild(UiFactory.CreatePortrait(data, new Vector2(58, 58)));
            var summary = new VBoxContainer();
            summary.AddThemeConstantOverride("separation", 2);
            summary.AddChild(UiFactory.CreateLabel($"Generation {data.FamilyGeneration}", 9));
            summary.AddChild(UiFactory.CreateLabel($"Inbreeding burden: {data.InbreedingBurdenLevel}", 7));
            summary.AddChild(UiFactory.CreateLabel("Two inherited alleles determine the shown grade; training changes CURRENT only.", 7));
            header.AddChild(summary);
            body.AddChild(header);

            body.AddChild(CreateDnaHeaderRow());
            foreach (var statId in GameRules.StatIds)
                body.AddChild(CreateDnaStatRow(data, statId));

            var expressedColor = data.Genome.ExpressedColorIndex == 0
                ? data.Genome.ColorAlleleA
                : data.Genome.ColorAlleleB;
            body.AddChild(UiFactory.CreateLabel(
                $"Color genes  A #{data.Genome.ColorAlleleA}   B #{data.Genome.ColorAlleleB}   → expressed #{expressedColor}", 7));
        }

        void RenderVisual()
        {
            ClearBody();
            dnaTab.Disabled = false;
            visualTab.Disabled = true;

            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 20);
            row.AddChild(UiFactory.CreatePortrait(data, new Vector2(132, 132)));

            var info = new VBoxContainer();
            info.AddThemeConstantOverride("separation", 8);
            info.AddChild(UiFactory.CreateLabel("CURRENT APPEARANCE", 10));
            info.AddChild(new ColorRect
            {
                Color = GameRules.TintColor(data.TintHex),
                CustomMinimumSize = new Vector2(120, 32)
            });

            var expressedColor = data.Genome.ExpressedColorIndex == 0
                ? data.Genome.ColorAlleleA
                : data.Genome.ColorAlleleB;
            info.AddChild(UiFactory.CreateLabel($"Expressed color gene: #{expressedColor}", 8));
            info.AddChild(UiFactory.CreateLabel($"Color alleles: #{data.Genome.ColorAlleleA} / #{data.Genome.ColorAlleleB}", 8));
            row.AddChild(info);
            body.AddChild(row);

            if (data.RareTraits.Count == 0)
            {
                body.AddChild(UiFactory.CreateLabel("Rare appearance trait: none", 8));
            }
            else
            {
                foreach (var trait in data.RareTraits)
                {
                    var founderName = GameSession.Instance.NameFor(trait.FounderCreatureId);
                    body.AddChild(UiFactory.CreateLabel(
                        $"{trait.TraitId} • founder {founderName} • G{trait.GenerationFromFounder} • {(trait.CanTransmit ? "can pass on" : "terminal")}", 7));
                }
            }
        }

        dnaTab.Pressed += RenderDna;
        visualTab.Pressed += RenderVisual;
        RenderDna();
    }

    private static Control CreateDnaHeaderRow()
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 5);
        row.AddChild(CreateDnaCell("STAT", 78, 7, true));
        row.AddChild(CreateDnaCell("ALLELE A", 72, 7, true));
        row.AddChild(CreateDnaCell("ALLELE B", 72, 7, true));
        row.AddChild(CreateDnaCell("SHOWS", 72, 7, true));
        row.AddChild(CreateDnaCell("CURRENT", 78, 7, true));
        return row;
    }

    private static Control CreateDnaStatRow(VoidlingData data, string statId)
    {
        var gene = GameRules.GetGene(data, statId);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 5);
        row.AddChild(CreateDnaCell(GameRules.StatDisplayNames[statId], 78, 8, false));
        row.AddChild(CreateDnaCell(GameRules.GradeName(gene.AlleleA), 72, 9, false));
        row.AddChild(CreateDnaCell(GameRules.GradeName(gene.AlleleB), 72, 9, false));
        row.AddChild(CreateDnaCell(GameRules.GradeName(gene.ExpressedValue), 72, 9, false, Color.FromHtml("#FFF0A6")));
        row.AddChild(CreateDnaCell(((int)Math.Round(GameRules.EffectiveStat(data, statId))).ToString(), 78, 9, false));
        return row;
    }

    private static Control CreateDnaCell(string text, float width, int fontSize, bool header, Color? background = null)
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(width, header ? 20 : 24) };
        var style = new StyleBoxFlat
        {
            BgColor = background ?? (header ? Color.FromHtml("#C9B98D") : Color.FromHtml("#F1DCAA")),
            BorderColor = Color.FromHtml("#BE916C")
        };
        style.SetBorderWidthAll(1);
        style.ContentMarginLeft = 3;
        style.ContentMarginRight = 3;
        style.ContentMarginTop = 2;
        style.ContentMarginBottom = 2;
        panel.AddThemeStyleboxOverride("panel", style);

        var label = UiFactory.CreateLabel(text, fontSize);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.VerticalAlignment = VerticalAlignment.Center;
        panel.AddChild(label);
        return panel;
    }

    private void ShowFamilyTree()
    {
        var data = GameSession.Instance.FindVoidling(_selectedId);
        if (data == null)
            return;

        var box = OpenModal($"{data.Name.ToUpperInvariant()} — FAMILY TREE", new Vector2(612, 330));
        var note = UiFactory.CreateLabel("Click any family member for stats and parents. Grey LEFT nodes remain after a Voidling leaves the farm.", 7);
        note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(note);

        var content = new HBoxContainer();
        content.AddThemeConstantOverride("separation", 8);
        box.AddChild(content);

        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(425, 252),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        content.AddChild(scroll);

        var tree = new FamilyTreeView();
        tree.Build(data.Id, GameSession.Instance.State.Voidlings, GameSession.Instance.State.DepartedVoidlings);
        scroll.AddChild(tree);

        var inspector = UiFactory.CreatePanel(new Vector2(153, 252));
        inspector.CustomMinimumSize = new Vector2(153, 252);
        inspector.Visible = false;
        content.AddChild(inspector);
        var inspectorBox = new VBoxContainer();
        inspectorBox.AddThemeConstantOverride("separation", 3);
        inspector.AddChild(inspectorBox);

        void ShowMember(string memberId)
        {
            var member = GameSession.Instance.FindLineageVoidling(memberId);
            if (member == null)
                return;

            foreach (var old in inspectorBox.GetChildren())
            {
                inspectorBox.RemoveChild(old);
                old.QueueFree();
            }

            inspector.Visible = true;
            tree.SetSelectedMember(memberId);

            var heading = new HBoxContainer();
            var memberName = UiFactory.CreateTitle(member.Name);
            memberName.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            heading.AddChild(memberName);
            var dismiss = UiFactory.CreateButton("X");
            dismiss.CustomMinimumSize = new Vector2(24, 20);
            dismiss.Pressed += () => inspector.Visible = false;
            heading.AddChild(dismiss);
            inspectorBox.AddChild(heading);

            var portrait = UiFactory.CreatePortrait(member, new Vector2(62, 62));
            if (GameSession.Instance.IsDeparted(member.Id))
                portrait.SelfModulate = new Color(0.55f, 0.55f, 0.55f, 0.72f);
            inspectorBox.AddChild(portrait);

            if (GameSession.Instance.IsDeparted(member.Id))
            {
                var gone = UiFactory.CreateLabel("LEFT THE FARM", 7);
                gone.AddThemeColorOverride("font_color", Color.FromHtml("#8B6257"));
                inspectorBox.AddChild(gone);
            }

            foreach (var statId in GameRules.StatIds)
            {
                var gene = GameRules.GetGene(member, statId);
                var value = (int)Math.Round(GameRules.EffectiveStat(member, statId));
                inspectorBox.AddChild(UiFactory.CreateLabel(
                    $"{GameRules.StatDisplayNames[statId]}  {GameRules.GradeName(gene.ExpressedValue)}  {value}", 7));
            }

            var parentText = member.ParentAId.Length > 0
                ? $"Parents:\n{GameSession.Instance.NameFor(member.ParentAId)}\n+ {GameSession.Instance.NameFor(member.ParentBId)}"
                : "Parents:\nFounder / store line";
            var parents = UiFactory.CreateLabel(parentText, 7);
            parents.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            inspectorBox.AddChild(parents);
        }

        tree.MemberSelected += ShowMember;
    }

    private void ShowGoodbyeFirst(string creatureId)
    {
        var data = GameSession.Instance.FindVoidling(creatureId);
        if (data == null)
            return;

        var box = OpenModal("SAY GOODBYE?", new Vector2(405, 175));
        var text = UiFactory.CreateLabel(
            $"Send {data.Name} away from the farm? They will disappear from the garden, but remain in every family tree as a departed family member.", 8);
        text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(text);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        var cancel = UiFactory.CreateButton("Cancel");
        cancel.Pressed += CloseModal;
        row.AddChild(cancel);
        var next = UiFactory.CreateButton("Continue");
        next.Pressed += () => ShowGoodbyeFinal(creatureId);
        row.AddChild(next);
        box.AddChild(row);
    }

    private void ShowGoodbyeFinal(string creatureId)
    {
        var data = GameSession.Instance.FindVoidling(creatureId);
        if (data == null)
        {
            CloseModal();
            return;
        }

        var box = OpenModal("FINAL WARNING", new Vector2(420, 185));
        var warning = UiFactory.CreateLabel(
            $"This cannot be undone. If you say goodbye now, {data.Name} leaves the farm forever. Their grey family-tree memorial is the only record that remains.", 8);
        warning.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        warning.AddThemeColorOverride("font_color", Color.FromHtml("#9C514B"));
        box.AddChild(warning);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        var keep = UiFactory.CreateButton("Keep Voidling");
        keep.Pressed += CloseModal;
        row.AddChild(keep);
        var goodbye = UiFactory.CreateButton("Goodbye forever");
        goodbye.AddThemeColorOverride("font_color", Color.FromHtml("#9C514B"));
        goodbye.Pressed += () =>
        {
            CloseModal();
            if (GameSession.Instance.SayGoodbye(creatureId))
            {
                _selectedId = "";
                _garden.ClearSelection();
                _garden.StopFollowing();
                RefreshUi();
            }
        };
        row.AddChild(goodbye);
        box.AddChild(row);
    }

    private void ShowResetConfirm()
    {
        var box = OpenModal("RESET DEMO?", new Vector2(320, 155));
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
        box.AddThemeConstantOverride("separation", 7);
        panel.AddChild(box);

        var heading = new HBoxContainer();
        heading.AddThemeConstantOverride("separation", 7);
        var titleLabel = UiFactory.CreateTitle(title);
        titleLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        titleLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        heading.AddChild(titleLabel);
        var close = UiFactory.CreateButton("X");
        close.CustomMinimumSize = new Vector2(30, 23);
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
        _garden.SetGameplayActive(false);
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
        _garden.SetGameplayActive(true);
        _uiRoot.Visible = true;
        RefreshUi();
    }

    private void OnVoidlingSelected(string creatureId)
    {
        if (_selectedId != creatureId)
            _garden.StopFollowing();

        _selectedId = creatureId;
        RefreshUi();
    }

    private void DeselectVoidling()
    {
        _selectedId = "";
        _garden.ClearSelection();
        _garden.StopFollowing();
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
        option.CustomMinimumSize = new Vector2(165, 24);
        UiFactory.ApplyPixelFont(option, 8);
        option.AddThemeColorOverride("font_color", Color.FromHtml("#465247"));
    }
}
