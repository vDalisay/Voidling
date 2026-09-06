using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Voidling.Presentation.UI.Common;
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
/// Nesting: the same paged roster the race picker uses, choosing two parents instead of one. The
/// picked pair stands either side of the premium heart, and the pairing verdict is a card rather
/// than a paragraph. Validation, genetics, persistence, placement and animation stay outside.
/// </summary>
public partial class BreedingScreen : HBoxContainer
{
    public event Action<string, string>? PairChanged;
    public event Action<string, string>? BreedRequested;

    private static readonly Texture2D WoodHearts = GD.Load<Texture2D>(
        UiFactory.UiRoot + "Icons/special icons/Hearts in wood.png");
    private static readonly Texture2D CheckMark = GD.Load<Texture2D>(
        UiFactory.UiRoot + "Other UI sprites/Xs and check marks/1s/check mark.png");
    private static readonly Texture2D CrossMark = GD.Load<Texture2D>(
        UiFactory.UiRoot + "Other UI sprites/Xs and check marks/1s/X.png");

    private BreedingScreenState? _state;
    private BreedingPreviewViewState _currentPreview;
    private string _parentAId = string.Empty;
    private string _parentBId = string.Empty;
    private int _page;

    private VBoxContainer _rosterBox = null!;
    private VBoxContainer _pairBox = null!;
    private Label? _preview;
    private TextureRect? _verdict;
    private Button? _breed;

    public void Configure(BreedingScreenState state)
    {
        if (IsInsideTree())
            throw new InvalidOperationException("BreedingScreen must be configured before it enters the scene tree.");

        _state = state ?? throw new ArgumentNullException(nameof(state));
        _currentPreview = state.InitialPreview;
        if (state.Parents.Count >= 2)
        {
            _parentAId = state.Parents[0].Id;
            _parentBId = state.Parents[1].Id;
        }
    }

    public override void _Ready()
    {
        if (_state == null)
            throw new InvalidOperationException("BreedingScreen must be configured before AddChild.");

        Name = "Nesting";
        AddThemeConstantOverride("separation", 8);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;

        if (_state.Parents.Count < 2)
        {
            AddChild(UiFactory.CreateLabel(Tr("UI_BREED_NEED_TWO_ADULTS"), 10));
            return;
        }

        var rosterPanel = PaperCard.Panel(Vector2.Zero);
        rosterPanel.Name = "RosterPanel";
        rosterPanel.SizeFlagsVertical = SizeFlags.ShrinkBegin;
        AddChild(rosterPanel);
        _rosterBox = new VBoxContainer();
        _rosterBox.AddThemeConstantOverride("separation", 3);
        rosterPanel.AddChild(_rosterBox);

        var pairPanel = PaperCard.Panel(new Vector2(196, 218));
        pairPanel.Name = "PairCard";
        AddChild(pairPanel);
        _pairBox = new VBoxContainer();
        _pairBox.AddThemeConstantOverride("separation", 5);
        pairPanel.AddChild(_pairBox);

        RebuildRoster();
        RebuildPair();
    }

    public void SetPreview(BreedingPreviewViewState preview)
    {
        _currentPreview = preview;
        if (_preview != null && GodotObject.IsInstanceValid(_preview))
            _preview.Text = preview.Text;
        if (_verdict != null && GodotObject.IsInstanceValid(_verdict))
            _verdict.Texture = preview.CanBreed ? CheckMark : CrossMark;
        if (_breed != null && GodotObject.IsInstanceValid(_breed))
            _breed.Disabled = !preview.CanBreed;
    }

    public void FocusSelection() => _breed?.GrabFocus();

    // ---- roster ---------------------------------------------------------------------------

    private void RebuildRoster()
    {
        PaperCard.Clear(_rosterBox);
        var roster = _state!.Parents
            .Select(parent => new RosterEntry(
                parent.Id, parent.Name, parent.Appearance, parent.HasAngelMutation, parent.OtherMutationCount))
            .ToArray();
        _rosterBox.AddChild(VoidlingRosterGrid.Build(
            roster,
            _page,
            entry => entry.Id == _parentAId || entry.Id == _parentBId,
            entry => entry.Id == _parentAId ? "A" : entry.Id == _parentBId ? "B" : string.Empty,
            Pick,
            page => { _page = page; RebuildRoster(); },
            out var pages));
        _page = Mathf.Clamp(_page, 0, pages - 1);
        if (pages <= 1) return;
        var pageLabel = UiFactory.CreateLabel(string.Format(Tr("UI_RACE_ROSTER_PAGE"), _page + 1, pages), 6);
        pageLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _rosterBox.AddChild(pageLabel);
    }

