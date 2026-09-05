# Voidling UI/UX overhaul: five layout directions

**Date:** 5 September 2026  
**Status:** design exploration; game code is unchanged.  
**Scope:** Garden, Shop, and Racing navigation, information hierarchy, and placement. Five comparable visual directions, each with all three screens; racing also includes entry and results.

Start with **01 · Classic dock**. It gives management-game players a familiar place to find actions while keeping Voidlings central. **05 · Creature first** is the strongest alternative if repeated care and attachment should dominate the experience.

The clickable comparison is shown in the accompanying Codex response. Its garden, names, balances, levels, event times, race positions, and rewards are illustrative. It uses real repository artwork assembled into static scene studies, rather than screenshots of an implemented overhaul. Only the preview's local state changes when a control is clicked.

## What the current interface actually contains

This inventory follows the current presentation code, including destinations reached through the prominent screens. It does not promote older product-plan ideas into new features.

| Category | Current controls and location | Proposed home | Placement reason |
|---|---|---|---|
| Identity and resources | Editable garden name and Sprouts, upper left | Small stable status area | Information to glance at, rather than a row of commands. Keep user names editable and untranslated. |
| Main destinations | Shop, Inventory, Breed, Race, bottom left | One consistently ordered navigation group | These are the recurring care/progression loop. They should stay directly discoverable. |
| Creature selection | Voidlings drawer at bottom right; name/color search; clicking a world creature | Labelled roster entry, or the portrait tray in option 05 | Selection is a frequent action. Keep both world selection and a reliable list for finding a creature. |
| Creature inspection | Selected profile; Details with Stats/DNA/Visual tabs; Family tree | Context inspector attached to the selected creature's identity | Information describes one Voidling, so it should not compete with garden-wide navigation. |
| Care and training | Five stat-specific treat actions; passive-training status and Stop | Selected creature's care area | Put stock, effect, target, and action together. A compact Give treat chooser is shown in the studies; it adds a click compared with the current per-stat buttons and needs testing. |
| Identity and camera context | Rename by clicking the creature's name; Follow; Close profile | Profile header or Details | These actions make sense only after selecting a creature. |
| Permanent removal | Say goodbye and its confirmation flow | Details → Say goodbye, with the existing confirmations retained | Give this much less emphasis than care and family inspection. |
| Camera utility | Center, currently a full button at upper right | Small labelled camera control away from the main dock | Camera recovery is useful, but it is not a destination. Do not put it in Settings. |
| System configuration | Settings button at upper right; Escape also opens Settings | ESC → Garden menu → Settings | Audio, edge panning, and auto-finish preferences are occasional commands. A quiet clickable “ESC · Menu” hint keeps a mouse-accessible route. |
| Reset | Reset inside extended Settings | Settings → Reset garden… → separate confirmation | Keep the existing protection and preserve the distinction from returning to the garden. |
| Social/online | Online → create/join, invite, leave, selected-creature sharing, Trades, Challenges, Daily race, Friends leaderboards | Quiet Friends/Online entry in a corner; actions within their relevant pages | Social play is a parallel activity. Its state should be understandable without filling the garden with network controls. “Friends” is a proposed label; test whether “Online” communicates the connected-garden feature better. |
| Land and training grounds | Modules is inside the shop's daily-check-in panel; ground hex menus handle construction/management; inventory places stored land | Build → Land & training grounds; retain direct hex selection | Buying land belongs in Shop. Managing owned ground belongs in the Garden. “Modules” is an implementation-oriented label. |
| Decoration | Decorate is also inside that shop panel; Place and placed-object actions | Build → Decorate | Arrange the world while looking at it. Do not require a shop visit to use existing decorations. |
| Session information | Persistent scrollable Garden event log | Compact readable log with History | An idle game needs an answer to “what happened while I looked away?” Keep recent sentences visible. |
| Activities/rewards | Claim daily check-in and Missions inside Shop | Activities entry in the log header, with a badge only when actionable | Shopping and checking progression are different intentions. Retain manual claiming; do not add automatic interruption. |
| Shopping | Five training treats; individually fixed mystery eggs; five land footprints; conditional rare offer; owned counts, prices, rotation timer, Buy | Product categories + a selected-item purchase area | Category selection avoids stacking every shelf into a tall scrolling window. Price, effect, ownership and Buy stay together. |
| Inventory actions | Counts; Place stored eggs/land; use incubation skip; sell eggshells; discard failed eggs | Per-item actions in Inventory; contextual shortcuts where relevant | Action verbs depend on the selected item. Do not add all of them to global navigation. |
| Race entry | Course cards, entrant cards, stat preview, Start race | Course + racer choice, then one prominent Start | Players should see the commitment before entering. Keep Run/Swim/Fly/Power/Stamina vocabulary. |
| Live race | Course title; Cheer; stamina; minimap; placement feedback; Escape menu | World centre; stable standings and course information at edges; Cheer near player stamina | Movement is the spectacle. A single active action deserves emphasis; passive information stays peripheral. |
| Race exit/results | Local-race Resume / Quit to garden; finish podium and Return | ESC race menu; results panel after finish | Leaving is always discoverable. Results can occupy the centre once the competition has ended. |

