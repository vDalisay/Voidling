# Voidling Demo MVP

## Purpose

This is a deliberately small playable vertical slice for validating the core loop before expanding the garden simulation.

```text
Buy / breed egg
      ↓
Incubate + hatch
      ↓
Raise / feed training treats
      ↓
Automated race
      ↓
Breed stronger or more interesting bloodlines
```

## Garden

`Scenes/Garden.tscn` contains two real `TileMapLayer` nodes:

- `WaterLayer`
- `IslandLayer`

Both use `Resources/Tiles/garden_tileset.tres`, which exposes the Sprout Lands `Grass.png` and `Water.png` atlases as 16×16 tiles.

The starter island is serialized into the scene as tile data. It is **not** a flattened background and is intentionally editable with the Godot TileMap tools. Open `Garden.tscn`, select either tile layer, and paint/erase tiles normally. Decorations are also ordinary Sprite2D nodes, so their positions and atlas regions can be edited in the inspector.

## Voidlings

All MVP creatures use:

`Assets/Sprout Lands - Sprites - Basic pack/Characters/Basic Charakter Spritesheet.png`

A color tint differentiates individuals. They wander independently inside the central garden area. Click one to select it.

The detail panel shows each visible stat grade, effective stat and training-item count. Hover a stat to see the two DNA grades and current training points.

Core stats are:

- Run
- Swim
- Fly
- Power
- Stamina

There is no Intelligence or Luck stat.

## Shop

The shop uses Sprout Lands UI textures and sells:

- one training treat for each stat;
- three mystery eggs.

### Store egg invariant

Each shop egg receives its complete genome **when it is created as store inventory**. Buying, saving, loading or hatching does not reroll it. After purchase, a new replacement egg is generated and locked immediately.

The MVP hides the egg's stat grades while it is in the shop but shows its color swatch.

## Training

Buy a stat treat, select a Voidling, then press the small `+` button next to that stat.

A treat adds a small randomized amount of training points. Training improves current performance but does not rewrite the inherited alleles.

## Breeding and hatching

Press **Breed**, choose two adult Voidlings, review the relationship warning, then create the egg.

Each stat independently inherits:

- one allele from parent A;
- one allele from parent B.

The child genome is resolved when the egg is created and never rerolled at hatch time.

Eggs naturally incubate and hatch after the MVP incubation timer. There is no force-hatch action.

Children become breedable adults after the short MVP growth timer.

### Inbreeding

The demo checks ancestry through a configurable depth. A related pairing escalates the child's active burden:

| Burden | Hatch failure |
|---:|---:|
| 0 | 0% |
| 1 | 20% |
| 2 | 50% |
| 3 | 80% |
| 4 | 100% |

Failure is rolled once when the egg is created, stored, and only revealed when the egg reaches hatch time. Reloading cannot reroll it.

Breeding a burdened Voidling with an unrelated burden-0 Voidling reduces the active burden by one level. Historical inbreeding remains marked on descendants.

## Rare appearance traits

Store/starter generation has an extremely small chance to found a rare appearance trait.

Transmission depth is stored per trait:

- founder (`G0`) can transmit;
- founder child (`G1`) can transmit;
- second-generation carrier (`G2`) can display the trait but is terminal and cannot transmit it further.

The MVP represents rare traits with a small sparkle orbit around the creature while retaining the normal color tint.

## Automated race

Press **Race** and choose a Voidling.

The MVP race is a side-view, four-lane, left-to-right automated test with three obstacles per lane.

- higher **Run** directly increases movement speed;
- higher **Run** increases obstacle-avoidance chance;
- failed avoidance causes a stumble delay;
- successful avoidance shows a small jump;
- **Stamina** reduces late-race fatigue.

The selected Voidling races owned Voidlings first, then temporary CPU entrants fill remaining lanes. Race placement awards sprouts.

## Persistence

The demo automatically saves to:

`user://voidling_mvp_save.json`

Use **Reset** in the top bar to restore the two starter adults, initial items, coins and shop eggs.
