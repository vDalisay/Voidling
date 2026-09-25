# Garden UI overhaul: premium chrome and incremental-game motion

Branch: `feature/garden-ui-overhaul`

Goal: every menu, panel, button and counter the player can reach from the Garden looks like part
of the Sprout Lands world (wood, paper, leaves, seeds, badges) and answers every touch the way an
incremental game does: buttons lift, squash and bounce, panels pop open, lists stagger in, numbers
roll and throw "+N", rewards fly to the wallet and confirmations burst into pixel sparkles.

Status: implemented on this branch (see "Outcome" at the end).

Scope is presentation only. No rule, price, reward, save field or simulation step changes, except
one additive settings flag (Reduce motion, default off). Every click acts immediately; motion is
decoration layered on top and never delays or blocks the action.

## Rules for this overhaul

- Pack art first. Everything below comes from the Sprout Lands premium UI pack unless marked
  "generated". Generated pieces are shaders, particles and one-pixel-wide lines only.
- Crisp at rest. Rest states are pure stylebox swaps at whole pixels (hover draws the chrome 1px
  higher, pressed 1px lower). Scale/position tweens are transient and always settle to exactly
  1.0 / the laid-out position. No lasting fractional scale; the 0.75-scaled day dial goes.
- Motion is routed through one place (`UiMotion`). Reduce motion (new setting) turns off pops,
  stagger, particles, floaters, idle loops and counter rolls; the static hover/pressed art stays.
- Tweens are keyed per node and channel, so a new animation replaces the old one (no stacking) and
  dies with its node (no leaks). Idle loops use stepped tweens or shader `TIME`: no `_Process`.
- Node names that smokes and probes use are kept (`GardenRail`, `Actions`, rail destinations,
  `GardenStatus`, `GardenUtilities`, `Settings`, `Mute`, `ToggleHeight`, `Activities`, `GiveTreat`,
  `Progress_*`, `Rate_*`, `ShopLedger`, `Category*`, `Product_*`, `BuySelected`, `Receipt`,
  `Satchel`, `Slots`, `ItemDetail`, `Slot_*`, `Nesting`, `RosterPanel`, `PairCard`, `BreedAction`,
  `RaceEntry`, `EntryPrimary`, `EntryBack`, `Course_*`, `Level_*`, `Racer_*`, `Claim`, `Missions`,
  `Resume`, `EdgePan`/`Mark`, `DayNightArrow`/`ArrowArtwork`, `LandSlots`/`LandDetail`, ...). The
  modal keeps `ModalHost > CenterContainer > PanelContainer` and its full-screen shade.

## Inventory (everything reachable from the Garden)

HUD: manager's rail (8 destinations, settings + mute utilities, resize edge, collapsed handle),
garden name + wallet, day/night dial, event log (collapse toggle, Activities), Voidling inspector,
land/failed-egg inspector, quick roster, placement hint, land-purchase bar, save status, tooltips,
first-launch tutorial overlay, purchase celebration.

Modals (all through `ModalHost`): Garden menu, Settings, Reset, Build chooser, Land, Decorate,
Activities, Daily missions, Journal, Inventory, Shop, Breeding, Race entry (course / racer /
confirm, full screen), Details, Family tree, Treat chooser, Goodbye (x2), Online hub, Friends
leaderboard, Daily race (+ racer select), Challenges, Multiplayer race setup, Trades, Trade room.

## Pack assets -> UI elements

