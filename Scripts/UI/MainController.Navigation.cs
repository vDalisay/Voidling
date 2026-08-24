using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace VoidlingGame;

public partial class MainController : Node
{
    private void ShowRacePicker()
    {
        var owned = _session.State.Voidlings.ToList();
        var box = OpenModal("CHOOSE A RACER", new Vector2(552, 310));

        if (owned.Count == 0)
        {
            box.AddChild(UiFactory.CreateLabel("No Voidlings are currently on the farm.", 9));
            return;
        }

        var chosen = owned.FirstOrDefault(v => v.Id == _selectedId) ?? owned[0];
        box.AddChild(UiFactory.CreateLabel("Choose one Voidling. All other racers are generated CPU opponents.", 7));

        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(510, 90),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            VerticalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        var cards = new HBoxContainer();
        cards.AddThemeConstantOverride("separation", 7);
        scroll.AddChild(cards);
        box.AddChild(scroll);

        var previewRow = new HBoxContainer();
        previewRow.AddThemeConstantOverride("separation", 12);
        var previewPortrait = UiFactory.CreatePortrait(chosen, new Vector2(72, 72));
        previewRow.AddChild(previewPortrait);
        var previewText = new VBoxContainer();
        previewText.AddThemeConstantOverride("separation", 2);
        var previewName = UiFactory.CreateTitle(chosen.Name);
        var previewStats = UiFactory.CreateLabel("", 7);
        previewText.AddChild(previewName);
        previewText.AddChild(previewStats);
        previewRow.AddChild(previewText);
        box.AddChild(previewRow);

        var cardButtons = new Dictionary<string, Button>(StringComparer.Ordinal);

        void UpdatePreview(VoidlingData candidate)
        {
            chosen = candidate;
            UiFactory.SetPortraitData(previewPortrait, candidate);
            previewName.Text = candidate.Name;
            previewStats.Text = string.Join("   ", GameRules.StatIds.Select(stat =>
                $"{GameRules.StatDisplayNames[stat]} {GameRules.GradeName(GameRules.GetGene(candidate, stat).ExpressedValue)} {Mathf.RoundToInt(GameRules.EffectiveStat(candidate, stat))}"));

            foreach (var pair in cardButtons)
                pair.Value.ButtonPressed = pair.Key == candidate.Id;
        }

        foreach (var creature in owned)
        {
            var entry = new VBoxContainer { CustomMinimumSize = new Vector2(84, 78) };
            entry.AddThemeConstantOverride("separation", 1);

            var card = UiFactory.CreateButton("");
            card.CustomMinimumSize = new Vector2(80, 58);
            card.ToggleMode = true;
            card.KeepPressedOutside = true;
            cardButtons[creature.Id] = card;

            var portrait = UiFactory.CreatePortrait(creature, new Vector2(48, 48));
            portrait.Position = new Vector2(16, 4);
            portrait.Size = new Vector2(48, 48);
            card.AddChild(portrait);

            var captured = creature;
            card.Pressed += () => UpdatePreview(captured);
            entry.AddChild(card);

            var name = UiFactory.CreateLabel(creature.Name, 6);
            name.HorizontalAlignment = HorizontalAlignment.Center;
            name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            entry.AddChild(name);
            cards.AddChild(entry);
        }

        UpdatePreview(chosen);

        var start = UiFactory.CreateButton("Start Race");
        start.CustomMinimumSize = new Vector2(170, 26);
        start.Pressed += () =>
        {
            CloseModal();
            StartRace(chosen);
        };
        box.AddChild(start);
    }

