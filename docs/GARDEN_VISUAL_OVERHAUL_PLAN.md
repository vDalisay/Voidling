# Garden visual overhaul: water, light, weather

Branch: `feature/garden-visual-overhaul`

Goal: make the Garden feel alive: a sea that moves and meets the island properly, light that
changes with the hour and the weather and actually falls on the Voidlings, rain, and Voidlings
reflected in the water when they stand near it. The Sprout Lands premium packs supply every sprite
they have (water, cliff, fish shadows, bees, frogs, boat, lily pads, puddles, mushrooms); motion, light
and weather that the packs cannot ship are generated with shaders, particles and Light2D.

Status: implemented on this branch (see "Outcome" at the end).

Scope is presentation only. Nothing here is read by care, training, genetics, racing, economy or
persistence: time of day and weather are cosmetic, like the existing `GardenEnvironmentPalette`.
No save data changes.

## Baseline

- The sea is one flat `ColorRect`; the island edge is a brown line with a sand line inside it.
- The day/night tint is a `Modulate` on the whole Garden node, so there is no way for anything to
  glow: a light would be darkened along with everything else.
- No weather, no particles beyond dust and hearts, no reflections, trees are static.

## Plan

### 1. Island field (`GardenIslandField`)

A small raster of the island (land mask plus distance to the shore on both sides), rebuilt only
when land changes. It is the one source of truth the effects share: the sea shader reads it as a
texture for depth, shallows and shore waves; CPU code queries it to place rain splashes on land,
ripples on water and to decide whether a Voidling is close enough to the water to be reflected.
The distance transform is plain C# so it is unit-tested without Godot.

### 2. Sea (`GardenSea` + `GardenSea.gdshader`)

- Premium water tile as the base, drifting in two layers; deep water darker and cooler, shallows
  bright turquoise over a sandy bottom near the island.
- Shore waves: foam rings that roll in towards the coast from the distance field and break on the
  cliff foot, plus a lapping foam line.
- Caustics in the shallows, sun glints by day, star twinkles by night, dawn mist.
- Rain rings across the surface while it rains.
- Reflections: see 4. The sea shader folds them in through its ripples.

### 3. Coast (`GardenController.Land`)

- South-facing island edges get a real earth cliff face (the premium Hills ledge, composed into a
  tall face) dropping to the water, so the island reads as land standing in the sea.
- A dark grass rim with a sunlit lip just inside it, continuous around the whole island.
- A moored premium rowboat bobbing off the south shore; premium lily pads and a frog near the shore;
  premium fish shadows circling in the shallows.

### 4. Reflections (`VoidlingReflection2D`, `GardenReflections`)

- Every Voidling (and every tree) has a mirrored copy rendered into a reflection buffer, flipped
  about the water line under its feet. The copy is built through `VoidlingVisualFactory`, so palette and
  accessory layers match, and follows the source animation frame by frame.
- The sea reads them back with ripple distortion, a water tint and a fade with depth. Land and the
  cliff are drawn above, so only reflections that actually fall on water show. A Voidling far from
  the shore hides its copy.

### 5. Light (`GardenLighting`)

- The day/night/season tint moves from the Garden's `Modulate` to a `CanvasModulate`, so lights
  can add on top of it instead of being darkened with everything else. The Garden tint setting
  still switches all of it off.
- Weather dims and cools the ambient light.
- Light sources: fireflies at dusk and night (a few carry real `PointLight2D`s), glowing premium
  mushrooms on plain ground, and a soft glow from angel-halo Voidlings after dark.
- Voidlings and vegetation are shaded by those lights through generated normal maps (see
  Outcome), so the edge facing a light brightens instead of the whole sprite flatly.
- Golden-hour light shafts and drifting cloud shadows.

### 6. Weather (`GardenWeatherSchedule`, `GardenWeather`)

- A cosmetic schedule from the local clock (clear, overcast, rain, storm), deterministic per time
  slot and blended over 90 seconds at each change; unit-tested.
- Rain: two layers of streak particles angled by the wind, splash crowns on land, rings on the sea,
  premium puddles that fill while it rains and dry afterwards, stronger wind in the trees.
- Storms add brief lightning flashes (rare and soft).
- A command-line override (`--voidling-garden-weather=`, `--voidling-garden-hour=`) for review.

### 7. Ambient life and wind

- Trees sway through a wind shader, stronger in weather; leaves drift down now and then.
- Premium bees around the island by day, pollen motes in the light; fireflies at night.

### 8. Tooling and verification

- `--voidling-garden-shots` probe: builds a multi-hex island with Voidlings at the shore in an
  isolated profile and captures day, golden hour, night, rain and storm.
- CI mirror: builds, architecture checks, unit tests and all Godot smokes with scratch `APPDATA`.

## Rules kept

- Presentation only; weather and time are never read by game rules.
- Base Voidling art only through `VoidlingVisualFactory`; reflections reuse it.
- New components live under `Scripts/Presentation/Garden` and are composed by `GardenController`
  through one small partial, rather than growing the controller.
- Player-facing text through localization keys.

## Outcome

- `Presentation/Garden/Atmosphere/GardenAtmosphere` owns the sea (`GardenSea`), reflections
  (`GardenReflections` + `VoidlingReflection2D`), light (`GardenLighting`), weather
  (`GardenWeather`) and wildlife (`GardenWildlife`). `GardenController.Atmosphere` feeds it the
  island, the Voidlings and trees, the clock and the Garden camera.
- The island field is rebuilt on land changes (2px cells, 128px range) and shared by the sea
  shader, rain splashes, wildlife placement and reflection visibility.
- Reflections render into a transparent SubViewport that copies the Garden camera; the sea shader
  composites them through its ripples, so only water shows them. The buffer stops rendering while
  the Garden is hidden.
- Normal mapping: every in-world Voidling frame (body and accessory layers), the island's trees,
  player-placed trees and the glowing mushrooms carry a normal map generated from their silhouette
  alone (the edge rounds away over a few pixels, the middle faces the viewer; the art's own shading
  is never embossed into bumps). A sun that crosses the sky from the east in the morning to the
  west in the evening (a dim, cool moon at night), plus fireflies, mushrooms and halos, light them
  through `BalancedKeyLight`: whatever faces the viewer is lit exactly like the ground, so the
  drawn colours stay as drawn, and only the edges answer to the light, in two crisp steps of light
  on the near side and shade on the far side. Light is measured once per art pixel, so it never
  smears across one. The sun's share is taken out of the ambient light so the ground keeps its
  colour. Nothing changes where there are no lights (race, UI). The flat ground-detail tiles
  (grass tufts, flowers painted onto grass) stay flat on purpose.
- A first version rounded a narrow bevel and embossed the art's dark pixels, and multiplied the
  light's energy in twice (Godot's `LIGHT_COLOR` already carries it): Voidlings came out darker,
  duller and clay-like. The silhouette-only map and stepped edges replaced it.
- Shaders in `Resources/Presentation/Garden/`: `GardenSea`, `GardenPuddle`, `GardenCloudShadows`,
  `GardenFoliageSway`, `GardenSunShafts`.
- With the Garden tint setting off the ambient light stays white, so there are no night, weather
  or lightning colour changes; rain, reflections and wildlife still show.
- Review: `-- --voidling-garden-shots --voidling-dev-profile=garden_shots` (scratch APPDATA),
  plus `--voidling-garden-hour=` and `--voidling-garden-weather=` to pin the sky in normal play.
- Not covered: connected-zone (remote) Voidlings are not reflected yet.
