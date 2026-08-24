# ADR 0001: Functional core with a Godot shell

**Status:** Accepted  
**Date:** 2026-08-24

## Context

Voidling's roadmap is dominated by deterministic simulation systems (genetics, breeding, lifecycle, stats and racing), while the current MVP places some of those decisions inside Godot Nodes.

## Decision

Game rules and deterministic simulation live in pure C# Domain code. Godot Nodes/scenes are Presentation and Infrastructure adapters around that core. Application use cases coordinate domain operations.

Compile-time dependencies point inward. `Scripts/Domain/**` may not depend on Godot.

## Consequences

- domain behavior can run headlessly and be unit tested;
- animation/frame rate cannot accidentally become a game-rule input;
- Godot Resources remain useful for authoring, but are validated/converted before Domain consumption;
- some MVP controllers require incremental extraction;
- we avoid a big-bang rewrite by keeping compatibility facades during migration.
