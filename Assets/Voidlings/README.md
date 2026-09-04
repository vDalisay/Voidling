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
7. enter the exact source colors that are intended to be color-DNA-recolored in `SourcePaletteColors` in dark-to-light order;
8. run CI.

Pixels not represented by source palette slots remain unchanged.

## Palette and outline requirements

Color DNA uses palette-slot replacement rather than whole-sprite tinting. The production renderer does **not** generate, expand, or redraw a Voidling outline.

The source sheet is the pixel authority. The dark cyan outline/shadow pixels supplied by the artist are palette slots of the same body color family as the lighter cyan interior pixels, so changing Color DNA shifts their hue together while preserving their authored dark-to-light value relationship. Gold/tan markings and eye pixels are not body palette slots and remain unchanged.

- Supply lossless pixel-art PNGs.
- Keep recolorable source slots as exact canonical colors across the sheet.
- Avoid anti-aliased near-duplicate shades for palette slots.
- Keep nearest-neighbor filtering/import for pixel art.
- Use at most eight explicit source slots per definition/layer in the first production implementation.

If later art needs separate independently colored regions or more complicated patterns, add a palette-index/mask/LUT workflow rather than inventing an outline or hardcoding RGB exceptions.

## Layered sprites

Wings, crowns and similar combinatorial parts are separate layer sprites registered on the owning body definition.

Every runtime overlay sheet uses the **same canvas size, frame size, animation rows and timing/layout as its base body definition**. Transparent pixels fill unused space. The layer atlas is the registration contract: Garden, races and every portrait surface consume those same frame coordinates instead of applying page-specific positioning.

For animated bodies, attached art follows the body's authored per-frame pixel bob inside the atlas. Secondary motion such as the new wing float is separate runtime presentation metadata, not hand-authored page offsets.

Each `VoidlingVisualLayerDefinition` has:

- `LayerId` — stable render-layer variant ID;
- `SlotId` — replacement group;
- base/swim atlas references;
- relative Z-order, small offset and scale adjustments;
- palette participation and optional layer-specific source palette slots;
- optional `MotionGroupId`, `VerticalFollowLagSeconds` and `MaxVerticalLagAtScaleOne`.

Layers sharing one `MotionGroupId` receive exactly the same secondary follow offset. This is how the front and back wing halves remain one rigid wing unit while both trail vertical body movement by a very small amount.

Only developer-approved variants should be registered. Gameplay/breeding code selects semantic layer IDs; it does not know image paths.

## Current production `normal` art

The production `normal` definition uses the supplied six-frame two-tone Voidling walk cycle as the body source.

Artist sources:

- `Base/Normal/neutral_two_tone_voidling_source.png` — the exact supplied 192x32 six-frame body sheet.
- `Layers/Wings/golden_front_wing_source.png` — the supplied 32x64 front-wing alignment canvas.
- `Layers/Wings/golden_back_wing_source.png` — the supplied 32x64 back-wing alignment canvas.
- `Layers/Crown/golden_crown_source.png` — the supplied crown alignment canvas retained from the previous revision.

Runtime atlases normalize these sources to the canonical 32x48 frame grid without resampling the artist pixels. The body frames are padded transparently; their outline and interior pixels are copied exactly. The wing and crown alignment canvases are normalized from the same source coordinate system. Their per-frame vertical offsets follow the body's authored bob (`0, 0, +1, +2, +1, +1`).

The final Z order, front to back, is:

```text
crown
front wing
body
back wing
```

The front and back wing layers both belong to motion group `wings`. The group can trail the body vertically by at most two source pixels and eases back with a short follow delay. When the body rises, the wing pair remains fractionally lower; when the body falls, it remains fractionally higher and settles onto the body. The two wing halves never drift relative to each other.

The supplied body animation is authored facing right. `walk_right` therefore uses the pixels as authored and `walk_left` mirrors the complete assembled Voidling. `walk_up` and `walk_down` retain the most recent horizontal facing. This facing rule lives in the canonical layer/sprite synchronization path, preventing individual Garden/network consumers from accidentally making the Voidling walk backwards.

## One appearance recipe everywhere

`VoidlingVisualFactory` resolves the semantic body, palette and layer list once. Garden actors, remote Garden actors and race sprites use that recipe through `ApplyAppearance`. All UI creature representations use `VoidlingPortraitComposer`, which resolves the same definition and layer list.

Portrait composition keeps the body and overlays as sibling render items and preserves the same relative Z-order as world sprites. A UI panel therefore cannot swallow a behind-body layer. Inspector, family tree, breeding, trade, race picker/results and other cards must not configure creature assets independently.

The visual smoke test checks resolved layer count, relative Z-order and left/right flip propagation for world/race/portrait composition. A layer disappearing from one context while remaining in another is a CI failure.

## Replacing art later

For a same-layout art revision, replace the PNG or update the relevant definition resource. For a changed layout, edit that one definition's frame/profile metadata. Do not edit Garden, race, trade, family-tree, breeding, details or card code simply to change creature art.

## CI

`VoidlingVisualFactory` validates every catalog definition and registered layer. The dedicated visual-pipeline workflow imports resources, executes the headless visual smoke for every cataloged body family and rejects direct `Assets/Voidlings/` loads from consumer C#.
