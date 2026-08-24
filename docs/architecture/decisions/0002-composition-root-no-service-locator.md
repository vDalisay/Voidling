# ADR 0002: Manual composition root; no service locator

**Status:** Accepted  
**Date:** 2026-08-24

## Context

The MVP uses `GameSession.Instance` broadly. This is convenient but hides dependencies and makes isolated tests/screens harder. .NET guidance recommends avoiding static access/service-locator patterns. Godot also warns against broad invasive global managers.

## Decision

- Keep a game-lifetime Godot root/autoload only where broad lifetime is genuinely useful.
- Feature code receives dependencies explicitly through constructors (plain C#) or setup/configuration methods/node ownership (Godot Nodes).
- Add one explicit composition root responsible for constructing and wiring concrete services.
- Do not add an IoC/DI container yet.

## Consequences

- dependencies become searchable and testable;
- the current singleton can survive temporarily as a compatibility facade while callers migrate;
- manual wiring is intentionally preferred while the object graph is small;
- adding a container later requires a new ADR and a demonstrated wiring/lifetime problem.
