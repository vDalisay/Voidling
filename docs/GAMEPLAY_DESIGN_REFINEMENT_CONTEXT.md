# Voidling Gameplay & System Design Refinement Context

**Status:** Living player-facing product/design specification  
**Purpose:** Refine the technical implementation plan with concrete gameplay intent, progression rules, player information, UX expectations, and MVP priorities.  
**Companion:** `docs/GENETICS_BREEDING_HATCHING_RACING_IMPLEMENTATION_PLAN.md`  
**Primary references:** Sonic Adventure 2 Battle Chao Garden / Chao World Extended for creature raising, genetics, lifecycle and racing; Digimon Championship DS for modular Garden/passive-training concepts. All final content, naming, art, tuning and implementation remain original to Voidling.

> This document is the source of truth for player-facing intent established during the design interview. The technical implementation plan remains the source of truth for architecture where it does not conflict with these requirements.

---

## Interview progress

The normal refinement format is **20 primary questions plus 5 follow-up questions** generated from ambiguity in the answers.

| Section | Status |
|---|---|
| 1. Core fantasy & gameplay loop | Complete |
| 2. Genetics & inherited potential | Complete |
| 3. Appearance & rare bloodlines | **Partial — only 5 primary questions completed** |
| 4. Personality, preferences & individuality | Complete |
| 5. Garden & environment | Complete |
| 6. Progression, unlocks & long-term play | Complete |
| 7. Races, cups & competition | Complete |
| 8. Raising, training, stats & care | Complete |
| 9. Economy & rewards | Complete |
| 10. Session flow, tutorial & Garden event log | Complete |
| 11. UI, information & feedback | Complete |
| 12. MVP scope, priorities & technical readiness | Complete |

The appearance section still needs its full 20 + 5 pass. Breeding, lineage/inbreeding, eggs/hatching, lifecycle/reincarnation, exact Garden-module design and exact race/economy tuning also still benefit from dedicated deep dives even though many requirements are already fixed below.

---

# 1. Core fantasy & gameplay loop

## 1.1 Core fantasy

Voidling should capture the appeal of a **Chao Garden-style creature-raising game** built around three equally important pillars:

1. raising/caring for Voidlings;
2. breeding Voidlings;
3. racing Voidlings.

Breeding should carry slightly more strategic weight because it is the long-term mechanism for improving bloodlines and pursuing collection goals.

## 1.2 Self-directed projects

Players should invent their own projects rather than follow one universal objective. Examples include:

- breed an all-S-rank Voidling;
- discover every color or color combination;
- combine a favorite appearance with S-rank potential;
- build a race specialist or all-round champion;
- create prestigious bloodlines;
- pursue mutations and extremely rare appearances;
- produce a late-game immortal/trophy Voidling.

The game is **open-ended**. Completing one goal should naturally create another.

## 1.3 Idle-first structure

Voidling is a cozy desktop idle game intended to remain open while the player is doing something else.

The player can intermittently:

- inspect Voidlings;
- feed/train them;
- change passive-training zones;
- breed;
- check eggs and timers;
- enter races;
- buy from the shop;
- rearrange the Garden;
- then return attention elsewhere.

Mechanical idle progress currently happens **while the game is open**, not while closed.

## 1.4 Breeding as progression

Breeding is not merely optional side content. Later CPU racers should eventually become strong enough that players with weak starting genetics need improved bloodlines to keep progressing.

Intended high-level loop:

```text
Raise → race → earn rewards/unlocks → breed toward a goal → raise offspring → tackle harder content
```

## 1.5 Deliberate player-controlled breeding

Breeding must never happen autonomously.

- exactly two parents;
- both must be adults;
- no friendship requirement;
- selected through the breeding menu;
- player confirms the pairing;
- breeding should have a pleasant animation/presentation;
- egg is created immediately;
- parents receive a cooldown of roughly a few hours.

## 1.6 Long-term trophy form

A difficult Chaos-Chao-like endgame achievement is desirable without copying the exact source requirements.

The trophy Voidling should:

- require long-term/multi-lifecycle effort;
- become permanent/immortal;
- retain its final appearance/color;
- no longer be able to breed;
- still be able to race;
- remain only as strong as its actual stats;
- be eligible for special late-game races.

Exact transformation requirements remain unresolved.

---

# 2. Genetics & inherited potential

## 2.1 Two visible DNA profiles

