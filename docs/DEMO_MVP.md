# Voidling Demo MVP

## Purpose

This is the playable Godot 4.6 C# vertical slice for validating the creature-raising loop before expanding the simulation.

```text
Buy / breed egg
      ↓
See egg incubate in the garden
      ↓
Hatch + inspect Stats / DNA / appearance / lineage
      ↓
Train with shop treats
      ↓
Automated race against CPU Voidlings
      ↓
Breed stronger or more interesting bloodlines
```

## Garden and camera

`Scenes/Garden.tscn` contains real editable Godot `TileMapLayer` nodes backed by `Resources/Tiles/garden_tileset.tres` and the Sprout Lands 16×16 terrain atlas.

### Camera controls

- **LMB drag on empty ground:** pan around the garden.
- **Middle mouse drag:** alternate pan control.
- **Mouse wheel:** zoom in/out.
- **Center:** restore the default camera.
- **Eye button:** follow the currently selected Voidling with a centered camera.

Hold LMB on a Voidling to pick it up. Release to place it elsewhere. Pickup uses a grab cursor and lifted shadow; placement has a drop/bounce and dust effect.

## Voidlings

All MVP creatures use the Sprout Lands base character sheet and individual color tinting. Children render at half adult size. World sprites have close grounded ellipse shadows and a pulsing selection circle.

Click the selected Voidling's name in its inspection panel to rename it inline. Names persist in saves and genealogy.

Core stats are Run, Swim, Fly, Power and Stamina. There is no Intelligence or Luck.

The selected inspection panel contains training controls, Details, Family tree and Goodbye. Persistent garden HUD panels hide whenever a foreground menu opens so they cannot overlap menu content.

## Details

Details always opens as the only foreground menu and hides the garden inspection panel while open.

It has three pages:

- **Stats** — color-coded rank, level, current stat and level-progress bar.
- **DNA** — genotype only: DNA1 and DNA2 for each ability plus color DNA. Current/trained stat values are intentionally not shown here.
- **Visual** — current tint, appearance genes and Mutations.

Stat colors are Run green, Swim yellow, Fly purple, Power red and Stamina white.

The demo assigns one existing/new-save Voidling the **Angel** mutation, rendered as a halo in world and portraits.

## Family tree

Family tree is a clipped pannable overview with no scrollbars.

- LMB drag on empty space pans the tree.
- Middle mouse remains an alternate pan binding.
- Click a member for its compact stats/parents inspector.
- Selected family cards darken.
- Departed and historical inbreeding records remain visible.

## Shop, inventory and training

The shop sells one training treat for each stat and mystery eggs. Store-egg genomes are fixed when that individual egg enters shop stock. Purchased eggs are placed visibly in the garden.

Inventory shows item icons and owned counts. The selected Voidling's training buttons display `+1 (count)` and consume one item per click.

## Breeding and hatching

Breeding is player initiated. The menu shows both parent sprites above the selectors and previews relatedness/inbreeding consequences.

The garden breeding sequence has parents walk together with heart particles, perform a short dance, show a larger breeding heart, then spawn the egg with a bounce/pop animation.

Eggs visibly incubate, pulse more strongly/frequently as hatch approaches, burst at hatch, and the new child jumps out. There is no force hatch.

Inbreeding uses the 0/20/50/80/100% viability ladder and clean unrelated outcrossing reduces active burden one level per generation while keeping historical marks.

## Race

Race selection explicitly chooses one owned Voidling. All other racers are generated CPU opponents.

The course uses one shared side-view track and keeps the player centered at locked 1× camera zoom.

- **Run:** ground pace and hurdle avoidance.
- **Swim:** water-section speed using the dedicated `Assets/Sprout Sorry pack/Early Access/Ocean Pack/swimming.png` animation.
- **Fly:** finite glide endurance across a raised water crossing. All racers launch into visible 2D elevation; stronger Fly travels farther. If glide endurance expires before the opposite bank, the racer visibly falls into the water and uses Swim for the remainder.
- **Stamina:** continuous race energy and CHEER capacity.

CHEER spends Stamina, gives a two-second speed boost and renders trailing speed streak particles.

Auto Finish is ON by default. When the player finishes it deterministically fast-forwards remaining CPUs. If every CPU has already finished first, the race also ends immediately because the player's fourth-place result is known.

The finish screen uses centered 1st/2nd/3rd podium blocks and fourth place beside them in a puddle.

## Settings and persistence

Settings are available from the top bar or ESC. Master volume and Auto Finish persist with the save.

The demo automatically saves to `user://voidling_mvp_save.json`. It includes active/departed lineage, genetics, mutations, inventory, eggs, placements and settings.

## Running and validation

- `build.bat` restores/builds the C# project.
- `playgame.bat` launches the game directly without opening the editor.
- GitHub Actions validates package restore, C# compilation and Godot headless project/scene parsing.
