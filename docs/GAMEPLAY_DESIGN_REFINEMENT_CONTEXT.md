# Voidling Gameplay & System Design Refinement Context

**Status:** Living product-design context from the gameplay requirements interview  
**Purpose:** Expand the existing implementation plan with player-facing gameplay intent, system behavior, progression goals, and unresolved design decisions.  
**Companion document:** `docs/GENETICS_BREEDING_HATCHING_RACING_IMPLEMENTATION_PLAN.md`  
**Primary design references:** Sonic Adventure 2 Battle Chao Garden / Chao World Extended-style depth, plus Digimon Championship DS-style passive training and modular Garden ideas, adapted into original Voidling systems and content.

> This document intentionally stays at the gameplay and requirements level. It should guide later implementation decisions without prescribing C# architecture, persistence formats, or engine structure unless a technical constraint is necessary to preserve the intended player experience.

---

## Design interview progress

The refinement interview is being conducted one gameplay/design area at a time. The normal target is **20 primary questions plus 5 follow-up questions** based on ambiguities discovered in the answers.

| Interview section | Status |
|---|---|
| 1. Core fantasy & gameplay loop | Complete: 20 + 5 follow-ups |
| 2. Genetics & inherited potential | Complete: 20 + 5 follow-ups |
| 3. Appearance & rare bloodlines | Partial: 5 primary questions answered; deeper pass still required |
| 4. Personality, preferences & individuality | Complete: 20 + 5 follow-ups |
| 5. Garden & environment | Complete: 20 + 5 follow-ups |
| 6. Progression, unlocks & long-term play | Complete: 20 + 5 follow-ups |
| 7. Races, cups & competition | Complete: 20 + 5 follow-ups |
| 8. Raising, training, stats & care | Complete: 20 + 5 follow-ups |
| 9. Economy & rewards | Complete: 20 + 5 follow-ups |

A dedicated deep-dive on breeding itself, lineage/inbreeding, eggs/hatching, lifecycle/evolution/reincarnation, and the unfinished appearance section is still required. Some requirements for those systems are already established because they came up naturally in other sections.

---

# 1. Core fantasy & gameplay loop

## 1.1 Target experience

Voidling should evoke the core appeal of **SA2 Chao Garden / Chao World Extended** rather than functioning primarily as a traditional active-action game.

The game has three major pillars:

1. **Raising / caring for Voidlings**
2. **Breeding Voidlings**
3. **Racing Voidlings**

All three matter, but **breeding should carry slightly more strategic weight**.

The player should choose their own long-term project. Valid goals include:

- breeding an all-S-rank Voidling;
- discovering every color or color combination;
- producing specific appearance + stat combinations;
- producing a Voidling capable of dominating the race ladder;
- building prestigious bloodlines;
- pursuing rare mutations;
- obtaining a long-term immortal/trophy transformation comparable in role to a Chaos Chao.

The game is therefore **open-ended**. Finishing one project creates room for another rather than ending the game.

## 1.2 Idle-first structure

Voidling is intended to be a **cozy idle game that can stay open while the player is doing something else on the computer**.

The player can occasionally:

- check an egg timer;
- feed or interact with a Voidling;
- manage a breeding project;
- change a passive-training setup;
- enter a race;
- inspect progression;
- buy something from the shop;
- then return attention to another task.

Visible timers are desirable because they make background progress feel productive rather than demanding.

**Current timing rule:** mechanical progression such as incubation, breeding cooldowns, passive training, and idle income should progress while the game is open. Closing the game should not silently provide the same mechanical progress for now.

## 1.3 Starting state and early breeding access

Breeding should not require an artificial feature unlock.

The player starts with **one Voidling**, so breeding is naturally unavailable until the player acquires and raises another eligible Voidling.

## 1.4 Breeding as a progression requirement

Breeding is not intended to be optional side content.

Later CPU racers should eventually exceed what weak starting genetics can reliably defeat. The player is expected to improve their bloodline to continue progressing through increasingly difficult races.

The intended relationship is:

```text
Raise → race → earn rewards/unlocks → improve breeding project → raise stronger offspring → tackle harder races
```

## 1.5 Breeding is deliberate, never autonomous

Voidlings must never randomly mate in the Garden.

Current intended flow:

- open the breeding menu;
- select exactly two parents;
- both parents must be adults;
- no friendship/relationship requirement is needed;
- confirm pairing;
- play a pleasant breeding animation/presentation;
- create the egg immediately;
- apply a breeding cooldown to the parents.

A carefully planned lineage must never be altered by autonomous mating.

## 1.6 Breeding information and uncertainty

The intended information model is close to Chao World Extended:

