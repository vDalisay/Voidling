# Voidling Gameplay & System Design Refinement Context

**Status:** Living product-design context from the gameplay requirements interview  
**Purpose:** Expand the existing implementation plan with player-facing gameplay intent, system behavior, progression goals, and unresolved design decisions.  
**Companion document:** `docs/GENETICS_BREEDING_HATCHING_RACING_IMPLEMENTATION_PLAN.md`  
**Primary design reference:** Sonic Adventure 2 Battle Chao Garden / Chao World Extended-style depth, adapted into original Voidling systems and content.  

> This document intentionally stays at the gameplay and requirements level. It should guide later implementation decisions without prescribing C# architecture, persistence formats, or engine structure unless a technical constraint is necessary to preserve the intended player experience.

---

## Design interview progress

The refinement interview is being conducted one gameplay/design area at a time. The normal target is **20 primary questions plus 5 follow-up questions** based on ambiguities discovered in the answers.

Current progress:

| Interview section | Status |
|---|---|
| 1. Core fantasy & gameplay loop | Complete: 20 + 5 follow-ups |
| 2. Genetics & inherited potential | Complete: 20 + 5 follow-ups |
| 3. Appearance & rare bloodlines | Partial: 5 primary questions answered; deeper pass still required |
| 4. Personality, preferences & individuality | Complete: 20 + 5 follow-ups |
| 5. Garden & environment | Complete: 20 + 5 follow-ups |

A dedicated deep-dive on breeding itself, lineage/inbreeding, egg/hatching design, raising/training, lifecycle/evolution, and racing is still required even though some decisions about those systems already appear below because they came up naturally in earlier sections.

---

# 1. Core fantasy & gameplay loop

## 1.1 Target experience

Voidling should evoke the core appeal of the **SA2 Chao Garden / Chao World Extended** experience rather than functioning primarily as a traditional active-action game.

The game has three major pillars:

1. **Raising / caring for Voidlings**
2. **Breeding Voidlings**
3. **Racing Voidlings**

All three are important, but **breeding is intended to carry slightly more strategic weight** than the other two.

The player should be able to form their own long-term project rather than following only one prescribed objective. Example player projects include:

- breeding an all-S-rank Voidling;
- discovering or collecting every color;
- discovering new color combinations;
- producing a specific color with all-S stats;
- producing a Voidling that can dominate every race;
- building a prestigious lineage;
- pursuing rare mutations or mythic forms;
- completing a long multi-lifecycle transformation comparable in role to a Chaos Chao.

The game therefore should remain **open-ended after individual milestones are completed**. An all-S Voidling is a major achievement, but it is not intended to be the single final endpoint of the game.

## 1.2 Idle-first structure

Voidling is intended to be a **cozy idle game that can remain open while the player is doing something else**.

The player should be able to glance at it occasionally, make a meaningful decision, interact with a Voidling, check an egg timer, initiate a race, or manage a breeding project, and then return attention to something else.

This means:

- the Garden should remain pleasant to look at without demanding constant attention;
- passive waiting is a legitimate part of progression;
- visible timers can create a sense of ongoing productivity;
- the game should support both short interventions and longer active sessions when the player wants to focus on breeding, training, racing, or organization.

**Important timing rule currently intended:** progression timers such as egg incubation and breeding cooldowns advance while the game is open. Closing the game should not silently progress those systems for now.

This is separate from presentation time: the Garden's day/night state should follow the player's real-world local time when the game is opened.

## 1.3 Starting state and early breeding access

Breeding itself should **not be progression-locked**.

The player can theoretically use breeding from the beginning, but the game starts with only **one Voidling**, so the player must first acquire another Voidling/egg before they have two eligible adults.

This creates a natural early-game gate without an artificial "unlock breeding" requirement.

## 1.4 Breeding as a progression requirement

Breeding is not intended to be purely optional side content.

As race difficulty increases, CPU racers should eventually become strong enough that the player cannot reliably clear later races using only weak starting genetics. The player is expected to improve their bloodline over time if they want to keep progressing through the racing ladder.

The intended relationship is therefore:

```text
Raise → race → earn resources/rewards → improve breeding project → raise stronger offspring → unlock harder racing progress
```

Players can still choose aesthetic or collection-focused goals, but optimized breeding should be one of the main ways to reach high-end competitive content.

## 1.5 Breeding is deliberate, never autonomous

Breeding should be **planned and explicitly player-controlled**.