    private void ShowDetails()
    {
        var data = _session.FindVoidling(_selectedId);
        if (data == null)
            return;

        // OpenModal closes any existing menu and hides the persistent inspection/egg HUD,
        // leaving Details as the single foreground context.
        var box = OpenModal($"{data.Name.ToUpperInvariant()} — DETAILS", new Vector2(536, 318));

        var tabs = new HBoxContainer();
        tabs.AddThemeConstantOverride("separation", 5);
        var statsTab = UiFactory.CreateButton("Stats");
        var dnaTab = UiFactory.CreateButton("DNA");
        var visualTab = UiFactory.CreateButton("Visual");
        foreach (var tab in new[] { statsTab, dnaTab, visualTab })
        {
            tab.CustomMinimumSize = new Vector2(92, 23);
            tab.ToggleMode = true;
            tabs.AddChild(tab);
        }
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

        void SelectTab(Button active)
        {
            statsTab.ButtonPressed = active == statsTab;
            dnaTab.ButtonPressed = active == dnaTab;
            visualTab.ButtonPressed = active == visualTab;
        }

        void RenderStats()
        {
            ClearBody();
            SelectTab(statsTab);

            var header = new HBoxContainer();
            header.AddThemeConstantOverride("separation", 12);
            header.AddChild(UiFactory.CreatePortrait(data, new Vector2(58, 58)));
            var summary = new VBoxContainer();
            summary.AddThemeConstantOverride("separation", 2);
            summary.AddChild(UiFactory.CreateLabel(data.Stage == LifeStage.Adult ? "ADULT" : "CHILD", 8));
            summary.AddChild(UiFactory.CreateLabel("Rank controls potential; level/progress reflects training.", 6));
            header.AddChild(summary);
            body.AddChild(header);

            foreach (var statId in GameRules.StatIds)
                body.AddChild(CreateDetailsStatRow(data, statId));
        }

        void RenderDna()
        {
            ClearBody();
            SelectTab(dnaTab);

            var intro = new HBoxContainer();
            intro.AddThemeConstantOverride("separation", 10);
            intro.AddChild(UiFactory.CreatePortrait(data, new Vector2(54, 54)));
            var summary = new VBoxContainer();
            summary.AddThemeConstantOverride("separation", 2);
            summary.AddChild(UiFactory.CreateLabel($"Generation {data.FamilyGeneration}", 8));
            summary.AddChild(UiFactory.CreateLabel($"Inbreeding burden: {data.InbreedingBurdenLevel}", 7));
            summary.AddChild(UiFactory.CreateLabel("DNA1 and DNA2 are the two inherited ability genes.", 6));
            intro.AddChild(summary);
            body.AddChild(intro);

            body.AddChild(CreateDnaHeaderRow());
            foreach (var statId in GameRules.StatIds)
                body.AddChild(CreateDnaStatRow(data, statId));

            body.AddChild(UiFactory.CreateLabel(
                $"Color DNA1 #{data.Genome.ColorAlleleA}    DNA2 #{data.Genome.ColorAlleleB}", 7));
        }

        void RenderVisual()
        {
            ClearBody();
            SelectTab(visualTab);

            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 18);
            row.AddChild(UiFactory.CreatePortrait(data, new Vector2(130, 130)));

            var info = new VBoxContainer();
            info.AddThemeConstantOverride("separation", 7);
            info.AddChild(UiFactory.CreateLabel("CURRENT APPEARANCE", 9));
            info.AddChild(new ColorRect
            {
                Color = GameRules.TintColor(data.TintHex),
                CustomMinimumSize = new Vector2(118, 30)
            });
            var expressedColor = data.Genome.ExpressedColorIndex == 0
                ? data.Genome.ColorAlleleA
                : data.Genome.ColorAlleleB;
            info.AddChild(UiFactory.CreateLabel($"Shown color DNA: #{expressedColor}", 7));
            info.AddChild(UiFactory.CreateLabel($"Color DNA: #{data.Genome.ColorAlleleA} / #{data.Genome.ColorAlleleB}", 7));
            row.AddChild(info);
            body.AddChild(row);

            if (data.RareTraits.Count == 0)
            {
                body.AddChild(UiFactory.CreateLabel("Mutation: none", 8));
            }
            else
            {
                foreach (var trait in data.RareTraits)
                {
                    var founderName = _session.NameFor(trait.FounderCreatureId);
                    body.AddChild(UiFactory.CreateLabel(
                        $"Mutation: {trait.TraitId}  • founder {founderName}  • G{trait.GenerationFromFounder}  • {(trait.CanTransmit ? "can pass on" : "terminal")}", 7));
                }
            }
        }