- current stat profile is visible;
- both breeding/DNA profiles are visible;
- relevant inherited colors can be inspected;
- exact offspring probabilities are not shown;
- there is no perfect offspring prediction screen;
- the exact child remains uncertain until generated.

The player should have enough information to make deliberate decisions without removing discovery and luck.

## 1.7 Breeding cooldown

Voidlings can breed repeatedly during their eligible adult life, but each breeding should trigger a cooldown of **roughly a few hours**.

Exact duration remains a balance value.

## 1.8 Eggs and incubation

Breeding produces an egg immediately; the waiting period is the incubation period.

Current intended behavior:

- multiple eggs may exist simultaneously;
- starting egg capacity is small;
- capacity can be upgraded;
- eggs have visible countdown timers;
- when ready, the player clicks/interacts with the egg to hatch it;
- eggs do not need to auto-hatch the moment the timer reaches zero.

## 1.9 Incubation acceleration

Items can accelerate incubation.

Possible tiers:

- ordinary items remove part of the remaining time;
- a very expensive item can remove the remaining time completely.

The full-skip item should be uncommon in a rotating shop rather than permanently available. Other acquisition routes can be added later.

## 1.10 Late-game trophy Voidling

Voidling should eventually support a difficult permanent transformation that fills a similar gameplay role to a **Chaos Chao** without copying its exact requirements or content.

Desired properties:

- requires a long multi-lifecycle project;
- represents a major prestige achievement;
- becomes immortal/permanent;
- retains its resulting appearance/color permanently;
- can no longer breed;
- can still race;
- does not gain arbitrary race strength merely for being a trophy form;
- may participate in special late-game races intended for endgame creatures.

Exact transformation requirements remain unresolved.

---

# 2. Genetics & inherited potential

## 2.1 Two visible breeding DNA profiles

Each Voidling has **two visible DNA/breeding profiles** defining what it can pass to offspring.

These can differ from the creature's current trained stats.

There should be **no secret third normal-stat inheritance layer** beyond the two visible profiles. Normal inherited stat outcomes must come from the selected parents' DNA unless an explicit mutation rule applies.

## 2.2 Parent-only normal inheritance for now

Normal inherited stat values should come from the **two selected parents only**.

Grandparents/deeper ancestors remain relevant for family-tree and inbreeding logic, but should not independently reintroduce ordinary stat values in v1.

## 2.3 Randomness must remain genetically understandable

Inheritance should be random inside plausible parental bounds.

The player should be able to inspect the parents and understand what kinds of outcomes are possible, while still not knowing the exact child or exact probability distribution.

## 2.4 Rare +1-rank improvement

A very low-probability child improvement is desirable.

Example:

- parents carry B-level potential;
- the child may rarely obtain an A result.

Rules:

- exceptional improvement is at most **+1 rank**;
- very weak parents must not randomly produce an S result from nowhere;
- the event should feel lucky and uncommon;
- the player should not have an item or toggle that guarantees/manipulates this chance for now.

## 2.5 Stat ranks and caps

Current rank language:

```text
E → D → C → B → A → S
```

S is highest for now.

The rank determines the maximum amount of growth a stat can contain. A higher rank therefore represents higher developmental potential.

## 2.6 Equal DNA weighting initially

The two stat DNA profiles are equally valid inheritance sources for now.

Do not assume a universal dominant/recessive stat hierarchy in v1. Trait-specific dominance may be explored later.

## 2.7 Luck remains part of breeding mastery

Breeding knowledge should improve decision quality, not eliminate luck.

Skill comes from:

- parent selection;
- reading DNA profiles;
- managing generations;
- keeping/discarding offspring strategically;
- patiently repeating a project.

There should be no guaranteed perfect-child button.

## 2.8 Hard consequences and abandoning a line

The genetics system is allowed to be unforgiving.

Bad breeding decisions do not need a universal repair mechanic. The reset route is the **Goodbye** action:

- send away unwanted Voidlings;
- abandon the line;
- acquire a new Voidling;
- begin again.

## 2.9 Inbreeding consequence for now

For now, inbreeding should directly affect **hatch failure risk only**.

Do not initially add stat degradation, diseases, deformities, or personality penalties.

The risk percentage should be visible to the player, and the family tree should communicate relatedness/history.

The existing implementation plan's burden ladder remains the baseline unless later design work changes it.

## 2.10 Stats can subtly influence Garden behavior

Stats may influence autonomous actions.

Example: a high-Swim Voidling may choose to swim more often.

The game does not need to explicitly explain these behavioral tendencies. Observation and discovery are preferable.

## 2.11 Stat-driven appearance development

Appearance can gradually reflect how the Voidling is raised.

Example: training heavily toward Swim can gradually introduce Swim-associated morphology.

This is a consequence of development:

```text
stats / development → appearance
```

not a free power bonus from appearance.

## 2.12 Open-ended genetic goals