Voidlings must not randomly mate with one another in the Garden. The player should never discover that a creature bred with an unintended partner and compromised a carefully planned lineage.

Current intended interaction:

- open the existing breeding menu;
- select exactly two parents;
- parents must be adults;
- no relationship/bond requirement is needed for eligibility;
- player confirms the pairing;
- breeding produces the egg immediately;
- breeding should include an appealing animation/presentation rather than feeling like a spreadsheet-only action.

## 1.6 Breeding information and uncertainty

The intended information model is close to Chao World Extended:

- the player can inspect the Voidling's current stat profile;
- the player can inspect both of its breeding/DNA profiles;
- relevant parental color information can be shown;
- the game should **not provide an exact offspring prediction screen**;
- the player should never know the exact child that will result before breeding.

The system should provide enough information for deliberate breeding while preserving uncertainty and discovery.

## 1.7 Breeding cooldown

A Voidling may breed repeatedly throughout its eligible adult life, but breeding should have a **cooldown of roughly a few hours**.

The exact duration remains a balance value to determine later.

## 1.8 Egg creation and incubation

A successful breeding interaction creates the egg **immediately**.

The delay comes from incubation/hatching rather than conception.

Current intended behavior:

- multiple eggs may exist at once;
- the player should begin with a relatively small egg capacity;
- egg capacity can later be expanded through upgrades;
- each egg has a visible incubation countdown;
- once incubation finishes, the player clicks/interacts with the ready egg to trigger the actual hatch;
- an egg does not need to auto-hatch the instant its timer reaches zero.

## 1.9 Incubation acceleration items

Items may shorten egg incubation.

The intended item spectrum can include:

- ordinary items that reduce part of the remaining incubation time;
- an expensive item capable of removing the remaining wait entirely.

The instant-hatch item should not be constantly available. The current concept is a **rotating shop** that changes approximately hourly, with the instant-hatch item appearing infrequently.

For now, the rotating shop is the intended acquisition source. Additional acquisition sources may be added later.

## 1.10 Long-term trophy / ultimate Voidling

Voidling should eventually support a very late-game transformation or state that fills a similar gameplay role to a **Chaos Chao** without copying its exact content.

The intended high-level qualities are:

- requires a long-term project across multiple lifecycles and/or difficult completion requirements;
- represents a major prestige achievement;
- becomes permanent / immortal once achieved;
- permanently retains its resulting appearance/color;
- **cannot breed anymore** after reaching this trophy state;
- can still participate in races;
- does not receive arbitrary race strength merely for being a trophy form: its race performance still depends on its actual stats;
- may participate in special **late-game races** designed for these endgame creatures;
- should not trivialize beginner or normal racing content merely because the form exists.

The exact transformation requirements remain to be designed later. The current reference concept is the long investment and ritual-like checklist of Chaos Chao, not a direct reproduction of its exact animal/lifecycle requirements.

---

# 2. Genetics & inherited potential

## 2.1 Visible breeding DNA

Each Voidling should have **two visible DNA/breeding profiles** that define what it can pass to offspring.

These are distinct from the creature's immediately visible/current trained stat profile. A Voidling's current stats can therefore differ from the values represented in its breeding DNA.

However, there should be **no additional secret third layer of hidden breeding potential** beyond those two visible DNA profiles. If something can be inherited through the normal stat-breeding system, it should come from those parental DNA profiles or from an explicitly defined rare mutation rule.

## 2.2 Parent-only inheritance for now

For the initial system, normal inherited stat values should come from the **two selected parents only**.

Do not introduce ancestral stat reappearance from grandparents or deeper ancestors as a separate inheritance mechanic yet.

The family tree remains relevant for lineage and inbreeding, but not for pulling unexpected normal stat values from old ancestors in v1.

## 2.3 Randomness must stay within understandable genetic bounds

Inheritance should contain meaningful randomness, but it must still "make sense" from the parents' DNA.

The player should be able to look at the two parents and understand the range of plausible outcomes even though they cannot calculate the exact child.

The game should **not expose exact numerical offspring probabilities** to the normal player.

## 2.4 Rare one-rank improvement

A very low-probability offspring improvement is desirable.

Example:

- both relevant parental values are around B;
- the child may very rarely emerge at A for that stat.

This improvement must be tightly bounded:

- the normal exceptional jump should be **at most +1 rank**;
- two extremely weak parents must not suddenly produce an S-rank result from nowhere;
- the chance should be low enough that it feels lucky rather than routine;
- the player should not have a direct item, toggle, or other mechanic that manipulates this luck for now.