Every Voidling has two visible breeding/DNA profiles. These are the normal source of inheritable stat potential and may differ from the Voidling's current trained stats.

There should be **no secret third normal-stat inheritance layer**.

## 2.2 Parent-only normal inheritance

For now, normal inherited stat outcomes come from the two selected parents only.

Grandparents/deeper ancestors matter for lineage/inbreeding but should not independently reintroduce normal stat values in v1.

## 2.3 Random but understandable inheritance

Breeding outcomes remain random, but they must make sense from parental DNA.

The player can inspect both DNA profiles but should **not** receive an exact offspring probability calculator.

## 2.4 Rare +1 rank breakthrough

A child may very rarely exceed the expected parental rank by **one rank**.

Example: B-level parental potential may rarely yield A.

Constraints:

- maximum normal breakthrough is +1 rank;
- weak E-level parents cannot suddenly produce S from nowhere;
- chance should be genuinely low;
- no item or direct mechanic guarantees/manipulates this chance for now.

## 2.5 Stat ranks and growth ceilings

Current rank scale:

```text
E → D → C → B → A → S
```

The rank defines the stat's growth ceiling. S is highest for now.

Rank itself should not be freely increased through ordinary training. Higher potential comes through breeding and major lifecycle progression such as adulthood/evolution or reincarnation.

## 2.6 Luck remains important

Even expert breeders should not be able to fully eliminate luck. Skill comes from choosing parents, understanding DNA, managing generations and deciding which offspring to continue with.

## 2.7 Hard consequences

Bad breeding/training choices may be permanent. The game does not need a universal repair/reset feature.

The final escape hatch is **Goodbye**: send away unwanted Voidlings and start a new line.

## 2.8 Inbreeding consequence for now

For now, inbreeding directly affects **hatch-failure risk only**.

Do not initially add diseases, deformities, stat degradation or personality penalties.

The risk percentage should be visible through family-tree/lineage presentation. The existing implementation-plan burden ladder remains the baseline until changed by a dedicated lineage pass.

## 2.9 Stats can affect ambient behavior

Stats can subtly influence autonomous Garden behavior. A strong Swim Voidling may choose water more often, for example.

This does not need an explicit tooltip; players can discover it through observation.

## 2.10 Stat-driven morphology

Appearance may gradually reflect how a Voidling is raised, similar in spirit to Chao development.

```text
stat development → appearance change
```

The visual change itself is not a bonus source; it reflects the underlying development.

---

# 3. Appearance & rare bloodlines

> **Incomplete interview section.** The following direction is confirmed, but detailed inheritance still requires the remaining questions.

## 3.1 Appearance is a major breeding goal

Players may deliberately breed for:

- missing colors;
- new color combinations;
- every visual type in every color;
- desired appearance + desired stats;
- rare mutations;
- prestige/trophy forms.

## 3.2 Extremely rare looks

Some appearances should be genuinely mythic/prestigious. Players can grind toward them, but success still depends on patience and luck.

There is no universal pity system for the rarest outcomes.

## 3.3 Rare appearance can represent a special state

A rare look may have gameplay meaning when it represents a lifecycle state such as the immortal/trophy form. Its special rules may include immortality and inability to breed, but it should not receive arbitrary hidden race power simply because it is rare.

## 3.4 Still unresolved

A future full appearance pass must define:

- ordinary color inheritance;
- pattern rules;
- shiny/special coats;
- mutation rates;
- rare-trait transmission depth;
- discovery/collection encyclopedia behavior;
- whether undiscovered combinations are hinted;
- stacking of rare traits;
- exact trophy-form appearance rules.

---

# 4. Personality, preferences & individuality

## 4.1 Purpose: atmosphere and attachment

Personality initially exists mainly to make Voidlings feel like individuals. It should support charm, cozy observation and emotional attachment rather than become a major optimization layer.

Personality should not affect racing in v1.

## 4.2 Core personality vs treatment

Voidlings can have a relatively stable core personality while player treatment influences current demeanor.

Positive actions:

- petting;
- feeding;
- gentle handling.

Negative actions:

- throwing;
- repeated mistreatment.

Later positive treatment can repair some negative demeanor, but should not rewrite the underlying core personality.

## 4.3 Feedback without exposing formulas

A Voidling should visibly react when it dislikes something—angry expression, icon, animation, etc.—without showing messages such as `Happiness -3`.