There is no single genetic completion state. Players may continue pursuing:

- all colors;
- all colors with S-rank goals;
- specialized race builds;
- perfect all-rounders;
- rare mutations;
- prestige lines;
- trophy transformations.

---

# 3. Appearance & rare bloodlines

> **Interview status:** Partial. Only five primary questions were completed before the interview moved on. Treat this as confirmed direction, not a complete specification.

## 3.1 Appearance is a real breeding objective

Appearance supports both collecting and deliberate breeding strategy.

Players may breed for:

- undiscovered colors;
- color combinations;
- every visual type in every color;
- appearance + stat combinations.

## 3.2 Mythically rare appearances should exist

Some visual outcomes should be extremely rare and prestigious.

They should remain realistically grindable through patience and breeding effort, but success can still depend heavily on luck.

## 3.3 No pity system for top rarity

The rarest appearance goals do not need a guaranteed pity mechanic. They are intended to require work and luck.

## 3.4 Special appearance can represent a gameplay state

A rare appearance can have gameplay meaning when it represents a special state such as the late-game immortal/trophy transformation.

That state can change lifecycle/breeding rules, but should not grant arbitrary hidden race bonuses just because it looks rare.

## 3.5 Existing cross-system appearance decisions

- discovering colors is a long-term goal;
- stat specialization can gradually alter morphology;
- stat-driven morphology is cosmetic feedback from development;
- trophy Voidlings retain their final appearance permanently.

### Still unresolved

- ordinary color inheritance;
- patterns;
- shiny/special-coat behavior;
- mutation rates;
- rare-trait transmission depth;
- collection encyclopedia behavior;
- undiscovered-combination hints;
- stacking rare traits;
- exact prestige transformation requirements.

---

# 4. Personality, preferences & individuality

## 4.1 Primary purpose: atmosphere

Personality initially exists mostly to make Voidlings feel like individuals.

Its main role is:

- charm;
- cozy observation;
- idle animation variety;
- emotional attachment.

It should not be a major optimization system in v1.

## 4.2 No race effect initially

Personality should not affect racing in the initial version.

## 4.3 Core personality vs treatment-driven demeanor

A relatively stable core personality can coexist with treatment-driven behavior.

Positive treatment includes petting, feeding, and gentle handling.

Negative treatment includes throwing or repeatedly treating the Voidling badly.

Later positive treatment can repair some negative demeanor, but should not rewrite the fundamental core personality.

## 4.4 Difficult behavior should primarily come from mistreatment

Newborn Voidlings should not commonly be assigned severe negative personalities at random.

Angry/difficult behavior is more appropriate as a response to how the player treats them.

## 4.5 Immediate expressive feedback, hidden long-term consequence

When a Voidling dislikes an action, show a reaction such as:

- angry face;
- angry icon;
- unhappy animation.

Do not display a mechanical message explaining the exact personality/happiness consequence.

## 4.6 Profile flavor text

Instead of a detailed personality matrix, the profile can contain a short flavor sentence reflecting current observable demeanor.

The sentence may change over time and can be playful/cozy rather than clinical.

## 4.7 Personality rarity

Some personality styles may be rarer than others.

Rare personality behavior should remain cosmetic/behavioral initially.

## 4.8 Favorite food

Each Voidling may have a favorite food.

The player discovers it through trial and error. Once discovered, it is recorded in the profile/DNA information.

Favorite food gives slightly more stat gain than ordinary food.

## 4.9 No friendship/rivalry system initially

Voidlings can interact with each other through small ambient animations, but there is no need for relationship meters in v1.

Interactions can be mostly random while loosely matching personality.

---

# 5. Garden & environment

## 5.1 Garden role

The Garden is both:

- a functional management space;
- a customized cozy environment.

Its target feeling is **rest + anticipation**: quiet enough to leave open beside another activity, but alive enough that the player occasionally looks over to see what is happening.

## 5.2 Modular Garden inspired by Digimon Championship DS

The current direction is one overall Garden built from predetermined modular grid/hex-like sections.

The player chooses:

- which modules to buy/unlock;
- which modules to place;
- how to combine them;
- which modules to upgrade.

Modules can define visual theme and can also have explicit passive-training/stat effects.

Exact geometry and stat effects remain unresolved.

## 5.3 Progressive module acquisition

Garden modules are purchased/unlocked over time rather than all being available immediately.

## 5.4 No cleaning/upkeep chores

Do not require routine cleaning, waste removal, or similar maintenance chores in the initial design.

## 5.5 Hard Voidling capacity

The Garden has a hard Voidling limit.

Exact capacity is unresolved.

When full, show a clear message such as:

> The Garden is full. To add a new Voidling, one must leave first.

## 5.6 Free roaming

