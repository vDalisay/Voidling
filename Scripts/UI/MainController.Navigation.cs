using System;
using System.Linq;
using Godot;
using Voidling.Application.Breeding;
using Voidling.Domain.Racing;
using Voidling.Presentation.UI.Common;
using Voidling.Presentation.UI.Details;
using Voidling.Presentation.UI.Racing;
using Voidling.Presentation.Voidlings;

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
        var courses = RaceCourseCatalog.All
            .Select(course => new RacePickerCourseViewState(
                course.Id,
                course.Version,
                course.Id == RaceCourseCatalog.Demo.Id
                    ? Tr("UI_RACE_COURSE_DEMO_NAME")
                    : Tr("UI_RACE_COURSE_LONG_NAME"),
                course.Id == RaceCourseCatalog.Demo.Id
                    ? Tr("UI_RACE_COURSE_DEMO_SUMMARY")
                    : Tr("UI_RACE_COURSE_LONG_SUMMARY")))
            .ToArray();

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
            if (_session.FindVoidling(creatureId) == null)
                return;

            CloseModal();
            StartRaceWithCourse(creatureId, courseId, courseVersion);
        };
        box.AddChild(screen);
    }

    private RacePickerVoidlingViewState CreateRacePickerView(VoidlingData creature)
    {
        var profile = _session.CreateCreatureProfileProjection(creature.Id)
            ?? throw new InvalidOperationException($"Could not project race-picker data for '{creature.Id}'.");
        var statSummary = string.Join("   ", profile.Stats.Select(stat =>
            $"{StatPresentationCatalog.NameFor(stat.StatId)} {stat.InheritedRank} {stat.EffectiveValue}"));

        return new RacePickerVoidlingViewState(
            profile.CreatureId,
            profile.Name,
            ProfileAppearance(profile),
            profile.HasAngelMutation,
            profile.OtherMutationCount,
            statSummary);
    }

    private void ShowDetails()
    {
        var profile = _session.CreateCreatureProfileProjection(_selectedId);
        if (profile == null)
            return;

        var stats = profile.Stats
            .Select(stat => new DetailsStatViewState(
                StatPresentationCatalog.NameFor(stat.StatId),
                StatPresentationCatalog.ColorFor(stat.StatId),
                stat.InheritedRank,
                stat.TrainingLevel,
                stat.EffectiveValue,
                stat.TrainingProgress,
                stat.Dna1Rank,
                stat.Dna2Rank))
            .ToArray();
        var rareTraits = profile.RareTraits
            .Select(trait => new DetailsRareTraitViewState(
                trait.TraitId,
                trait.FounderName,
                trait.GenerationFromFounder,
                trait.CanTransmit))
            .ToArray();

        var state = new DetailsScreenState(
            profile.Name,
            profile.IsAdult,
            profile.FamilyGeneration,
            Tr(LineageRiskTranslationKey(profile.LineageRisk)),
            ProfileAppearance(profile),
            GameRules.TintColor(profile.TintHex),
            profile.HasAngelMutation,
            profile.OtherMutationCount,
            profile.ColorAlleleA,
            profile.ColorAlleleB,
            profile.ExpressedColorIndex,
            stats,
            rareTraits);

        var box = OpenModal($"{profile.Name.ToUpperInvariant()} — DETAILS", new Vector2(536, 318));
        var screen = new DetailsScreen();
        screen.Configure(state);
        box.AddChild(screen);
    }

    private void ShowFamilyTree()
    {
        var data = _session.FindVoidling(_selectedId);
        if (data == null)
            return;

        var projection = _session.CreateLineageTreeProjection(data.Id);
        var membersById = projection.Members.ToDictionary(member => member.CreatureId, StringComparer.Ordinal);

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
                string.Equals(traitId, GameRules.AngelMutationId, StringComparison.OrdinalIgnoreCase));
            var otherMutations = member.RareTraitIds.Count(traitId =>
                !string.Equals(traitId, GameRules.AngelMutationId, StringComparison.OrdinalIgnoreCase));
            var portrait = UiFactory.CreatePortrait(
                MemberAppearance(member),
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
                        $"{StatPresentationCatalog.NameFor(stat.StatId)}  {GameRules.GradeName(stat.ExpressedAllele)}  LV{stat.Level}", 6);
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
        ShowMember(data.Id);
    }

    private static VoidlingVisualAppearance ProfileAppearance(
        Voidling.Application.Roster.CreatureProfileProjection profile)
        => new(
            profile.VisualTypeId,
            profile.PaletteHue,
            profile.LayerIds,
            profile.TintHex);

    private static VoidlingVisualAppearance MemberAppearance(LineageMemberProjection member)
        => new(
            member.VisualTypeId,
            member.PaletteHue,
            member.LayerIds,
            member.TintHex);

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