## 4.4 Profile flavor text

A short sentence in the profile can describe observable personality/demeanor. It may change over time and should be cozy/flavorful rather than clinical.

## 4.5 Favorite food

Each Voidling may have a favorite food.

- discovered by trial and error;
- once discovered, recorded in the profile/DNA information;
- favorite food grants slightly better stat gain.

## 4.6 No relationship simulation initially

Voidlings may interact through ambient animations, loosely matching personality, but no friendship/rivalry meters are required in v1.

---

# 5. Garden & environment

## 5.1 Garden role

The Garden is both a management space and a personalized cozy desktop environment.

Target feeling: **calm + anticipation**. It should be pleasant to leave open without constantly demanding attention.

## 5.2 Modular grid/zone concept

Use a Digimon Championship DS-inspired modular Garden system.

- one overall Garden;
- predetermined grid/hex-like modules;
- player chooses which modules to buy/unlock;
- player chooses placement and upgrades;
- module choice determines visual themes and may affect passive stat training.

Geometry resolved: a flat-top hex 210x180 world units, roughly a Voidling's living space. The island
starts as one free hex of plain ground; the shop sells pieces of one to three connected hexes that
must be placed touching the island, and a placed hex is turned into training ground for one stat
separately. One Voidling trains per training hex. Rates remain as authored in `GameBalanceRules`.

## 5.3 No routine cleaning chores

Cleaning/waste-management is not a required core mechanic.

## 5.4 Hard Voidling capacity

The Garden has a hard population limit. Exact capacity is unresolved.

When full, show a direct message that one Voidling must leave before another can be added.

## 5.5 Free roaming and decorations

Voidlings roam available Garden areas freely.

Decorations:

- freely placeable;
- may have a practical performance/readability cap;
- can trigger autonomous or player-triggered flavor animations;
- decorative interactions do not provide meaningful stat bonuses.

Functional training modules are separate from decorations.

## 5.6 Real-world presentation

The Garden visually follows the player's real local time and real-world seasons.

- open at night → Garden appears at night;
- seasonal visuals follow season;
- later seasonal animations such as snow/leaves may be added;
- these seasonal effects are initially cosmetic.

## 5.7 Closing the game

Closing the game should not create mechanical neglect punishment.

Open-game time drives current progression; closed time does not silently grant the same progress and should not damage the Voidlings either.

## 5.8 Quiet audio direction

No continuous idle music is required. The game should remain quiet in the background, with sound effects for active interactions.

## 5.9 Timers are desirable

Visible countdowns such as egg incubation are useful because they create a feeling of productivity and anticipation without demanding constant input.

---

# 6. Progression, unlocks & long-term play

## 6.1 Connected progression

Progress comes from a combination of:

- money;
- acquiring/hatching Voidlings;
- breeding;
- stat raising;
- races/cups;
- Garden modules/upgrades;
- cosmetics/prestige.

## 6.2 Grind is intentional

Major goals should require time, repetition and planning. Progress may stall temporarily because of money, genetics or luck; this is acceptable in an idle game.

## 6.3 Daily rhythm

The game should reward both:

- frequent check-ins;
- long open-game idle sessions.

Daily structure can include login chains, daily missions, shop rotations and occasional special opportunities, but the player's overall project remains self-directed.

## 6.4 Shop structure

- core/basic items are always available;
- rarer items rotate;
- egg inventory follows a predictable slot structure while exact eggs vary;
- rare convenience items such as a full incubation skip can appear infrequently;
- approximately hourly rotation is a working idea, not yet a locked tuning value.

## 6.5 Endless progression

No universal end state. Players continue inventing goals across stats, collections, racing, cosmetics, Garden expansion and prestige lines.

## 6.6 Unlocks should feel earned

The central progression feeling is:

> I worked toward this and earned it.

Luck can matter, but sustained effort should remain visible in success.

## 6.7 Seasonal events

Seasonal events are desirable and may include cosmetics, login calendars and temporary rewards.

---

# 7. Races, cups & competition

## 7.1 Stats first, randomness for tension

Race outcomes should primarily reflect raised stats. Random events exist to keep races exciting rather than erase preparation.

The reference example is a small chance to fall while running, creating only time loss.

A provisional example mentioned was roughly a 0.5% fall check every ~3 seconds; treat that strictly as a tuning placeholder.

## 7.2 Racing drives progression