Voidlings can roam available Garden areas freely instead of being permanently assigned to a single habitat.

## 5.7 Decoration placement

Decorations should be freely placeable rather than restricted to fixed slots.

A practical decoration cap may exist for readability/performance.

## 5.8 Decoration interactions are flavor-only

Decorations can support autonomous and player-triggered animations/interactions, but these should not themselves provide meaningful stat advantages.

This is separate from functional Garden training modules.

## 5.9 Real-world day/night presentation

The Garden follows the player's real local time. Opening the game at night should show night.

The real-time presentation is part of the game's identity and should not be optional by default.

## 5.10 Real-world seasons

Season visuals follow real-world seasons.

Initially this is primarily cosmetic. Later, seasonal idle animations may be added, such as playing in snow or autumn leaves.

## 5.11 Closing the game should not mechanically punish the player

Long absence while the game is closed should not reduce stats or cause mechanical neglect penalties.

A flavor reaction on return may imply the Voidlings noticed the absence, but it must not create loss.

## 5.12 Audio direction

The baseline idle Garden should be quiet.

Current preference:

- no continuous Garden music while idling;
- no need for constant ambience;
- clear SFX for clicks, picking up, placing, and other direct interactions.

## 5.13 Cozy and lively, not attention-hungry

The Garden can feel alive without becoming visually frantic or distracting.

## 5.14 Visible timers are desirable

Egg timers and similar countdowns are useful because they create anticipation and a sense of background productivity.

---

# 6. Progression, unlocks & long-term play

## 6.1 Progression comes from multiple connected systems

Progression should come from a combination of:

- earning and spending money;
- hatching/acquiring Voidlings;
- breeding stronger or rarer lines;
- raising stats;
- winning races/cups;
- unlocking Garden content;
- earning cosmetic and prestige rewards.

No single progression currency should replace the creature-raising loop.

## 6.2 Money remains useful but is not the only endgame goal

Money should remain useful into later play, but it is acceptable for a highly progressed player to care less about ordinary utility purchases.

Long-term spending can include:

- cosmetics;
- rare shop inventory;
- eggs;
- Garden modules/upgrades;
- convenience items.

Cosmetics are a valid long-term use for excess money, but the economy should not feel designed solely around draining currency.

## 6.3 Eggs remain meaningful purchases

Egg buying may eventually become routine, but eggs should remain relatively expensive.

The economy must account for players who leave the game open for many hours per day and therefore accumulate substantial idle income.

## 6.4 Progress can intentionally stall

It is acceptable for progression to pause when the player lacks money, suitable genetics, or luck.

The game is designed around sometimes leaving it open and allowing background progress to accumulate rather than guaranteeing constant rapid advancement.

## 6.5 Grind is intentional

Long-term goals are supposed to take meaningful time and repetition.

The game should not flatten every objective into a short guaranteed unlock path.

## 6.6 Daily rhythm without a prescribed daily playstyle

The game should support daily engagement, but the player should still choose their own project.

Daily structure may include:

- login chains;
- daily missions;
- rotating inventory;
- checking eggs/timers;
- occasional special opportunities.

The game should reward both:

- brief frequent check-ins;
- keeping the game open for long periods.

## 6.7 Shop structure

The shop should combine predictable baseline access with rotating rarity.

Current direction:

- core/basic items remain consistently available;
- rare items rotate;
- egg inventory follows a reasonably predictable format, while exact eggs can vary;
- an exact permanent egg-slot count is not yet locked;
- rare convenience items such as full incubation skips should appear infrequently.

The user suggested an approximately hourly rotation as a working direction, not a final tuning requirement.

## 6.8 Endless self-directed progression

There is no final universal completion objective.

The player creates goals such as:

- perfect stats;
- rare colors;
- complete collections;
- stronger race builds;
- cup completion;
- trophy Voidlings;
- Garden customization;
- prestige bloodlines.

## 6.9 No pity system for rare outcomes

Rare genetic/cosmetic outcomes can remain genuinely rare. The player is expected to grind and rely partly on luck.

## 6.10 Single-player first, multiplayer later

The current core is single-player.

Later multiplayer ambitions include:

- racing other players;
- potentially player trading once multiplayer exists;
- local/global competitive comparison and leaderboards.

These are later-phase systems and should not complicate the initial economy unnecessarily.

## 6.11 Economy should be tightly understandable

The economy should be deliberately balanced rather than intentionally messy or opaque.

Players should be able to understand how they earn and spend currency.

## 6.12 Mistakes can have consequences

Progression decisions can be irreversible. The game does not need to protect the player from every poor purchase, stat choice, or breeding decision.

## 6.13 Progress should be visible

Where progression is numeric, use clear UI such as bars and numbers rather than hiding all advancement behind flavor.

