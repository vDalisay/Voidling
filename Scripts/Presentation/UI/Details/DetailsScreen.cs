using System;
using System.Collections.Generic;
using Godot;
using VoidlingGame;

namespace Voidling.Presentation.UI.Details;

public readonly record struct DetailsStatViewState(
    string DisplayName,
    Color IdentityColor,
    string Rank,
    int Level,
    int EffectiveValue,
    double Progress,
    string AlleleA,
    string AlleleB);

public readonly record struct DetailsRareTraitViewState(
    string TraitId,
    string FounderName,
    int GenerationFromFounder,
    bool CanTransmit);

public sealed record DetailsScreenState(
    string Name,
    bool IsAdult,
    int FamilyGeneration,
    int InbreedingBurden,
    Color TintColor,
    bool HasAngelMutation,
    int OtherMutationCount,
    int ColorAlleleA,
    int ColorAlleleB,
    int ExpressedColorIndex,
    IReadOnlyList<DetailsStatViewState> Stats,
    IReadOnlyList<DetailsRareTraitViewState> RareTraits);

/// <summary>
/// Standalone Stats/DNA/Visual detail view over immutable presentation-ready data. Genetics,
/// progression, lineage lookup and mutation interpretation are resolved before the screen is
/// configured so this class cannot become another gameplay-rules owner.
/// </summary>
public partial class DetailsScreen : VBoxContainer
{
    private DetailsScreenState? _state;
    private VBoxContainer _body = null!;
    private Button _statsTab = null!;
    private Button _dnaTab = null!;
    private Button _visualTab = null!;

    public void Configure(DetailsScreenState state)
    {
        if (IsInsideTree())
            throw new InvalidOperationException("DetailsScreen must be configured before it enters the scene tree.");

        _state = state ?? throw new ArgumentNullException(nameof(state));
    }

    public override void _Ready()
    {
        if (_state == null)
            throw new InvalidOperationException("DetailsScreen must be configured before AddChild.");

        AddThemeConstantOverride("separation", 5);
        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        SizeFlagsVertical = Control.SizeFlags.ExpandFill;

        var tabs = new HBoxContainer();
        tabs.AddThemeConstantOverride("separation", 5);
        _statsTab = CreateTab(Tr("UI_DETAILS_STATS"));
        _dnaTab = CreateTab(Tr("UI_DETAILS_DNA"));
        _visualTab = CreateTab(Tr("UI_DETAILS_VISUAL"));
        tabs.AddChild(_statsTab);
        tabs.AddChild(_dnaTab);
        tabs.AddChild(_visualTab);
        AddChild(tabs);

        _body = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(492, 238),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        _body.AddThemeConstantOverride("separation", 5);
        AddChild(_body);

        _statsTab.Pressed += RenderStats;
        _dnaTab.Pressed += RenderDna;
        _visualTab.Pressed += RenderVisual;
        RenderStats();
    }

    private static Button CreateTab(string text)
    {
        var tab = UiFactory.CreateButton(text);
        tab.CustomMinimumSize = new Vector2(92, 23);
        tab.ToggleMode = true;
        return tab;
    }

    private void ClearBody()
    {
        foreach (var child in _body.GetChildren())
        {
            _body.RemoveChild(child);
            child.QueueFree();
        }
    }

    private void SelectTab(Button active)
    {
        _statsTab.ButtonPressed = active == _statsTab;
        _dnaTab.ButtonPressed = active == _dnaTab;
        _visualTab.ButtonPressed = active == _visualTab;
    }

    private void RenderStats()
    {
        var state = _state!;
        ClearBody();
        SelectTab(_statsTab);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 12);
        header.AddChild(CreatePortrait(state, new Vector2(58, 58)));
        var summary = new VBoxContainer();
        summary.AddThemeConstantOverride("separation", 2);
        summary.AddChild(UiFactory.CreateLabel(state.IsAdult ? Tr("UI_DETAILS_ADULT") : Tr("UI_DETAILS_CHILD"), 8));
        summary.AddChild(UiFactory.CreateLabel(Tr("UI_DETAILS_STATS_HINT"), 6));
        header.AddChild(summary);
        _body.AddChild(header);

