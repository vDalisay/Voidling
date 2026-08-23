# Voidling Demo MVP

Godot 4.6 C# vertical slice for the initial raising, breeding and racing loop.

## Current controls and systems

- Editable Sprout Lands TileMap garden.
- LMB drag on empty garden ground pans the camera; middle mouse remains an alternate pan binding. Mouse wheel zooms, Center resets, and the eye button follows the selected Voidling.
- Click a selected Voidling's name to rename it inline. Hold LMB on a Voidling to pick it up and release to place it.
- Garden HUD panels hide while menus are open, avoiding overlap.
- Details has separate **Stats**, **DNA** and **Visual** tabs. Stats contains rank/level/current value/progress; DNA contains only DNA1/DNA2 genotype information; Visual contains appearance and Mutations.
- Stat colors: Run green, Swim yellow, Fly purple, Power red, Stamina white.
- Angel demo mutation renders a halo.
- Family tree is a scrollbar-free pannable overview; LMB drag on empty space pans it.
- Shop/inventory/training, player-controlled breeding, visible eggs, hatching, inbreeding penalties and lineage persistence are implemented.
- Race enters exactly one owned Voidling versus generated CPUs. Run handles ground/hurdles; Swim uses the Ocean Pack `swimming.png`; Fly provides finite elevated glide distance before a possible fall into water; Stamina powers sustained pace and CHEER.
- CHEER adds a two-second speed boost with speed streak particles.
- Auto Finish defaults ON and ends once the player's placement is known, including when all CPUs finish first.
- Results use aligned 1st/2nd/3rd podiums with fourth beside them in a puddle.
- Settings via top bar or ESC include persistent master volume and Auto Finish.

Run with `playgame.bat`; build with `build.bat`. CI validates restore, C# build and headless Godot project/scene parsing.