This keeps a small possibility of exciting genetic breakthroughs while preserving lineage logic.

## 2.5 Stat ranks and caps

The current rank language remains:

```text
E → D → C → B → A → S
```

S is the highest rank for now.

A Voidling's stat ceiling should be constrained by its relevant stat rank. Higher rank therefore represents greater growth potential, not merely a cosmetic letter grade.

Exact numerical caps per rank remain to be balanced later.

## 2.6 Equal DNA weighting initially

The two DNA profiles should be treated as equally valid inheritance sources for now.

No general dominant/recessive hierarchy is required for stat DNA in the first version.

Dominance systems may be explored later for specific genes or traits, but should not be assumed as a universal rule yet.

## 2.7 Luck remains part of breeding mastery

Even highly knowledgeable players should never be able to remove luck entirely from breeding.

Breeding skill comes from:

- choosing better parents;
- understanding the two DNA profiles;
- managing generations;
- deciding which offspring to keep breeding;
- accepting or abandoning lines;
- patiently repeating a project.

It does **not** come from eventually obtaining a guaranteed perfect-child button.

## 2.8 Hard consequences and restarting a line

The genetic system is allowed to be somewhat unforgiving.

A player can make poor breeding decisions and end up with a line they no longer want. The game does not need to guarantee a magical repair mechanic for every bad line.

The player's ultimate reset option is the **Goodbye** action:

- send away/delete unwanted Voidlings;
- abandon that bloodline;
- buy/acquire a new Voidling;
- begin a new breeding project.

This keeps consequences meaningful while ensuring the player can never permanently brick their entire save.

## 2.9 Inbreeding consequence for now

For the current design, inbreeding's direct gameplay consequence should remain **hatch failure risk only**.

Do not add stat degradation, deformities, personality penalties, disease systems, or other additional punishment yet.

The design can revisit extra consequences later.

The inbreeding risk percentage should be visible to the player, and family-tree presentation is expected to communicate relatedness and historical lineage risk.

The existing implementation plan's burden ladder remains the technical/product baseline unless later design refinement changes it.

## 2.10 Genetics can subtly influence autonomous behavior

Stats may influence what a Voidling naturally chooses to do in the Garden.

Example:

- a Voidling with a strong Swim stat may choose to enter/swim in Garden water more often.

This should be presented as emergent behavior rather than documented as an explicit tooltip rule. Players can notice and learn these tendencies through observation.

Where personality and stat tendencies conflict in the initial version, **stat-driven behavior should take priority**.

## 2.11 Stat-driven visual development

Voidling appearance should respond to how the creature is raised, similar in spirit to Chao stat-form development.

Example:

- raising/training strongly toward Swim gradually introduces Swim-associated visual traits.

This should be capable of changing over the Voidling's life rather than only switching once at a single evolution screen.

These visual traits are **an effect of development**, not a source of additional power. In other words:

```text
Stats / development → appearance change
```

not:

```text
appearance change → free stat bonus
```

## 2.12 Open-ended genetic goals

There should be no single final genetic completion condition.

Players can continue inventing harder projects, such as:

- every color;
- every color with S rank;
- specific stat distributions;
- a perfect racing specialist;
- a mythic/trophy transformation;
- mutation-focused lines;
- combinations of cosmetic and performance goals.

---

# 3. Appearance & rare bloodlines

> **Interview status:** This section is only partially refined. Five primary questions were answered before the interview moved on. Treat the requirements below as confirmed direction, not a complete appearance specification.

## 3.1 Appearance is a real breeding objective

Appearance should support both:

- collecting / completionist play;
- deliberate breeding strategy.

A player may specifically breed to obtain:

- colors they have never owned;
- new color combinations;
- every visual type in every color;
- specific aesthetic + stat combinations.

Appearance therefore must not feel like random decoration detached from the breeding loop.

## 3.2 Mythically rare looks should exist

Some visual outcomes should be **extremely rare and prestigious**.

They should be rare enough that seeing or owning one feels noteworthy rather than inevitable.

Players should be able to pursue them through persistent breeding effort, but success can still depend on luck. The system should reward patience rather than provide an automatic completion guarantee.

## 3.3 No pity system for top rarity

The current direction is **no pity/guarantee system** for the rarest appearance goals.

These are intended to be achievements the player works toward, with some outcomes remaining genuinely difficult to obtain.

## 3.4 Rare appearance can matter through state, not raw bonuses

