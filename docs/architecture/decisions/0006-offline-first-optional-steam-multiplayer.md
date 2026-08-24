# ADR 0006 — Offline-first optional Steam multiplayer

**Status:** Accepted for multiplayer implementation

## Context

Voidling is primarily a singleplayer idle creature-raising game. Multiplayer adds connected Garden zones, trading, challenges and friends leaderboards, but those features must not make Steam or an internet connection a prerequisite for the core game.

The existing architecture already treats platform integrations as Infrastructure behind Godot-free Application ports. `GameBootstrap` is the composition root and `GameSession` must not become a multiplayer manager.

## Decision

Singleplayer has architectural priority over multiplayer.

The following are hard invariants:

- Voidling starts and remains playable without an internet connection.
- Voidling starts and remains playable when the Steam client is not running.
- Voidling starts and remains playable when GodotSteam is not installed/loaded in a development or CI environment.
- Steam initialization failure is a capability loss, never a fatal startup error.
- Saving, Garden simulation, breeding, hatching, training, local racing, shop/progression and local lifecycle systems do not depend on multiplayer services.
- Domain and Application code never reference GodotSteam or the Steam singleton.
- Steam-specific code lives in Infrastructure and is selected by Bootstrap.
- When Steam is unavailable, Bootstrap composes offline/no-op adapters implementing the same Application ports.
- Multiplayer presentation must receive its dependencies explicitly; no new global service locator is introduced.
- Local saves remain the ownership source for the casual multiplayer trust model. Guardrails protect ordinary consistency errors, not determined save editing.

## Initial implementation shape

```text
GameBootstrap
    |
    +-- existing singleplayer GameSession composition
    |
    +-- OptionalMultiplayerComposer
            |
            +-- Steam available
            |      GodotSteamRuntime
            |      SteamPlatformIdentityService
            |      SteamLobbyService
            |      SteamNetworkingMessagesTransport
            |
            +-- Steam unavailable
                   OfflinePlatformIdentityService
                   OfflineLobbyService
                   OfflineMultiplayerTransport
```

The multiplayer Application layer sees only:

- `IPlatformIdentityService`;
- `ILobbyService`;
- `IMultiplayerTransport`;
- typed protocol/application services built on those ports.

## Consequences

Positive:

- CI and development do not require Steam.
- players can play the entire singleplayer game offline;
- future multiplayer UI can cleanly disable online controls while leaving the game intact;
- Steam/GodotSteam API drift is isolated to Infrastructure;
- other transports could replace Steam later without rewriting genetics/racing/training.

Trade-offs:

- multiplayer code must handle `Unavailable` as a normal state;
- some integration behavior needs real two-account Steam testing outside CI;
- local-save trading deliberately cannot provide anti-cheat-grade ownership guarantees.
