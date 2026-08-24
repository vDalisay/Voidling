# ADR 0003: Domain rule data; Godot Resources as authoring adapters

**Status:** Accepted  
**Date:** 2026-08-24

## Context

Current balance values and presentation colors are concentrated in `GameRules`. The roadmap requires multiple configurable genetics, breeding, lifecycle and race rule sets. Godot custom Resources provide Inspector-editable serialized data, but Domain code must remain Godot-free.

## Decision

- Domain consumes typed plain C# rule objects.
- Designer-facing custom Godot Resources may author these values.
- Infrastructure validates and converts Resources to domain rule objects at startup.
- Presentation-only data such as stat colors remains separate from gameplay balance.
- Existing `GameRules` remains a compatibility facade during migration.

## Consequences

- designers can tune rules in the Godot Inspector;
- domain tests can construct rules without Godot;
- invalid resource data fails validation close to startup rather than corrupting simulation;
- we only add Resource types for currently used rule groups, avoiding speculative content schemas.
