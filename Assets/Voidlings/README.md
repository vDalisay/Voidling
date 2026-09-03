# Voidling production art

`Resources/Presentation/Voidlings/DefaultVoidlingVisualCatalog.tres` is the authoritative catalog for production Voidling presentation. Each semantic body family (for example `normal`, `water`, `power`) resolves to one `VoidlingVisualDefinition` resource.

## Recommended source layout

```text
Assets/Voidlings/
├─ Base/
│  ├─ Normal/
│  ├─ Water/
│  └─ Power/
└─ Layers/
   ├─ Wings/
   └─ Crown/
```

Exact folder names are flexible; semantic IDs in the catalog are the stable identity. Do not use display names as art keys.

## Base body import

For each production body family:

1. add the source PNG(s) under `Assets/Voidlings/`;
2. create/update one `VoidlingVisualDefinition` and register it in `DefaultVoidlingVisualCatalog.tres`;
3. set its stable `DefinitionId` (`normal`, `water`, `power`, etc.);
4. set `BaseAtlas` and `SwimAtlas` only when a separate swim sheet is genuinely needed;
5. author frame dimensions/rows/counts and portrait coordinates;
6. author silhouette-dependent geometry: adult/child/race scale, feet offset, hitboxes, held offset, shadow and mutation anchors;
7. enter the exact source colors that are intended to be color-DNA-recolored in `SourcePaletteColors` in dark-to-light (or otherwise consistent) order;
8. run CI.

Pixels not represented by source palette slots remain unchanged. This is useful for fixed outlines, eyes and markings.

## Palette requirements

Color DNA uses palette-slot replacement rather than whole-sprite tinting for production definitions.

- Supply lossless pixel-art PNGs.
- Keep recolorable source slots as exact canonical colors across the sheet.
- Avoid anti-aliased near-duplicate shades for palette slots.
- Keep nearest-neighbor filtering/import for pixel art.
- Use at most eight explicit source slots per definition/layer in the first production implementation.

The runtime rotates the authored monochromatic palette to the inherited target hue while preserving each slot's saturation/value relationship, so shadows remain shadows and highlights remain highlights.

If later art needs separate independently colored regions or more complicated patterns, add a palette-index/mask/LUT workflow rather than hardcoding many RGB exceptions.

## Layered sprites

Wings, crowns and similar combinatorial parts are separate layer sprites registered on the owning body definition.

For the cleanest pipeline, every overlay sheet should use the **same canvas size, frame size, animation rows and frame timing/layout as its base body definition**. Transparent pixels fill the unused area. That lets the renderer mirror the base animation/frame exactly.

Each `VoidlingVisualLayerDefinition` has:

- `LayerId` — stable variant ID such as `wings_golden`;
- `SlotId` — replacement group such as `wings` or `crown`;
- base/swim atlas references;
- Z-order, small offset and scale adjustments;
- palette participation and optional layer-specific source palette slots.

A body definition can set default layers. A phenotype can select another registered layer in the same slot; it replaces the default rather than stacking two wing sets accidentally.

Only developer-approved variants should be registered. Gameplay/breeding code later selects semantic layer IDs; it does not know image paths.

## Current production `normal` art

The production `normal` definition now uses the neutral two-tone 6-frame Voidling sheet plus the golden wings and crown supplied with it.

The artist files are retained alongside canonical runtime atlases:

- `Base/Normal/neutral_two_tone_voidling_source.png` is the original 192x32 six-frame body sheet.
- `Layers/Wings/golden_wings_source.png` and `Layers/Crown/golden_crown_source.png` are the original 32x64 alignment canvases.
- The matching runtime PNGs normalize those sources to a shared 32x48 frame grid. The body is placed at y=14 in that frame; the source accessory canvases are cropped by the same 16 transparent top rows and repeated across all six animation frames.

That normalization preserves the exact body/wing/crown registration shown in the completed-art reference while satisfying the pipeline invariant that every composited layer has the same frame grid. Wings render behind the body and the crown renders in front. The cyan body palette is DNA-recolorable; the gold/tan markings and accessory pixels remain authored colors.

The supplied animation has one semantic direction, so `walk_down`, `walk_up`, `walk_left`, `walk_right`, `run` and `swim` currently resolve to the same six-frame row. Dedicated directional/swim art can be introduced later by changing only the canonical definition/source atlases.

## Replacing art later

For a same-layout art revision, replace the PNG or update the relevant definition resource. For a changed layout, edit that one definition's frame/profile metadata. Do not edit Garden, race, trade, family-tree, breeding, details or card code simply to change creature art.

## CI

`VoidlingVisualFactory` validates every catalog definition and registered layer. The dedicated visual-pipeline workflow imports resources, executes the headless visual smoke for every cataloged body family and rejects direct `Assets/Voidlings/` loads from consumer C#.
