using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Voidling.Presentation.Garden;
using Voidling.Presentation.UI.Common;

namespace VoidlingGame;

public partial class MainController
{
    private static readonly Vector2 DecorationSlotSize = new(58, 58);
    private const int DecorationSlotColumns = 5;
    private const int DecorationSlotRows = 3;

    private static readonly Texture2D DecorationTexture =
        GD.Load<Texture2D>(GardenDecorationCatalog.TexturePath);

    private string _selectedDecoration = string.Empty;

    /// <summary>
    /// One kind of decoration to place, or one already standing in the garden. Both live in the
    /// same grid drawn with their own sprite, and the card carries whichever action applies.
    /// </summary>
    private sealed record DecorationSlot(string Key, string TypeId, string Name, string? InstanceId);

    private void ShowGardenDecorations()
    {
        var box = OpenModal(Tr("UI_GARDEN_BUILD_DECORATE"), new Vector2(520, 292), ShowGardenBuild);
        box.AddThemeConstantOverride("separation", 5);

        var body = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 6);
        box.AddChild(body);

        var gridPanel = PaperCard.Panel(new Vector2(316, 0));
        gridPanel.Name = "DecorationSlots";
        gridPanel.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
        body.AddChild(gridPanel);
        var grid = new GridContainer { Columns = DecorationSlotColumns };
        grid.AddThemeConstantOverride("h_separation", 3);
        grid.AddThemeConstantOverride("v_separation", 3);
        gridPanel.AddChild(grid);

        var detailPanel = PaperCard.Panel(new Vector2(172, 232));
        detailPanel.Name = "DecorationDetail";
        body.AddChild(detailPanel);
        var detail = new VBoxContainer();
        detail.AddThemeConstantOverride("separation", 4);
        detailPanel.AddChild(detail);

        var slots = DecorationSlots().ToArray();
        if (slots.All(slot => slot.Key != _selectedDecoration)) _selectedDecoration = slots[0].Key;

        foreach (var slot in slots) grid.AddChild(BuildDecorationSlot(slot));
        for (var filler = slots.Length; filler < DecorationSlotColumns * DecorationSlotRows; filler++)
            grid.AddChild(PaperCard.EmptySlot(DecorationSlotSize));

        BuildDecorationDetail(detail, slots.First(slot => slot.Key == _selectedDecoration));
    }

    private IEnumerable<DecorationSlot> DecorationSlots()
    {
        foreach (var definition in GardenDecorationCatalog.All)
            yield return new DecorationSlot("type:" + definition.TypeId, definition.TypeId, DecorationName(definition.TypeId), null);

        foreach (var decoration in _session.State.GardenDecorations
                     .Where(data => data != null && GardenDecorationCatalog.TryGet(data.TypeId, out _))
                     .OrderBy(data => data.TypeId, StringComparer.Ordinal)
                     .ThenBy(data => data.Id, StringComparer.Ordinal))
        {
            yield return new DecorationSlot(
                "placed:" + decoration.Id, decoration.TypeId, DecorationName(decoration.TypeId), decoration.Id);
        }
    }

    private Button BuildDecorationSlot(DecorationSlot slot)
    {
        var button = UiFactory.CreateButton(string.Empty);
        button.Name = "Decoration_" + slot.Key.Replace(':', '_');
        button.ToggleMode = true;
        button.ButtonPressed = slot.Key == _selectedDecoration;
        button.CustomMinimumSize = DecorationSlotSize;
        button.TooltipText = slot.Name;

        var art = DecorationArt(slot.TypeId, new Vector2(30, 42));
        art.Position = (DecorationSlotSize - art.CustomMinimumSize) * 0.5f - new Vector2(0, 3);
        // A kind you can still place reads lighter than one already standing in the garden.
        art.Modulate = new Color(1, 1, 1, slot.InstanceId == null ? 0.6f : 1.0f);
        button.AddChild(art);
        if (slot.Key == _selectedDecoration) button.AddChild(PaperCard.Star(new Vector2(42, 1), 14));

        var capturedKey = slot.Key;
        button.Pressed += () => { _selectedDecoration = capturedKey; CallDeferred(nameof(ShowGardenDecorations)); };
        return button;
    }

    private void BuildDecorationDetail(VBoxContainer detail, DecorationSlot slot)
    {
        var artPanel = new PanelContainer { CustomMinimumSize = new Vector2(0, 64) };
        artPanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = Color.FromHtml("#F7E5BD") });
        var center = new CenterContainer();
        center.AddChild(DecorationArt(slot.TypeId, new Vector2(38, 54)));
        artPanel.AddChild(center);
        detail.AddChild(artPanel);

        var name = UiFactory.CreateLabel(slot.Name, 9);
        name.Name = "DecorationName";
        name.HorizontalAlignment = HorizontalAlignment.Center;
        detail.AddChild(name);

        var state = UiFactory.CreateLabel(Tr(slot.InstanceId == null ? "UI_DECORATION_CATALOGUE" : "UI_DECORATION_PLACED"), 7);
        state.HorizontalAlignment = HorizontalAlignment.Center;
        detail.AddChild(state);
        detail.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });

        var capturedTypeId = slot.TypeId;
        var capturedInstanceId = slot.InstanceId;
        var primary = UiFactory.CreateButton(Tr(capturedInstanceId == null ? "UI_DECORATION_PLACE" : "UI_DECORATION_MOVE"));
        primary.Name = "DecorationAction";
        primary.CustomMinimumSize = new Vector2(152, 26);
        UiFactory.ApplyPrimaryStyle(primary);
        primary.Pressed += () =>
        {
            CloseModal();
            _garden.BeginDecorationPlacement(capturedTypeId, capturedInstanceId);
        };
        detail.AddChild(primary);

        if (capturedInstanceId == null) return;
        var remove = UiFactory.CreateButton(Tr("UI_DECORATION_REMOVE"));
        remove.Name = "DecorationRemove";
        remove.CustomMinimumSize = new Vector2(152, 22);
        UiFactory.ApplyPixelFont(remove, 6);
        remove.AddThemeColorOverride("font_color", Color.FromHtml("#914E42"));
        remove.Pressed += () =>
        {
            if (!_session.RemoveGardenDecoration(capturedInstanceId)) return;
            _selectedDecoration = string.Empty;
            CallDeferred(nameof(ShowGardenDecorations));
        };
        detail.AddChild(remove);
    }

    /// <summary>The decoration's own sprite, at the proportions the catalogue authored for it.</summary>
    private static TextureRect DecorationArt(string typeId, Vector2 size)
    {
        GardenDecorationCatalog.TryGet(typeId, out var definition);
        var region = definition.AtlasRegion == default ? new Rect2(0, 0, 32, 48) : definition.AtlasRegion;
        // The catalogue's authored scale is relative to the garden's largest piece, so normalise
        // against it: a small tree has to read as smaller than a large one inside the same slot.
        var scale = definition.Scale <= 0.0f ? 1.0f : definition.Scale;
        var scaled = size * Mathf.Clamp(scale / 1.25f, 0.5f, 1.0f);
        return new TextureRect
        {
            Texture = new AtlasTexture { Atlas = DecorationTexture, Region = region },
            CustomMinimumSize = scaled,
            Size = scaled,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
    }

    private static string DecorationName(string typeId)
    {
        var key = "UI_DECORATION_" + typeId.ToUpperInvariant();
        var translated = TranslationServer.Translate(key);
        // A catalogue entry added without a key still shows its authored name rather than the key.
        return translated == key ? GardenDecorationCatalog.NameFor(typeId) : translated;
    }
}
