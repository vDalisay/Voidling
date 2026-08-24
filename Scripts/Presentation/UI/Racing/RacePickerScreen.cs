using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using VoidlingGame;

namespace Voidling.Presentation.UI.Racing;

public readonly record struct RacePickerVoidlingViewState(
    string Id,
    string Name,
    Color TintColor,
    bool HasAngelMutation,
    int OtherMutationCount,
    string StatSummary);

public sealed record RacePickerScreenState(
    IReadOnlyList<RacePickerVoidlingViewState> Voidlings,
    string SelectedId);

/// <summary>
/// Standalone race-selection view. It renders immutable, presentation-ready participant snapshots
/// and emits the selected creature ID. It has no knowledge of GameSession, race construction,
/// persistence, balance rules, or the race simulator.
/// </summary>
public partial class RacePickerScreen : VBoxContainer
{
    public event Action<string>? RaceRequested;

    private RacePickerScreenState? _state;
    private string _selectedId = string.Empty;

    public void Configure(RacePickerScreenState state)
    {
        if (IsInsideTree())
            throw new InvalidOperationException("RacePickerScreen must be configured before it enters the scene tree.");

        _state = state ?? throw new ArgumentNullException(nameof(state));
    }

    public override void _Ready()
    {
        if (_state == null)
            throw new InvalidOperationException("RacePickerScreen must be configured before AddChild.");

        AddThemeConstantOverride("separation", 7);
        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        if (_state.Voidlings.Count == 0)
        {
            AddChild(UiFactory.CreateLabel(Tr("UI_RACE_PICKER_EMPTY"), 9));
            return;
        }

        _selectedId = _state.Voidlings.Any(v => v.Id == _state.SelectedId)
            ? _state.SelectedId
            : _state.Voidlings[0].Id;

        AddChild(UiFactory.CreateLabel(Tr("UI_RACE_PICKER_HINT"), 7));
        BuildPicker(_state.Voidlings);
    }

    private void BuildPicker(IReadOnlyList<RacePickerVoidlingViewState> voidlings)
    {
        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(510, 90),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            VerticalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        var cards = new HBoxContainer();
        cards.AddThemeConstantOverride("separation", 7);
        scroll.AddChild(cards);
        AddChild(scroll);

        var selected = voidlings.First(v => v.Id == _selectedId);
        var previewRow = new HBoxContainer();
        previewRow.AddThemeConstantOverride("separation", 12);
        var previewPortrait = UiFactory.CreatePortrait(
            selected.TintColor,
            selected.HasAngelMutation,
            selected.OtherMutationCount,
            new Vector2(72, 72));
        previewRow.AddChild(previewPortrait);

        var previewText = new VBoxContainer();
        previewText.AddThemeConstantOverride("separation", 2);
        var previewName = UiFactory.CreateTitle(selected.Name);
        var previewStats = UiFactory.CreateLabel(selected.StatSummary, 7);
        previewText.AddChild(previewName);
        previewText.AddChild(previewStats);
        previewRow.AddChild(previewText);
        AddChild(previewRow);

        var cardButtons = new Dictionary<string, Button>(StringComparer.Ordinal);

        void UpdatePreview(RacePickerVoidlingViewState candidate)
        {
            _selectedId = candidate.Id;
            UiFactory.SetPortraitData(
                previewPortrait,
                candidate.TintColor,
                candidate.HasAngelMutation,
                candidate.OtherMutationCount);
            previewName.Text = candidate.Name;
            previewStats.Text = candidate.StatSummary;

            foreach (var pair in cardButtons)
                pair.Value.ButtonPressed = pair.Key == candidate.Id;
        }

        foreach (var creature in voidlings)
        {
            var entry = new VBoxContainer { CustomMinimumSize = new Vector2(84, 78) };
            entry.AddThemeConstantOverride("separation", 1);

            var card = UiFactory.CreateButton("");
            card.CustomMinimumSize = new Vector2(80, 58);
            card.ToggleMode = true;
            card.KeepPressedOutside = true;
            cardButtons[creature.Id] = card;

            var portrait = UiFactory.CreatePortrait(
                creature.TintColor,
                creature.HasAngelMutation,
                creature.OtherMutationCount,
                new Vector2(48, 48));
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

        UpdatePreview(selected);

        var start = UiFactory.CreateButton(Tr("UI_RACE_START"));
        start.CustomMinimumSize = new Vector2(170, 26);
        start.Pressed += () => RaceRequested?.Invoke(_selectedId);
        AddChild(start);
    }
}