## 6.14 Cosmetics are valid rewards

Cosmetic rewards should exist independently of power progression.

## 6.15 Seasonal events

Seasonal events are desirable.

They may include:

- seasonal cosmetics;
- login calendars;
- temporary reward structures;
- other event content.

For now there is no requirement that seasonal events alter core genetics/race balance.

## 6.16 Unlocks should feel earned

Unlocks should primarily feel like **rewards for progress**, not arbitrary surprises.

## 6.17 Progression fantasy

The core feeling should be:

> I worked toward this and earned it.

The game can contain luck, but long-term success should still feel connected to sustained effort and planning.

---

# 7. Races, cups & competition

## 7.1 Stats determine performance; randomness creates tension

Race performance should primarily come from the Voidling's stats.

Randomness exists to keep races exciting rather than to override progression.

The clearest example discussed is a small random chance to **fall while running**, causing lost time.

A provisional example given during the interview was checking approximately every 3 seconds with around a 0.5% fall chance. Treat those exact numbers as tuning placeholders, not locked balance constants.

## 7.2 Falling is simple time loss

For now, falling does not need to affect morale, personality, or long-term state.

It simply costs race time.

## 7.3 Racing should be encouraged through progression

Racing is not mandatory every minute, but it should unlock meaningful content.

Examples:

- winning a certain cup tier unlocks new content;
- medals/trophies similar in role to Chao Garden race medals;
- advanced progression can require cup victories.

## 7.4 Standard races vs special championships

Current direction:

- ordinary races remain generally available;
- certain championship cups can appear only at specific times/rotations;
- future multiplayer may include a daily multiplayer race.

## 7.5 Existing cheer/stamina interaction remains

The current race interaction already includes a **Cheer** button similar in role to Chao Garden.

Current intended behavior:

- stamina drains naturally during a race;
- cheering consumes additional stamina;
- if stamina reaches zero, the Voidling runs more slowly;
- using Cheer is therefore a timing/resource decision;
- its impact depends on the Voidling and race situation.

This interaction is already sufficiently specified for now and does not need extra mechanics added simply for complexity.

## 7.6 Casual races and serious cups should feel different

There should be a recognizable distinction between ordinary/casual racing and major cup/championship content.

## 7.7 Rewards should favor winning

Racing should not become an easy grind source where repeated losses still generate large rewards.

Major rewards belong primarily to winning.

## 7.8 No general race-attempt limit

Players should be able to race repeatedly.

A proposed cooldown for major cups was explicitly reconsidered and should **not** be assumed for now.

## 7.9 Race length can vary

Race duration does not need one global limit.

Higher cups can be longer and contain more obstacles/segments.

## 7.10 Stat influence does not need explicit live explanation

Stats should clearly matter to the relevant race segments, but the race UI does not need to continuously display formulas explaining why every action succeeded.

The presentation can remain close to the observational feel of Chao Garden.

## 7.11 Training and racing are equally important

Training creates capability; racing validates and rewards it. Neither should become irrelevant to the other.

## 7.12 Races may be somewhat chaotic

A little chaos, stumbling, and uncertainty is desirable. The race should feel exciting rather than like a deterministic spreadsheet animation.

## 7.13 Failure can hurt lightly

Failure may cost something, particularly in major cups.

The clearest example is losing an entry fee when entering a significant cup and failing to win.

The penalty should sting enough to create stakes without feeling cruel.

## 7.14 Cups are mechanically functional, but NPCs have identity

Cups do not need elaborate story campaigns.

However, each cup should have a recognizable fixed NPC cast with:

- names;
- small personality/flavor;
- consistent presence within that cup.

Normal races can use more randomized opponents.

Different cups can have different casts.

## 7.15 Race strength comes from raising choices

A Voidling excels based on how its stats were bred and raised. There is no separate universal racer archetype that overrides stat development.

## 7.16 Race rewards should not replace idle income

Racing can return money, but it should not become the primary currency farming method because that would undermine the idle economy.

The current preferred major-cup reward model is:

- entry fee creates financial stakes;
- winning can refund the entry fee;
- the meaningful profit is an item, medal, trophy, unlock, or other progression reward.

A later economy answer also suggested placement-based partial entry-fee returns. This needs one explicit final decision in a later refinement pass rather than being silently implemented either way.

## 7.17 Leaderboards later

When multiplayer/competitive infrastructure exists, both local and global leaderboards are desirable.

## 7.18 Desired race feeling

Racing should feel like:

- a payoff for stats the player deliberately raised;
- tense even when the player prepared well;
- slightly like watching a horse race where the player has invested in the competitor;
- exciting because small chance events can still change the moment-to-moment outcome.

## 7.19 Chance can create large swings, but not arbitrary daily unfairness