A special appearance may have gameplay meaning when that appearance represents a **special transformation/state**.

The desired model is similar in role to the Chaos Chao concept:

- the special state can make the creature immortal/permanent;
- the special state can make it unable to breed;
- the appearance communicates that achievement;
- it should not simply add arbitrary hidden combat/racing bonuses because it looks rare.

Raw racing strength should still come from the Voidling's stats.

## 3.5 Cross-system appearance direction already established

Other confirmed appearance-related decisions from previous sections:

- breeding for undiscovered colors is a major long-term goal;
- stat specialization can gradually change a Voidling's visual traits;
- those stat-driven morphological changes are cosmetic reflections of development;
- late-game trophy Voidlings should retain their final appearance/color permanently;
- the exact rare-trait inheritance rules, mutation catalog, color-combination rules, and bloodline transmission limits still need a dedicated deeper design pass.

### Appearance questions still requiring refinement

At minimum, the future appearance pass should define:

- ordinary color inheritance rules;
- pattern inheritance;
- shiny/special coat behavior;
- mutation acquisition rates;
- whether specific rare traits can transmit across generations and for how long;
- whether a collection encyclopedia records discovered colors/looks;
- whether undiscovered combinations are previewed, hinted, or fully hidden;
- how stat-driven morphology combines with inherited colors/patterns;
- whether multiple rare visual traits can stack;
- exact rules for prestige/trophy transformations.

---

# 4. Personality, preferences & individuality

## 4.1 Primary purpose: atmosphere

For the initial version, personality exists primarily to make Voidlings feel like **little individuals living in the Garden**.

Its main purpose is:

- charm;
- atmosphere;
- cozy observation;
- varied idle behavior;
- emotional attachment.

It should not initially be a major optimization layer.

## 4.2 No race effect initially

Personality should **not affect racing in the first version**.

Race performance should remain easier to understand through stats, condition, and course rules first.

Personality-driven racing may be revisited later.

## 4.3 Core personality versus treatment-driven demeanor

A Voidling can have a core personality, but treatment can influence how it behaves over time.

The intended distinction is:

- **core personality:** relatively stable and not something the player can freely rewrite;
- **treatment-driven demeanor:** can become more positive or negative depending on how the player treats the creature.

Examples:

- petting, gently placing, and generally treating a Voidling well supports positive demeanor;
- throwing or otherwise mistreating a Voidling can push behavior in a more negative direction;
- later kind treatment can partially repair the damage;
- the underlying core personality should still remain recognizable.

## 4.4 Negative/difficult behavior should come primarily from mistreatment

Difficult traits such as angry, unpleasant, or similar negative behavior should not normally be randomly assigned to a brand-new Voidling as an unavoidable punishment.

They should mainly emerge when the player repeatedly treats that Voidling badly.

This keeps the system readable as an emotional response rather than making some newborns feel arbitrarily "bad."

## 4.5 Feedback should be visible but consequences should be discovered

When the player performs something the Voidling dislikes, the game should provide immediate expressive feedback.

Example:

- angry face animation;
- angry icon above the head;
- unhappy reaction after being thrown.

However, the game does not need to display a mechanical message such as "Personality -3" or explain exactly what long-term effect the action has.

The player should learn the system through observation.

## 4.6 Personality is primarily behavioral, not a stat sheet

The game should not expose a large personality-stat matrix to the player in v1.

The preferred presentation is a short **flavor sentence** in the Voidling profile that broadly reflects how it behaves.

The sentence can be playful/cozy rather than clinical, but it should still match the observable personality well enough not to mislead the player.

The flavor sentence may change over time if the Voidling's demeanor changes.

For now, there is no requirement for a hidden complex numerical personality simulation behind this text.

## 4.7 Personality rarity

Some personality styles may be rarer than others.

Extremely rare personality variants may exist, but any special effect should initially remain **cosmetic/behavioral**, not a large stat advantage.

## 4.8 Favorite food

Each Voidling can have a favorite food.

The player should discover it through **trial and error** rather than receiving it automatically at birth.

Once discovered:

- the favorite food should be recorded in the Voidling's profile/DNA information;
- feeding that favorite food grants slightly more stat points than the ordinary version of that feeding interaction.

This is intentionally a small gameplay benefit attached to a character preference rather than a deep personality optimization system.

## 4.9 No relationship system initially

Voidlings do not need a friendship/rivalry relationship simulation in the first version.

