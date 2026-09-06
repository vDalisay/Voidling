# Remaining menus — Inventory, Breed, Settings, Build

Status: **stage 6 implemented and verified; waiting for player testing.**
Branch: `ui-overhaul-remaining-menus`.

The stage 1–5 overhaul covered Garden, Shop, race entry, the live race and results. Four screens
reached from the manager's rail were never touched and still use the pre-overhaul layout:

| Screen | Reached from | Current form |
|---|---|---|
| Inventory | rail → Inventory, Shop → Open inventory | one scrolling column of text rows under English section headings |
| Breed | rail → Breed | two dropdowns, a `+`, a paragraph of preview text |
| Settings | rail → settings icon, Escape → Settings | a bare stack of sliders and toggle buttons, no card |
| Build → Land / Decorate / Stored | rail → Build | three plain buttons, then text rows carrying `(q, r)` hex coordinates |

## What the overhauled screens established

Reading the stage 1–5 diffs, the house pattern is consistent and worth stating once:

1. **Bands, not stacks.** Every screen is two or three fixed bands: a narrow chooser, the browsable
   content, and a persistent detail card on the right. Shop is categories · catalogue · receipt.
   Racer select is roster · stat inspector.
2. **A grid of slots keeps its shape.** Racer select draws empty slots so a 3x3 stays 3x3 whatever
   the roster holds. The selected slot is marked with the premium wooden star.
3. **One green primary action per screen**, `UiFactory.ApplyPrimaryStyle`, at the bottom right of
   the detail card. Everything else is quiet chrome.
4. **Column headers replace sentences.** Shop's `Type` / `Price` headers replaced the prompt, the
   item count and the receipt description. Race entry dropped the course summary entirely.
5. **Prose was deleted on purpose.** Hint lines, "you can place this anywhere" copy, Escape footers
   and inventory notes were all removed by player feedback. Nothing explains the screen; the screen
   shows itself.
6. **Premium art carries meaning, not decoration.** Treat identity is the fruit sprite, egg identity
   is the tint, land identity is its drawn footprint, difficulty is a colour-coded round button,
   level is a filled/empty star row.
7. **Every part is a named node** so the CI smoke asserts layout instead of matching English.
8. **All player-facing text is a localization key.**

## What Chao Garden and its neighbours actually show