Chance is acceptable in races, and later clarification explicitly allowed occasional strongly positive or negative race outcomes caused by chance.

This should not mean every progression system is equally random. Daily rewards, for example, should remain fair and predictable.

---

# 8. Raising, training, stats & care

## 8.1 Active + passive training

Stat growth should use a combination of:

- **active training/feeding/items**;
- **passive Garden training zones/modules** inspired by Digimon Championship DS.

Active training should always be faster/more effective than passive training.

Passive training exists so a player can leave a Voidling progressing while the game stays open.

## 8.2 Passive training continues until stopped

A Voidling placed into a passive-training zone should gradually gain progress until the player manually stops/removes it.

It is not a fixed one-shot timer session by default.

Passive training does not continue while the game is closed under the current idle model.

## 8.3 Growth speed is limited mainly by resources and time

There is no need for a separate arbitrary daily training cap.

Practical constraints include:

- available money/items;
- player time;
- passive elapsed open-game time;
- the stat's rank-based maximum.

## 8.4 Rank determines maximum stat capacity

The stat rank acts as the hard growth cap, similar in role to Chao Garden.

A Voidling with an A-rank stat has a lower maximum potential than one with S rank.

Normal training cannot simply push beyond that rank-defined ceiling.

## 8.5 Rank improvement comes from major lifecycle/genetic events

The rank itself should not be freely increased through ordinary training items.

Current intended routes to improving rank/potential include:

- growing from child to adult/evolution;
- reincarnation-related progression;
- breeding better genetics.

Exact rules remain subject to the lifecycle deep dive.

## 8.6 Food and Stamina

Feeding should fill the Chao Garden-like role of supporting Stamina development.

Favorite food provides slightly more benefit than ordinary food.

Cleaning is not required as a care mechanic.

## 8.7 Some training items may trade one stat for another

Stats do not inherently need a universal tradeoff such as "Power always reduces Run."

However, specific powerful items may intentionally:

- grant a large increase to one stat;
- remove/reduce progress from another stat.

This creates item-level strategic tradeoffs without forcing every stat pair into opposition.

## 8.8 Training/item effects should be understandable

The player should have enough information to make deliberate item choices.

The user explicitly clarified that training/item information should be **insightful/visible rather than completely hidden**, using Chao Garden-style numeric stat presentation as the reference.

Exact presentation of per-item values can still remain stylistically simple rather than becoming a probability spreadsheet.

## 8.9 Min-maxing is supported

Players should be allowed to deliberately optimize stats and builds.

## 8.10 Small daily activities are acceptable, but goals remain self-directed

The game can offer small things to do each day, but it should not dictate one mandatory daily routine.

Players decide whether today's focus is breeding, training, racing, collecting, Garden expansion, or simply idling.

## 8.11 Mood is not a stat-growth modifier initially

Mood/personality can remain cosmetic for stat growth in the initial version.

## 8.12 Training progress does not continue while closed

Passive training follows the same open-game idle rule as the rest of the current design.

## 8.13 Training choices can be irreversible

If the player invests in the wrong stat or uses an item with an unwanted tradeoff, there does not need to be a reset/refund mechanic.

Both small and large mistakes may have permanent consequences for that Voidling.

## 8.14 Active item training is immediate

Using an active stat item/food should apply its effect immediately.

Passive zone training is the slow time-based alternative.

## 8.15 No punishment for simply not training

A Voidling does not lose stats or suffer simply because the player did not train it recently.

## 8.16 Minimal tutorial, then discovery

The game should provide a small introductory tutorial and then allow the player to discover deeper optimization themselves.

## 8.17 Desired training feeling

Training should make the player feel:

> I am making visible progress toward a build or goal I chose.

## 8.18 Hidden happiness and lifecycle care consequence

Care does have one important non-stat consequence: lifecycle outcome.

Current concept:

- hidden `Happiness` range approximately 0-100;
- starts at 0;
- gains points from positive actions such as petting and feeding;
- loses points from negative treatment such as throwing the Voidling;
- can lose points from very long periods without attention while the game is actively running;
- the value is **completely hidden** from the player;
- the player reads happiness through behavior/feedback instead of a meter.

If the Voidling reaches the relevant lifecycle endpoint without being happy enough, it may **die instead of reincarnating**, comparable in role to Chao Garden's care consequence.

Exact thresholds, decay rates, and reincarnation rules remain unresolved until the lifecycle section is refined.

---

# 9. Economy & rewards

## 9.1 Primary money source: slow open-game idle income

The Garden should generate currency slowly over time while the game is open.

The baseline model is a small amount **per minute** rather than requiring the Voidling to perform a specific money-making animation.

The player should be told clearly that keeping the game open generates income; idle earning is an intended mechanic, not a hidden exploit.

