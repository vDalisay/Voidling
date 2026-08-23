# Voidling Demo MVP

This branch is the playable Godot 4.6 C# vertical slice for validating the initial raising/breeding/racing loop.

## Core loop

```text
Buy / breed egg → hatch → inspect → train → race → breed the next generation
```

## Garden

- Editable Sprout Lands `TileMapLayer` island using `Resources/Tiles/garden_tileset.tres`.
- LMB drag on empty ground pans the camera; middle mouse is an alternate pan binding.
- Mouse wheel zoom, Center reset and eye-button follow mode.
- Click a selected Voidling's name to rename it inline; the name persists into saves and genealogy.
- Hold LMB on a Voidling to pick it up and release to place it, with grab cursor, lifted sprite/shadow, drop bounce and dust.
- Voidlings have close grounded ellipse shadows, a centered pulsing selection ring, tint variation and half-size child sprites.
- Persistent garden HUD panels hide whenever a foreground menu opens, preventing the lower-left egg/status box or inspection panel from overlapping menus.

## Details

Opening Details closes/replaces any other menu context and hides the selected inspection panel until Details is dismissed.

Details has three tabs:

- **Stats** — Chao-inspired color-coded rank, level, current stat and progress bar.
- **DNA** — genotype only: DNA1 and DNA2 for each ability plus color DNA. Trained/current stat values are intentionally absent.
- **Visual** — tint/appearance information and Mutations.

Stat colors are Run green, Swim yellow, Fly purple, Power red and Stamina white.

One existing/new-save Voidling receives the demo **Angel** mutation, displayed with a halo.

## Family tree

- Pannable clipped overview with no scrollbars.
- LMB drag on empty space pans; middle mouse remains an alternate binding.
- Family cards show sprites, names, generation and parent links.
- Clicking a member opens its compact stats/parents inspector.
- Selected cards darken.
- Departed and historical inbreeding records remain visible.

## Shop / inventory / training

- Shop sells stat treats and mystery eggs.
- Store eggs are rolled when that exact egg enters stock.
- Inventory lists item icons and counts.
- Training buttons show `+1 (count)` and consume one owned item.

## Breeding / hatching

- Player-selected parents only, with parent portraits in the breeding menu.
- Parents approach with heart particles, dance, show a breeding heart, and spawn a bouncing egg.
- Eggs exist on the island, pulse increasingly toward hatch, burst, and spawn a jumping child.
- Inbreeding viability uses the 0/20/50/80/100% ladder; clean unrelated outcrossing reduces active burden one level per generation while preserving family-tree history.

## Race

Exactly one owned Voidling is selected for the race; all opponents are generated CPUs. The side-view camera remains centered on the player at locked 1× zoom.

- **Run:** ground speed and hurdle avoidance.
- **Swim:** water speed using the dedicated Ocean Pack `Assets/Sprout Sorry pack/Early Access/Ocean Pack/swimming.png` animation.
- **Fly:** finite glide endurance across a raised water crossing. Racers launch into visible vertical elevation; stronger Fly sustains the glide farther. If endurance runs out, the racer visibly drops into the water and switches to Swim for the remaining crossing.
- **Stamina:** ongoing race energy and CHEER capacity.

CHEER spends Stamina, gives a two-second speed boost and emits trailing speed streak particles.

Auto Finish defaults ON. It fast-forwards CPUs once the player finishes; if all CPUs have already finished while the player is still running, the race ends immediately because fourth place is already determined.

The results screen uses aligned 1st/2nd/3rd podium blocks and fourth place beside them in a puddle.

## Settings / persistence

- Settings are available from the top bar or ESC.
- Master volume and Auto Finish persist.
- Save data includes active/departed lineage, genomes, mutations, inventory, eggs, placements and settings.

## Running / validation

- `build.bat` builds the C# project.
- `playgame.bat` launches directly without opening the editor.
- GitHub Actions validates restore, C# compilation, and headless Godot project/scene parsing.