Researched against the reference the project already follows (SA2's Chao Garden) plus the
slot-grid lineage it shares with Stardew Valley and Animal Crossing:

- **Chao Garden inventory** is a grid of icon slots, not a list. The item's sprite *is* its name.
  Quantity is a small numeral in the slot corner. Selecting a slot fills one detail area with the
  item name and a single line of what it does. There is no scrolling wall of text.
- **The Chao stat screen** shows the five/seven stats as a bar plus a letter grade and a level.
  Nothing else. Our racer-select inspector already copies this exactly.
- **Chao mating** is portrait-driven: two chao, a heart between them, and a yes/no state. The player
  never reads a paragraph to learn whether a pair works.
- **The Black Market** is category tabs, a list, and one detail box with the price and one buy
  button — which is precisely what our Shop became in stage 2.
- **Building and decorating** in these games happens in the world, with the menu acting only as a
  palette of what you own. Nothing shows the player a coordinate.

The conclusion for us is not new: the four missing screens should converge on the same two-band
grid-plus-detail composition the Shop and racer select already use. Nothing here needs a new
pattern; it needs the existing one applied.

## Stage 6 — the four screens

### 6a. Shared paper chrome

`RaceEntryScreen` holds four small helpers the other screens each need — the paper panel, the drawn
empty slot, the paper-safe ink correction for stat colours, and a column header. They move to
`Scripts/Presentation/UI/Common/PaperCard.cs` and `RaceEntryScreen` calls them there. The roster grid
(portrait cards, wooden star on the picked one, premium play-glyph paging arrows, empty filler slots)
moves to `Scripts/Presentation/UI/Common/VoidlingRosterGrid.cs`, because Breed needs the same grid
with two picks instead of one. No behaviour changes; this is the reuse that keeps 6b–6d short.

### 6b. Inventory — slot grid and one detail card

Replace the scrolling text column with the Shop's composition:

- **Category column** (left, same chrome as the Shop's): Treats, Eggs, Land, Shells. A category
  appears only when the player owns something in it, so the column reflects the save.
- **Slot grid** (centre): the item's own sprite in the slot, its count as a corner numeral, empty slots drawn to keep the grid shape,
  the selected slot marked with the wooden star. Treats use their fruit sprite, eggs their tinted
  egg sprite, land its drawn hex footprint tinted by the stat it carries.
- **Detail card** (right): the item art large, its name, the one fact that matters (count, or the
  incubation countdown for an egg), and one green action — Place, Use skip, Sell, Discard.
- **Deleted**: `UI_INVENTORY_SUBTITLE`, `UI_INVENTORY_PLACE_HINT`, `UI_INVENTORY_LAND_HINT`, the
  "No egg currently needs an incubation skip" line, and the English literals `INCUBATION SKIPS x{n}`,
  `EGGSHELLS`, `Use Skip`, `Sell +{n}`, `Eggshell {n}`, `Egg {n}` — which are localized on the way.

Failed eggs keep their warning red as a slot tint rather than a red heading.

### 6c. Breed — pick two, like racer select

Replace both dropdowns with the shared roster grid, choosing parent A and parent B. The two picked
portraits sit either side of a premium heart from `Icons/special icons/Hearts in wood.png`; the paragraph
becomes the detail card, showing each parent's name and the pairing state as one short line with a
premium check or X glyph. `UI_BREED_ACTION` becomes the single green action.

The preview strings the session already returns (`UI_BREED_RELATED`, `UI_BREED_CLEAN_OUTCROSS`, …)
are reused unchanged; only where they are shown changes.

### 6d. Settings — one card, sectioned

Wrap the stack in the paper card the other screens use, with the audio sliders in one section and
the two switches in another. Channel names become localized instead of the literals `Master`, `SFX`,
`UI`; the toggles become check/X glyphs from the premium `Xs and check marks` sheet rather than
buttons whose label changes. `UI_SETTINGS_HINT` is deleted for the same reason the Shop's prompt
was. Reset stays where it is, quiet, at the bottom.

### 6e. Build — the training grounds you own

`ShowGardenBuild`'s three plain buttons gain the rail's icon treatment. Land and Decorate become one
screen with the same two bands as the rest:

- **Land**: a grid of the placed grounds, each slot drawn as its own hex footprint tinted by its
  stat, with its level as a star row; stored pieces occupy the same grid, dimmed. The detail card
  shows the ground's stat, level, rate, who trains there, and the one action that applies — Build
  for plain ground, Upgrade for a training ground, Place for a stored piece. Hex coordinates are
  gone: they name a position the player cannot read anyway, and the piece's shape identifies it.
- **Decorate**: the same grid, showing each decoration's actual sprite from the catalogue, with
  Place / Move / Remove in the detail card. Its three English literals are localized.

The existing `ShowLandHexMenu` right-side inspector for a clicked hex in the world is untouched —
it is the stage 1 pattern and already correct.

## Deliberately unchanged

No economy, genetics, breeding rules, save schema, placement flow or race behaviour changes. Every
screen keeps calling the same session methods it calls today; only the composition and the strings
move. `ModalHost` ownership, Escape semantics and focus return stay as stage 1 left them.

## Verification

The CI checks from `.github/workflows/ci.yml`, plus the Garden UI smoke extended to walk the four
screens — category switching, slot selection following into the detail card, the two-parent pick,
the settings toggles, and Build reaching land and decorations — with review captures at 1280×720.

## Stage 6 handoff

All four screens now use the two- or three-band grid-plus-card composition the rest of the overhaul
established. What actually changed:

**Shared chrome (6a).** `PaperCard` holds the paper panel, column header, drawn empty slot, wooden
star, star rating, paper ink correction and container clear; `VoidlingRosterGrid` holds the paged
3x3 portrait roster with its premium play-glyph arrows. `RaceEntryScreen` now calls both instead of
owning private copies, and the hex-footprint drawing that existed three times — Shop, Inventory and
the land menu — is one `LandShapePresentation.CreateShapeArt`.

One real bug came out of that consolidation: `Clear` queued its children free but left them
parented, so a node rebuilt in the same frame was silently renamed by Godot (`DetailName` →
`DetailName2`). Every screen that rebuilds a band was affected. `Clear` now removes before freeing.

**Inventory (6b).** Category column · slot grid · detail card. Categories appear only when the save
holds something in them. Treats show their fruit sprite, eggs their tint, land its footprint, and
counts sit in the slot corner. The selected slot carries the wooden star and its detail card holds
the item's single action. Deleted: the subtitle, both hint lines, the "no egg needs a skip" line and
the English literals `INCUBATION SKIPS x{n}`, `EGGSHELLS`, `Use Skip`, `Sell +{n}`, `Eggshell {n}`
and `Egg {n}`, which are localized now. The `Eggs on Island` row is gone — it counted eggs the grid
already shows.

**Breed (6c).** Both dropdowns are replaced by the shared roster; the picked pair is marked A and B
on the same wooden star and stands either side of the premium wooden heart. A third pick replaces
the older parent rather than stalling. The paragraph became the pair card: a premium check or cross
plus the preview line the session already returns, over one green Breed action.

**Settings (6d).** Two headed paper cards. Channel names, `Master` / `Effects` / `Interface`, are
localized, and the volume reads as a number rather than a sentence. Each switch is a stable label
with a premium check or cross that follows its state, so `UI_SETTINGS_EDGE_PAN_ON/OFF` and
`UI_SETTINGS_AUTO_FINISH_ON/OFF` are gone. `UI_SETTINGS_HINT` is deleted. Reset stays quiet at the
bottom where the caller puts it.

**Build (6e).** The chooser's three entries carry their rail icons. Land is a grid of the pieces you
own, drawn as their own footprints, training level as a corner `L`, stored ground dimmed in the same
grid; the card shows the stat, its level as a star row, its rate, who trains there, and the one
action that applies — Build (as a sprout price plus the five stats), Upgrade, or Place. Hex
coordinates and the summary/hint prose are gone. Decorate is the same grid using each decoration's
own sprite at the catalogue's authored scale, with Place / Move / Remove in the card and its three
English literals localized.

The world-side `ShowLandHexMenu` inspector is untouched.

### Deliberately unchanged

No economy, genetics, breeding, save-schema, placement or race behaviour changed. Every screen calls
the session methods it called before. `ModalHost` ownership, Escape semantics and focus return are as
stage 1 left them. Inventory and Breed modals widened to 520 to hold three bands.

Review captures: [inventory](ui-overhaul/inventory.png), [breed](ui-overhaul/breed.png),
[settings](ui-overhaul/settings.png), [build chooser](ui-overhaul/menu-Build.png),
[land](ui-overhaul/build-land.png), [decorate](ui-overhaul/build-decorate.png).

### Verification completed

Local environment: Windows, .NET 8, Godot 4.6.1 Mono.

| Check | Result |
|---|---|
| Debug and Release builds | Pass; the two existing `TradeNegotiationCoordinator` warnings only |
| `dotnet test` | 242 passed, zero failures |
| Architecture, creature-art and localization greps | Pass |
| Every localization key referenced by `Scripts/` resolves | Pass |
| Godot import and main-scene runtime | Pass |
| Garden UI smoke, extended over all four screens | Pass |
| Voidling visual, race presentation, race completion, family tree, persistence recovery | Pass |
| Rendered review at 1280×720 | Pass |
| `git diff --check` | Pass |

The smoke now asserts the inventory's three bands, its grid shape, its single selection, the detail
card following a slot and every category staying on screen; breeding's two marked parents, verdict
and action; Build reaching land and decorations as separated grids and cards; and that each settings
switch reaches the session and changes its own mark. The reusable check is unchanged:

```text
godot --headless --path . -- --voidling-garden-ui-smoke --voidling-dev-profile=ui_menus_check
```

## Stage 6 feedback pass

Player feedback after the first stage 6 build. Six changes:

**The bullet was printing as text.** `UI_INVENTORY_LAND_PLAIN` and `UI_INVENTORY_LAND_TRAINED` held a
literal `\u2022`; gettext has no such escape, so the name read `Plain ground \u2022 Double (2 hex)`.
Both now carry a real separator, like every other string in the file.

**One line per item, saying what it is.** The detail card's status line became a description. A treat
says what it raises and by how much; the 5-9 range moved out of `TrainingUseCase` into
`TrainingItemRules`, so the number shown is the number rolled rather than a copy of it. An egg says
where it came from — a common egg, or one bred in your garden — instead of "Not placed"; its genome
stays hidden until it hatches, so nothing here spoils a breeding outcome. Land says how many hexes it
covers, a shell what it sells for, and a failed egg that it is worth nothing.

**Selection stars are gone.** A picked slot was marked with a wooden star on top of its own art, in
the inventory, the land and decoration grids, and both race-entry steps. The pressed button chrome
already says which slot is picked, so the star was noise; it now survives only as the filled/empty
level rating. Breeding keeps a badge because pressed chrome alone cannot say which of two picked
slots is which, and it is now the premium wooden heart lettered A and B.

**Food on the ground.** `GardenController.Treats` puts a treat down where you click. Every Voidling
within 150px smells it and runs for it at 2.6x its own roaming speed — which already scales with Run,
so a fast one genuinely arrives first — and the first to come within 9px eats it. The treat is only
spent when it is eaten, so `UseTrainingItem` stays the one place that validates, decrements, applies
the gain and writes the log line. The satchel refuses to put down more of one treat than it holds.

The eating beat is three seconds: the Voidling squashes and stretches on the spot while the food
shrinks 30% per bite and fades out. The inspector's Give treat plays the same beat with a treat
spawned at the Voidling's feet, and the chooser no longer closes, so pressing it again spends another
treat and restarts the animation instead of queueing a second one.

**Failed eggs are reachable.** A failed egg in the Garden had no hitbox at all, so it could not be
touched. It now takes a click and opens the same right-side inspector a hex uses, offering Take back
or Discard. `EggData.Stowed` is additive — saves written before it load with every failed egg exactly
where it lay — and a stowed egg leaves the island while staying in the satchel, still discardable.
It is worth nothing either way, which is what the card says.

**Running ground looks used.** A Voidling training on run ground now breaks into a stride at random
intervals, 0.9 to 2.4 seconds apart, kicking up the Garden's own dust at its feet. Swim ground stays
quiet. Presentation only: how often it fires has no effect on the tile's training rate.

### Verification

Debug and Release builds; 246 tests (four new ones cover stowing a failed egg, refusing a healthy
one, staying discardable afterwards, and older saves loading unstowed); architecture greps; every
`UI_*` key referenced by `Scripts/` resolves and the keys this pass orphaned are removed; Godot
import; the Garden UI smoke, extended to drop a treat, watch a Voidling run to it and eat it, and
confirm the chooser spends one treat per press while staying open; the visual, race-presentation,
race-completion, family-tree and persistence-recovery probes; `git diff --check`.

The treat probe waits on wall-clock rather than a frame budget, because headless Godot runs the idle
loop uncapped and a frame count says nothing about whether a three-second beat has finished.

### Known, not addressed

`MainController.ShopBreeding` builds three of the breeding preview lines from interpolated English
rather than localization keys, leaving `UI_BREED_RELATED`, `UI_BREED_CLEAN_OUTCROSS` and
`UI_BREED_UNRELATED_BURDEN` orphaned in `en.po`. It predates this branch and is left alone here.

(A treat left on the ground used to be lost on quit. That is fixed in the pass below.)

## Stage 6 second feedback pass

**Shop eggs and bred eggs are separate.** The satchel's single Eggs category became two, Shop eggs
and Bred eggs, each appearing only when the save holds one of that kind. An egg is named for its own
source and numbered within it, so a player with one bred egg reads "Bred egg 1" rather than whatever
position it happened to hold among the shop's. Stored, incubating and failed eggs all follow their
source, and neither category ever lists the other's egg. The genome still stays hidden until it
hatches; only where the egg came from is shown.

**A treat on the ground survives a quit.** A drop was presentation state living in the
`GardenController`. It is now `GameStateData.DroppedTreats`, additive and non-null, so a save written
before it loads with bare ground. `GameSession.DropTreat` records the drop and refuses to put down
more of one treat than the satchel holds; `ClaimDroppedTreat` takes the drop off the ground *before*
calling `UseTrainingItem`, so a second claim in the same frame, or a quit mid-animation, cannot spend
the same treat twice. The Garden now renders drops from state exactly as it renders eggs — building a
visual for each and freeing the ones the save no longer has — and a treat restored from a save skips
the drop hop, because it was already lying there.

Nothing the player owns changes by dropping: the training item stays in the satchel until something
eats it, which is what makes a saved drop free.

### Verification

Debug and Release builds; 249 tests — three new ones cover the JSON round trip the repository
performs, an older save loading with bare ground, and migration discarding a drop that lost its stat;
architecture greps; every `UI_*` key referenced by `Scripts/` resolves, with the keys this pass
orphaned removed; Godot import; the Garden UI smoke, extended to check that putting a treat down does
not spend it, that the drop is recorded in save state, and that each egg category lists its own eggs
and never the other's; the visual, race-presentation, race-completion, family-tree and
persistence-recovery probes; `git diff --check`.

Review capture: [split egg categories](ui-overhaul/inventory-eggs.png).

## Garden roaming fix

A Voidling standing on plain ground could not leave the hex it was on. `ClampToLand` treated a
position over placed ground by pulling it to within 60% of *that hex's* inner radius, so every step
toward a neighbouring hex was yanked back toward the middle of the current one. The Voidling orbited
a small disc, and because the movement loop repaths whenever the clamp moves it, it also repeatedly
picked new targets it could never reach.

A point over any placed hex is on land, so it is now left where it is. Only a point that has actually
left the island is pulled back, to the nearest ground it can stand on, which is what turns a Voidling
around at the shoreline. Training grounds are untouched: a trainee is held by its tile radius in
`ClampToWanderArea` before the land clamp is ever consulted.

The Garden smoke checks the clamp directly rather than waiting on a random walk: for every placed
hex, a point out at 90% of the inner radius must not come back inside the old 60% disc, a point far
off the island must still land on placed ground, and a real free-roaming Voidling must reach past
that disc within ten seconds. Reverting the one-line clamp change fails the first of those, so the
check is known to catch the regression rather than merely passing beside it.
