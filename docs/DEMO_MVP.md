# Voidling Demo MVP

Godot 4.6 C# vertical slice for the initial raising, breeding and racing loop.

- Editable Sprout Lands TileMap garden.
- LMB drag on empty garden ground pans; middle mouse is an alternate pan control; mouse wheel zooms; Center resets; eye button follows.
- Click a selected Voidling name to rename it inline. Hold LMB on a Voidling to pick it up and release to place it.
- Garden HUD panels hide while menus are open.
- Details has **Stats**, **DNA** and **Visual** tabs. Stats owns rank/level/current value/progress; DNA contains only DNA1/DNA2 genotype information; Visual contains appearance and Mutations.
- Run green, Swim yellow, Fly purple, Power red, Stamina white. Angel mutation renders a halo.
- Family tree is scrollbar-free and pans with LMB on empty space.
- Shop/inventory/training, player-controlled breeding, visible eggs/hatching, inbreeding and persistent lineage are implemented.
- Race uses one owned Voidling vs generated CPUs. Run handles ground/hurdles; Swim uses the Ocean Pack `swimming.png`; Fly gives finite elevated glide endurance before a possible fall into water; Stamina powers pace and CHEER.
- CHEER adds a two-second speed boost and speed streak particles.
- Auto Finish defaults ON and ends once the player's placement is known, including when all CPUs finish first.
- Results use aligned 1st/2nd/3rd podiums and fourth beside them in a puddle.
- Settings via top bar or ESC include persistent master volume and Auto Finish.

Run with `playgame.bat`; build with `build.bat`. CI validates restore, C# build and headless Godot scene/project parsing.
