using System;
using System.Linq;
using Godot;
using Voidling.Application.Breeding;
using Voidling.Application.Creatures;
using Voidling.Domain.Genetics;
using Voidling.Domain.Racing;
using Voidling.Presentation.UI.Common;
using Voidling.Presentation.UI.Details;
using Voidling.Presentation.UI.Racing;

namespace VoidlingGame;

public partial class MainController : Node
{
    private void ShowRacePicker()
    {
        var owned = _session.CreateActiveVoidlingProfileProjections().ToArray();
        var selectedId = owned.Any(v => v.CreatureId == _selectedId)
            ? _selectedId
            : owned.FirstOrDefault()?.CreatureId ?? string.Empty;

        var viewState = owned.Select(CreateRacePickerView).ToArray();
        var courses = new[]
        {
            new RacePickerCourseViewState(
                RaceCourseCatalog.Demo.Id,
                RaceCourseCatalog.Demo.Version,
                Tr("UI_RACE_COURSE_DEMO_NAME"),
                Tr("UI_RACE_COURSE_DEMO_SUMMARY")),
            new RacePickerCourseViewState(
                RaceCourseCatalog.LongStandard.Id,
                RaceCourseCatalog.LongStandard.Version,
                Tr("UI_RACE_COURSE_LONG_NAME"),
                Tr("UI_RACE_COURSE_LONG_SUMMARY"))
        };

        var box = OpenModal(Tr("UI_RACE_PICKER_TITLE"), new Vector2(552, 335));
        var screen = new RacePickerScreen();
        screen.Configure(new RacePickerScreenState(
            viewState,
            selectedId,
            courses,
            RaceCourseCatalog.Demo.Id,
            RaceCourseCatalog.Demo.Version));
        screen.RaceRequested += (creatureId, courseId, courseVersion) =>
        {
            if (_session.CreateVoidlingProfileProjection(creatureId) == null)
                return;

            CloseModal();
            StartRace(creatureId, courseId, courseVersion);
        };
        box.AddChild(screen);
    }

    private static RacePickerVoidlingViewState CreateRacePickerView(VoidlingProfileProjection creature)
    {
        var statSummary = string.Join("   ", creature.Stats.Select(stat =>
            $"{StatPresentationCatalog.NameFor(stat.StatId)} {StatPresentationCatalog.RankFor(stat.ExpressedPotentialRank)} {Mathf.RoundToInt(stat.EffectiveValue)}"));

        return new RacePickerVoidlingViewState(
            creature.CreatureId,
            creature.DisplayName,
            ParseProfileTint(creature.TintHex),
            creature.HasAngelMutation,
            creature.OtherMutationCount,
            statSummary);
    }

    private void StartRace(string creatureId, string courseId, int courseVersion)
    {
        var entry = _session.CreateRaceEntryFor(creatureId, courseId, courseVersion);
        var autoFinish = _session.State.AutoFinishRaces;

        _garden.SetGameplayActive(false);
        _garden.Visible = false;
        _uiRoot.Visible = false;

        var race = new Voidling.Presentation.Racing.RaceScreen();
        race.Configure(entry, autoFinish);
        race.RaceCompleted += OnRaceCompleted;
        race.ReturnRequested += EndRace;
        _race = race;
        AddChild(race);
    }

    private void ShowDetails()
    {
        var data = _session.CreateVoidlingProfileProjection(_selectedId);
        if (data == null)
            return;

        var stats = data.Stats.Select(stat => new DetailsStatViewState(
            StatPresentationCatalog.NameFor(stat.StatId),
            StatPresentationCatalog.ColorFor(stat.StatId),
            StatPresentationCatalog.RankFor(stat.ExpressedPotentialRank),
            stat.TrainingLevel,
            Mathf.RoundToInt(stat.EffectiveValue),
            stat.TrainingLevelProgress,
            StatPresentationCatalog.RankFor(stat.DnaProfile1Rank),
            StatPresentationCatalog.RankFor(stat.DnaProfile2Rank))).ToArray();
        var rareTraits = data.RareTraits
            .Select(trait => new DetailsRareTraitViewState(
                trait.TraitId,
                trait.FounderDisplayName,
                trait.GenerationFromFounder,
                trait.CanTransmit))
            .ToArray();

        var state = new DetailsScreenState(
            data.DisplayName,
            data.IsAdult,
            data.FamilyGeneration,
            data.ActiveInbreedingBurden,
            data.InbreedingHistoryFlag,
            ParseProfileTint(data.TintHex),
            data.HasAngelMutation,
            data.OtherMutationCount,
            data.ColorDnaProfile1,
            data.ColorDnaProfile2,
            data.ExpressedColorProfileIndex,
            stats,
            rareTraits);

        var box = OpenModal($"{data.DisplayName.ToUpperInvariant()} — DETAILS", new Vector2(536, 318));
        var screen = new DetailsScreen();
        screen.Configure(state);
        box.AddChild(screen);
    }

