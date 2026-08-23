# Voidling Demo MVP

This branch is the playable Godot 4.6 C# vertical slice for validating the initial raising/breeding/racing loop.

## Core loop

`Buy / breed egg → hatch → inspect → train → race → breed the next generation`

## Garden

The island remains an editor-editable Sprout Lands `TileMapLayer` setup using `Resources/Tiles/garden_tileset.tres`.

Controls and interactions:

- LMB drag on **empty ground** pans the garden.
- Middle mouse drag remains an alternate pan control.
- Mouse wheel zooms and **Center** restores the default camera.
- The eye button follows the selected Voidling.
- Click the selected Voidling's **name** to rename it inline; names persist to saves and genealogy.
- Hold LMB on a Voidling to pick it up and release to place it, with grab cursor, lifted shadow, drop bounce and dust.
- Voidlings have close grounded ellipse shadows, a pulsing selection ring, tint variation and half-size child sprites.

Persistent garden HUD panels hide while foreground menus are open, preventing the lower-left egg/status box or inspection panel from overlapping menu content.

## Details

Opening **Details** replaces any other open modal and hides the selected inspection panel until Details closes.

Details has three tabs:

- **Stats** — color-coded rank, level, current stat and progress bar.
- **DNA** — genotype only: DNA1 and DNA2 for each ability plus color DNA. Current/trained values do not appear here.
- **Visual** — tint/appearance information and Mutations.

Stat colors are Run green, Swim yellow, Fly purple, Power red and Stamina white. One existing/new-save Voidling receives the demo **Angel** mutation, displayed with a halo.

## Family tree

Family tree is a clipped overview with no scrollbars. LMB drag on empty tree space pans it; middle mouse remains an alternate binding. Cards show sprites/names/generation/parents, selected cards darken, and departed or historical inbreeding records remain visible.

## Shop / inventory / training

The shop sells stat treats and mystery eggs. Store eggs are rolled when that specific egg enters stock. Inventory lists icons/counts, and the selected Voidling's training buttons show `+1 (count)` while consuming one owned item per use.

## Breeding / hatching

Breeding is player-selected and shows parent portraits. Parents approach with heart particles, dance, show a breeding heart and spawn a bouncing egg. Eggs pulse toward hatch, burst, and spawn a jumping child.

Inbreeding viability uses the 0/20/50/80/100% ladder. Clean unrelated outcrossing reduces active burden one level per generation while preserving family-tree history.

## Race

Exactly one owned Voidling is selected; every opponent is a generated CPU. The side-view camera remains centered on the player at locked 1× zoom.

- **Run:** ground speed and hurdle avoidance.
- **Swim:** water speed using the dedicated Ocean Pack `Assets/Sprout Sorry pack/Early Access/Ocean Pack/swimming.png` animation.
- **Fly:** finite glide endurance across the raised water crossing. Racers launch into visible vertical elevation; stronger Fly sustains the glide farther. If endurance expires, the racer visibly falls into the water and uses Swim for the remainder.
- **Stamina:** continuous race energy and CHEER capacity.

CHEER spends Stamina, gives a two-second speed boost and emits trailing speed streak particles.

Auto Finish defaults ON. It fast-forwards CPUs when the player finishes; if all CPUs finish first, the race also ends immediately because fourth place is already determined.

The result screen uses aligned 1st/2nd/3rd podium blocks with fourth place beside them in a puddle.

## Settings / persistence / running

Settings are available from the top bar or ESC. Master volume and Auto Finish persist. Save data includes active/departed lineage, genomes, mutations, inventory, eggs, placements and settings.

- `build.bat` builds the C# project.
- `playgame.bat` launches directly without opening the Godot editor.
- GitHub Actions validates restore, C# compilation and headless Godot project/scene parsing.