Current entry points: [MainController](../Scripts/UI/MainController.cs), [Daily check-in panel](../Scripts/UI/MainController.DailyLogin.cs), [creature profile](../Scripts/UI/MainController.Profile.cs), [Shop orchestration](../Scripts/UI/MainController.ShopBreeding.cs), [ShopScreen](../Scripts/Presentation/UI/Shop/ShopScreen.cs), [InventoryScreen](../Scripts/Presentation/UI/Inventory/InventoryScreen.cs), [RacePickerScreen](../Scripts/Presentation/UI/Racing/RacePickerScreen.cs), [RaceScreen](../Scripts/Presentation/Racing/RaceScreen.cs), and [race pause menu](../Scripts/Presentation/Racing/RaceScreen.PauseMenu.cs).

Direct manipulation should remain direct: click to select, double-click to pet, drag to move, and select a ground hex to manage it. The overhaul should not replace these with a permanent screen-wide row of creature commands.

## Reference games and the useful patterns

These are specific documented reference layouts, not a claim that every version of every management game has identical UI. The reasons in the last column are our design interpretation.

| Reference | Observed or documented pattern | Translation to Voidling, and why |
|---|---|---|
| **Timberborn — 2021 UI overhaul** | Upper resources, left population information, bottom construction categories, right contextual selection panel; collapsible detail. The accompanying screenshot also shows a lower-left event history. | Separate “how is the garden doing?”, “what can I do?”, and “what did I select?” Fixed edges leave room for world selection. This is the closest structural reference for option 01. |
| **Cities: Skylines — original PC manual** | Categorised service/build tools along the lower edge; management information around the view; Escape exits the current tool/menu and also opens the system menu. | Group related tools and unwind the current interaction before opening system commands. This supports a shared Build entry and predictable Escape behaviour. |
| **Anno 1800 — UI design devblog** | UI designers explicitly prioritise functional hierarchy, reducing window browsing and unnecessary clicks, and limiting decoration that competes with the world. | Premium decoration should frame controls without weakening labels. Put an action beside the information required to choose it, especially buying and race entry. |
| **Planet Zoo — official basics guide** | Clicking an object opens relevant interaction options; Escape/right click cancels or closes; PC camera and management shortcuts are documented separately. | Creature inspection should begin with the creature. Keep camera operations distinct from management destinations, and preserve a consistent way back. |