All art is by Cup Nooble (Sprout Lands UI Pack Premium unless noted; credit as "Assets from Sprout
Lands by Cup Nooble").

| UI element | Pack asset |
|---|---|
| Standard button (up / hover / pressed / disabled) | `UI Sprites/buttons/square/Square Buttons 26x19.png`, light-tan up and pressed frames, 9-sliced |
| Selected toggle / tab | same sheet, the light-tan pressed frame recoloured honey |
| Primary call to action (Buy, Breed, Claim, Place, Start, Give) | same sheet's white frames tinted leaf green |
| Danger action (Goodbye, Discard, Reset, Remove) | same white frames tinted clay red |
| Close / back / settings / sound / rail handle | `UI Sprites/buttons/Icon Buttons/Icon Buttons Spritesheet.png` at native size |
| Modal windows and HUD panels (status, inspector, roster); the side board | `UI Sprites/Other UI sprites/Setting menu.png` blank panel, 9-sliced (windows lightened, the board in the pack's own beige) |
| Paper cards, slots, wells, tooltips, toasts, wallet chips | `emojis/emoji style ui/Inventory_Blocks_Spritesheet.png` light and mid blocks, 9-sliced |
| Window title tag | `UI Sprites/Dialouge UI/dialog box.png` (notched), drawn 1:1 at its 28px height |
| Title stickers, Missions icon | `emojis/Emoji spritesheet.png` (sprout, apple, heart, star, clipboard, hammer, clover, tulip, rainbow, check, paw, blue heart, strawberry, egg, "!", music) |
| Focus cursor, tutorial highlight | `UI Sprites/Other UI sprites/Selectors/Selectorst.png` corner brackets, two breathing frames |
| Settings switches | `UI Sprites/Other UI sprites/UI Settings Buttons.png` toggle track + knob (off, moving, on) |
| Attention badge | `UI Sprites/buttons/round/small colored round buttons.png` (salmon) |
| Counters, "+N" floaters, check-in stamps, treat stock | `fonts/Font files TTF/pixelFont-4-7x7-sproutLands.ttf` (digits only; words keep the UI face) |
| Burst sparkles | `UI Sprites/Icons/special icons/stars.png` (tiny star) |
| Check-in and claimed stamps | `UI Sprites/Other UI sprites/Xs and check marks/1s/check mark.png` |
| Day/night dial | `emojis/emoji style ui/weather/Weather_UI.png` small frame, plate, sun/moon insert and pointer frames; `Weather_Icons_small.png` |
| Tutorial "continue" arrow | `UI Sprites/Dialouge UI/dialog box character finished talking click to continue indicator - spritesheet .png` |
| Review video cursor (probe only) | `UI Sprites/Mouse sprites/Catpaw Mouse icon.png`, `Catpaw holding Mouse icon.png` |
| Rail icons, sprout currency, treats, eggs | existing: `UI Sprites/Icons/All Icons.png`, Sprites premium `Farming Plants.png`, `fruit-n-berries-items.png`, Basic pack `Egg item.png` |
| Generated | shine-sweep shader (`Resources/Presentation/UI/UiShine.gdshader`), square-pixel bursts, reward flights, one-pixel dividers, pixel progress-bar fills, the modal shade |

## Motion system (`Scripts/Presentation/UI/Motion/`)

- `UiMotion` (static): the one switch (`Reduced`), durations, keyed tween helpers (`Pop`,
  `Squash`, `Release`, `Flash`, `Nudge`, `Appear`, `StaggerIn`, `Loop`) and `IsSettling` for
  probes/smokes.
- `UiMotionMath` (pure, unit-tested): easing, counter roll values and durations, stagger delays,
  burst particle trajectories, reward flight arcs, stepped bob offsets.
- `ButtonJuice` (component node on any `BaseButton`): hover/focus lift + overshoot pop, press
  squash, release bounce, inert disabled look with a "no" nudge, focus cursor, optional shine and
  confirm burst. Attached by `UiFactory` so every factory button gets it. Survives the screens'
  rebuild-on-select pattern: a rebuilt button with the name just pressed resumes the bounce.
- `FocusCursor`: the pack selector brackets around the focused control, breathing in 1px steps.
- `Resources/Presentation/UI/UiShine.gdshader` (applied by `ButtonJuice` to primary buttons): a
  periodic stepped highlight driven by shader `TIME`.
- `RollingCounter` (component on a `Label`): rolls to a new value with a pop and green/red flash,
  optionally throwing a `FloatingNumber` ("+N"/"-N").
- `PixelBurst`: crisp square-pixel + pack-star burst on an overlay layer; `RewardFlight`: sprouts
  that fly from a button to a counter and feed it on arrival.
- `AttentionBadge`: bobbing "!" on actions that have something to claim.
- `ToastStack`: messages slide in above modals when the Garden log is hidden or compact.
- `TooltipJuice`: pops Godot's tooltips in; the UI root's theme draws them as pack paper notes.
- `ModalHost` animates open (shade fade, panel pop, staggered content), close (quick fold) and
  swaps between screens; re-rendering the same screen in place keeps its header, so the wallet chip
  rolls instead of rebuilding.

## Per-screen plan

- HUD: rail buttons with 16px icons at 1x, utilities as pack icon buttons, wallet as a chip with a
  rolling pixel-font counter, dial rebuilt from the small weather frame at 1x showing the real
  weather, event log as a paper board whose new lines flash, inspector with pixel stat bars that
  roll and flash on level-up, roster and land inspector slide in, attention badge on Activities.
- Modals: window + title tag + icon close/back; content staggers in on open.
- Shop: wallet chip in the header rolls on purchase; buy bursts; tabs re-stagger rows.
- Inventory / Journal / Land / Decorate: grids stagger; selection pops; actions burst.
- Activities / Missions: claim throws sprouts at the wallet chip; claimed rows stamp a check.
- Breeding: Breed is a shining CTA; the heart between parents beats when the pair can breed.
- Race entry: moves onto the shared chrome (title tag banner, paper course cards and plaque, juiced
  level pills, shining Start); its course preview and chips keep their art as square, non
  anti-aliased boxes.
- Settings: pack switches; new Reduce motion switch.
- Online, dialogs, tutorial: shared chrome and motion; the tutorial board types its text, bobs the
  pack's "click to continue" arrow and brackets the highlighted area with the pack selectors.

## Verification

- Unit tests for `UiMotionMath` and the Reduce motion setting.
- `-- --voidling-garden-menu-shots --voidling-dev-profile=garden_menu_shots` probe for before/after
  shots, and `--voidling-garden-menu-showcase` with `--write-movie` for the motion video.
- CI mirror: builds, boundary checks, tests, every Godot smoke with scratch APPDATA.

## Palette

The product owner preferred the lighter beige of the original side menu, so the chrome is
recoloured along the pack's own warm beige ramp (`Presentation/UI/Common/UiPalette`), exact colour
for colour as each piece is cut (`PaletteSwap`), keeping every sprite's shapes and outlines:

| Layer | Colour | Used for |
|---|---|---|
| Highlight | `#FFF8E6` | rims on cards and buttons (the one shade added above the pack's ramp) |
| Parchment | `#F3E5C2` | cards, tan buttons, title tags |
| Sand | `#E8CFA6` | window bodies |
| Beige | `#DCB98A` | the side board (the original rail colour), sunken wells, bar and slider tracks |
| Tan | `#C49A6C` | inner lines, button lips, dividers, scroll pegs |
| Bark / Umber | `#AA7959` / `#90625D` | outlines, kept dark so shapes stay crisp over the Garden |
| Honey | `#F1D59B` | picked tabs, slots and rows |
| Leaf / Clay | `#86C95E` / `#DE8A74` | the primary action and irreversible actions |
| Ink | `#4A3A2A` | all text, warm dark brown (well above 7:1 on every surface) |

- Value does the layering: board < window < card < highlight, with wells and tracks one step down.
  Cards and wells inside a window get their outlines softened one step, so the window keeps the
  strongest frame.
- One analogous warm hue family for surfaces; saturation is reserved for small accents (leaf green,
  clay red, honey for selection), so they read at a glance without clashing.
- Selection is a warm honey plus the pressed-in position, not a darker wood, so it draws the eye
  without becoming the heaviest thing on screen.
- Stat names on paper use the paper ink of their stat colour (`PaperCard.Ink`); the bright bar
  colours are for bars only.

## Outcome

- Shared pieces: `Presentation/UI/Common/UiSkin` (all pack regions and 9-slice margins),
  `WalletChip`, `Sticker`, `ScreenIcons`; `Presentation/UI/Motion/` with `UiMotion`,
  `UiMotionMath`, `ButtonJuice`, `FocusCursor`, `RollingCounter`, `FloatingNumber`, `PixelBurst`,
  `RewardFlight`, `AttentionBadge`, `ToastStack`, `TooltipJuice`, `UiFxLayer`; the shine shader.
- `UiFactory` buttons get the pack chrome and `ButtonJuice` automatically, so every factory button in
  the Garden, its menus and the online screens has the feel without per-screen code. The UI root's
  theme styles tooltips and any stray control.
- `ModalHost` pops windows open over a fading shade, cascades their buttons and cards in, folds
  them away on a ghost when closed (input returns immediately), swaps screens under a steady shade
  and redraws the same screen in place, keeping its header and purse.
- New screens: `ActivitiesScreen` (reward-stamp calendar, `CheckInCalendar` pure logic) and
  `DailyMissionsScreen` (paper cards with progress bars, localized mission texts).
- HUD: the side board (the pack's own beige) with 1:1 icons and pack icon buttons, status board with a rolling purse, the
  small weather dial at 1:1 showing the real (cosmetic) weather, a paper log that types new lines,
  an attention badge on Activities, paper toasts over menus, a save slip, a bobbing placement note.
- Reduce motion (Settings > Play) switches every effect off; the state art stays.
- Crispness: rest states are stylebox swaps at whole pixels. The probe audits every visible panel,
  button and picture after each shot for off-grid positions or sizes; the last run reports zero.
  Decoration slots now draw their sprite 1:1 (they were scaled), family-tree cards sit on whole
  pixels, and the day dial lost its 0.75 scale.
- Smoke: `SettleGardenUi` also waits (up to 1.5 s) for `UiMotion.IsSettling`; the day-dial checks
  follow the new dial (drawn pointer frames instead of a rotated arrow, time centred on its plate).
- Not done: no UI sounds (the project has a UI bus and volume but no sound hooks or UI sounds yet);
  the race HUD, pause menu and results card keep their own panels (their buttons pick up the new
  chrome through `UiFactory`).
