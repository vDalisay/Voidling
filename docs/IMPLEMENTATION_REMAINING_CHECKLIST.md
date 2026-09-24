# Remaining implementation checklist

This checklist tracks confirmed remaining work against current `main`, `docs/AGENT_HANDOFF_IMPLEMENTATION_PLAN.md`, and `docs/GAMEPLAY_DESIGN_REFINEMENT_CONTEXT.md`.

Items under **Product decisions required before implementation** are deliberate stop conditions. They must not be implemented by inventing missing rules.

## Integration / release baseline

- [x] Wire the existing Garden real-time/season environment presentation into the live Garden scene.
- [x] Wire the existing lifecycle cocoon presentation into the live Garden scene.
- [x] Raise Garden event history from 80 to the specified 300 messages.
- [x] Fix the race-completion smoke shutdown regression.
- [x] Verify the complete GitHub Actions suite is green after the Garden/ambient integration work.
- [x] Stabilize the command-line LAN trade smoke so a completed durable exchange is not reported as failed by the Godot 4.6 Mono ENet teardown crash.

## Confirmed implementation-ready feature work

- [x] Add player-placeable Garden decorations, separate from functional training modules. Merged in PR #28.
- [x] Extend stat-driven ambient Garden behavior with confirmed Run/Stamina behavior and Swim shoreline affinity. Merged in PR #29.
- [x] Add Cup/championship scaffolding: stable Cup IDs, stable NPC casts, progression/unlock hooks and authorable content. Merged in PR #31 with full CI green.
- [ ] Continue production Voidling art ingestion through the centralized visual pipeline as new authored art revisions arrive.
  - [x] Latest authored Normal-body outline palette revision is integrated on `main` (`9f971202` / merge `71b4c3a3`) and keeps the centralized palette/resource path intact.
  - [ ] Ingest the next authored body/wing/crown/form revision when artwork is supplied; do not synthesize new production art or fork the visual pipeline.

## Lifecycle, biomes & encyclopedia (September 2026 design meetings)

Planned in `docs/LIFECYCLE_BIOMES_ENCYCLOPEDIA_IMPLEMENTATION_PLAN.md`; its §11 records every decision and §17 the implementation notes.

- [x] WP-A: life stages in open-game hours; happiness ≥ 70 is the only reincarnation condition.
- [x] WP-K: Chao Garden stats (levels 0–99, stat points, reincarnation to level 1 with 10%).
- [x] WP-B: adult form from the highest stat; Neutral adults show the artist's colors.
- [x] WP-H: incubation grows with S ranks and rarity.
- [x] WP-C / WP-E: biomes, biome tiles, stacking to level 4 (Swamp, Volcano), picking tiles up.
- [x] WP-L: eggs can be picked up and moved in the Garden.
- [x] WP-G: the Swamp guy (one-time spawn, Swamp-only hatching, respawn egg, untradable).
- [x] WP-I: the Journal.
- [ ] WP-J: register art for `neutral`, `run`, `water`, `fly`, `power` and `swamp-variant`, and biome ground art, as it arrives.

## Product decisions required before implementation

- [x] Lock the stat-driven morphology/evolution mapping: the highest stat at level 10+ decides the adult form (lifecycle plan §4.3). Implemented.
- [ ] Lock remaining appearance-inheritance probabilities, dominance, rare-trait depth and stacking rules. Neutral is a type whose adults show the artist colors (lifecycle plan §5.2); per-type hue ranges are still open.
- [ ] Lock the final trophy transformation recipe. Reincarnation is decided and implemented (happiness ≥ 70, lifecycle plan §4.4); the trophy form is still open.
- [ ] Lock Cup entry-fee/refund/reward economy details. Cup scaffolding keeps these values/rules out until decided.
- [ ] Decide whether active-computer-use income should exist; do not implement activity monitoring until its privacy/platform/UX requirements are approved.

## Current stopping point

All currently confirmed implementation-ready gameplay/system work is implemented and merged. Production-art ingestion remains an ongoing pipeline task rather than a missing gameplay system: the latest authored outline revision is already integrated, and the next implementation step requires new authored art. The remaining gameplay/system items are explicit product-decision stop conditions and should not be implemented by inventing rules.
