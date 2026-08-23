# Voidling Demo MVP

Godot 4.6 C# vertical slice for the initial raising, breeding and racing loop.

- Editable Sprout Lands TileMap garden.
- LMB drag empty ground to pan; middle mouse also pans; mouse wheel zooms; Center resets; eye button follows.
- Click a selected Voidling name to rename it inline. Hold LMB on a Voidling to pick up and release to place.
- Garden HUD panels hide while menus are open.
- Details has **Stats**, **DNA**, **Visual** tabs. Stats owns rank/level/current/progress; DNA only shows DNA1/DNA2 genotype; Visual shows appearance and Mutations.
- Stat colors: Run green, Swim yellow, Fly purple, Power red, Stamina white. Angel mutation renders a halo.
- Family tree has no scrollbars and pans with LMB on empty space.
- Shop/inventory/training, breeding, visible eggs/hatching, inbreeding and persistent lineage are implemented.
- Race: one owned Voidling vs CPU racers; Run for ground/hurdles, Ocean Pack `swimming.png` for Swim, finite elevated Fly glide with water fallover, Stamina/CHEER with speed streaks.
- Auto Finish defaults ON and ends once placement is known, including when all CPUs finish first.
- Aligned 1st/2nd/3rd podiums plus fourth-place puddle.
- Settings from top bar or ESC persist master volume and Auto Finish.

Use `playgame.bat` to run and `build.bat` to build. GitHub Actions validates restore, C# build and Godot headless scene/project parsing.