They may still interact with each other in the Garden for atmosphere.

These interactions should:

- appear as small background animations;
- be mostly random;
- loosely match the involved Voidlings' personalities when possible;
- not create serious mechanical consequences;
- not require the player to manage friendship meters.

---

# 5. Garden & environment

## 5.1 Garden role

The Garden should be both:

- a functional management space;
- a personally customized cozy environment.

It is expected to remain visible for long periods while the player is doing something else, so it should communicate life and progress without constantly demanding attention.

The target emotional combination is **rest + anticipation/excitement**: calm enough to leave open, but alive enough that the player sometimes looks over because something interesting may be happening.

## 5.2 Modular Garden inspired by Digimon Championship DS

The current expansion direction is a modular layout concept inspired by **Digimon Championship DS**.

The player has one overall Garden that can be customized using modular predetermined sections/tiles, described during the interview as a grid/hex-style system.

Intended rules:

- individual grid/hex modules are authored/predetermined content;
- the player decides which modules to buy/unlock;
- the player decides which modules to place/combine;
- the player decides which modules to upgrade;
- placed modules collectively determine the personal Garden layout;
- module choice can influence training/stat development;
- the visual theme of the Garden emerges from the modules the player places rather than selecting one global theme from a menu.

The exact grid geometry, module footprint, adjacency rules, and stat effects still need a dedicated Garden-system design pass.

## 5.3 Progressive module acquisition

The player should **not** have every Garden module available immediately.

Modules should be purchased/unlocked progressively, giving Garden expansion its own long-term progression track.

## 5.4 No cleaning/upkeep chores

The Garden should not require routine cleaning, waste removal, or similar maintenance chores in the initial design.

Neglecting chores should not be part of the core pressure loop.

## 5.5 Hard Voidling capacity

The Garden should have a **hard capacity limit** for how many Voidlings can live there at once.

The exact number is not decided yet.

When full, the player receives a clear message along the lines of:

> The Garden is full. To add a new Voidling, one must leave first.

The game should not silently overpopulate the Garden or apply hidden overcrowding penalties as a substitute for a clear capacity rule.

## 5.6 Voidlings roam freely

Voidlings do not need fixed preferred zones within the Garden for now.

They can roam through available areas rather than being assigned to one permanent biome/module.

Their actions may still be influenced by stats—for example, a strong swimmer choosing water more often—but the player does not need to manually bind each Voidling to a specific habitat.

## 5.7 Decoration placement

Decorations should be **freely placeable** rather than restricted to fixed decoration slots.

A practical cap on decoration count may be added for performance/readability, but the exact limit is currently undecided.

## 5.8 Decoration interactions are flavor-only

Decorations may support small interactions similar to toys/objects in a Chao Garden.

Those interactions should be for charm rather than progression.

Examples of intended behavior:

- a Voidling autonomously uses/plays with an object;
- the player can trigger some decoration interactions directly;
- an animation plays;
- no meaningful stat advantage is attached to the decorative interaction itself.

This is separate from Garden grid/modules, which *may* have explicit stat/training effects.

## 5.9 Real-world day/night presentation

The Garden should reflect the player's **real local time**.

If the player opens the game at night, the Garden should appear at night. If opened during the day, it should appear during the day.

This day/night cycle should not be optional in the intended baseline because real-time presentation is part of the game's identity.

## 5.10 Real-world seasons

Seasonal visuals should follow real-world seasonal time as well.

For the initial version, seasons are primarily visual.

Future atmosphere extensions can add season-specific behavior/animations, for example:

- playing in snow;
- interacting with autumn leaves;
- other seasonal ambient actions.

These do not need gameplay/stat consequences initially.

## 5.11 Closing the game should not punish the player

If the player leaves the game closed for a long time, there should be **no mechanical neglect punishment**.

A small flavor reaction on return may gently imply that the Voidlings noticed the player's absence, but it must not reduce stats, damage health, ruin personality, or otherwise guilt the player through mechanical loss.

## 5.12 Audio direction

The baseline idle Garden should be quiet.

Current preference:

- no continuous Garden music while idling;
- no need for constant ambient soundscape;
- clear sound effects when the player actively interacts with the game.

Examples of interaction SFX:

- clicks;
- picking up a Voidling;
- placing a Voidling;
- other direct actions.

The goal is to avoid distracting the player from whatever else they are doing while the idle game remains open.

## 5.13 Cozy, lively, but not attention-hungry

The Garden may feel alive and animated, but should avoid becoming visually frantic.

