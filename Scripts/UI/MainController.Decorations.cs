using System;
using System.Linq;
using Godot;
using Voidling.Presentation.Garden;

namespace VoidlingGame;

public partial class MainController
{
    private void ShowGardenDecorations()
    {
        var box = OpenModal("DECORATE GARDEN", new Vector2(510, 350));
        box.AddThemeConstantOverride("separation", 6);

        var hint = UiFactory.CreateLabel(
            "Decorations are cosmetic. Place them anywhere on the garden floor; they never change stats or race results.",
            6);
        hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        hint.CustomMinimumSize = new Vector2(486, 30);
        box.AddChild(hint);

        box.AddChild(UiFactory.CreateLabel("ADD DECORATION", 8));
        foreach (var definition in GardenDecorationCatalog.All)
        {
            var row = UiFactory.CreatePanel(new Vector2(486, 34));
            row.CustomMinimumSize = new Vector2(486, 34);
            box.AddChild(row);
            var controls = new HBoxContainer();
            controls.AddThemeConstantOverride("separation", 7);
            row.AddChild(controls);

            var name = UiFactory.CreateLabel(definition.DisplayName, 7);
            name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            name.VerticalAlignment = VerticalAlignment.Center;
            controls.AddChild(name);

            var capturedTypeId = definition.TypeId;
            var place = UiFactory.CreateButton("Place");
            place.CustomMinimumSize = new Vector2(72, 22);
            UiFactory.ApplyPixelFont(place, 6);
            place.Pressed += () =>
            {
                CloseModal();
                _garden.BeginDecorationPlacement(capturedTypeId);
            };
            controls.AddChild(place);
        }

        box.AddChild(UiFactory.CreateLabel("PLACED", 8));
        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(486, 120),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        UiFactory.StyleScroll(scroll);
        box.AddChild(scroll);
        var list = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(474, 1),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        list.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(list);

        var placed = _session.State.GardenDecorations
            .Where(data => data != null && GardenDecorationCatalog.TryGet(data.TypeId, out _))
            .OrderBy(data => data.TypeId, StringComparer.Ordinal)
            .ThenBy(data => data.Id, StringComparer.Ordinal)
            .ToList();

        if (placed.Count == 0)
        {
            list.AddChild(UiFactory.CreateLabel("No player decorations placed yet.", 6));
            return;
        }

        foreach (var decoration in placed)
        {
            var row = UiFactory.CreatePanel(new Vector2(468, 34));
            row.CustomMinimumSize = new Vector2(468, 34);
            list.AddChild(row);
            var controls = new HBoxContainer();
            controls.AddThemeConstantOverride("separation", 6);
            row.AddChild(controls);

            var name = UiFactory.CreateLabel(
                $"{GardenDecorationCatalog.NameFor(decoration.TypeId)}  ({decoration.X:0}, {decoration.Y:0})",
                6);
            name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            name.VerticalAlignment = VerticalAlignment.Center;
            controls.AddChild(name);

            var capturedId = decoration.Id;
            var capturedTypeId = decoration.TypeId;
            var move = UiFactory.CreateButton("Move");
            move.CustomMinimumSize = new Vector2(62, 22);
            UiFactory.ApplyPixelFont(move, 6);
            move.Pressed += () =>
            {
                CloseModal();
                _garden.BeginDecorationPlacement(capturedTypeId, capturedId);
            };
            controls.AddChild(move);

            var remove = UiFactory.CreateButton("Remove");
            remove.CustomMinimumSize = new Vector2(72, 22);
            UiFactory.ApplyPixelFont(remove, 6);
            remove.Pressed += () =>
            {
                if (_session.RemoveGardenDecoration(capturedId))
                    CallDeferred(nameof(ShowGardenDecorations));
            };
            controls.AddChild(remove);
        }
    }
}
