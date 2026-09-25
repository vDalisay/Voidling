# Race track and course select visual overhaul

Branch: `feature/race-track-visual-overhaul`

Goal: make the race and the race entry flow feel closer to the Chao Garden races (a lively
stadium course with distinct Run / Swim / Climb / Fly stretches, and a course board you pick a
race from) while drawing on the Sprout Lands premium packs wherever they have the art. Anything the
packs do not ship (water motion, underwater look, wind, particles) is generated in code or shaders.

Status: implemented on this branch (see "Outcome" at the end).

Scope is presentation only. `RaceSimulation`, course geometry in `Scripts/Domain/Racing` and every
result-affecting rule stay untouched; visuals keep reading simulation snapshots and never feed back.

## Problems seen in the baseline shots

- The climb is a 20px smoothstep painted as 4px strips, so its edge reads as a staircase and the
  wall face is a flat brown slab rather than a cliff.
- The launch ramp is a stack of fence sprites on stepped planks, so it reads as a hatch pattern and
  the plateau just stops at the lake with nothing under the lip.
- Swimmers sit under a pale rectangle-ish overlay with a white ring; the river is a flat tile with
  lines on it and bare edges against the grass.
- The course select screen shows a flat colour-block minimap and a plain list.

## Plan

### 1. Terrain geometry (RaceTrackArt)

- Paint every raised surface as polygon strips whose UVs follow the slope, instead of stepped
  rectangles. Removes the staircase everywhere (climb, drops, ramp).
- Climb wall: a short, steep, linear rise (`WallSpan` 26px, `ClimbHeight` 64px) drawn as a leaning
  cliff face. The face and the plateau's front are textured from a tall masonry texture composed
  once from the premium `Old tiles/Hills.png` cliff (grass overhang cap, earth-block courses,
  tufted foot). Vines, hand holds and an ambient-occlusion falloff are generated on top.
- Plateau drops use the same face, with foam where they meet water.
- Launch ramp: a wooden deck from `Wooden_Bridge_v2.png` mapped along the slope, trestle posts
  from the deck down to the clifftop, premium `Fences.png` rails rotated to the slope, and a
  flagged take-off lip. The cliff under the lip continues down into the lake.

### 2. Water (`RaceTrackLayers` + `RaceWater.gdshader`)

- One shader-driven node per water stretch: premium water frames as the base, flowing current,
  caustic shimmer, depth shading and glints; the premium grass-hill lip lines each bank with an
  animated foam line.
- Dressing from the packs: lily pads, reeds and stones (`Water Objects.png`, biome water plants),
  plus dark fish silhouettes (`Fish Sprites.png`) drifting under the surface.

### 3. Swimmers (`RaceScreen.Effects.cs` + `RaceUnderwater.gdshader`)

- Replace the flat submersion polygon with a screen-reading underwater shader over the submerged
  body: water tint, refraction wobble and caustics below an animated waterline with a foam edge.
- Wake rings, a trailing V-wake, a splash on entry and exit (premium fishing splash frames plus
  droplet particles) and occasional bubbles.

### 4. Run / climb / glide effects and ambience

- Climbers lean into the wall and knock pebbles loose; gliders get a take-off burst and a sparkle
  trail; a soft shadow tracks them across the water.
- Drifting cloud shadows and ambient leaves; trees sway through a wind shader.
- Chao-style spectators: premium chickens idling and hopping by the start and finish.
- Premium signposts announce each stretch; hurdles use premium fence runs; the start and finish
  arches get bunting.

### 5. Course select (RaceEntryScreen)

- Course board in the Chao Garden race-entrance style: course cards on the left, each with its
  section icons and level medals; on the right a live preview that renders the real track art in a
  `SubViewport` and pans along it like the race-entrance preview, with the record plaque and
  difficulty stars underneath.
- The course profile strip is redrawn as an elevation silhouette with section icons.
- Confirm step: the course preview beside the racer card.
- Node names the Garden UI smoke drives (`CourseList`, `CourseRecord`, `Minimap`, `Course_*`,
  `Level_*`, `EntryPrimary`, `EntryBack`, `RosterGrid`, `RacerStats`, `ConfirmCourse`) are kept.

### 6. Verification

- `dotnet build`, the test project, and the CI Godot smokes (runtime, Garden UI, Voidling visual,
  race presentation, race completion) with `APPDATA` redirected to scratch.
- Before/after screenshots from `--voidling-race-shots` and the new `--voidling-race-menu-shots`.

## Rules kept

- Decoration placement is a hash of world X, never the simulation random stream.
- VFX use the screen's own seeded `_vfxRandom`; nothing visual decides a result.
- New player-facing text goes through localization keys.
- Base Voidling art still comes only from `VoidlingVisualFactory`.

## Outcome

- Terrain: `RaceTrackArt` paints three passes (`PaintBack`, `PaintFront`, `PaintFoliage`) that
  `RaceTrackLayers` stacks under the racers with the shader water between them. Tiles come from
  `RaceTrackTiles`; signposts, fence hurdles and the chicken crowd from `RaceTrackFurniture`.
- Climb pacing: `RaceTrackArt.PresentationX` spends the first 45% of a climb stretch's distance on
  the wall, so racers visibly haul up it. It is continuous and only moves the drawn position; the
  camera still follows the simulated X.
- Shaders live in `Resources/Presentation/Racing/`: `RaceWater`, `RaceUnderwater` (screen-reading
  overlay for swimmers), `RaceFoliageSway`, `RaceCloudShadows`.
- Course select: `CoursePreview` renders the real course layers in a SubViewport at the window's
  pixel density; `RaceSectionGlyphs` supplies the run/swim/climb/fly glyphs used by the board, the
  profile strip and the track signposts.
- Review tooling: `-- --voidling-race-shots` (track) and `-- --voidling-race-menu-shots` (entry
  flow) write screenshots to `.godot/race-shots/`.
