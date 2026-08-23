# Voidling Demo MVP

Playable Godot 4.6 C# vertical slice for the initial raising, breeding and racing loop.

## Garden

- Editor-editable Sprout Lands TileMap.
- LMB drag on empty ground pans; middle mouse remains an alternate pan binding.
- Mouse wheel zoom, Center reset and eye-button follow mode.
- Click a selected Voidling's name to rename it inline; the name persists in saves and genealogy.
- Hold LMB on a Voidling to pick it up; release to place it with grab cursor, lifted shadow, bounce and dust feedback.
- Close grounded ellipse shadows, pulsing selection ring, tint variation and half-size children.
- Persistent garden HUD panels hide while foreground menus are open, so the lower-left egg/status panel and inspection panel do not overlap menus.

## Details

Opening Details closes/replaces other modal context and hides the selected inspection panel until Details is dismissed.

- **Stats:** color-coded rank, level, current stat and progress.
- **DNA:** genotype only — DNA1 and DNA2 for each ability plus color DNA; no trained/current stat column.
- **Visual:** appearance information and Mutations.

Stat colors: Run green, Swim yellow, Fly purple, Power red, Stamina white. One existing/new-save Voidling receives the demo Angel mutation and halo.

## Family tree

Pannable clipped overview with no scrollbars. LMB drag on empty space pans it; middle mouse is an alternate binding. Cards preserve parent links, departed records and historical inbreeding marks.

## Shop, training, breeding and hatching

The shop sells stat treats and mystery eggs. Inventory lists icons/counts. Training controls show `+1 (count)`. Store eggs are rolled when the individual egg enters stock.

Breeding uses player-selected parents with portraits, approach/heart particles, a short dance and a bouncing egg spawn. Eggs pulse toward hatch, burst, and spawn a jumping child. Inbreeding uses the 0/20/50/80/100% hatch-failure ladder with one-level-per-clean-generation outcross recovery.

## Race

Exactly one owned Voidling enters; opponents are generated CPUs. Camera remains centered on the player at locked 1× zoom.

- Run controls ground pace and hurdle avoidance.
- Swim uses the dedicated Ocean Pack `Assets/Sprout Sorry pack/Early Access/Ocean Pack/swimming.png` animation.
- Fly provides finite glide endurance across a raised water section. Racers visibly gain vertical elevation; if glide endurance expires they drop into the water and use Swim for the remainder.
- Stamina powers sustained movement and CHEER.
- CHEER gives a two-second speed boost and trailing speed streak particles.
- Auto Finish defaults ON: CPUs fast-forward after the player finishes, or the race ends immediately as fourth if every CPU has already finished first.
- Results use aligned 1st/2nd/3rd podium blocks and fourth place beside them in a puddle.

## Settings and running

Settings are available from the top bar or ESC. Master volume and Auto Finish persist.

- `build.bat` builds the C# project.
- `playgame.bat` launches directly without opening the editor.
- CI validates restore, C# build and headless Godot project/scene parsing.
