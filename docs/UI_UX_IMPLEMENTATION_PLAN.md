# Selected UI overhaul — staged implementation

Status: **Stages 1–2 implemented and verified; waiting for Shop player testing.** Each subsequent screen waits for the previous screen's player test.

Branch: `codex/ui-overhaul-managers-rail`  
Workspace: `C:/Users/Home/Documents/Voidling-ui-overhaul`  
Baseline: `e03691b` from main. No save schema, economy, genetics or race simulation changes.

## Approved visual combination

| Stage | Selected reference | Implementation and test gate |
|---|---|---|
| 1. Garden | 02 — Manager's rail | Warm premium wood rail at left; Voidlings, Inventory, Shop, Breed, Races, Build and Online in that order. The rail resizes from its edge and collapses to a half-circle handle. Settings and mute sit at its lower edge; Escape alone opens the Garden menu. Stop for Garden playtesting. |
| 2. Shop | 04 — Keeper's ledger window, with 02's side rail replacing the top menu | Preserve option 04's paper window, category column, readable product rows and purchase receipt. Keep the manager's rail on the left. Preserve fixed egg identities, stock, prices and ownership. Stop for Shop playtesting. |
| 3. Race entry | 02 — Manager's rail | Course and creature selection beside the rail, clear selected entrant and one Start action. Stop for entry playtesting. |
| 4. Live race | 05 — Creature first | Player portrait, stamina and Cheer together at bottom centre; opponents right; course progress bottom left. Preserve existing simulation and local/online exit semantics. Stop for live-race playtesting. |
| 5. Results | 01 — Classic dock | Central podium and clear Return to garden action. Preserve rewards and one-time result handling. Stop for results playtesting. |

“Keeper's Leisure” in the selection refers to option 04, labelled Keeper's ledger in the studies.

## Selected mockups — visual source of truth

These are the exact studies selected by the user. Use them as composition references together with the written refinements later in this plan. When a later refinement conflicts with text or controls visible in a mockup, the later refinement wins. The implemented Garden and Shop captures linked below show the current result after those refinements.

### Garden — 02 Manager's rail

Keep the persistent left destination rail, central garden world, lower event log, and right contextual inspector. Later feedback replaces the mockup's fixed collapse button with a draggable rail edge, removes Center and the visible ESC button, moves Online into the rail, and moves garden identity/resources to the upper right.