## 9.2 Possible active-presence bonus requires refinement

The interview suggested that the player might earn somewhat more while they are actively doing something on the computer—for example typing on the keyboard—than when the machine is simply unattended.

This idea is **not yet implementation-ready**. It needs a later UX/privacy/platform feasibility discussion before being treated as a hard requirement.

The stable requirement is only that open-game idle time earns money.

## 9.3 Eggshells are sellable hatch rewards

After an egg hatches, its shell can be sold once, similar in role to Chao Garden.

Rare eggs can produce more valuable shells.

This is a one-time post-hatch value source, not a repeatable shell farm from the same egg.

## 9.4 Daily login chains

Daily login chains are desirable.

The player should be able to see upcoming rewards in the chain rather than receiving fully opaque random rewards.

Daily login rewards should be comparatively fair and predictable.

## 9.5 Daily missions

Daily missions are desired.

Most can be achievable in a normal session, while some longer daily objectives are acceptable occasionally.

The exact mission catalog and completion window remain unresolved.

## 9.6 Rare drops can be genuinely rare

Some rewards may have genuinely low drop/appearance rates. No universal pity system is required.

## 9.7 No maximum wallet

There is no need for a hard cap on how much currency the player can save.

## 9.8 Prices are fixed

Normal item prices should be fixed rather than dynamically fluctuating with inflation or market simulation.

The **inventory** may rotate; the price of a given item should remain understandable and stable unless deliberately rebalanced by a game update.

## 9.9 Cosmetics are optional purchases, not merely an anti-inflation sink

Cosmetics can cost money and provide an ongoing spending option, but they should exist because customization is enjoyable, not solely because the game needs to delete currency from the economy.

## 9.10 Spending style is player-defined

The game should support both:

- saving for expensive purchases;
- making frequent smaller purchases.

The player chooses the strategy based on their current project.

## 9.11 No daily earning cap

If the player keeps the game open for a very long time, idle earnings can continue accumulating.

The economy should be balanced with this behavior in mind rather than stopping income after an arbitrary daily limit.

## 9.12 Trading can come with multiplayer

For now the economy is single-player.

Once multiplayer exists, player-to-player trading may be added.

Do not build the initial economy around a speculative player market before multiplayer exists.

## 9.13 No inflation system for now

The game does not need simulated inflation/anti-inflation mechanics in the initial design.

## 9.14 No random money jackpot layer

There is no current need for a generic random chance to suddenly receive bonus money during normal idle earning.

Randomness is better attached to systems where it creates meaningful excitement, such as race incidents, genetic outcomes, or rotating rare inventory.

## 9.15 Session reward is primarily the login structure

There does not need to be a separate arbitrary "played a session" payout if daily login already fulfills that role.

## 9.16 Race entry creates financial stakes

Money can be lost by entering races/cups and failing to achieve the required placement/win condition.

Racing should not be the primary currency grind, but financial stakes make major cups more exciting.

The exact refund curve still needs a final decision because two related ideas emerged:

1. **winner-focused model:** winner gets entry fee back; main profit is item/medal/unlock;
2. **placement model:** some entry fee is returned based on finish position, with last place receiving no consolation.

Do not hard-code one until this is explicitly resolved.

## 9.17 One currency for now

Use one main currency initially.

No premium/secondary currency is currently required.

## 9.18 No loans/advances

Players cannot borrow money or go into debt to buy items/enter races.

## 9.19 Economy should be immediately understandable

The basic economy should be simple enough that the player quickly understands:

- how idle income works;
- what things cost;
- what can be sold;
- how races can lose/refund entry money;
- when the shop rotates.

Complexity should come from deciding what to spend on, not deciphering hidden currency rules.

## 9.20 Time is a resource

Time is part of the economy because money, passive training, incubation, cooldowns, and rotations all use open-game elapsed time.

This supports the idle-game fantasy of making progress while the game sits beside another activity.

## 9.21 Fun over rigid fairness, but not arbitrary unfairness

When forced to prioritize, the design should favor **fun** over sterile mathematical fairness.

However, this does not mean every system should feel unfair.

Current distinction:

- **daily login rewards:** fair and predictable;
- **races:** chance can create unusually positive or negative outcomes;
- **shop:** rotating availability can create lucky/unlucky timing;
- **rare genetics/cosmetics:** luck is expected;
- ordinary economy rules should remain understandable.

---

# Cross-system requirements currently established

Treat the following as product-level constraints until explicitly revised:

1. **Breeding is deliberate and player-initiated.** No autonomous mating.
2. **Two visible DNA profiles are the normal inheritance source.** No secret third normal-stat layer.
3. **Exact offspring outcomes remain uncertain.** No perfect prediction calculator in normal gameplay.
4. **Luck always remains part of breeding.** Better parent selection improves possibilities but does not guarantee the result.
5. **Breeding supports performance and collection goals.** All-S stats, rare colors, and prestige lines are equally legitimate projects.
6. **Later racing should require better bloodlines.** Genetics must matter to progression.
7. **The game is idle-first but currently not offline-progress-first.** Mechanical progress occurs while open.
8. **The Garden follows real-world local time and seasons visually.**
9. **The Garden stays cozy and low-demand.** No cleaning chores or closed-game neglect punishment.
10. **Personality exists mainly for atmosphere in v1.** It should not complicate race optimization yet.
11. **Care still matters through hidden happiness.** Poor care can eventually prevent reincarnation and lead to death.
12. **Rare achievements remain genuinely rare.** No universal pity system.
13. **A permanent late-game trophy form is desirable.** It can race, cannot breed, and gains no free race strength beyond its stats.
14. **Players can abandon bad lines using Goodbye.** The design does not guarantee reversible genetics/training.
15. **Active training is faster than passive training.** Passive zone training is an open-game idle layer.
16. **Stat rank sets the growth ceiling.** Normal training cannot bypass it.
17. **Races primarily reward preparation/stats but retain small chance events for tension.**
18. **Major cups can carry entry-fee stakes.** Racing should not replace idle income as the best money farm.
19. **The basic economy uses one currency, fixed prices, slow open-game idle income, and no earning cap.**
20. **Daily rewards should be predictable, while races/shop/rare genetics may contain stronger chance swings.**
21. **Progression is intentionally grindy and self-directed.** The player should feel they worked toward major outcomes.

---

# Explicitly unresolved / pending design work

Do **not** silently decide these during implementation:

- exact breeding cooldown duration;
- exact egg capacity and upgrade tiers;
- exact incubation durations and acceleration values;
- exact shop rotation interval and fixed egg/item slot counts;
- exact requirements for the immortal/trophy transformation;
- exact stat caps per E/D/C/B/A/S rank;
- exact lifecycle rules for rank promotion on adulthood/reincarnation;
- exact probability/eligibility for rare +1-rank offspring improvement;
- complete color/pattern/shiny/mutation inheritance rules;
- rare-trait generation/transmission limits;
- appearance encyclopedia/collection tracking;
- exact Garden Voidling capacity;
- exact decoration cap;
- exact Garden module/grid geometry;
- exact passive-training rates and module stat effects;
- exact Garden upgrade economy;
- whether personality receives deeper numerical simulation later;
- whether personality eventually affects racing;
- whether inbreeding later gains consequences beyond hatch failure;
- exact hidden-Happiness thresholds, gains, losses, decay, and reincarnation requirement;
- exact active item effects, including stat-tradeoff items;
- exact standard race/cup ladder and unlock map;
- exact temporary championship schedule;
- exact running-fall probability and evaluation interval;
- exact major-cup entry fees;
- whether cup entry refunds are winner-only or placement-based;
- exact cup prize/medal/item catalog;
- exact daily mission catalog;
- exact daily login chain/rewards;
- exact idle income rate;
- whether an active-computer-use income bonus should exist at all;
- exact eggshell sale values;
- exact seasonal-event structure;
- multiplayer race, trading, and leaderboard implementation details.

---

# Recommended next design interviews

The next refinement work should prioritize:

1. **Return to Appearance & Rare Bloodlines** — complete the unfinished 20 + 5 pass.
2. **Breeding deep dive** — eligibility, cooldowns, costs, animation, information visibility, repeat-breeding incentives.
3. **Family tree / lineage / inbreeding** — relatedness depth, warnings, burden visibility, cleansing, failed eggs, prestige lines.
4. **Eggs / incubation / hatching** — capacity, timers, acceleration, ready-state interaction, failed eggs, hatch presentation.
5. **Evolution / lifecycle / reincarnation** — age pacing, morphology, death, hidden Happiness requirement, rank changes, trophy transformation.
6. **Garden training modules** — exact module progression, passive-training behavior, layout rules, upgrades.
7. **Race ladder & reward resolution** — cups, entry-fee refund model, NPC casts, special championships, prizes, endgame racing.
8. **Economy tuning pass** — idle rate, egg prices, shells, daily chain, rotating shop, event rewards.

---

# Usage rule for implementation planning

When this document and the technical implementation plan differ in specificity:

- use this document as the source of truth for **player-facing intent and gameplay requirements** established by the refinement interview;
- use the existing implementation plan as the source of truth for **technical architecture** where gameplay intent has not changed;
- when two interview answers conflict, preserve the conflict as an unresolved design decision rather than choosing silently;
- do not invent values for unresolved items merely to finish implementation;
- prefer configurable/data-driven tuning for unresolved numerical values so later refinement does not require structural rewrites.
