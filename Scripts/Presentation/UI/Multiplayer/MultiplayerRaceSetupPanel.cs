using System;
using System.Collections.Generic;
using Godot;
using Voidling.Application.Multiplayer.Challenges;
using Voidling.Application.Multiplayer.Racing;
using VoidlingGame;

namespace Voidling.Presentation.UI.Multiplayer;

public sealed record MultiplayerRaceSetupVoidlingView(
    string Id,
    string Name,
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
        var selectionRow = new HBoxContainer();
        selectionRow.AddThemeConstantOverride("separation", 6);
        var option = new OptionButton
        {
            CustomMinimumSize = new Vector2(240, 25),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            FocusMode = Control.FocusModeEnum.None
        };
        UiFactory.ApplyPixelFont(option, 7);
        UiFactory.ApplyButtonChrome(option);
        var selectedIndex = 0;
        for (var i = 0; i < state.Voidlings.Count; i++)
        {
            var creature = state.Voidlings[i];
            option.AddItem(creature.Name, i);
            option.SetItemMetadata(i, creature.Id);
            if (string.Equals(creature.Id, prep.SelectedCreatureId, StringComparison.Ordinal))
                selectedIndex = i;
        }
        option.Select(selectedIndex);
        selectionRow.AddChild(option);

        var lockIn = UiFactory.CreateButton(Tr("UI_MP_RACE_LOCK_IN"));
        lockIn.CustomMinimumSize = new Vector2(112, 25);
        lockIn.Pressed += () =>
        {
            var id = option.GetItemMetadata(option.Selected).AsString();
            if (!string.IsNullOrWhiteSpace(id))
                SelectionRequested?.Invoke(id);
        };
        selectionRow.AddChild(lockIn);
        AddChild(selectionRow);

        var selected = state.Voidlings[selectedIndex];
        var stats = UiFactory.CreateLabel(selected.StatSummary, 6);
        stats.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        AddChild(stats);

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
