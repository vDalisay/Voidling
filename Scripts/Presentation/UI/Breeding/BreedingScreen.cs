using System;
using System.Collections.Generic;
using Godot;
using Voidling.Presentation.Voidlings;
using VoidlingGame;

namespace Voidling.Presentation.UI.Breeding;

public readonly record struct BreedingParentViewState(
    string Id,
    string Name,
    VoidlingVisualAppearance Appearance,
    bool HasAngelMutation,
    int OtherMutationCount);

public readonly record struct BreedingPreviewViewState(string Text, bool CanBreed);

public sealed record BreedingScreenState(
    IReadOnlyList<BreedingParentViewState> Parents,
    BreedingPreviewViewState InitialPreview);

/// <summary>
/// Standalone breeding view. Parent portraits resolve through the same semantic visual composition
/// as Garden/race rendering; validation, genetics, persistence, placement and animation stay outside.
/// </summary>
public partial class BreedingScreen : VBoxContainer
{
    public event Action<string, string>? PairChanged;
    public event Action<string, string>? BreedRequested;

    private BreedingScreenState? _state;
    private OptionButton? _parentA;
    private OptionButton? _parentB;
    private TextureRect? _portraitA;
    private TextureRect? _portraitB;
    private Label? _preview;
    private BreedingPreviewViewState _currentPreview;

    public void Configure(BreedingScreenState state)
    {
        if (IsInsideTree())
            throw new InvalidOperationException("BreedingScreen must be configured before it enters the scene tree.");

        _state = state ?? throw new ArgumentNullException(nameof(state));
        _currentPreview = state.InitialPreview;
    }

    public override void _Ready()
    {
        if (_state == null)
            throw new InvalidOperationException("BreedingScreen must be configured before AddChild.");

        AddThemeConstantOverride("separation", 7);
        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        if (_state.Parents.Count < 2)
        {
            AddChild(UiFactory.CreateLabel(Tr("UI_BREED_NEED_TWO_ADULTS"), 10));
            return;
        }

        _parentA = new OptionButton();
        _parentB = new OptionButton();
        StyleOption(_parentA);
        StyleOption(_parentB);

        foreach (var parent in _state.Parents)
        {
            _parentA.AddItem(parent.Name);
            _parentB.AddItem(parent.Name);
        }

        _parentA.Selected = 0;
        _parentB.Selected = 1;

        var selectors = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        selectors.AddThemeConstantOverride("separation", 12);

        var left = CreateSelector(_state.Parents[0], _parentA);
        var right = CreateSelector(_state.Parents[1], _parentB);
        _portraitA = left.Portrait;
        _portraitB = right.Portrait;
        selectors.AddChild(left.Container);
        selectors.AddChild(UiFactory.CreateLabel("+", 14));
        selectors.AddChild(right.Container);
        AddChild(selectors);

        _preview = UiFactory.CreateLabel(_currentPreview.Text, 7);
        _preview.CustomMinimumSize = new Vector2(390, 36);
        _preview.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        AddChild(_preview);

        _parentA.ItemSelected += _ => UpdateSelectionAndEmit();
        _parentB.ItemSelected += _ => UpdateSelectionAndEmit();

        var breed = UiFactory.CreateButton(Tr("UI_BREED_ACTION"));
        breed.CustomMinimumSize = new Vector2(120, 26);
        breed.Pressed += () =>
        {
            var pair = CurrentPair();
            if (pair != null)
                BreedRequested?.Invoke(pair.Value.First.Id, pair.Value.Second.Id);
        };
        AddChild(breed);
    }

    public void SetPreview(BreedingPreviewViewState preview)
    {
        _currentPreview = preview;
        if (_preview != null && GodotObject.IsInstanceValid(_preview))
            _preview.Text = preview.Text;
    }

    private void UpdateSelectionAndEmit()
    {
        var pair = CurrentPair();
        if (pair == null || _portraitA == null || _portraitB == null)
            return;

        SetPortrait(_portraitA, pair.Value.First);
        SetPortrait(_portraitB, pair.Value.Second);
        PairChanged?.Invoke(pair.Value.First.Id, pair.Value.Second.Id);
    }

    private (BreedingParentViewState First, BreedingParentViewState Second)? CurrentPair()
    {
        if (_state == null || _parentA == null || _parentB == null ||
            _parentA.Selected < 0 || _parentB.Selected < 0)
        {
            return null;
        }

        return (_state.Parents[_parentA.Selected], _state.Parents[_parentB.Selected]);
    }

    private static (VBoxContainer Container, TextureRect Portrait) CreateSelector(
        BreedingParentViewState parent,
        OptionButton option)
    {
        var column = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        column.AddThemeConstantOverride("separation", 3);
        var portrait = UiFactory.CreatePortrait(
            parent.Appearance,
            parent.HasAngelMutation,
            parent.OtherMutationCount,
            new Vector2(70, 70));
        column.AddChild(portrait);
        column.AddChild(option);
        return (column, portrait);
    }

    private static void SetPortrait(TextureRect portrait, BreedingParentViewState parent)
        => UiFactory.SetPortraitData(
            portrait,
            parent.Appearance,
            parent.HasAngelMutation,
            parent.OtherMutationCount);

    private static void StyleOption(OptionButton option)
    {
        option.CustomMinimumSize = new Vector2(165, 24);
        UiFactory.ApplyPixelFont(option, 8);
        UiFactory.ApplyButtonChrome(option);
        option.AddThemeColorOverride("font_color", Color.FromHtml("#465247"));
    }
}