    /// <summary>
    /// Picking always replaces the older of the two parents, so a third click swaps rather than
    /// stalling on a full pair — the same feel as walking two Chao together.
    /// </summary>
    private void Pick(RosterEntry entry)
    {
        if (entry.Id == _parentAId || entry.Id == _parentBId) return;
        _parentBId = _parentAId;
        _parentAId = entry.Id;
        RebuildRoster();
        RebuildPair();
        PairChanged?.Invoke(_parentAId, _parentBId);
    }

    // ---- the pair -------------------------------------------------------------------------

    private void RebuildPair()
    {
        PaperCard.Clear(_pairBox);
        var parentA = Find(_parentAId);
        var parentB = Find(_parentBId);

        var portraits = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        portraits.AddThemeConstantOverride("separation", 4);
        portraits.AddChild(PortraitColumn(parentA, "ParentA"));
        portraits.AddChild(new TextureRect
        {
            Texture = new AtlasTexture { Atlas = WoodHearts, Region = new Rect2(0, 0, 32, 32) },
            CustomMinimumSize = new Vector2(26, 26),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore
        });
        portraits.AddChild(PortraitColumn(parentB, "ParentB"));
        _pairBox.AddChild(portraits);

        _pairBox.AddChild(new ColorRect
        {
            Color = Color.FromHtml("#B7926F"),
            CustomMinimumSize = new Vector2(1, 1),
            MouseFilter = MouseFilterEnum.Ignore
        });

        var verdictRow = new HBoxContainer();
        verdictRow.AddThemeConstantOverride("separation", 4);
        _verdict = new TextureRect
        {
            Name = "PairVerdict",
            Texture = _currentPreview.CanBreed ? CheckMark : CrossMark,
            CustomMinimumSize = new Vector2(16, 16),
            SizeFlagsVertical = SizeFlags.ShrinkBegin,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore
        };
        verdictRow.AddChild(_verdict);
        _preview = UiFactory.CreateLabel(_currentPreview.Text, 7);
        _preview.Name = "PairPreview";
        _preview.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _preview.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        verdictRow.AddChild(_preview);
        _pairBox.AddChild(verdictRow);

        _pairBox.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });

        _breed = UiFactory.CreateButton(Tr("UI_BREED_ACTION"));
        _breed.Name = "BreedAction";
        _breed.CustomMinimumSize = new Vector2(176, 28);
        UiFactory.ApplyPrimaryStyle(_breed);
        _breed.Disabled = !_currentPreview.CanBreed;
        _breed.Pressed += () =>
        {
            if (_parentAId.Length > 0 && _parentBId.Length > 0)
                BreedRequested?.Invoke(_parentAId, _parentBId);
        };
        _pairBox.AddChild(_breed);
    }

    private Control PortraitColumn(BreedingParentViewState? parent, string name)
    {
        var column = new VBoxContainer { Name = name, Alignment = BoxContainer.AlignmentMode.Center };
        column.AddThemeConstantOverride("separation", 2);
        if (parent == null)
        {
            column.AddChild(PaperCard.EmptySlot(new Vector2(66, 66)));
            var waiting = UiFactory.CreateLabel(Tr("UI_BREED_PICK_PARENTS"), 6);
            waiting.HorizontalAlignment = HorizontalAlignment.Center;
            waiting.CustomMinimumSize = new Vector2(66, 0);
            waiting.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            column.AddChild(waiting);
            return column;
        }
        column.AddChild(UiFactory.CreatePortrait(
            parent.Value.Appearance,
            parent.Value.HasAngelMutation,
            parent.Value.OtherMutationCount,
            new Vector2(66, 66)));
        var label = UiFactory.CreateLabel(parent.Value.Name, 7);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.CustomMinimumSize = new Vector2(66, 0);
        label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        column.AddChild(label);
        return column;
    }

    private BreedingParentViewState? Find(string id)
    {
        if (id.Length == 0) return null;
        foreach (var parent in _state!.Parents)
            if (parent.Id == id) return parent;
        return null;
    }
}