![Selected Garden mockup — option 02 Manager's rail](ui-overhaul/selected-mockups/garden-02-managers-rail.png)

### Shop — 04 Keeper's Leisure / Keeper's ledger, with the 02 rail

Keep option 04's warm paper window, vertical categories, product list, and selected-product purchase area. Replace its top navigation with option 02's left rail. Later feedback removes the garden-name eyebrow, prompts, item count, long receipt description, inventory note, and Escape footer; Type/Price headers and sprout currency icons take their place.

![Selected Shop mockup — option 04 Keeper's ledger](ui-overhaul/selected-mockups/shop-04-keepers-ledger.png)

### Race entry — 02 Manager's rail

Keep the left manager's rail while presenting course choice and racer choice together, followed by one clear Start race action. This is the next unimplemented stage and its primary visual reference.

![Selected race-entry mockup — option 02 Manager's rail](ui-overhaul/selected-mockups/race-entry-02-managers-rail.png)

### Live race — 05 Creature first

Keep the race world dominant. Group the player's portrait, place, stamina, and Cheer at bottom centre; keep course progress at bottom left and opponent standings on the right.

![Selected live-race mockup — option 05 Creature first](ui-overhaul/selected-mockups/live-race-05-creature-first.png)

### Race results — 01 Classic dock

Use a centered podium/result card over the completed race, with the outcome, reward, and Return to garden action clearly visible. Preserve current one-time reward/result handling. “Race again” is illustrative and is not approved as new functionality by this selection.

![Selected race-results mockup — option 01 Classic dock](ui-overhaul/selected-mockups/race-results-01-classic-dock.png)

## Stage 1 sequence

1. Reuse `UiFactory` premium panel/button atlas, premium icon sheet, existing system font and canonical `VoidlingVisualFactory` portraits. Match reference 02's composition at the existing 640×360 logical viewport (1280×720 desktop window), without changing world art or zoom rules.
2. Replace the bottom dock with a left rail. Open the existing searchable roster beside it. Keep world selection, petting, dragging and follow behavior.
3. Compact the right inspector: identity, stage/personality, qualitative care, five colored rank/level/progress rows, and Give treat / Details / Family / Follow. The actively trained stat's progress bar pulses and updates with live fractional progress; do not restore the removed passive-training text/Stop row. Keep detailed statistics in Details and permanent departure behind Details with both existing confirmations. Treat choice calls the existing training action.
4. Escape first unwinds an open interaction, then opens the Garden menu. Settings remains mouse-accessible from the rail and returns directly to the Garden; Settings opened from the Escape menu returns to that menu. Menus do not pause Garden simulation. Keep camera recovery outside Settings.
5. Add Build → land/training grounds and decorations, plus log Activities → daily check-in and missions. Reuse current feature screens. The legacy Shop links are removed during the Shop stage, when that screen is replaced.
6. Extend the existing Garden runtime smoke to exercise rail destinations, roster selection, inspector, treat inventory consumption, ESC/back/focus behavior, and non-overlapping bounds. Run Debug/Release builds, tests and the Godot checks from CI, then inspect rendered Garden screenshots.
7. Provide the branch, workspace, screenshots, isolated-save launch command and a short player checklist. Do not start stage 2 until the user has tested and asked to proceed.

## Stage 2 sequence

1. Keep the manager's rail visible and usable while the Shop is open; place the ledger window in the remaining screen area.
2. Replace the stacked stall with three stable bands: vertical categories, readable Type/Price product rows, and a persistent purchase area containing selected-item art, ownership, sprout price, and Buy action. The later refinement deliberately removes long descriptions and inventory-note copy.
3. Reuse premium paper/button chrome and premium produce art for treats. Keep egg tint identity, land footprints, stock, prices, rare offers, rotation timing, and the existing purchase use cases unchanged.
4. Preserve the selected category/item across a purchase redraw. Remove daily check-in from the Shop because it now lives under Garden log → Activities.
5. Extend the UI smoke check for rail access, category/item focus, purchases, fixed egg stock during a visit, and bounds; render the actual Shop and stop for Shop playtesting before race-entry work.

## Garden player checklist

- Find/select another Voidling through both the world and rail; search by name/color and use Follow.
- Read all five colored rank/level/progress rows; give a treat; inspect Details and Family; verify the actively trained stat pulses and progresses while the inspector remains open.
- Open every rail destination and return; Build reaches owned ground and decorations.
- Read and scroll the Garden log; Activities reaches rewards and missions.
- ESC opens the Garden menu, Settings returns to it, and returning restores usable focus/selection.
- Verify rail, log and inspector remain readable at 1280×720 and a larger desktop window; long names stay inside their panels.
- Pet/drag/pan around the UI; clicking UI must not select/drop a creature underneath it.

## Shop player checklist

- Open Shop from the manager's rail, then switch directly to another rail destination and back.
- Browse Training treats, Mystery eggs, Land, and Special when a rare offer is present; the selected product should remain clear in the receipt.
- Buy a treat and confirm its owned count and sprouts update without losing the current category or selection.
- Buy an egg and confirm that exact egg slot stays sold out for the rest of the visit while the other egg identities remain unchanged.
- Buy land when affordable and confirm the owned count and sprouts update through the existing purchase flow.
- Use keyboard/controller focus across categories, products, Buy, Close, and the rail; Escape should close the Shop and return focus to Shop on the rail.
- Verify the ledger remains readable at 1280×720 and a larger desktop window without covering the manager's rail.

## Scope and source rules

The mockup is a layout reference, not new gameplay requirements. All balances, current save IDs, rewards, shop rolls and deterministic race outcomes remain authoritative in existing application/domain owners. Reuse premium Sprout Lands art with existing attribution; do not copy base creature atlases into UI consumers. Player-facing additions use localization keys. Existing standalone Settings/Shop/Details and modal ownership remain in place; no generic navigation framework is needed.

The original five-option rationale is in `UI_UX_OVERHAUL_OPTIONS.md`. This plan records the chosen combination and the per-screen test gates.

## Stage 1 handoff

Implemented the premium wood manager's rail, compact profile with persistent controls, real treat chooser, Build and Activities entry points, and ESC → Garden menu → Settings. The roster now opens beside the rail and focuses care after selection. Goodbye remains available through Details with both confirmations. The tutorial highlight follows the new Garden layout. Mouse clicks on the rail cannot place decorations underneath it; edge panning respects UI surfaces.

The Shop still uses its previous window until stage 2. Its legacy shortcuts remain for now; Missions now returns to Activities. Race entry, live race and results still use their previous layouts. No economy, creature simulation, race rules, network protocol or production persistence changes were made.

![Implemented Garden at 1280×720](ui-overhaul/garden-stage1.png)

### Launch the isolated playtest

From this workspace in PowerShell:

```powershell
.\playgame.bat --no-build --voidling-dev-profile=ui_overhaul_playtest
```

This uses a separate development save. On its first launch, choose **Skip** in the tutorial to inspect the Garden immediately, or walk through the updated guide. Start with the Garden checklist above; send feedback before stage 2 begins.

### Verification completed

Local environment: Windows, .NET 8, Godot 4.6.1 Mono (CI uses Linux/Godot 4.6.0). Ran the checks represented in `.github/workflows/ci.yml`:

| Check | Result |
|---|---|
| Restore; Debug and Release builds | Pass; two existing warnings in `TradeNegotiationCoordinator` |
| Domain/application/presentation test project | 236 passed, zero failures |
| Architecture and canonical creature-art boundaries | Pass |
| Localization source and new Garden keys | Pass |
| Godot import and actual main-scene runtime | Pass; bundled asset duplicate-UID warnings remain |
| Garden interactions, actual pointer clicks, ESC, focus return/Tab containment, treat consumption, expanded inspector bounds, continued simulation | Pass |
| Rendered Garden review | Pass at 1280×720 and 1600×900; long name/favorite-food/training state checked |
| Voidling visual, race presentation, race completion and family-tree smoke | Pass |
| Persistence recovery and trade-panel smoke | Pass |
| Two-process LAN handshake and durable trade | Both peers passed and exited successfully |
| `git diff --check` | Pass |

Two existing test probes needed local-verification corrections: persistence corruption/cleanup now resolves the same development-profile path as its repository; the LAN trade probe uses normal Godot shutdown on Windows because exiting the CLR directly from a native callback failed after a completed trade. These changes are probe-only; the existing Linux workaround and production save/trade behavior remain unchanged.

The reusable Garden check is:

```text
godot --headless --path . -- --voidling-garden-ui-smoke --voidling-dev-profile=ui_overhaul_check
```

Add `--voidling-garden-ui-shots` with a graphical renderer to capture the review states. Local verification logs are in `.godot/ui-checks/`; screenshot-probe logs also live in `.godot/`.

### Garden feedback refinement

- Added a premium weather-sheet sun/moon display in the rail's upper-left area, following the same local clock as Garden lighting (dawn, day, dusk, night). When the rail collapses it moves below the Garden name.
- Removed the floating notification line. Toast-only notices now reach the Garden log, while matching toast/event notifications within one deferred batch produce one entry.
- Replaced the expanded rail toggle with a draggable resize edge and horizontal-resize cursor. Dragging below the collapse threshold closes the rail; only the collapsed state shows a centered half-circle expand arrow. The Garden name/sprouts and log slide into the freed space while the day/night dial moves below the Garden name.
- Moved Online into the rail and replaced the upper-right Escape button with premium settings and mute icon buttons at the rail's lower-left edge. The Garden menu now opens only from the Escape key.
- Removed the redundant “Garden log” heading, reduced the log panel artwork to 50% opacity, and added a keyboard-accessible top-left toggle that animates between the full history and a one-line view while preserving the same bottom margin in both states.
- Extended the Garden smoke check with clock boundaries, collapse/expand and interrupted animation, keyboard access, and notification deduplication. All 236 tests and the CI-equivalent local checks passed; graphical review passed at 1280×720.

Review captures: [expanded](ui-overhaul/garden-refined.png), [companion stats](ui-overhaul/companion-stats.png), [collapsed navigation](ui-overhaul/garden-collapsed.png), [one-line log](ui-overhaul/garden-log-compact.png), [land inspector](ui-overhaul/land-inspector.png).

## Stage 2 handoff

Implemented the option 04 Sprout Market composition beside the manager's rail: warm paper window, icon category column, Type/Price product rows, selected-item art, ownership, sprout price, green purchase action, and inventory shortcut. The garden-name eyebrow, prompt/count line, long receipt copy, inventory note, and Escape footer were removed through player feedback. Treats keep the premium produce art; fixed egg tints, per-visit stock, land shapes, rare offers, prices, owned counts, refresh timing, and existing purchase services remain authoritative.

Daily check-in no longer appears in the Shop because it is available through Garden log → Activities. The full-screen shade now covers the Garden and rail while the rail's controls remain usable; Escape returns focus to Shop on the rail. No economy, inventory, save, or simulation rules changed.

Review captures: [training treats](ui-overhaul/shop-treats.png), [mystery eggs](ui-overhaul/shop-eggs.png), [land](ui-overhaul/shop-land.png).

### Launch the Shop playtest

From this workspace in PowerShell:

```powershell
.\playgame.bat --no-build --voidling-dev-profile=ui_overhaul_shop_playtest
```

Choose **Skip** in the tutorial if the isolated profile is new, then open **Shop** from the left rail. Complete the Shop checklist above before race-entry work begins.

Stage 2 passed Debug and Release builds, all 236 tests, architecture/localization checks, Godot import and runtime, Garden/visual/race/family/persistence/trade probes, and both two-process LAN probes. The extended Garden UI smoke also completed the Shop purchase and focus checks and produced the three review captures at 1280×720.

### Shop and Garden refinement

- Removed the garden-name eyebrow, stall prompt and Escape footer from Sprout Market; matched the reference's warm beige paper palette and added item previews to the Treats, Eggs and Land category buttons.
- Clicking the shaded area outside any modal now closes it through `ModalHost`, covering Settings and every rail submenu that uses the shared host.
- Moved the Garden name and numeric sprout balance to the upper right with the premium sprout icon, moved the day/night dial into the former garden-card position, and removed Center.
- Added faint pointer hover outlines for Voidlings and hexes, with the Voidling taking priority when both overlap. Clicking a placed hex now opens its conversion, occupancy and upgrade controls in the right-side Garden inspector without blocking the world.
- Restored per-stat training progress bars in the compact companion inspector. Stat names and fills use their stat colors; the active bar has a visible pulsing outline, includes fractional live progress, and shows its actual `+… EXP/s` rate beside the stat name. The redundant passive/stop row is removed.
- Simplified the Shop catalogue to Type and Price columns, replaced currency wording with the premium farming sprout, and removed receipt description and inventory-note copy.
