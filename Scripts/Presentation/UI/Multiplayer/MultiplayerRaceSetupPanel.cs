using System;
using System.Collections.Generic;
using Godot;
using Voidling.Application.Multiplayer.Challenges;
using Voidling.Application.Multiplayer.Racing;
using Voidling.Domain.Genetics;
using Voidling.Presentation.Voidlings;
using VoidlingGame;

namespace Voidling.Presentation.UI.Multiplayer;

public sealed record MultiplayerRaceSetupVoidlingView(
    string Id,
    string Name,
    string TintHex,
    AppearancePhenotype Appearance,
    bool HasAngelMutation,
    int OtherMutationCount,
    string StatSummary);

public sealed record MultiplayerRaceSetupPanelState(
    MultiplayerRacePreparationView Preparation,
    IReadOnlyList<MultiplayerRaceSetupVoidlingView> Voidlings);

/// <summary>
/// Race-specific lobby setup. The panel only selects one local Voidling and requests the existing
/// synchronized start handshake; it does not construct network race data or advance simulation.
/// </summary>
public partial class MultiplayerRaceSetupPanel : VBoxContainer
{
    public event Action<string>? SelectionRequested;
    public event Action? StartRequested;

    private MultiplayerRaceSetupPanelState? _state;
    private bool _ready;

    public void Configure(MultiplayerRaceSetupPanelState state)
    {
        if (IsInsideTree())
            throw new InvalidOperationException("MultiplayerRaceSetupPanel must be configured before entering the scene tree.");
        _state = state ?? throw new ArgumentNullException(nameof(state));
    }

    public void Render(MultiplayerRaceSetupPanelState state)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        if (_ready)
            Rebuild();
    }

    public override void _Ready()
    {
        if (_state == null)
            throw new InvalidOperationException("MultiplayerRaceSetupPanel must be configured before AddChild.");
        AddThemeConstantOverride("separation", 6);
        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _ready = true;
        Rebuild();
    }

    private void Rebuild()
    {
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        var state = _state!;
        var prep = state.Preparation;
        if (!prep.Exists)
        {
            AddChild(UiFactory.CreateLabel(prep.Error ?? Tr("UI_MP_RACE_MISSING"), 8));
            return;
        }

        AddChild(UiFactory.CreateLabel(
            string.Format(Tr("UI_MP_RACE_PARTICIPANTS"), prep.ParticipantCount, prep.MaxParticipants),
            8));

        if (prep.Phase == ChallengePhase.Ready)
        {
            AddChild(UiFactory.CreateLabel(Tr("UI_MP_RACE_HANDSHAKE"), 8));
            return;
        }
        if (prep.Phase == ChallengePhase.Running)
        {
            AddChild(UiFactory.CreateLabel(Tr("UI_MP_RACE_LAUNCHING"), 8));
            return;
        }
        if (!prep.CanSelectVoidling)
        {
            AddChild(UiFactory.CreateLabel(Tr("UI_MP_RACE_SELECTION_LOCKED"), 8));
            return;
        }

        if (state.Voidlings.Count == 0)
        {
            AddChild(UiFactory.CreateLabel(Tr("UI_RACE_PICKER_EMPTY"), 8));
            return;
        }

        AddChild(UiFactory.CreateLabel(Tr("UI_MP_RACE_PICK"), 7));
        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(490, 90),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            VerticalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        var cards = new HBoxContainer();
        cards.AddThemeConstantOverride("separation", 7);
        scroll.AddChild(cards);
        AddChild(scroll);

        var selectedIndex = 0;
        for (var i = 0; i < state.Voidlings.Count; i++)
        {
            if (string.Equals(state.Voidlings[i].Id, prep.SelectedCreatureId, StringComparison.Ordinal))
                selectedIndex = i;
        }
        var selectedId = state.Voidlings[selectedIndex].Id;
        var stats = UiFactory.CreateLabel(state.Voidlings[selectedIndex].StatSummary, 6);
        stats.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        var cardButtons = new Dictionary<string, Button>(StringComparer.Ordinal);
        foreach (var creature in state.Voidlings)
        {
            var captured = creature;
            var entry = new VBoxContainer { CustomMinimumSize = new Vector2(84, 78) };
            entry.AddThemeConstantOverride("separation", 1);

            var card = UiFactory.CreateButton(string.Empty);
            card.CustomMinimumSize = new Vector2(80, 58);
            card.ToggleMode = true;
            card.KeepPressedOutside = true;
            var portrait = VoidlingAppearancePresenter.CreatePortrait(
                creature.TintHex,
                creature.Appearance,
                creature.HasAngelMutation,
                creature.OtherMutationCount,
                new Vector2(48, 48));
            portrait.Position = new Vector2(16, 4);
            portrait.Size = new Vector2(48, 48);
            card.AddChild(portrait);
            card.Toggled += pressed =>
            {
                if (!pressed)
                    return;
                selectedId = captured.Id;
                stats.Text = captured.StatSummary;
                foreach (var pair in cardButtons)
                    pair.Value.ButtonPressed = pair.Key == captured.Id;
            };
            entry.AddChild(card);

            var label = UiFactory.CreateLabel(creature.Name, 6);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            label.AddThemeColorOverride("font_color", Color.FromHtml("#2F4437"));
            entry.AddChild(label);

            card.ButtonPressed = creature.Id == selectedId;
            cardButtons[creature.Id] = card;
            cards.AddChild(entry);
        }
        AddChild(stats);

        var lockIn = UiFactory.CreateButton(Tr("UI_MP_RACE_LOCK_IN"));
        lockIn.CustomMinimumSize = new Vector2(112, 25);
        lockIn.Pressed += () =>
        {
            if (!string.IsNullOrWhiteSpace(selectedId))
                SelectionRequested?.Invoke(selectedId);
        };
        AddChild(lockIn);

        if (!string.IsNullOrWhiteSpace(prep.SelectedCreatureName))
        {
            AddChild(UiFactory.CreateLabel(
                string.Format(Tr("UI_MP_RACE_LOCKED"), prep.SelectedCreatureName),
                7));
        }

        if (prep.IsLocalCreator || prep.IsLocalHost)
        {
            var start = UiFactory.CreateButton(Tr("UI_MP_RACE_START"));
            start.CustomMinimumSize = new Vector2(148, 25);
            start.Disabled = !prep.CanRequestStart;
            start.Pressed += () => StartRequested?.Invoke();
            AddChild(start);

            if (!prep.CanRequestStart)
            {
                var hint = prep.IsLocalHost && !prep.AllSelectionsReady
                    ? Tr("UI_MP_RACE_WAIT_SELECTIONS")
                    : Tr("UI_MP_RACE_NEED_TWO");
                AddChild(UiFactory.CreateLabel(hint, 6));
            }
        }
        else
        {
            AddChild(UiFactory.CreateLabel(Tr("UI_MP_RACE_WAIT_HOST"), 6));
        }
    }
}