Races should be encouraged through meaningful rewards/unlocks. Winning higher cups may unlock new content, medals or progression gates.

## 7.3 Standard races vs cups

- ordinary races generally available;
- major/special championship cups can be time/rotation limited;
- future multiplayer may include daily races.

## 7.4 Cheer/stamina mechanic

Keep the existing Chao-Garden-like Cheer mechanic:

- stamina drains naturally;
- Cheer spends additional stamina;
- empty stamina slows the Voidling;
- Cheer is a timing/resource decision.

No additional complexity is required here for now.

## 7.5 Repeatable races

No general race-attempt limit is required.

## 7.6 Race length and course complexity

Race duration can vary. Higher cups may be longer and contain more obstacles/segments.

## 7.7 Cup NPC identity

Normal races may use randomized opponents.

Each cup should instead have a stable recognizable NPC cast with names and small personalities/flavor. A cup's cast remains the same inside that cup; different cups can have different casts.

## 7.8 Financial stakes

Major cups may charge an entry fee. Losing can therefore hurt lightly.

Race rewards should not become the best way to farm money, because idle income is the main currency engine.

Current preferred reward direction:

- entry fee creates stakes;
- prize value is primarily item/medal/trophy/unlock;
- exact refund behavior is unresolved because both winner-only refund and placement-based partial refund were discussed.

## 7.9 Race feeling

Racing should feel like watching a horse race involving a creature the player personally invested in:

- preparation matters;
- small random incidents keep it tense;
- winning feels like validation of breeding/training.

Later multiplayer can add local/global leaderboards and player competition.

---

# 8. Raising, training, stats & care

## 8.1 Active and passive training

Use both:

- active feeding/items;
- passive Garden training zones/modules.

Active training is always faster/more effective than passive training.

Passive zone training continues gradually until the player removes/stops the Voidling.

## 8.2 Open-game passive progression

Passive training happens only while the game is open under the current idle model.

There is no arbitrary daily training cap. Practical limits are money, items, time and the rank-defined stat ceiling.

## 8.3 Rank-based stat caps

The rank E-S is the hard growth ceiling for each stat. An A-rank stat caps below an S-rank stat.

Normal training cannot exceed the ceiling.

Rank improvement should happen through major lifecycle/genetic systems, including breeding, adulthood/evolution and reincarnation rather than ordinary consumables.

## 8.4 Food and Stamina

Food should support Stamina growth in a Chao-Garden-like role. Favorite food gives slightly better gain.

## 8.5 Strategic training items

Stats do not universally oppose each other, but specific powerful items may:

- give a large boost to one stat;
- subtract from another stat.

This creates deliberate item-level tradeoffs.

## 8.6 Item/stat information should be understandable

Players should be able to see numeric stat information similar in spirit to Chao Garden. Training effects should be understandable enough for deliberate min-maxing rather than fully hidden.

## 8.7 Irreversible choices

If a player invests in the wrong stat or uses an item with a drawback, there is no required refund/reset. Both small and large mistakes may be permanent for that Voidling.

## 8.8 Immediate active training

Using an active item applies its effect immediately. Passive zones are the slow idle alternative.

## 8.9 Hidden happiness and care consequence

Care has a major lifecycle effect through a completely hidden happiness value.

Working concept:

- approximate range 0-100;
- starts at 0;
- positive treatment such as petting/feeding raises it;
- throwing/mistreatment lowers it;
- very long periods without attention while the game is actively running may lower it;
- no visible happiness meter.

At lifecycle end, insufficient happiness may cause death instead of reincarnation.

Exact values/thresholds remain unresolved.

---

# 9. Economy & rewards

## 9.1 Main income source

The Garden slowly generates money while the game is open, approximately as a passive per-minute stream.

There is no daily earning cap.

## 9.2 Possible active-computer-use bonus

A possible idea was earning somewhat more when the player is actively using their computer—for example typing—than when fully unattended.

This is **not implementation-ready** and requires privacy/platform/UX evaluation. The stable requirement is simply that open-game idle time earns currency.

## 9.3 Eggshell sales

After an egg hatches, its shell can be sold once. Rare egg shells can be more valuable.

## 9.4 Daily login chain

Daily login chains are desirable and upcoming rewards should be visible/predictable.

Daily rewards should be fair rather than wildly randomized.

## 9.5 Daily missions

Daily missions are desired. Most can be short, while occasional longer objectives are acceptable.