Sources: [Timberborn's developer UI breakdown and images](https://store.steampowered.com/news/posts/?appids=1062090&enddate=1631631610&feed=steam_community_announcements), [Cities: Skylines PC manual, especially UI and controls](https://cdn.akamai.steamstatic.com/steam/apps/255710/manuals/CitiesSkylines-UserManual_EN.pdf), [Anno 1800 UI devblog](https://www.anno-union.com/devblog-user-interface-2/), [Planet Zoo: The Basics](https://www.planetzoogame.com/es-ES/centro-de-ayuda/guias-jugador/the-basics).

We should borrow the organisation, not entire HUDs. Voidling does not need a city-builder resource wall, a real-world clock, or prominent simulation-speed controls. Its three central pillars are raising, breeding and racing, and its [gameplay context](GAMEPLAY_DESIGN_REFINEMENT_CONTEXT.md#11-ui-information--feedback) already requires a visible event log, stable layout and continued garden simulation while menus are open.

## The five visual directions

### 01 · Classic dock — recommended starting point

**Garden:** garden name and Sprouts at upper left; Friends and a quiet ESC entry at upper right; a labelled bottom dock for Voidlings, Inventory, Shop, Breed, Races and Build. Selected-creature details occupy the right side. Recent events remain at lower left. The camera control is small and separate.

**Shop:** a centred market window. Category tabs run across the catalogue; products occupy the left side; the selected item, effect, owned count and Buy occupy the right. The wallet remains at the window's upper edge. Closing returns to the same garden selection.

**Racing:** course and racer are reviewed together before Start. During the race, standings sit upper left, course title above, Cheer and stamina together at bottom centre, and course progress at bottom right. Results become a central podium.

**Why choose it:** strong city-builder familiarity without burying care. **Cost:** six dock entries need a deliberate width budget; the right inspector needs a compact state for smaller windows. A two-step product selection/purchase replaces direct per-card buying for most items.

### 02 · Manager's rail — predictable repeated navigation

**Garden:** a permanent left column holds the main destinations. The status block starts beside it, the world remains central, the inspector sits right, and the log occupies the lower edge of the remaining world area.

**Shop:** the same left navigation remains visible. Products become compact rows/cards beside a persistent purchase inspector. There is a clear distinction between global destinations and local product categories.

**Racing:** entry uses the same management structure. Once the race starts, the left column becomes standings, while Cheer and course progress form a right control column. Garden destinations return after the race.

**Why choose it:** easy to learn and expand with existing destinations; good for frequent switching. **Cost:** persistent chrome reduces world width. Active-race navigation must deliberately change so Shop and Build cannot distract from an ongoing race.

### 03 · Corner studio — a spacious garden with task groups

**Garden:** identity and roster access live upper left; Breed and Races form a compact upper-right group; Inventory, Shop and Build sit bottom right. The log stays bottom left. Selection opens a compact lower-centre care card; full stats are one step deeper.

**Shop:** a drawer occupies the right half. The garden remains visible on the left. Categories and products sit above a compact item explanation and purchase action. This is useful for buying supplies and immediately returning to the creature you were watching.

**Racing:** course identity moves upper left, course progress upper centre, standings lower left, and Cheer/stamina lower right. The centre remains devoted to the track.

**Why choose it:** preserves the most open middle area and suits short idle check-ins. **Cost:** destinations are split between two corners, so discovery is less obvious. The narrow shop has less room for descriptions and long translations.

### 04 · Keeper's ledger — information in stable bands

**Garden:** navigation becomes a top strip. Selection opens a wide lower inspector with identity, stat comparison and care actions arranged horizontally. The lower-left log has its own defined space.

**Shop:** a broad catalogue beneath the top strip. Categories are vertical on the left, products appear in a readable list, and a receipt-like purchase area sits right. This is the clearest price/stock scanning option.

**Racing:** entry retains the top navigation. During the race, the lower band contains positions, player action/stamina and course progress in adjacent sections. Results use a broad central panel.

**Why choose it:** strong table/list readability, stable focus movement, fewer floating blocks. **Cost:** it consumes more vertical space. It is the least suitable direction for a short window with the garden continually in view.

### 05 · Creature first — care and attachment lead

**Garden:** a persistent portrait tray at lower left identifies the currently selected Voidling. Its care panel continues along the bottom. Global destinations form a compact right column; the event log stays visible under the upper-left status area.

**Shop:** a small product shelf sits beside a larger, friendly item presentation. The selected item's effect and Buy receive the strongest emphasis. Buying still adds to Inventory; it does not automatically feed whichever creature is selected.

**Racing:** entrant selection takes precedence in the entry layout. The live HUD centres the player's portrait, position, stamina and Cheer in a single lower panel. Opponent standings sit right and course progress sits lower left.

**Why choose it:** the game feels about caring for named creatures, and switching between the visible few is quick. **Cost:** a large roster will need the existing searchable list behind a More/View all entry; do not turn the tray into dozens of tiny portraits. The more generous product art reduces catalogue density.

## Shared interaction decisions

- **Escape closes the current layer first:** dropdown/chooser or placement mode, then modal/selection, then the system menu. Restore focus to the control that opened the closed layer. Keep a mouse-accessible menu entry. Shortcut labels in a later build should come from the active bindings.
- **Garden menu is not a pause promise.** Use “Garden menu” and a short indication that the garden continues. A local live race already pauses through its own menu; preserve that behaviour. An online race must not claim to pause everyone when only one player's menu opens.
- **Selection is stable.** Closing the shop or a detail subpage should preserve the creature, category, and useful scroll position. Escape must never accidentally trigger a purchase, goodbye, reset or race abandonment.
- **Give every persistent element one job.** Status is status; destinations change context; selected-object controls act on that object; system options live in the system menu.
- **Do not hide essential care in ESC.** Settings is infrequent. Feeding, training, breeding, inventory and races are the game.
- **Show current trained values separately from inherited potential.** The study labels Rank and Level separately. Details remains the home for complete DNA and visual information. Do not invent numeric happiness bars or an exact offspring probability calculator.
- **Use events rather than surprise popups.** Keep the Garden log readable and retain its scrollable history. Activities can expose missions/check-in without moving everything into a giant notification centre.
- **Distinguish navigation from confirmation.** Buy is beside the product effect/price; Start race follows course and entrant; Goodbye/Reset retain their own confirmation flows.
- **Do not accidentally refill a bought egg slot during a UI redraw.** Keep the current Shop opening/refill boundary and item IDs. A purchased egg is unavailable for another purchase during the visit.

“Help & controls,” “Quit to desktop,” “Race again,” an Activities entry, a unified Build entry and the live standings panel are proposed navigation/presentation additions. They are not claims that all these exact controls already ship. The studies do not decide new economy, genetics, race or lifecycle rules.

## Reusing the premium assets

The palette remains warm paper, muted green and soft wood. The different layouts share a visual vocabulary so this comparison measures placement rather than five unrelated art directions.

| Asset in this repository | Proposed use |
|---|---|
| `Assets/Sprout Lands - UI Pack - Premium pack/UI Sprites/buttons/square/Small Square Buttons.png` | Existing panel and button nine-slice frames; normal/selected states. |
| `Assets/Sprout Lands - UI Pack - Premium pack/UI Sprites/Icons/All Icons.png` | Darker high-contrast variants of shopping, heart, trophy, creature, inventory and utility glyphs, always paired with labels where meaning is uncertain. |
| `Assets/Sprout Lands - UI Pack - Premium pack/UI Sprites/Dialouge UI/Premade dialog box medium.png` | Available for a later selected-creature/confirmation frame treatment; not required for the layout decision. |
| `Assets/Sprout Lands - UI Pack - Premium pack/emojis/emoji style ui/Inventory_Spritesheet.png` | Available tray/block states for further polishing option 05. Decorative hearts must not imply a newly visible happiness meter. |
| Premium sprite pack `Tilesets/ground tiles/New tiles/Grass_tiles_v2.png` and `Soil_Ground_Tiles.png` | Garden and race world surfaces in the studies. |
| Premium sprite pack `Objects/Trees, stumps and bushes.png` and `Objects/signs.png` | Existing world decoration, keeping UI proposals in the game's current visual context. |
| `Resources/Presentation/Voidlings/DefaultVoidlingVisual.tres` | The source used to resolve the existing base, wing and crown files for the mock. A game implementation continues to use `VoidlingVisualFactory` and the shared portrait/mutation/ground components. |
| Basic sprite pack `Objects/Egg item.png` | Current shop egg art, retained for continuity. |

Treat packets and land footprints are simple programmatic displays, as in the existing Shop. The prototypes use Segoe UI/system text for clarity, matching the current `UiFactory`; the pack's bitmap alphabet is better reserved for occasional short decorative headings after legibility testing. Scale pixel borders and sprites cleanly, and scale text independently enough to remain readable.

**Credit:** Sprout Lands assets by Cup Nooble. The premium pack permits use in projects and modification, requires credit, and does not permit redistributing the asset pack itself. Retain the repository's bundled licence/read-me terms when sharing a derivative project.

## Refinement and implementation plan

1. **Choose the layout family, then test the task flow.** Start with 01 and compare it directly with 05. Ask a player to find a named creature, give a specific treat, locate decoration, buy an egg, start a race, leave a race and change audio. Record wrong turns and extra clicks. These are hypotheses, not measured usability results.
2. **Move navigation without changing use cases.** Add the system-menu layer; remove the prominent Settings button; relocate Modules/Decorate/Missions from the shop check-in panel. Reuse existing handlers, `ModalHost`, `UiFactory` and feature screens. This should be a small Presentation migration, not a navigation framework.
3. **Lay out the Garden with anchors and containers.** Replace the relevant fixed-position top-bar/dock/inspector arrangement with the selected regions. Keep world hit testing, camera panning, drag/pet input, event log and focus rules intact. Preserve the canonical creature visual pipeline.
4. **Recompose Shop.** Keep existing `ShopScreenState`, purchase events, pricing, egg identity/rotation, inventory effects and error handling. Introduce category selection and the chosen purchase layout. Show sold-out, unavailable rare offer, insufficient-sprouts and empty-stock states clearly.
5. **Recompose Racing.** Start with `RacePickerScreen`, then the live `RaceScreen` HUD and results. Reuse simulation snapshots/available position information. Keep Cheer semantics, auto-finish, local pause/exit behaviour, reward application and multiplayer synchronisation unchanged.
6. **Verify the chosen layout at implementation time.** Run the repository's full `.github/workflows/ci.yml` Godot/.NET checks and touched UI/architecture tests. Extend the existing Garden/Race smoke probes only for meaningful navigation and layout regressions. Use the existing isolated development save profile for screenshots.

Acceptance for the first implementation:

- The main care/progression destinations remain visible, labelled and keyboard reachable.
- Settings is accessible from the ESC menu; the garden-running message is accurate.
- Build/Decorate and Missions no longer require shopping.
- Every displayed item has a clear target, price/effect and enabled/disabled explanation; eggs retain stable stock identity.
- No HUD region overlaps another required region at the supported desktop window sizes; smaller windows use compact states rather than unreadably shrinking all text.
- Typical body text is at least 16 display pixels in the 1280×720 presentation, with larger important headings/actions. Use effective pointer targets of at least 32 pixels, visible keyboard focus and non-colour-only selected states. Expand target size for coarse-pointer support if it becomes a supported platform.
- Test long player names, more than three Voidlings, longer translated labels, empty inventory, low funds, incubation/failed eggs, unavailable online services and a race in progress.
- Closing a nested view restores selection/focus and never performs a destructive action. A confirmation overlay blocks world input underneath it.
- No changes to saves, deterministic genetics, rewards, racing balance or the canonical visual architecture are bundled into the layout work.

Current [architecture](../ARCHITECTURE.md), [execution contract](AGENT_HANDOFF_IMPLEMENTATION_PLAN.md) and [visual pipeline](architecture/VOIDLING_VISUAL_ASSET_PIPELINE.md) remain authoritative. Update migration status only when the selected implementation actually moves a subsystem boundary.

## Validation of this exploration

The current Garden screenshot probe completed successfully with an isolated development profile. The comparison was rendered across all five designs and all three requested screens, plus race entry/results. Preview checks cover category/item selection, purchasing in local mock state, selecting a course/racer, race start, Cheer feedback and Escape. Desktop and narrower conversation widths are inspected separately; narrow previews reflow for review and do not establish a mobile product target.

The surrounding comparison controls are not game HUD elements. Secondary destination dialogs show their proposed home and enough content to demonstrate navigation; they are not finished implementations of breeding, trading, inventory or settings. No production C#, scenes, resources or save schema were edited for this exploration.
