---
paths:
  - "Scripts/Presentation/**/*.cs"
  - "Scripts/UI/**/*.cs"
  - "Scripts/Actors/**/*.cs"
  - "Scripts/Garden/**/*.cs"
  - "Scripts/Race/**/*.cs"
---

# Presentation rules

- Godot Nodes are presentation/input adapters, not the source of truth for game rules.
- Prefer composed child nodes and plain C# collaborators over deep inheritance.
- Keep signals/events scoped to a scene or feature owner; do not add a global event bus.
- Screens should receive the application capabilities/state they need instead of reaching through a service locator.
- New player-facing strings use localization keys. User-generated Voidling names remain literal and must not be auto-translated.
- UI layouts should use Containers, minimum sizes and wrapping so pseudolocalized/long strings can expand.
- Visual timing must not determine deterministic domain outcomes such as genetics or race results.