    private void ShowFamilyTree()
    {
        var data = _session.CreateVoidlingProfileProjection(_selectedId);
        if (data == null)
            return;

        var projection = _session.CreateLineageTreeProjection(data.CreatureId);
        var membersById = projection.Members.ToDictionary(member => member.CreatureId, StringComparer.Ordinal);

        var box = OpenModal($"{data.DisplayName.ToUpperInvariant()} — FAMILY TREE", new Vector2(612, 330));
        var note = UiFactory.CreateLabel("Drag empty space with left mouse. Click a family member for stats and parents.", 6);
        box.AddChild(note);

        var content = new HBoxContainer();
        content.AddThemeConstantOverride("separation", 8);
        box.AddChild(content);

        var tree = new FamilyTreeView
        {
            EdgePanningEnabled = _session.State.EdgePanning
        };
        tree.Build(projection);
        content.AddChild(tree);

        var inspector = UiFactory.CreatePanel(new Vector2(153, 252));
        inspector.CustomMinimumSize = new Vector2(153, 252);
        inspector.Visible = false;
        content.AddChild(inspector);
        var inspectorBox = new VBoxContainer();
        inspectorBox.AddThemeConstantOverride("separation", 3);
        inspector.AddChild(inspectorBox);

        string NameFor(string memberId)
            => membersById.TryGetValue(memberId, out var known) ? known.DisplayName : "Unknown";

        void ShowMember(string memberId)
        {
            if (!membersById.TryGetValue(memberId, out var member))
                return;

            foreach (var old in inspectorBox.GetChildren())
            {
                inspectorBox.RemoveChild(old);
                old.QueueFree();
            }

            inspector.Visible = true;
            tree.SetSelectedMember(memberId);

            var heading = new HBoxContainer();
            var memberName = UiFactory.CreateTitle(member.DisplayName);
            memberName.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            heading.AddChild(memberName);
            var dismiss = UiFactory.CreateButton("X");
            dismiss.CustomMinimumSize = new Vector2(24, 20);
            dismiss.Pressed += () => inspector.Visible = false;
            heading.AddChild(dismiss);
            inspectorBox.AddChild(heading);

            var hasAngel = member.RareTraitIds.Any(traitId =>
                string.Equals(traitId, MutationIds.Angel, StringComparison.OrdinalIgnoreCase));
            var otherMutations = member.RareTraitIds.Count(traitId =>
                !string.Equals(traitId, MutationIds.Angel, StringComparison.OrdinalIgnoreCase));
            var portrait = UiFactory.CreatePortrait(
                ParseProfileTint(member.TintHex),
                hasAngel,
                otherMutations,
                new Vector2(60, 60));
            if (member.Presence != LineageMemberPresence.Owned)
                portrait.Modulate = new Color(0.55f, 0.55f, 0.55f, 0.72f);
            inspectorBox.AddChild(portrait);

            if (member.Presence == LineageMemberPresence.Departed)
                inspectorBox.AddChild(UiFactory.CreateLabel("LEFT THE FARM", 6));
            else if (member.Presence == LineageMemberPresence.Archived)
                inspectorBox.AddChild(UiFactory.CreateLabel("ARCHIVED RECORD", 6));

            if (member.ActiveInbreedingBurden.HasValue)
            {
                var burdenLabel = member.InbreedingHistoryFlag
                    ? $"Burden {member.ActiveInbreedingBurden.Value} • history marked"
                    : $"Burden {member.ActiveInbreedingBurden.Value}";
                inspectorBox.AddChild(UiFactory.CreateLabel(burdenLabel, 5));
            }
            else if (member.InbreedingHistoryFlag)
            {
                inspectorBox.AddChild(UiFactory.CreateLabel("Historical inbreeding mark", 5));
            }

            if (member.Stats.Count == 0)
            {
                inspectorBox.AddChild(UiFactory.CreateLabel("Stats not retained in archive.", 5));
            }
            else
            {
                foreach (var stat in member.Stats)
                {
                    var label = UiFactory.CreateLabel(
                        $"{StatPresentationCatalog.NameFor(stat.StatId)}  {StatPresentationCatalog.RankFor(stat.ExpressedAllele)}  LV{stat.Level}", 6);
                    label.AddThemeColorOverride("font_color", StatPresentationCatalog.ColorFor(stat.StatId));
                    inspectorBox.AddChild(label);
                }
            }

            var parentText = member.ParentAId.Length > 0
                ? $"Parents:\n{NameFor(member.ParentAId)}\n+ {NameFor(member.ParentBId)}"
                : "Parents:\nFounder / store line";
            var parents = UiFactory.CreateLabel(parentText, 6);
            parents.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            inspectorBox.AddChild(parents);
        }

        tree.MemberSelected += ShowMember;
        ShowMember(data.CreatureId);
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

    private static Color ParseProfileTint(string html)
        => string.IsNullOrWhiteSpace(html) ? Colors.White : Color.FromHtml(html);
}