Ambient motion and Voidling interactions should support the sense that the world continues on its own without constantly pulling the player's eyes away from another task.

This is a core presentation constraint, not merely an art preference.

## 5.14 Timers are acceptable and desirable

Although the Garden should not demand constant attention, visible timers are acceptable because they create a sense of progress.

Example:

- an egg incubation timer counting down gives the player a reason to glance over occasionally and think, "Is it almost ready?"

This type of anticipation fits the idle/cozy loop and should not be treated as unwanted pressure by default.

---

# Cross-system requirements currently established

The following requirements cut across multiple sections and should be treated as product-level constraints until explicitly revised:

1. **Breeding is deliberate and player-initiated.** No autonomous mating.
2. **Two visible DNA profiles are the normal inheritance source.** No additional secret normal-stat inheritance layer.
3. **Exact offspring outcomes remain uncertain.** Do not expose a perfect prediction calculator in normal gameplay.
4. **Luck always remains part of breeding.** The player improves odds through parent selection, not through guaranteed outcome controls.
5. **Breeding supports both performance and collection goals.** All-S stats and rare colors are equally valid projects.
6. **Late-game racing should eventually require improved bloodlines.** Genetics must matter to progression.
7. **The game is idle-first but not offline-progress-first.** Current progression timers advance while open; real-world presentation reflects local time.
8. **The Garden should remain cozy and low-demand.** No mandatory cleaning or neglect punishment in the current design.
9. **Personality exists mainly for atmosphere in v1.** It should not complicate race optimization yet.
10. **Rare achievements should remain genuinely rare.** No pity system is currently desired for the most prestigious visual outcomes.
11. **A permanent late-game trophy form is desirable.** It can race, cannot breed, and does not receive free race power beyond its actual stats.
12. **The player is allowed to abandon bad lines.** Goodbye/delete is the clean reset rather than guaranteeing reversible genetics.

---

# Explicitly unresolved / pending design work

These points should **not** be silently decided by implementation yet:

- exact breeding cooldown duration;
- exact egg capacity at each upgrade tier;
- exact rotating-shop interval and item rarity;
- exact incubation durations and acceleration values;
- exact requirements for the immortal/trophy Voidling transformation;
- exact stat caps per E/D/C/B/A/S rank;
- exact probability and eligibility rules for the rare +1-rank offspring improvement;
- whether rare appearance traits have generational transmission limits and, if so, their exact rules;
- complete color/pattern/shiny/mutation inheritance rules;
- whether an appearance encyclopedia/collection tracker exists;
- exact Garden Voidling capacity;
- exact decoration count cap;
- exact Garden grid/hex geometry and placement rules;
- exact Garden module stat/training effects;
- exact Garden upgrade economy;
- whether personality receives a deeper numerical simulation later;
- whether personality eventually affects racing;
- whether inbreeding later receives consequences beyond hatch failure;
- additional acquisition sources for instant/accelerated hatch items;
- detailed race structure, tournament ladder, rewards, and endgame race requirements.

---

# Recommended next design interviews

The next gameplay refinement work should prioritize systems that are central to the game but not yet deeply specified:

1. **Breeding system deep dive** — interaction flow, eligibility, cooldowns, costs, breeding animation, player information, repeated breeding incentives.
2. **Family tree / lineage / inbreeding** — relatedness depth, warning UI, visible burden, cleansing, failed eggs, prestige lines.
3. **Eggs / incubation / hatching** — capacity, timers, item acceleration, ready-state interaction, non-viable egg handling, hatch reveal.
4. **Raising / food / stats / training** — how players actually increase stats, favorite-food bonus size, active versus passive raising, stat caps.
5. **Evolution / lifecycle / reincarnation** — age pacing, morphological change, death, reincarnation, trophy transformation requirements.
6. **Racing / competition / rewards** — race ladder, CPU progression, course roles, reward economy, late-game trophy races.
7. **Return to Appearance & Rare Bloodlines** — complete the unfinished 20 + 5 design interview for section 3.

---

# Usage rule for implementation planning

When this document and the technical implementation plan differ in specificity:

- use this document as the source of truth for **player-facing intent and gameplay requirements** established by the refinement interview;
- use the existing implementation plan as the source of truth for **technical architecture** where the gameplay intent has not changed;
- do not invent values for unresolved items above merely to complete an implementation task;
- prefer data-driven/configurable values for unresolved tuning so later design refinement does not require structural rewrites.
