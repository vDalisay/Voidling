# ADR 0007 — Development LAN multiplayer transport

**Status:** Accepted

## Context

Voidling's production multiplayer stack uses GodotSteam/Steam lobbies and Steam Networking Messages, while single-player remains fully functional without Steam. Most multiplayer behavior lives behind Godot-free Application ports and can therefore be validated independently of Steam.

Waiting until two Steam accounts are available to test every Garden, trade, challenge, and deterministic-race change would make platform integration failures difficult to distinguish from gameplay-protocol failures.

## Decision

Add a **development-only ENet LAN adapter** implementing the same `IPlatformIdentityService`, `ILobbyService`, and `IMultiplayerTransport` ports used by Steam.

The LAN adapter:

- activates only through explicit `--voidling-lan-host` or `--voidling-lan-join=...` flags;
- uses Godot `ENetMultiplayerPeer` over UDP;
- keeps Application message channels and typed payloads unchanged;
- uses a host-relayed topology for client-to-client messages;
- preserves logical sender/target IDs and reliable/unreliable delivery intent;
- can use per-process development save profiles for same-machine testing;
- does not emulate Steam friends, invites, leaderboards, SDR, or NAT traversal;
- is not a supported shipping multiplayer mode.

Normal launch composition remains Steam-if-available, otherwise offline. LAN mode is never selected implicitly.

## Consequences

### Positive

- Real cross-process socket behavior can be tested without Steam.
- Trading and race synchronization can be debugged using the same Application protocols used in production.
- CI can run a two-process typed-Hello socket smoke test.
- Two local processes can safely use distinct `user://` saves through explicit development profile flags.
- Steam-specific failures remain isolated to the Steam adapter validation phase.

### Tradeoffs

- There is a second Infrastructure transport implementation to maintain.
- LAN host migration is deliberately not equivalent to Steam lobby-owner migration; host exit ends the development LAN session.
- LAN success does not prove Steam callback signatures, invite behavior, leaderboards, SDR, or internet connectivity.

## Guardrails

- Do not move ENet types into Application or Domain.
- Do not introduce LAN-specific branches into Garden, trading, challenge, or race rules.
- Do not expose LAN mode as a normal player-facing network option without a separate product decision.
- A LAN failure must never prevent ordinary offline single-player startup.
- Real two-account GodotSteam validation remains required before the multiplayer branch is considered production-ready.

See `docs/LAN_MULTIPLAYER_TESTING.md` for the test procedure.
