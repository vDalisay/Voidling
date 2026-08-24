using System;
using System.Linq;
using Godot;
using Voidling.Presentation.UI.Common;
using Voidling.Presentation.UI.Details;
using Voidling.Presentation.UI.Racing;

namespace VoidlingGame;

public partial class MainController : Node
{
    private void ShowRacePicker()
    {
        var owned = _session.State.Voidlings.ToArray();
        var selectedId = owned.Any(v => v.Id == _selectedId)
            ? _selectedId
            : owned.FirstOrDefault()?.Id ?? string.Empty;

        var viewState = owned.Select(CreateRacePickerView).ToArray();
        var box = OpenModal(Tr("UI_RACE_PICKER_TITLE"), new Vector2(552, 310));
        var screen = new RacePickerScreen();
        screen.Configure(new RacePickerScreenState(viewState, selectedId));
        screen.RaceRequested += creatureId =>
        {
            var selected = _session.FindVoidling(creatureId);
            if (selected == null)
                return;

            CloseModal();
            StartRace(selected);
        };
        box.AddChild(screen);
    }

    private static RacePickerVoidlingViewState CreateRacePickerView(VoidlingData creature)
    {
        var hasAngel = GameRules.HasMutation(creature, GameRules.AngelMutationId);
        var otherMutations = creature.RareTraits?.Count(trait =>
            !string.Equals(trait.TraitId, GameRules.AngelMutationId, StringComparison.OrdinalIgnoreCase)) ?? 0;
        var statSummary = string.Join("   ", GameRules.StatIds.Select(stat =>
            $"{StatPresentationCatalog.NameFor(stat)} {GameRules.GradeName(GameRules.GetGene(creature, stat).ExpressedValue)} {Mathf.RoundToInt(GameRules.EffectiveStat(creature, stat))}"));

        return new RacePickerVoidlingViewState(
            creature.Id,
            creature.Name,
            GameRules.TintColor(creature.TintHex),
            hasAngel,
            otherMutations,
            statSummary);
    }

    private void ShowDetails()
    {
        var data = _session.FindVoidling(_selectedId);
        if (data == null)
            return;

        var hasAngel = GameRules.HasMutation(data, GameRules.AngelMutationId);
        var otherMutations = data.RareTraits?.Count(trait =>
            !string.Equals(trait.TraitId, GameRules.AngelMutationId, StringComparison.OrdinalIgnoreCase)) ?? 0;
        var stats = GameRules.StatIds.Select(statId =>
        {
            var gene = GameRules.GetGene(data, statId);
            return new DetailsStatViewState(
                StatPresentationCatalog.NameFor(statId),
                StatPresentationCatalog.ColorFor(statId),
                GameRules.GradeName(gene.ExpressedValue),
                GameRules.StatLevel(data, statId),
                Mathf.RoundToInt(GameRules.EffectiveStat(data, statId)),
                GameRules.StatLevelProgress(data, statId),
                GameRules.GradeName(gene.AlleleA),
                GameRules.GradeName(gene.AlleleB));
        }).ToArray();
        var rareTraits = data.RareTraits?
            .Select(trait => new DetailsRareTraitViewState(
                trait.TraitId,
                _session.NameFor(trait.FounderCreatureId),
                trait.GenerationFromFounder,
                trait.CanTransmit))
            .ToArray() ?? Array.Empty<DetailsRareTraitViewState>();

        var state = new DetailsScreenState(
            data.Name,
            data.Stage == LifeStage.Adult,
            data.FamilyGeneration,
            data.InbreedingBurdenLevel,
            GameRules.TintColor(data.TintHex),
            hasAngel,
            otherMutations,
            data.Genome.ColorAlleleA,
            data.Genome.ColorAlleleB,
            data.Genome.ExpressedColorIndex,
            stats,
            rareTraits);

        var box = OpenModal($"{data.Name.ToUpperInvariant()} — DETAILS", new Vector2(536, 318));
        var screen = new DetailsScreen();
        screen.Configure(state);
        box.AddChild(screen);
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

        var tree = new FamilyTreeView
        {
            EdgePanningEnabled = _session.State.EdgePanning
        };
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
                    $"{StatPresentationCatalog.NameFor(statId)}  {GameRules.GradeName(gene.ExpressedValue)}  LV{GameRules.StatLevel(member, statId)}", 6);
                stat.AddThemeColorOverride("font_color", StatPresentationCatalog.ColorFor(statId));
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
