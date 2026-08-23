# Voidling Demo MVP

## Purpose

This is a deliberately small playable vertical slice for validating the creature-raising loop before expanding the garden simulation.

```text
Buy / breed egg
      ↓
See egg incubate in the garden
      ↓
Hatch + inspect DNA / appearance / lineage
      ↓
Train with shop treats
      ↓
Automated race against CPU Voidlings
      ↓
Breed stronger or more interesting bloodlines
```

## Garden and camera

`Scenes/Garden.tscn` contains real Godot `TileMapLayer` nodes:

- `WaterLayer`
- `IslandLayer`

Both use `Resources/Tiles/garden_tileset.tres` and the original Sprout Lands 16×16 atlas. The island is serialized tile data, not a flattened picture or a runtime-only generated map, so it remains editable in the Godot editor.

The grass TileSet is configured as a terrain-aware atlas using distinct corner, edge and interior tiles. This follows the same terrain layout used by the public Sprout Lands TileMap Godot project rather than treating one corner sprite as a generic ground tile.

### Camera controls

- **Middle mouse drag:** pan around the garden.
- **Mouse wheel:** zoom in/out.
- **Center:** restore the default camera position and zoom.

Open `Scenes/Garden.tscn`, select `IslandLayer`, then use Godot's TileMap/terrain tools to repaint the island. `WaterLayer` is available as a separate editable layer.

The old decorative berries/plants/material props were removed from the starter island. The initial scenery now stays limited to terrain, trees and rocks.

## Voidlings

All MVP creatures use:

`Assets/Sprout Lands - Sprites - Basic pack/Characters/Basic Charakter Spritesheet.png`

A color tint differentiates individuals. They wander independently inside the garden. Click one to select it; use the **X** button in its profile to deselect it.

Core stats are:

- Run
- Swim
- Fly
- Power
- Stamina

There is no Intelligence or Luck stat.

### DNA profile

The selected Voidling has a **DNA** button. It exposes, per stat:

- allele A;
- allele B;
- currently expressed grade;
- current trained/effective value.

It also shows color genes, generation, active inbreeding burden and rare-trait provenance/transmission state.

### Visual profile

The **Visual** button shows a larger portrait, current tint, expressed/base color genes and shiny-level appearance traits.

### Family tree

The **Family tree** button sits next to the parents information. It opens a scrollable genealogy view containing the whole connected family currently present in the save. Each node uses the Voidling's tinted sprite and name. Parent/child connections are drawn between generations, the selected Voidling is highlighted, and historical inbreeding marks stay visible even after the active burden is cleansed.

## Shop

The UI uses the Sprout Lands UI pack and pixel font. The shop sells:

- one training treat for each stat;
- three mystery eggs.

Each shop egg receives its complete genome when that exact egg enters shop inventory. Buying, saving, loading or hatching does not reroll it. The shop can hide those predetermined values from the player.

Purchased eggs are placed visibly into the garden and incubate there.

## Training

Buy a stat treat, select a Voidling, then press the small `+` button beside that stat. A treat adds a small randomized amount of training points. Training improves current performance but does not rewrite inherited DNA.

## Breeding and hatching

Press **Breed** and choose two adult Voidlings. The relationship preview shows whether the pairing is related and what inbreeding burden the egg would inherit.

When breeding succeeds:

1. both parent sprites pause their normal wandering;
2. they walk toward one another;
3. a small pixel heart appears;
4. the bred egg is created and placed visibly between them;
5. both parents resume wandering.

Each stat independently inherits one allele from each parent. The child genome and viability are resolved when the egg is created and are never rerolled at hatch time.

Eggs hatch naturally after the MVP incubation timer. There is no force-hatch action. A newly hatched child appears where its egg was located.

### Inbreeding

The demo checks ancestry through a configurable depth. Related pairings escalate the child's burden:

| Burden | Hatch failure |
|---:|---:|
| 0 | 0% |
| 1 | 20% |
| 2 | 50% |
| 3 | 80% |
| 4 | 100% |

Failure is rolled once when the egg is created and stored in the save. Reloading cannot reroll it. Breeding a burdened Voidling with an unrelated burden-0 Voidling reduces the active burden by one level per clean generation; historical inbreeding remains visible in the family tree.

## Rare appearance traits

Store/starter generation has an extremely small chance to found a rare appearance trait. Transmission depth is stored per trait:

- founder (`G0`) can transmit;
- founder child (`G1`) can transmit;
- second-generation carrier (`G2`) can display the trait but is terminal and cannot transmit it further.

The MVP represents these visually with a small sparkle orbit and exposes their inheritance state in DNA/visual profiles.

## Automated race

Select one Voidling in the garden, then press **Race**.

Exactly **one owned Voidling** enters the minigame: the currently selected one. All three opponents are temporary CPU-generated Voidlings. Other owned Voidlings are never inserted into the race automatically.

The side-view race runs left-to-right with obstacles:

- higher **Run** increases movement speed;
- higher **Run** increases obstacle-avoidance chance;
- failed avoidance causes a stumble delay;
- successful avoidance shows a small jump;
- **Stamina** reduces late-race fatigue.

Race placement awards sprouts.

## Persistence

The demo automatically saves to:

`user://voidling_mvp_save.json`

Older MVP saves are migrated with garden positions for existing Voidlings and eggs. Use **Reset** only when you intentionally want to wipe the local MVP save and restore the starter state.