## 9.6 Currency rules

- one currency for now;
- no wallet cap;
- fixed item prices;
- no simulated inflation;
- no loans/debt;
- no generic random cash jackpot layer.

## 9.7 Cosmetics and spending

Cosmetics may cost money and remain useful late-game purchases, but should exist because customization is enjoyable rather than purely as a currency sink.

Players choose whether to save for expensive goals or make frequent small purchases.

## 9.8 Race money

Entering major races/cups can lose money.

Exact refund behavior remains unresolved:

1. winner gets entry fee back; or
2. partial refund depends on placement, with no consolation for the bottom placement.

Daily rewards should remain predictable even though races, rare genetics and rotating shop opportunities may swing strongly positive or negative through chance.

## 9.9 Multiplayer trading later

Player trading may be added when multiplayer exists. The initial economy remains single-player and should not be designed around a speculative player market.

---

# 10. Session flow, tutorial & Garden event log

## 10.1 Desired engagement

The desired core loop should feel **compelling/"addictive" in the positive game-design sense**: the player continually has another personally chosen goal to work toward.

The recurring pull comes from:

- hatching new Voidlings;
- strengthening existing Voidlings;
- breeding toward better stats/looks;
- spending time with individual Voidlings;
- setting self-directed collection/racing/breeding goals.

## 10.2 Emotional and mechanical attachment

The player's bond with a Voidling should be both:

- **mechanical:** stats, breeding value, race history, lifecycle;
- **emotional:** time spent caring for and observing that individual.

Time spent in the Garden is meaningful even when the player is not optimizing something.

## 10.3 Idle time still feels active

Quiet periods should arise naturally rather than being forced as explicit downtime.

Even when the player is not clicking:

- money is accumulating;
- passive training may be progressing;
- egg/cooldown timers may be moving;
- Voidlings continue ambient behavior.

The player therefore feels that something is happening without active input.

## 10.4 Garden event log

The Garden should contain a scrollable event/chat-style log for important session events.

Important events include:

- egg created through breeding;
- egg becomes ready/hatches;
- Voidling finishes a meaningful training event;
- Voidling is in a dangerously poor state / nearing lifecycle failure;
- Voidling dies;
- Voidling enters a cocoon before reincarnation/death;
- occasional positive flavor events such as a Voidling being especially happy.

The cocoon's presentation/color can help communicate whether the lifecycle event is heading toward reincarnation or death, similar in spirit to Chao Garden.

## 10.5 Log behavior

- scrolling history;
- session-based history is sufficient;
- no player-authored notes;
- no forced session-summary popup;
- player can simply scroll backward;
- target maximum history: **300 log sentences/messages** before older entries fall out.

The log may use light flavor text, but its primary job is to tell the player what happened in the Garden while their attention was elsewhere.

## 10.6 Tutorial

The tutorial should:

- start automatically on the first launch;
- be skippable;
- remain relatively basic;
- show each major screen once;
- combine text, visual highlighting and guided click-through interaction;
- teach the minimum needed to navigate the game, then allow discovery.

The game should not rely on a large permanent hint/tutorial system after that.

---

# 11. UI, information & feedback

## 11.1 Always-visible Garden information

The desktop Garden should keep the essential interface visible:

- Voidlings/world view;
- current money;
- Garden event log;
- the necessary navigation/actions around the Garden and grid system.

Visibility customization may be added, but the baseline uses a stable fixed layout rather than freely movable panels.

## 11.2 Desktop-first

Mobile is not a current priority. The interface is designed for desktop first.

It must scale automatically across desktop resolutions.

## 11.3 Visual direction

UI should feel:

- cozy;
- pastel-colored;
- moderately playful;
- clearly readable.

When character and readability conflict, **readability wins**.

Panels should be visually bounded/defined rather than heavily transparent floating overlays.

## 11.4 Notifications

Important lifecycle/egg events should normally appear in the Garden log rather than as disruptive pop-up windows.

## 11.5 Accessibility

Accessibility/colorblind support is desired, but can arrive after the earliest prototype rather than blocking initial internal playability.

## 11.6 Audio feedback

Use both visual feedback and sound effects.

No continuous music is required for the idle experience.

Desired volume controls:

- master volume;
- sound-effect volume;
- UI sound volume.

A dedicated mute hotkey is not required.

## 11.7 Input remapping