        foreach (var stat in state.Stats)
            _body.AddChild(CreateDetailsStatRow(stat));
    }

    private void RenderDna()
    {
        var state = _state!;
        ClearBody();
        SelectTab(_dnaTab);

        var intro = new HBoxContainer();
        intro.AddThemeConstantOverride("separation", 10);
        intro.AddChild(CreatePortrait(state, new Vector2(54, 54)));
        var summary = new VBoxContainer();
        summary.AddThemeConstantOverride("separation", 2);
        summary.AddChild(UiFactory.CreateLabel(string.Format(Tr("UI_DETAILS_GENERATION"), state.FamilyGeneration), 8));
        summary.AddChild(UiFactory.CreateLabel(string.Format(Tr("UI_DETAILS_INBREEDING"), state.InbreedingBurden), 7));
        summary.AddChild(UiFactory.CreateLabel(Tr("UI_DETAILS_DNA_HINT"), 6));
        intro.AddChild(summary);
        _body.AddChild(intro);

        _body.AddChild(CreateDnaHeaderRow());
        foreach (var stat in state.Stats)
            _body.AddChild(CreateDnaStatRow(stat));

        _body.AddChild(UiFactory.CreateLabel(
            string.Format(Tr("UI_DETAILS_COLOR_DNA"), state.ColorAlleleA, state.ColorAlleleB), 7));
    }

    private void RenderVisual()
    {
        var state = _state!;
        ClearBody();
        SelectTab(_visualTab);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 18);
        row.AddChild(CreatePortrait(state, new Vector2(130, 130)));

        var info = new VBoxContainer();
        info.AddThemeConstantOverride("separation", 7);
        info.AddChild(UiFactory.CreateLabel(Tr("UI_DETAILS_CURRENT_APPEARANCE"), 9));
        info.AddChild(new ColorRect
        {
            Color = state.TintColor,
            CustomMinimumSize = new Vector2(118, 30)
        });
        var expressedColor = state.ExpressedColorIndex == 0 ? state.ColorAlleleA : state.ColorAlleleB;
        info.AddChild(UiFactory.CreateLabel(string.Format(Tr("UI_DETAILS_SHOWN_COLOR"), expressedColor), 7));
        info.AddChild(UiFactory.CreateLabel(string.Format(Tr("UI_DETAILS_COLOR_PAIR"), state.ColorAlleleA, state.ColorAlleleB), 7));
        row.AddChild(info);
        _body.AddChild(row);

        if (state.RareTraits.Count == 0)
        {
            _body.AddChild(UiFactory.CreateLabel(Tr("UI_DETAILS_NO_MUTATION"), 8));
            return;
        }

        foreach (var trait in state.RareTraits)
        {
            _body.AddChild(UiFactory.CreateLabel(
                string.Format(
                    Tr(trait.CanTransmit ? "UI_DETAILS_MUTATION_TRANSMIT" : "UI_DETAILS_MUTATION_TERMINAL"),
                    trait.TraitId,
                    trait.FounderName,
                    trait.GenerationFromFounder),
                7));
        }
    }

    private static TextureRect CreatePortrait(DetailsScreenState state, Vector2 size)
        => UiFactory.CreatePortrait(
            state.Name,
            state.TintColor,
            state.HasAngelMutation,
            state.OtherMutationCount,
            size);

    private static Control CreateDetailsStatRow(DetailsStatViewState stat)
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(472, 29) };
        var background = stat.IdentityColor;
        background.A = string.Equals(stat.DisplayName, "Stamina", StringComparison.OrdinalIgnoreCase) ? 0.55f : 0.22f;
        var style = new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = Color.FromHtml("#BE916C")
        };
        style.SetBorderWidthAll(1);
        style.ContentMarginLeft = style.ContentMarginRight = 6;
        style.ContentMarginTop = style.ContentMarginBottom = 3;
        panel.AddThemeStyleboxOverride("panel", style);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        panel.AddChild(row);

        var name = UiFactory.CreateLabel(stat.DisplayName.ToUpperInvariant(), 8);
        name.CustomMinimumSize = new Vector2(75, 19);
        name.AddThemeColorOverride("font_color", stat.IdentityColor);
        name.AddThemeColorOverride("font_outline_color", Color.FromHtml("#465247"));
        name.AddThemeConstantOverride("outline_size", 1);
        row.AddChild(name);

        var values = UiFactory.CreateLabel(
            $"RANK {stat.Rank}   LV {stat.Level:00}   STAT {stat.EffectiveValue:00}", 7);
        values.CustomMinimumSize = new Vector2(205, 19);
        row.AddChild(values);

        var progress = CreateProgressBar(stat.Progress, stat.IdentityColor, new Vector2(165, 8));
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

    private static Control CreateDnaStatRow(DetailsStatViewState stat)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 4);

        var background = stat.IdentityColor;
        background.A = string.Equals(stat.DisplayName, "Stamina", StringComparison.OrdinalIgnoreCase) ? 0.65f : 0.28f;
        row.AddChild(CreateDnaCell(
            stat.DisplayName.ToUpperInvariant(), 176, 7, false, background, stat.IdentityColor));
        row.AddChild(CreateDnaCell(stat.AlleleA, 142, 9, false));
        row.AddChild(CreateDnaCell(stat.AlleleB, 142, 9, false));
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

    private static ProgressBar CreateProgressBar(double value, Color fillColor, Vector2 size)
    {
        var bar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 1,
            Value = value,
            ShowPercentage = false,
            CustomMinimumSize = size
        };
        var background = new StyleBoxFlat { BgColor = Color.FromHtml("#6D6658") };
        var fill = new StyleBoxFlat { BgColor = fillColor };
        background.CornerRadiusTopLeft = background.CornerRadiusTopRight = 1;
        background.CornerRadiusBottomLeft = background.CornerRadiusBottomRight = 1;
        fill.CornerRadiusTopLeft = fill.CornerRadiusTopRight = 1;
        fill.CornerRadiusBottomLeft = fill.CornerRadiusBottomRight = 1;
        bar.AddThemeStyleboxOverride("background", background);
        bar.AddThemeStyleboxOverride("fill", fill);
        return bar;
    }
}
