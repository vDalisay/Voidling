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
        var box = OpenModal(Tr("UI_GARDEN_BUILD_DECORATE"), new Vector2(520, 292), ShowGardenBuild, Voidling.Presentation.UI.Common.ScreenIcons.Decorate);
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

        var art = DecorationArt(slot.TypeId);
        art.Position = ((DecorationSlotSize - art.CustomMinimumSize) * 0.5f - new Vector2(0, 3)).Floor();
        // A kind you can still place reads lighter than one already standing in the garden.
        art.Modulate = new Color(1, 1, 1, slot.InstanceId == null ? 0.6f : 1.0f);
        button.AddChild(art);
        button.AddChild(DecorationSizeTag(slot.TypeId));

        var capturedKey = slot.Key;
        button.Pressed += () => { _selectedDecoration = capturedKey; CallDeferred(nameof(ShowGardenDecorations)); };
        return button;
    }

    private void BuildDecorationDetail(VBoxContainer detail, DecorationSlot slot)
    {
        var artPanel = new PanelContainer { CustomMinimumSize = new Vector2(0, 64) };
        artPanel.AddThemeStyleboxOverride("panel", Voidling.Presentation.UI.Common.UiSkin.Well());
        var center = new CenterContainer();
        center.AddChild(DecorationArt(slot.TypeId));
        artPanel.AddChild(center);
        detail.AddChild(artPanel);

        var name = UiFactory.CreateLabel(slot.Name, 9);
        name.Name = "DecorationName";
        name.HorizontalAlignment = HorizontalAlignment.Center;
        detail.AddChild(name);

        var size = UiFactory.CreateLabel(string.Format(Tr("UI_DECORATION_SIZE_LINE"), DecorationSizeName(slot.TypeId)), 7);
        size.Name = "DecorationSize";
        size.HorizontalAlignment = HorizontalAlignment.Center;
        detail.AddChild(size);

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
        UiFactory.ApplyDangerStyle(remove);
        remove.Pressed += () =>
        {
            if (!_session.RemoveGardenDecoration(capturedInstanceId)) return;
            _selectedDecoration = string.Empty;
            CallDeferred(nameof(ShowGardenDecorations));
        };
        detail.AddChild(remove);
    }

    /// <summary>
    /// The decoration's own sprite at the pack's 1:1 pixel size, so it is exactly as crisp as it
    /// is in the Garden; a small tree reads smaller than a large one because its sprite is.
    /// </summary>
    private static TextureRect DecorationArt(string typeId)
    {
        GardenDecorationCatalog.TryGet(typeId, out var definition);
        var region = definition.AtlasRegion == default ? GardenDecorationCatalog.RoundTreeRegion : definition.AtlasRegion;
        return new TextureRect
        {
            Texture = new AtlasTexture { Atlas = DecorationTexture, Region = region },
            CustomMinimumSize = region.Size,
            Size = region.Size,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
    }

    /// <summary>
    /// A small paper tag in the slot's lower corner naming the decoration's size, since the art is
    /// drawn at its true pixel size and a small tree looks the same as a large one here.
    /// </summary>
    private Control DecorationSizeTag(string typeId)
    {
        var tag = new PanelContainer { Name = "SizeTag", MouseFilter = Control.MouseFilterEnum.Ignore };
        var style = new StyleBoxFlat
        {
            BgColor = UiPalette.Parchment, AntiAliasing = false,
            BorderColor = UiPalette.Tan, ContentMarginLeft = 3, ContentMarginRight = 3,
            ContentMarginTop = 0, ContentMarginBottom = 1
        };
        style.SetBorderWidthAll(1);
        tag.AddThemeStyleboxOverride("panel", style);
        var label = UiFactory.CreateLabel(DecorationSizeName(typeId), 6);
        label.MouseFilter = Control.MouseFilterEnum.Ignore;
        tag.AddChild(label);
        // Pinned to the slot's lower-right corner on whole pixels, above the art.
        tag.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
        tag.GrowHorizontal = Control.GrowDirection.Begin;
        tag.GrowVertical = Control.GrowDirection.Begin;
        tag.OffsetRight = -4;
        tag.OffsetBottom = -5;
        return tag;
    }

    private string DecorationSizeName(string typeId)
    {
        var scale = GardenDecorationCatalog.TryGet(typeId, out var definition) ? definition.Scale : 1.0f;
        return Tr(GardenDecorationSizing.For(scale) switch
        {
            GardenDecorationSize.Small => "UI_DECORATION_SIZE_SMALL",
            GardenDecorationSize.Large => "UI_DECORATION_SIZE_LARGE",
            _ => "UI_DECORATION_SIZE_MEDIUM"
        });
    }

    private static string DecorationName(string typeId)
    {
        var key = "UI_DECORATION_" + typeId.ToUpperInvariant();
        var translated = TranslationServer.Translate(key);
        // A catalogue entry added without a key still shows its authored name rather than the key.
        return translated == key ? GardenDecorationCatalog.NameFor(typeId) : translated;
    }
}