Keyboard controls should eventually be remappable, but full remapping can be a full-release feature rather than an MVP blocker.

## 11.8 Game continues running

Opening menus/settings should not imply that the world has stopped. The idle simulation is generally expected to continue running.

There is no requirement for a separate prominent in-game real-world clock; the player's computer already provides the time.

---

# 12. MVP scope, priorities & technical readiness

## 12.1 MVP identity

The first internally playable version must already feel recognizably like the intended Chao-Garden-inspired experience rather than a disconnected collection of technical prototypes.

The MVP should include the core loop of:

- acquiring/buying eggs;
- hatching;
- raising/training;
- breeding;
- Garden/grid passive training;
- racing;
- shop purchases;
- lifecycle/reincarnation;
- economy/save/settings support.

## 12.2 Breeding is MVP-critical

Breeding cannot be postponed to a later version. It is one of the central pillars and should work in the first meaningful internal build.

## 12.3 Reincarnation is MVP-critical

Reincarnation should also be present and **meaningfully implemented**, not just represented by a placeholder button/state.

The tutorial is more expendable than reincarnation if schedule pressure appears.

## 12.4 Tutorial quality can be modest

The first tutorial can be functional rather than highly polished. Full multiplayer is also later, although its technical foundation cannot be ignored.

## 12.5 Shop MVP

A simple working shop is enough initially. It must support the real economy loop, eggs and stat-improving items, but does not need every future shop feature.

## 12.6 Training must be real

Training in the MVP must actually alter stats and therefore meaningfully affect race performance. Passive training zones must be functional rather than decorative placeholders.

## 12.7 Economy

The MVP economy should be **roughly balanced enough to play**, with later manual fine-tuning expected.

## 12.8 Autosave

The game should always autosave.

Desired approach combines:

- frequent/continuous background persistence where safe;
- explicit save points/checkpoints after meaningful mutations;
- a small saving indicator when appropriate.

The player should not need to manage manual saves for normal play.

## 12.9 Multiplayer technical foundation

Full multiplayer can arrive later, but the MVP should prove that the chosen architecture can support it.

Current requirement:

- use Steamworks as the intended platform/network ecosystem;
- research what is free/common/recommended for Steam idle games and small multiplayer experiences;
- establish the architecture with multiplayer expansion in mind;
- perform at least a **simple real connection test** rather than leaving multiplayer entirely theoretical.

The exact Steamworks networking/lobby architecture is still a research task and should not be guessed in implementation planning.

## 12.10 Open-ended MVP

The MVP does not need a final victory screen or hard completion condition. The game remains open-ended.

## 12.11 Art strategy

Use the asset packs already owned/selected as the initial visual style. They can be progressively replaced with custom assets later.

## 12.12 Optimization priority

For early internal builds:

**playability > optimization**.

Optimize later once the gameplay systems are proven, while avoiding architectural decisions that obviously make future optimization impossible.

## 12.13 Achievements

Achievements are desirable but schedule-flexible and may come later.

## 12.14 Balance telemetry/debug logging

The MVP should log data useful for race/stat balancing, especially:

- creature stats;
- speeds reached;
- race outcomes;
- win rates correlated with stats/builds;
- enough information to understand whether progression/racing curves are behaving as intended.

This is primarily internal balancing instrumentation, not player-facing analytics.

## 12.15 Testing audience

Initial builds are for **internal testing first**, not external public testers.

## 12.16 Settings

A minimal settings screen belongs in the MVP, including the core audio/settings controls defined above.

## 12.17 Error handling

Basic user-friendly error handling is preferred where practical rather than exposing raw failures everywhere.

## 12.18 No in-game roadmap

Do not show a development roadmap inside the game.

## 12.19 No hard MVP date

There is no fixed release date requirement for the MVP at this stage.

## 12.20 Real race system, not a fake result screen

The MVP must exercise the actual race system and produce real race outcomes from the intended stats/simulation. A temporary fake "roll a winner" or result-only mock is not sufficient as the final MVP validation of racing.

---

# Cross-system requirements

Treat these as product-level constraints until explicitly revised:

1. Breeding is always player-initiated; no autonomous mating.
2. Two visible DNA profiles define normal inherited stat potential.
3. Normal offspring outcomes remain uncertain; no exact player-facing probability calculator.
4. Luck remains part of breeding and rare appearance progression.
5. Breeding supports both performance and collection goals.
6. Later race progression should require stronger bloodlines.
7. Mechanical idle progress currently occurs while the game is open, not while closed.
8. The Garden should remain cozy, quiet and low-demand.
9. Garden presentation follows real-world local day/night and seasons.
10. Personality is primarily atmospheric in v1.
11. Hidden happiness makes care meaningful and can affect death vs reincarnation.
12. Rank determines a stat's growth ceiling.
13. Active training is faster than passive zone training.
14. Training and breeding choices may be irreversible.
15. Rare results do not require pity systems.
16. A permanent immortal/trophy Voidling is a desired late-game goal; it can race but cannot breed.
17. Race outcomes primarily reflect stats, with small/random incidents preserving tension.
18. Major cups can carry financial stakes but should not replace idle income as the best money source.
19. Economy starts with one currency, fixed prices and uncapped open-game idle income.
20. Daily rewards should be predictable; race/shop/genetic outcomes may contain stronger chance swings.
21. The player should feel that long-term rewards were earned through time and planning.
22. Important background Garden events should be recoverable through the 300-message session log.
23. UI is desktop-first, pastel/cozy, fixed-layout and readability-first.
24. The world generally keeps running while menus are open.
25. MVP must contain the real raise → breed → hatch → train → race → lifecycle loop.
26. Autosave is the default persistence model.
27. Multiplayer is later content, but Steamworks connectivity/architecture must be proven early enough to avoid a dead-end design.

---

# Explicitly unresolved / pending design work

Do **not** silently decide the following during implementation:

- full remaining Appearance & Rare Bloodlines interview;
- exact ordinary color/pattern/shiny inheritance;
- exact rare mutation and rare-trait transmission rules;
- exact breeding cooldown duration;
- exact egg capacity/upgrade tiers;
- exact incubation duration and acceleration values;
- exact shop rotation interval and slot counts;
- exact immortal/trophy transformation requirements;
- exact numerical caps for E/D/C/B/A/S;
- exact adulthood/reincarnation rank-promotion rules;
- exact probability/eligibility for the rare +1-rank offspring result;
- exact Garden population and decoration caps;
- exact Garden grid/module geometry, upgrade costs and passive-training rates;
- exact hidden Happiness gains/losses/decay and reincarnation threshold;
- exact active training-item catalog and stat tradeoffs;
- exact race ladder/cup unlock structure;
- exact championship schedules;
- exact random-fall chance/interval;
- exact cup entry fees;
- **winner-only vs placement-based entry-fee refund model**;
- exact race prize/medal/item catalog;
- exact daily mission catalog;
- exact daily login chain;
- exact idle-income rate;
- whether active keyboard/computer use should ever boost idle income;
- exact eggshell sale values;
- exact seasonal event structure;
- multiplayer race/trading/leaderboard design;
- **Steamworks networking/lobby architecture and which free/common Steamworks components should be used**;
- final accessibility implementation scope;
- final key-remapping/full-release settings scope.

---

# Recommended next work

1. Complete **Appearance & Rare Bloodlines** with the missing primary + follow-up questions.
2. Deep-dive **Breeding**: cooldown/costs, exact menu flow, repeated-breeding incentives and animation.
3. Deep-dive **Family tree / inbreeding**: relatedness depth, warning UI, burden visibility, cleansing and failed eggs.
4. Deep-dive **Eggs/hatching**: capacity, acceleration, failed/non-viable presentation, hatch reveal.
5. Deep-dive **Lifecycle/reincarnation**: timing, hidden Happiness thresholds, death, rank changes and trophy transformation.
6. Finalize **Garden modules/passive training**.
7. Finalize **race ladder, cup rewards and entry-fee refund model**.
8. Run a dedicated **economy tuning pass**.
9. Research and document the **Steamworks multiplayer foundation**, then validate it with a simple connection test.

---

# Implementation-planning usage rule

When this document and the technical implementation plan differ:

- this document wins for **player-facing behavior and product intent**;
- the technical plan wins for architecture where gameplay intent has not changed;
- preserve contradictory interview answers as unresolved decisions instead of choosing silently;
- do not invent exact values merely to finish a ticket;
- prefer configurable/data-driven tuning for unresolved numbers;
- architecture should leave room for later multiplayer, expanded cosmetics, additional Garden modules, deeper genetics and larger race content without forcing those systems into the MVP prematurely.
