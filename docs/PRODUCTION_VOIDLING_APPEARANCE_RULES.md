# Production Voidling appearance rules

**Status:** Confirmed supplement to `GAMEPLAY_DESIGN_REFINEMENT_CONTEXT.md` for production sprite/genetics work.

This document records appearance decisions confirmed after the original appearance interview section was marked incomplete. Where this document is specific, it supersedes older statements that ordinary color inheritance, base visual families, or sprite composition are wholly unresolved. Questions not addressed here remain unresolved.

## 1. Semantic base Voidling types

Production Voidlings can have different semantic base body families such as:

- `normal`;
- `water`;
- `power`;
- future confirmed types.

Each type has a developer-authored base sprite definition. Persistent/network state stores only the stable semantic type ID. It must never store a texture path, Godot Resource path, atlas coordinate, or display name as the art identity.

The exact gameplay/evolution rule that changes or inherits a Voidling type is **not yet defined here**. New children currently default to `normal` until that product rule is confirmed.

## 2. Color DNA and palette inheritance

Each production sprite is authored with a known base color palette. Color DNA changes that palette programmatically rather than requiring a separate spritesheet for every color.

Confirmed inheritance behavior:

1. the two selected parents remain the only ordinary color-DNA source;
2. the child receives one color-DNA profile from each parent;
3. one of those two child profiles wins expression at 50/50 odds;
4. the winning color range moves only a **small amount** toward the other inherited range rather than averaging both equally;
5. movement follows the shortest direction around the color wheel;
6. the amount of movement is designer-tunable and should remain modest.

Example:

- blue-family DNA + red-family DNA;
- if blue wins, the resulting monochromatic palette shifts somewhat toward blue-purple/darker purple territory;
- if red wins, it shifts somewhat toward red-pink/magenta territory.

The renderer must preserve the authored relative shade/value structure of the source palette. A dark source slot remains the dark member of the new family; a highlight remains a highlight. Flat whole-sprite tinting is only a legacy fallback.

The player should be able to inspect meaningful color DNA, but the UI must not become a full offspring color probability calculator.

## 3. Palette-authoring contract

For each base/layer sprite supplied by the developers/artists:

- use lossless pixel-art source images;
- provide the canonical source palette colors that are intended to be recolored;
- keep non-palette pixels (eyes, outlines, fixed markings, effects where applicable) outside those recolor slots;
- use exact canonical colors for recolorable slots rather than anti-aliased near-duplicates where possible;
- keep nearest-neighbor import/filtering for pixel art.

The first implementation supports up to eight explicit recolor slots per body/layer definition. If production art later needs independent recolor regions, patterns, or more complex gradients, prefer an authored palette-index/mask texture or lookup texture rather than proliferating hardcoded RGB rules.

## 4. Sprite composition and layers

Voidlings are composited from a base body plus optional synchronized sprite layers.

Examples:

```text
base body
+ wings
+ crystal
+ mutation/special adornment
= final Voidling visual
```

Layers are developer-authored variants grouped into semantic **slots**. Example slots:

- `wings`;
- `crystal`;
- future confirmed adornment/body-part slots.

A type definition can declare default variants. For example, a Flying-type definition may require a default wing variant and a default crystal variant. A saved phenotype may select another developer-approved variant for a slot when product rules allow it.

Rules:

- layers use stable semantic IDs, never paths;
- layer variants must be explicitly registered/approved by developers;
- one selected variant replaces another in the same slot rather than stacking accidental duplicates;
- base and overlay animation sheets should use the same canvas, frame dimensions, frame count/state layout and origin whenever practical;
- all layers are driven by the base animation/frame so they cannot drift out of sync;
- each layer may have a small authored offset/scale/Z-order when needed;
- a layer may share the body palette transform, define its own recolor source slots, or opt out of palette recoloring;
- mutation effects remain compositional and do not create specialized actor subclasses.

## 5. One appearance recipe across every context

The same semantic appearance recipe must resolve in:

- local Garden;
- connected/remote Garden;
- single-player race;
- multiplayer race;
- portraits/cards;
- breeding UI;
- details/profile;
- family tree;
- trade UI;
- podium/results;
- future creature surfaces.

A production art change belongs in the visual catalog/definition and source assets, not in individual consumers.

## 6. Persistence and networking

Persistent/network-safe appearance state may contain semantic values such as:

- visual type ID;
- color-DNA hue/profile values;
- resolved palette hue/phenotype;
- selected layer IDs;
- mutation IDs.

It must not contain presentation implementation details such as textures, source palette RGB slots, materials/shaders, atlas rows, pivots, or Godot Resource paths.

Existing saves must migrate deterministically. Migration may derive semantic color values from old color DNA/tints but must not consume RNG or reroll an existing creature.

## 7. Still unresolved

This supplement does **not** decide:

- the exact normal → Water/Power/Fly type development/evolution formula;
- whether/how base type itself is inherited at breeding time;
- the final list of body types;
- exact allowed wing/crystal variants per type;
- patterns or independently colored pattern regions;
- shiny/special-coat rules;
- rare appearance mutation rates;
- trophy-form appearance recipe;
- final color-blend influence tuning.

Those should be added as data/policies on top of this architecture when confirmed, not guessed by implementation agents.