        statsTab.Pressed += RenderStats;
        dnaTab.Pressed += RenderDna;
        visualTab.Pressed += RenderVisual;
        RenderStats();
    }

    private static Control CreateDetailsStatRow(VoidlingData data, string statId)
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(472, 29) };
        var tint = GameRules.StatColor(statId);
        var background = tint;
        background.A = statId == "stamina" ? 0.55f : 0.22f;
        var style = new StyleBoxFlat { BgColor = background, BorderColor = Color.FromHtml("#BE916C") };
        style.SetBorderWidthAll(1);
        style.ContentMarginLeft = style.ContentMarginRight = 6;
        style.ContentMarginTop = style.ContentMarginBottom = 3;
        panel.AddThemeStyleboxOverride("panel", style);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        panel.AddChild(row);

        var gene = GameRules.GetGene(data, statId);
        var name = UiFactory.CreateLabel(GameRules.StatDisplayNames[statId].ToUpperInvariant(), 8);
        name.CustomMinimumSize = new Vector2(75, 19);
        name.AddThemeColorOverride("font_color", tint);
        name.AddThemeColorOverride("font_outline_color", Color.FromHtml("#465247"));
        name.AddThemeConstantOverride("outline_size", 1);
        row.AddChild(name);

        var values = UiFactory.CreateLabel(
            $"RANK {GameRules.GradeName(gene.ExpressedValue)}   LV {GameRules.StatLevel(data, statId):00}   STAT {Mathf.RoundToInt(GameRules.EffectiveStat(data, statId)):00}", 7);
        values.CustomMinimumSize = new Vector2(205, 19);
        row.AddChild(values);

        var progress = CreateStatProgressBar(data, statId, new Vector2(165, 8));
        progress.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        row.AddChild(progress);
        return panel;
    }

    private static Control CreateDnaHeaderRow()
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 4);
        row.AddChild(CreateDnaCell("GENE", 176, 7, true));
        row.AddChild(CreateDnaCell("DNA1", 142, 7, true));
        row.AddChild(CreateDnaCell("DNA2", 142, 7, true));
        return row;
    }

    private static Control CreateDnaStatRow(VoidlingData data, string statId)
    {
        var gene = GameRules.GetGene(data, statId);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 4);

        var identity = GameRules.StatColor(statId);
        var bg = identity;
        bg.A = statId == "stamina" ? 0.65f : 0.28f;

        row.AddChild(CreateDnaCell(GameRules.StatDisplayNames[statId].ToUpperInvariant(), 176, 7, false, bg, identity));
        row.AddChild(CreateDnaCell(GameRules.GradeName(gene.AlleleA), 142, 9, false));
        row.AddChild(CreateDnaCell(GameRules.GradeName(gene.AlleleB), 142, 9, false));
        return row;
    }

    private static Control CreateDnaCell(
        string text,
        float width,
        int fontSize,
        bool header,
        Color? background = null,
        Color? fontColor = null)
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(width, header ? 19 : 23) };
        var style = new StyleBoxFlat
        {
            BgColor = background ?? (header ? Color.FromHtml("#C9B98D") : Color.FromHtml("#F1DCAA")),
            BorderColor = Color.FromHtml("#BE916C")
        };
        style.SetBorderWidthAll(1);
        style.ContentMarginLeft = style.ContentMarginRight = 3;
        style.ContentMarginTop = style.ContentMarginBottom = 2;
        panel.AddThemeStyleboxOverride("panel", style);

        var label = UiFactory.CreateLabel(text, fontSize);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.VerticalAlignment = VerticalAlignment.Center;
        if (fontColor.HasValue)
        {
            label.AddThemeColorOverride("font_color", fontColor.Value);
            label.AddThemeColorOverride("font_outline_color", Color.FromHtml("#465247"));
            label.AddThemeConstantOverride("outline_size", 1);
        }
        panel.AddChild(label);
        return panel;
    }

    private void ShowFamilyTree()
    {
        var data = _session.FindVoidling(_selectedId);
        if (data == null)
            return;

        var box = OpenModal($"{data.Name.ToUpperInvariant()} — FAMILY TREE", new Vector2(612, 330));
        var note = UiFactory.CreateLabel("Drag empty space with left mouse. Click a family member for stats and parents.", 6);
        box.AddChild(note);

        var content = new HBoxContainer();
        content.AddThemeConstantOverride("separation", 8);
        box.AddChild(content);

        var tree = new FamilyTreeView();
        tree.Build(data.Id, _session.State.Voidlings, _session.State.DepartedVoidlings);
        content.AddChild(tree);

        var inspector = UiFactory.CreatePanel(new Vector2(153, 252));
        inspector.CustomMinimumSize = new Vector2(153, 252);
        inspector.Visible = false;
        content.AddChild(inspector);
        var inspectorBox = new VBoxContainer();
        inspectorBox.AddThemeConstantOverride("separation", 3);
        inspector.AddChild(inspectorBox);

        void ShowMember(string memberId)
        {
            var member = _session.FindLineageVoidling(memberId);
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

            var portrait = UiFactory.CreatePortrait(member, new Vector2(60, 60));
            if (_session.IsDeparted(member.Id))
                portrait.Modulate = new Color(0.55f, 0.55f, 0.55f, 0.72f);
            inspectorBox.AddChild(portrait);

            if (_session.IsDeparted(member.Id))
                inspectorBox.AddChild(UiFactory.CreateLabel("LEFT THE FARM", 6));

            foreach (var statId in GameRules.StatIds)
            {
                var gene = GameRules.GetGene(member, statId);
                var stat = UiFactory.CreateLabel(
                    $"{GameRules.StatDisplayNames[statId]}  {GameRules.GradeName(gene.ExpressedValue)}  LV{GameRules.StatLevel(member, statId)}", 6);
                stat.AddThemeColorOverride("font_color", GameRules.StatColor(statId));
                inspectorBox.AddChild(stat);
            }

            var parentText = member.ParentAId.Length > 0
                ? $"Parents:\n{_session.NameFor(member.ParentAId)}\n+ {_session.NameFor(member.ParentBId)}"
                : "Parents:\nFounder / store line";
            var parents = UiFactory.CreateLabel(parentText, 6);
            parents.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            inspectorBox.AddChild(parents);
        }

        tree.MemberSelected += ShowMember;
        ShowMember(data.Id);
    }

    private void ShowSettings()
    {
        var box = OpenModal("SETTINGS", new Vector2(365, 215));
        box.AddChild(UiFactory.CreateLabel("Audio", 9));

        var volumeRow = new HBoxContainer();
        volumeRow.AddThemeConstantOverride("separation", 8);
        var volumeLabel = UiFactory.CreateLabel($"Volume {Mathf.RoundToInt(_session.State.MasterVolume * 100)}%", 7);
        volumeLabel.CustomMinimumSize = new Vector2(90, 22);
        volumeRow.AddChild(volumeLabel);
        var volume = new HSlider
        {
            MinValue = 0,
            MaxValue = 100,
            Step = 5,
            Value = _session.State.MasterVolume * 100,
            CustomMinimumSize = new Vector2(220, 22),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        volume.ValueChanged += value =>
        {
            _session.SetMasterVolume((float)value / 100.0f);
            volumeLabel.Text = $"Volume {Mathf.RoundToInt((float)value)}%";
        };
        volumeRow.AddChild(volume);
        box.AddChild(volumeRow);

        box.AddChild(UiFactory.CreateLabel("Race", 9));
        var autoFinish = UiFactory.CreateButton(_session.State.AutoFinishRaces ? "Auto Finish: ON" : "Auto Finish: OFF");
        autoFinish.ToggleMode = true;
        autoFinish.ButtonPressed = _session.State.AutoFinishRaces;
        autoFinish.CustomMinimumSize = new Vector2(190, 25);
        autoFinish.TooltipText = "Finish once either you finish or every CPU has already finished.";
        autoFinish.Pressed += () =>
        {
            _session.SetAutoFinishRaces(autoFinish.ButtonPressed);
            autoFinish.Text = autoFinish.ButtonPressed ? "Auto Finish: ON" : "Auto Finish: OFF";
        };
        box.AddChild(autoFinish);

        var hint = UiFactory.CreateLabel("ESC opens/closes this menu from the garden.", 6);
        box.AddChild(hint);
    }

    private void ShowGoodbyeFirst(string creatureId)
    {
        var data = _session.FindVoidling(creatureId);
        if (data == null)
            return;

        var box = OpenModal("SAY GOODBYE?", new Vector2(405, 175));
        var text = UiFactory.CreateLabel(
            $"Send {data.Name} away from the farm? They disappear from the garden but remain in family trees.", 8);
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
        var data = _session.FindVoidling(creatureId);
        if (data == null)
        {
            CloseModal();
            return;
        }

        var box = OpenModal("FINAL WARNING", new Vector2(420, 185));
        var warning = UiFactory.CreateLabel(
            $"This cannot be undone. {data.Name} will leave the farm forever. Their grey family-tree record remains.", 8);
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
            if (_session.SayGoodbye(creatureId))
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
            _session.ResetDemo();
        };
        row.AddChild(reset);
        box.AddChild(row);
    }
}
