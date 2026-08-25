using System;
using System.Collections.Generic;
using Godot;
using Voidling.Application.Multiplayer.Challenges;
using VoidlingGame;

namespace Voidling.Presentation.UI.Multiplayer;

/// <summary>
/// Standalone challenge-lobby view. It renders application-provided permissions and emits player
/// intent; it has no GameSession, Steam, transport or challenge-coordinator dependency.
/// </summary>
public partial class ChallengeHubPanel : VBoxContainer
{
    public event Action<int>? OfferRaceRequested;
    public event Action<string>? JoinRequested;
    public event Action<string>? LeaveRequested;
    public event Action<string>? CancelRequested;

    private ChallengeHubViewState? _state;
    private bool _ready;

    public void Configure(ChallengeHubViewState state)
    {
        if (IsInsideTree())
            throw new InvalidOperationException("ChallengeHubPanel must be configured before entering the scene tree.");
        _state = state ?? throw new ArgumentNullException(nameof(state));
    }

    public void Render(ChallengeHubViewState state)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        if (_ready)
            Rebuild();
    }

    public override void _Ready()
    {
        if (_state == null)
            throw new InvalidOperationException("ChallengeHubPanel must be configured before AddChild.");
        AddThemeConstantOverride("separation", 6);
        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        SizeFlagsVertical = Control.SizeFlags.ExpandFill;
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
        if (!state.Availability.IsAvailable || !state.IsConnected)
        {
            AddChild(UiFactory.CreateLabel(Tr("UI_CHALLENGE_NEED_GARDEN"), 8));
            return;
        }

        BuildOfferRow(state);
        AddChild(UiFactory.CreateLabel(Tr("UI_CHALLENGE_OPEN_TITLE"), 8));

        if (state.Challenges.Count == 0)
        {
            AddChild(UiFactory.CreateLabel(Tr("UI_CHALLENGE_EMPTY"), 7));
            return;
        }

        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(500, 220),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        var list = new VBoxContainer();
        list.AddThemeConstantOverride("separation", 6);
        list.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.AddChild(list);
        AddChild(scroll);

        foreach (var challenge in state.Challenges)
            list.AddChild(BuildChallengeCard(challenge));
    }

    private void BuildOfferRow(ChallengeHubViewState state)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);

        var offer = UiFactory.CreateButton(Tr("UI_CHALLENGE_OFFER_RACE"));
        offer.CustomMinimumSize = new Vector2(148, 25);
        offer.Disabled = !state.CanOffer;
        row.AddChild(offer);

        var maxPlayers = new OptionButton
        {
            CustomMinimumSize = new Vector2(105, 25),
            FocusMode = Control.FocusModeEnum.None
        };
        UiFactory.ApplyPixelFont(maxPlayers, 7);
        UiFactory.ApplyButtonChrome(maxPlayers);
        maxPlayers.AddItem(string.Format(Tr("UI_CHALLENGE_PLAYERS"), 2), 2);
        maxPlayers.AddItem(string.Format(Tr("UI_CHALLENGE_PLAYERS"), 3), 3);
        maxPlayers.AddItem(string.Format(Tr("UI_CHALLENGE_PLAYERS"), 4), 4);
        maxPlayers.Select(2);
        row.AddChild(maxPlayers);

        offer.Pressed += () =>
        {
            var max = maxPlayers.GetItemId(maxPlayers.Selected);
            OfferRaceRequested?.Invoke(max);
        };
        AddChild(row);

        if (!state.CanOffer)
            AddChild(UiFactory.CreateLabel(Tr("UI_CHALLENGE_ALREADY_ACTIVE"), 6));
    }

    private Control BuildChallengeCard(ChallengeView challenge)
    {
        var panel = UiFactory.CreatePanel(new Vector2(492, 86));
        panel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 3);
        panel.AddChild(box);

        var heading = new HBoxContainer();
        heading.AddThemeConstantOverride("separation", 6);
        var title = UiFactory.CreateLabel(
            string.Format(
                Tr("UI_CHALLENGE_CARD_TITLE"),
                KindLabel(challenge.Kind),
                PhaseLabel(challenge.Phase)),
            8);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        heading.AddChild(title);
        heading.AddChild(UiFactory.CreateLabel(
            string.Format(
                Tr("UI_CHALLENGE_COUNT"),
                challenge.Participants.Count,
                challenge.MaxParticipants),
            7));
        box.AddChild(heading);

        box.AddChild(UiFactory.CreateLabel(
            string.Format(Tr("UI_CHALLENGE_HOSTED_BY"), challenge.CreatorDisplayName),
            6));

        var names = new List<string>(challenge.Participants.Count);
        foreach (var participant in challenge.Participants)
        {
            var suffix = participant.IsLocal
                ? Tr("UI_CHALLENGE_YOU")
                : participant.IsCreator
                    ? Tr("UI_CHALLENGE_CREATOR")
                    : participant.IsHost
                        ? Tr("UI_CHALLENGE_HOST")
                        : string.Empty;
            names.Add(suffix.Length == 0
                ? participant.DisplayName
                : $"{participant.DisplayName} {suffix}");
        }
        var participants = UiFactory.CreateLabel(string.Join("  •  ", names), 6);
        participants.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        box.AddChild(participants);

        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 5);
        if (challenge.CanJoin)
        {
            var join = UiFactory.CreateButton(Tr("UI_CHALLENGE_JOIN"));
            join.CustomMinimumSize = new Vector2(82, 23);
            join.Pressed += () => JoinRequested?.Invoke(challenge.ChallengeId);
            actions.AddChild(join);
        }
        if (challenge.CanLeave)
        {
            var leave = UiFactory.CreateButton(Tr("UI_CHALLENGE_LEAVE"));
            leave.CustomMinimumSize = new Vector2(82, 23);
            leave.Pressed += () => LeaveRequested?.Invoke(challenge.ChallengeId);
            actions.AddChild(leave);
        }
        if (challenge.CanCancel)
        {
            var cancel = UiFactory.CreateButton(Tr("UI_CHALLENGE_CANCEL"));
            cancel.CustomMinimumSize = new Vector2(82, 23);
            cancel.Pressed += () => CancelRequested?.Invoke(challenge.ChallengeId);
            actions.AddChild(cancel);
        }
        if (actions.GetChildCount() > 0)
            box.AddChild(actions);

        return panel;
    }

    private string KindLabel(ChallengeKind kind)
        => kind switch
        {
            ChallengeKind.Race => Tr("UI_CHALLENGE_KIND_RACE"),
            ChallengeKind.AutoBattle => Tr("UI_CHALLENGE_KIND_BATTLE"),
            _ => kind.ToString()
        };

    private string PhaseLabel(ChallengePhase phase)
        => phase switch
        {
            ChallengePhase.Offered => Tr("UI_CHALLENGE_PHASE_OPEN"),
            ChallengePhase.Forming => Tr("UI_CHALLENGE_PHASE_FORMING"),
            ChallengePhase.Ready => Tr("UI_CHALLENGE_PHASE_READY"),
            ChallengePhase.Running => Tr("UI_CHALLENGE_PHASE_RUNNING"),
            _ => phase.ToString()
        };
}
