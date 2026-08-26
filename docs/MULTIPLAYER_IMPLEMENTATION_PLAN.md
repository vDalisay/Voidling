# Voidling Steam Multiplayer Implementation Plan

**Status:** Proposed implementation plan  
**Target branch:** `multiplayer-implementation`  
**Target runtime:** Godot 4.6.1 + C# / .NET 8  
**Platform:** Steam  
**Integration:** GodotSteam GDExtension + Steamworks matchmaking, Networking Messages, Friends and Leaderboards  
**Trust model:** Casual / client-owned saves. Protect consistency and ordinary misuse; do not build an anti-cheat backend.

> This plan extends the architecture in `ARCHITECTURE.md`. Domain and Application remain Godot-free. Steam/GodotSteam is an Infrastructure concern composed by `GameBootstrap`. Multiplayer must not become a second service-locator or a parallel gameplay architecture.

---

## 1. Product scope this architecture must support

Confirmed multiplayer features:

1. A **placeable connected Garden zone**.
   - A player can connect that zone with up to **16 Steam players**.
   - Voidlings placed/published into the connected zone appear for the other connected players.
   - Adding/removing/updating a shared Voidling is propagated to the session.
   - Remote Voidlings remain owned by their original player; receiving a visual copy must not insert it into another player's save.

2. **Voidling and egg trading** inside the connected zone.
   - Trading is casual and cooperative, not an economy requiring anti-cheat-grade authority.
   - General integrity checks should prevent accidental duplication, malformed payloads, stale offers and obvious spoofing.
   - Deliberate save editing is explicitly out of scope as a threat to defeat.

3. **Challenges for up to four players** while remaining in the connected zone.
   - Known challenge types: racing and future auto-battling.
   - A player posts an offer; other players opt in.
   - Do not create a second Steam lobby for every challenge unless a real need appears. The 16-player Garden lobby remains the rendezvous/session; challenges are application-level sub-sessions.

4. **Steam-friends leaderboard: multiplayer wins**.

5. **Steam-friends leaderboard: single-player race best times** per course/race definition.

6. **Daily race, friends-only**.
   - One locally enforced attempt per daily race.
   - Same daily course/seed for everyone.
   - Steam friends leaderboard for the day's result.
   - No worldwide leaderboard is required.

---

# 2. Architecture decision

## 2.1 Chosen stack

Use:

```text
Godot 4.6.1 C#
    |
    +-- GodotSteam GDExtension 4.22 (Steamworks SDK 1.65)
    |
    +-- Steam Matchmaking / Lobbies
    |      discovery, membership, owner, invites, metadata
    |
    +-- Steam Networking Messages
    |      P2P transport through Steam Networking Sockets / SDR
    |
    +-- Steam Friends
    |      identity, persona names, invite overlay
    |
    +-- Steam User Stats / Leaderboards
           multiplayer wins, race best-times, daily friend races
```

As of 2026-08-22, the Godot Asset Library lists **GodotSteam GDExtension 4.22**, based on **Steamworks SDK 1.65**, for Godot **4.4 and newer**. That includes the repository's current Godot 4.6.1 target. Pin this version for the first implementation spike rather than floating to whatever the Asset Library contains later.

### Why Networking Messages instead of `SteamMultiplayerPeer`

`SteamMultiplayerPeer` is useful for games that want Godot's high-level `MultiplayerAPI`, RPCs, `MultiplayerSpawner` and `MultiplayerSynchronizer` to behave like ENet over Steam.

Voidling's known multiplayer traffic is different:

- publish/remove a Voidling;
- trade commands;
- challenge offers and joins;
- deterministic race start/input commands;
- low-frequency Garden state;
- leaderboards handled separately by Steam.

This is naturally **message-oriented**, not scene-replication-oriented.

Steam's `ISteamNetworkingMessages` is specifically a message-oriented P2P API built on top of Steam Networking Sockets. Valve documents that it:

- establishes underlying peer sessions implicitly;
- supports reliable and unreliable messages;
- supports fragmentation/reassembly;
- can use Steam Datagram Relay;
- exposes numbered channels;
- guarantees reliable messages to the same host/channel are delivered exactly once and in order *if received*.

GodotSteam exposes the corresponding methods as:

```text
sendMessageToUser(remoteSteamId, data, flags, channel)
receiveMessagesOnChannel(channel, maxMessages)
acceptSessionWithUser(remoteSteamId)
closeSessionWithUser(remoteSteamId)
```

This avoids adding a second C# wrapper around a custom `MultiplayerPeer` class and keeps Steam IDs explicit throughout the social/trading layer.

## 2.2 Explicitly not chosen initially

### No dedicated/backend server

Not required for the intended trust model. Players who intentionally modify saves are allowed to do so. The implementation should guard against accidental corruption and trivial protocol spoofing, not operate like an MMO economy.

### No Steamworks.NET alongside GodotSteam

Do not maintain two Steam wrappers. GodotSteam already exposes the APIs needed here and is directly compatible with Godot's runtime/plugin model.

### No raw `ISteamNetworkingSockets` connection management initially

Networking Sockets gives more connection-level control, but Networking Messages is simpler and sufficient for Voidling's low-volume P2P protocol. Keep the transport behind an interface so it can be replaced if future realtime gameplay proves otherwise.

### No legacy Steam P2P API

Do not build new work on the older `sendP2PPacket`/`readP2PPacket` API. Prefer Steam Networking Messages / Networking Sockets generation APIs.

---

# 3. Fit with the current Voidling architecture

Current architecture already has the correct dependency direction:

```text
Presentation (Godot)
        |
        v
Application (Godot-free use cases)
        |
        v
Domain (pure deterministic C#)
        ^
        |
Infrastructure (Godot/platform adapters)
```

Relevant existing seams:

- `Scripts/Bootstrap/GameBootstrap.cs` is the only composition root.
- `Scripts/Application/Ports/**` already hosts infrastructure-facing ports.
- `Scripts/Infrastructure/**` is explicitly where future Steam/platform adapters belong.
- `GameSession` is transitional and **must not become the multiplayer manager**.
- `RaceEntryFactory` creates immutable race entries.
- `RaceParticipantSnapshot` already contains the pure data required to serialize a race entrant.
- `RaceSimulation` is deterministic/fixed-step and already suitable for lockstep multiplayer.
- `GameStateData` owns local persistent player state.
- `VoidlingData` / `EggData` use stable string IDs suitable for transfer protocol references.

The multiplayer implementation should therefore add focused Application services and Steam Infrastructure adapters without moving network concerns into genetics, breeding or race formulas.

---

# 4. Important design rule: three kinds of authority

Do not use the word "authority" as if it means one thing.

## 4.1 Local ownership authority

Each player's save remains authoritative for their own creatures, eggs and inventory.

Only the owner can publish/update one of their creatures into the shared zone.

```text
Steam user A owns Voidling X
    -> A publishes a network snapshot of X
    -> other peers render a remote copy
    -> nobody else inserts X into local ownership
```

Ownership changes only through a completed trade.

## 4.2 Session authority

The **Steam lobby owner** is the current session host/coordinator.

Clients send state-changing session commands to the host. The host validates them against session state and broadcasts accepted events.

This is not anti-cheat authority. It exists to give all honest clients one consistent ordering of events.

Valve's lobby documentation explicitly describes the lobby owner as a suitable decision-maker for arbitrated choices. Steam guarantees there is one lobby owner.

## 4.3 Simulation authority

For deterministic challenges, the agreed **start payload + seed + scheduled commands** are authoritative.

Each participating client runs the same pure simulation locally.

Do not stream race sprite positions as the source of truth.

---

# 5. New application-facing ports

Add narrow ports under `Scripts/Application/Ports/Multiplayer/` only when the corresponding implementation phase begins.

```csharp
public readonly record struct PlatformUserId(ulong Value);

public sealed record PlatformUser(
    PlatformUserId Id,
    string DisplayName);

public interface IPlatformIdentityService
{
    bool IsAvailable { get; }
    PlatformUser? LocalUser { get; }
}

public interface ILobbyService
{
    event Action<LobbySnapshot>? LobbyChanged;
    event Action<LobbyJoinRequest>? JoinRequested;

    Task<Result<LobbySnapshot>> CreateFriendsLobbyAsync(int maxMembers, CancellationToken ct);
    Task<Result<LobbySnapshot>> JoinAsync(ulong lobbyId, CancellationToken ct);
    Task LeaveAsync(CancellationToken ct);
    void OpenInviteOverlay();
}

public interface IMultiplayerTransport
{
    event Action<NetworkPacket>? PacketReceived;
    event Action<PlatformUserId>? PeerSessionFailed;

    void Send(PlatformUserId peer, NetworkChannel channel, ReadOnlyMemory<byte> payload, DeliveryMode delivery);
    void Poll();
    void Close(PlatformUserId peer);
}

public interface IFriendsLeaderboardService
{
    Task<LeaderboardResult> SubmitBestTimeAsync(string board, int milliseconds, IReadOnlyList<int> details, CancellationToken ct);
    Task<LeaderboardResult> SubmitScoreAsync(string board, int score, CancellationToken ct);
    Task<IReadOnlyList<LeaderboardEntry>> GetFriendsAsync(string board, CancellationToken ct);
}
```

Names can change during implementation, but preserve these responsibilities:

- Steam identity;
- lobby lifecycle/invites;
- byte-message transport;
- leaderboards.

Do **not** create one enormous `ISteamService` exposing every Steam method.

---

# 6. GodotSteam anti-corruption layer

## 6.1 Do not leak the `Steam` singleton inward

GodotSteam should only be referenced inside `Scripts/Infrastructure/Steam/**` and Bootstrap.

Recommended structure:

```text
Scripts/Infrastructure/Steam/
├─ GodotSteamRuntime.cs
├─ GodotSteamApi.cs
├─ SteamIdentityService.cs
├─ SteamLobbyService.cs
├─ SteamNetworkingMessagesTransport.cs
├─ SteamFriendsLeaderboardService.cs
└─ SteamNetworkCodec.cs
```

### `GodotSteamRuntime`

A Godot `Node` responsible only for:

- detecting whether the `Steam` singleton exists;
- initializing Steam once;
- pumping callbacks if callbacks are not embedded;
- exposing initialized/unavailable state;
- forwarding GodotSteam signals into typed C# events used by the concrete adapters.

It must not own gameplay/session state.

### `GodotSteamApi`

A very small C# wrapper around the GodotSteam GDExtension singleton.

Prefer dynamic Godot singleton access (`Engine.GetSingleton("Steam")` / `GodotObject.Call`) over adding a second unofficial compile-time C# Steam binding. Benefits:

- the normal .NET build can still compile without Steam-specific generated C# types;
- GodotSteam remains replaceable;
- CI/application tests can run without a Steam client;
- API version drift is isolated to one adapter;
- the rest of Infrastructure gets typed methods instead of stringly-typed calls.

Illustrative adapter shape:

```csharp
internal sealed class GodotSteamApi
{
    private readonly GodotObject _steam;

    public GodotSteamApi(GodotObject steam)
        => _steam = steam ?? throw new ArgumentNullException(nameof(steam));

    public ulong GetSteamId()
        => _steam.Call("getSteamID").AsUInt64();

    public string GetPersonaName()
        => _steam.Call("getPersonaName").AsString();

    public void CreateFriendsLobby(int maxMembers)
        => _steam.Call("createLobby", /* friends-only enum */, maxMembers);

    public void JoinLobby(ulong lobbyId)
        => _steam.Call("joinLobby", lobbyId);
}
```

This code is illustrative. During the spike, verify Variant numeric types and enum values against the pinned GodotSteam 4.22 class reference and add adapter tests around the actual calls.

## 6.2 Steam initialization

Use explicit startup rather than hiding platform setup throughout scenes.

GodotSteam's initialization guidance uses `steamInitEx(...)` and requires Steam callbacks to be processed, either through embedded callbacks or `Steam.run_callbacks()`/`runCallbacks()` depending on the exposed naming version.

For Voidling:

1. Bootstrap creates `GodotSteamRuntime`.
2. Runtime attempts initialization.
3. If Steam/GodotSteam is unavailable, set `IsAvailable=false` and continue single-player normally.
4. Multiplayer UI becomes unavailable with a localized explanation.
5. CI/headless smoke must not require Steam to be running.

Do not quit the game merely because Steam initialization failed; the core game is still valid single-player software during development/testing.

---

# 7. Steam lobby model

## 7.1 One lobby per connected Garden session

Create a **friends-only Steam lobby** with max members `16`.

Native Steam equivalent:

```cpp
SteamMatchmaking()->CreateLobby(k_ELobbyTypeFriendsOnly, 16);
```

GodotSteam equivalent:

```text
Steam.createLobby(Steam.LOBBY_TYPE_FRIENDS_ONLY, 16)
```

Creation completes asynchronously through GodotSteam's `lobby_created` signal / Steam's `LobbyCreated_t` result.

Joining uses `joinLobby(lobbyId)` / native `JoinLobby` and completes with `lobby_joined` / `LobbyEnter_t`.

Membership changes are observed from lobby chat/member callbacks. Steam's native API provides:

```text
GetNumLobbyMembers
GetLobbyMemberByIndex
GetLobbyOwner
```

## 7.2 Invites

Use the Steam overlay instead of building a custom friend picker first.

Native Steam:

```cpp
SteamFriends()->ActivateGameOverlayInviteDialog(lobbyId);
```

GodotSteam exposes the same capability through its Friends wrapper.

When a friend accepts an invite while already in-game, Steam posts `GameLobbyJoinRequested_t`; GodotSteam exposes the corresponding `join_requested` signal. If the game is not running, Steam can launch it with `+connect_lobby <lobby-id>`.

The startup adapter should inspect the Steam launch command line so deep-link joins can be supported after the basic in-game invite flow works.

## 7.3 Lobby metadata

Lobby metadata is rendezvous/configuration data, not gameplay replication.

Host sets values such as:

```text
voidling_protocol = "1"
build_compat      = "0.1"
zone_schema       = "1"
session_epoch     = "7"
state             = "garden" | "closing"
```

Do not put full Voidling JSON into lobby metadata.

Steam's lobby chat messages are also not the game transport; Valve notes that lobby chat bandwidth is limited and recommends the Networking API for game data.

---

# 8. Host migration

The connected Garden should survive the current host leaving when practical.

Steam lobbies always have an owner. When membership changes:

1. every client re-queries `GetLobbyOwner`;
2. if owner changed, update `SessionHostSteamId`;
3. if local user is the new owner, promote local `ConnectedZoneSession` to host;
4. increment an `AuthorityEpoch`;
5. broadcast a full `ZoneStateSnapshot`;
6. clients reject stale host events from older epochs.

Use this fencing value on host-authored events:

```csharp
public sealed record HostEnvelope(
    int ProtocolVersion,
    ulong SessionId,
    long AuthorityEpoch,
    long Sequence,
    Guid MessageId,
    string MessageType,
    byte[] Payload);
```

Every peer maintains the latest accepted replicated zone state, so the newly elected lobby owner has enough information to continue casual Garden sharing without a dedicated server.

A trade currently in `Prepared` state should be aborted on host migration unless it has already received a committed transaction ID.

A challenge that has not started should be cancelled/re-offered. A deterministic challenge already started can continue between its participants if transport is intact, but the simplest first implementation may cancel it cleanly on host loss.

---

# 9. Network protocol

## 9.1 Separate network contracts from save contracts

Never serialize `GameStateData` directly across the network.

Create explicit network DTOs under:

```text
Scripts/Application/Multiplayer/Protocol/
```

They are Godot-free and contain only fields that are intentionally shared.

Examples:

```csharp
public sealed record SharedVoidlingSnapshot(
    string CreatureId,
    PlatformUserId OwnerId,
    string DisplayName,
    string TintHex,
    LifeStage Stage,
    IReadOnlyDictionary<string, int> TrainingPoints,
    GenomeData Genome,
    IReadOnlyList<RareTraitData> RareTraits);

public sealed record SharedEggSnapshot(
    string EggId,
    PlatformUserId OwnerId,
    EggSource Source,
    ulong Seed,
    GenomeData Genome,
    float IncubationSeconds,
    float RequiredIncubationSeconds,
    string TintHex);
```

For display-only Garden replication, publish the minimum fields needed. For a trade transfer package, use a richer explicit DTO.

## 9.2 Initial codec

Use UTF-8 JSON via `System.Text.Json` for v1 protocol messages.

Why:

- low traffic volume;
- inspectable packets during development;
- no new serialization dependency;
- easy golden-payload tests;
- far below Steam Networking Messages' payload capability for normal Voidling snapshots.

Hide serialization behind:

```csharp
public interface INetworkCodec
{
    byte[] Encode<T>(T value);
    T Decode<T>(ReadOnlySpan<byte> payload);
}
```

If profiling later proves JSON wasteful, MessagePack/protobuf can replace the codec without changing Application services.

## 9.3 Message channels

Steam Networking Messages provides numbered channels. Use a small documented set:

```text
0 = ControlReliable
    handshake, snapshot request, host epoch, errors, keepalive

1 = ZoneReliable
    publish/remove Voidling, stable zone mutations

2 = ChallengeReliable
    offers, join/leave, ready, scheduled race commands, result agreement

3 = TradeReliable
    offer, accept, prepare, commit, abort

4 = GardenUnreliable
    optional transient position/facing/animation corrections
```

Use Steam's reliable send flag for channels 0-3. Use unreliable delivery only for data that is safe to lose and superseded quickly.

Steam documents that reliable messages on the same peer/channel are ordered. Do not assume ordering *between* channels.

## 9.4 Envelope and validation

Every packet carries:

```text
ProtocolVersion
SessionId
MessageType
MessageId
Sequence (when applicable)
Payload
```

Do not trust a `SenderSteamId` embedded in JSON. `SteamNetworkingMessages` identifies the actual remote Steam identity; the transport supplies that identity separately to Application.

Reject:

- unknown protocol versions;
- unknown message types;
- oversized messages;
- invalid enum values;
- strings beyond explicit limits;
- malformed IDs;
- messages from Steam IDs that are not members of the active lobby.

---

# 10. Connected Garden replication

## 10.1 Host keeps a replicated session model

Application model:

```csharp
public sealed class ConnectedZoneState
{
    public ulong LobbyId { get; init; }
    public PlatformUserId HostId { get; set; }
    public long AuthorityEpoch { get; set; }
    public Dictionary<PlatformUserId, ConnectedZoneMember> Members { get; } = new();
    public Dictionary<string, SharedVoidlingSnapshot> Voidlings { get; } = new();
    public Dictionary<Guid, ChallengeState> Challenges { get; } = new();
}
```

This is transient session state, not the local player's save aggregate.

## 10.2 Join handshake

```text
Steam lobby joined
    -> networking session established to host
    -> ClientHello(protocol, build, user)
    -> host validates lobby membership/version
    -> HostHello(sessionId, epoch)
    -> ZoneStateSnapshot
    -> client renders remote state
```

A full snapshot is always sent on join/rejoin. Do not attempt to replay an unbounded event log to a late joiner.

## 10.3 Publishing Voidlings

When a player places/publishes a Voidling into the connected zone:

```text
Presentation intent
    -> PublishVoidlingUseCase
    -> verifies local GameState owns CreatureId
    -> builds SharedVoidlingSnapshot
    -> client sends PublishVoidlingCommand to host
    -> host verifies sender owns that session entity namespace
    -> host applies state
    -> host broadcasts VoidlingPublishedEvent
    -> all presentations create/update remote actors
```

The local owner's actual `VoidlingData` remains in their own `GameStateData`.

A remote snapshot must never be fed to `AdvanceSimulationUseCase` as if the remote Voidling were locally owned.

## 10.4 Updates

Use reliable messages for meaningful changes:

- lifecycle stage changed;
- name changed;
- appearance/genome-visible snapshot changed;
- Voidling removed from zone.

For wandering/idle presentation, start with **local ambient behavior** plus occasional host/owner corrections. If common spatial consistency matters, send low-frequency unreliable transforms, not 60 FPS movement.

Suggested first target: 2-5 transform updates/second at most, only while a remote actor's position materially changes.

This is an idle Garden; do not build shooter-grade replication.

---

# 11. Trading design

## 11.1 Casual trust, strong consistency

The goal is not to prove an item was legitimately obtained. The goals are:

- both players clearly agree to the same transaction;
- wrong/stale objects cannot be traded accidentally;
- a duplicated network packet does not duplicate an item;
- a malformed peer cannot mutate another save directly;
- disconnects are handled predictably.

## 11.2 Trade state machine

```text
Draft
  -> Offered
  -> Accepted
  -> Preparing
  -> PreparedByBoth
  -> Committed

Any pre-commit state
  -> Aborted
```

Use a stable `TradeId` GUID.

### Flow

1. Initiator selects one or more owned Voidlings/eggs and a partner.
2. `TradeOfferCommand` goes to host.
3. Host checks both users are lobby members and no referenced session asset is already locked in another trade.
4. Host sends canonical `TradeOfferEvent` to both.
5. Receiver selects/accepts their side.
6. Host freezes canonical terms and sends `TradePrepare`.
7. Each client validates local ownership and writes a **pending trade journal record** to its save containing full outgoing/incoming transfer data.
8. Each client responds `TradePrepared`.
9. Only after both prepare, host broadcasts `TradeCommit(TradeId)`.
10. Each client applies the commit idempotently, saves immediately, records `TradeId` as applied, clears the journal and acknowledges.

This is not globally atomic without a server, but the prepare journal greatly reduces accidental data loss.

## 11.3 Deliberate rollback limitation

A player who edits/restores their save can duplicate items. That is accepted by product decision. Do not contort the architecture into fake anti-cheat cryptography that the local client can ultimately bypass.

## 11.4 Lineage transfer is mandatory

A trade cannot send only `VoidlingData` or `EggData`.

Current `RelationshipService` traverses parent IDs through a known population. If a traded creature arrives without its ancestors, future relatedness/inbreeding calculations become incorrect.

Introduce a minimal persistent lineage archive as part of the trading feature:

```csharp
public sealed record LineageArchiveEntry(
    string CreatureId,
    string DisplayName,
    string ParentAId,
    string ParentBId,
    int FamilyGeneration,
    string TintHex,
    bool InbreedingHistoryFlag);
```

`GameStateData` gains an archive/list/index of historical lineage entries. Migration populates it deterministically from currently known owned/departed creatures.

A transfer package includes ancestry closure up to the configured `RelationshipService` depth:

```csharp
public sealed record VoidlingTransferPackage(
    VoidlingData Creature,
    IReadOnlyList<LineageArchiveEntry> Lineage);

public sealed record EggTransferPackage(
    EggData Egg,
    IReadOnlyList<LineageArchiveEntry> Lineage);
```

On import:

- preserve original stable creature/egg IDs;
- merge lineage entries by ID;
- reject conflicting entries for the same ID unless the incoming entry is byte-for-byte/equivalently identical on lineage identity fields;
- never reroll genome, egg seed, rare-trait provenance or viability;
- add the received object to local ownership only during committed trade application.

Refactor relatedness lookup to read from the lineage graph/archive rather than requiring full historical `VoidlingData` instances forever.

This change is justified by trading and improves family-tree persistence independently of networking.

---

# 12. Challenge/lobby-inside-lobby architecture

Do not create nested Steam lobbies for a 2-4 player race.

Inside `ConnectedZoneState`, maintain challenge records:

```csharp
public enum ChallengeKind
{
    Race,
    AutoBattle
}

public enum ChallengePhase
{
    Offered,
    Forming,
    Ready,
    Running,
    Completed,
    Cancelled
}

public sealed record ChallengeState(
    Guid ChallengeId,
    ChallengeKind Kind,
    PlatformUserId Creator,
    IReadOnlyList<PlatformUserId> Participants,
    int MaxParticipants,
    ChallengePhase Phase);
```

Known max participants for current challenges: **4**.

Flow:

```text
player posts challenge
    -> host broadcasts offer
friends click Join
    -> host validates <= 4
creator/host starts
    -> mode-specific start payload created
    -> participants enter challenge presentation
    -> nonparticipants remain in connected Garden
```

Use a small mode handler seam because two concrete modes are already known:

```csharp
public interface IMultiplayerChallengeHandler
{
    ChallengeKind Kind { get; }
    Result<byte[]> BuildStartPayload(ChallengeContext context);
    void HandleNetworkCommand(ChallengeCommand command);
}
```

Do not build a generic plugin framework beyond what Race and AutoBattle actually need.

---

# 13. Multiplayer racing

## 13.1 Reuse the deterministic core

Current architecture is already ideal:

```text
RaceEntry
  -> RaceSimulation (pure fixed-step C#)
  -> state/events
  -> RaceScreen
```

Multiplayer should add orchestration around this, not fork the race formulas.

## 13.2 Multiplayer race entry

Add a factory that accepts player-selected snapshots rather than generating CPUs:

```csharp
public sealed class MultiplayerRaceEntryFactory
{
    public RaceEntry Create(
        IReadOnlyList<RaceEntrant> entrants,
        ulong simulationSeed,
        RaceRules rules);
}
```

Every participant receives the same serialized immutable entry:

```text
ChallengeId
CourseId / course definition version
RaceRulesVersion
SimulationSeed
RaceParticipantSnapshot[]
Mutation/display metadata needed by RaceScreen
```

Before starting, each client computes a hash of the canonical start payload. Host only starts when participants acknowledge the same hash.

## 13.3 Cheer/input synchronization

Cheer changes deterministic race state and therefore cannot be a purely local action.

Use host-scheduled lockstep commands:

```text
client presses Cheer
    -> CheerRequested(inputSequence)
    -> host validates participant
    -> host schedules command for SimulationTick N
    -> host broadcasts RaceCommandScheduled(N, participant, Cheer)
    -> every participant applies it at tick N
```

Add a small input buffer (for example several fixed simulation ticks) rather than rollback/netcode complexity. Exact latency budget is a tuning value discovered in the multiplayer spike.

Use reliable Challenge channel messages for race commands.

## 13.4 Desync detection

At intervals and at race completion, compute a lightweight deterministic checksum over authoritative simulation state/event counters.

```text
RaceStateChecksum(tick, participant states, finish order, RNG-relevant state)
```

Peers report hashes to host.

On mismatch:

- log protocol/build versions and hashes;
- do not try to hot-correct the simulation in v1;
- host result can be used for presentation/reward handoff;
- surface a non-fatal "race sync issue" diagnostic in debug builds.

The important outcome is discovering nondeterminism during testing.

## 13.5 Rewards

For a casual friend race, each client applies the result relevant to its own local save through an Application use case. Do not let a remote packet directly edit `Coins` or arbitrary inventory.

`MultiplayerRaceResultUseCase` receives a validated challenge result model and performs the local reward mutation.

---

# 14. Future auto-battle

Auto-battle should follow the same architecture as racing:

```text
immutable battle entry
    -> deterministic pure simulation if practical
    -> host-scheduled player commands only if the mode has active input
    -> typed result
    -> local result use case
```

Do not couple `ChallengeCoordinator` to `RaceSimulation`; it coordinates participant/session lifecycle and delegates mode behavior.

---

# 15. Steam friends leaderboards

Steam leaderboards are a good match for the accepted casual trust model.

Valve's leaderboards support:

- persistent per-player scores;
- ascending/descending sort;
- milliseconds display;
- up to 64 `int32` detail values per entry;
- global/friends queries;
- up to **10,000 leaderboards per Steamworks title**.

GodotSteam exposes:

```text
findLeaderboard(name)
findOrCreateLeaderboard(name, sortMethod, displayType)
uploadLeaderboardScore(score, keepBest, details, leaderboardHandle)
downloadLeaderboardEntries(start, end, requestType, leaderboardHandle)
```

with async signals such as:

```text
leaderboard_find_result
leaderboard_score_uploaded
leaderboard_scores_downloaded
```

## 15.1 Multiplayer wins

Leaderboard key:

```text
voidling_multiplayer_wins_v1
```

Sort: descending numeric.

Keep a local persistent total as well. On a confirmed multiplayer win:

1. increment local total;
2. save;
3. upload the total to Steam;
4. friends leaderboard reads `Friends` request type.

If Steam upload fails, retry later from the local total; Steam is a social projection of local progress, not the only copy.

## 15.2 Single-player best time per course

Leaderboard naming:

```text
voidling_course_<stable-course-id>_v<rules-version>
```

Sort: ascending.  
Display: milliseconds.

Use `keepBest=true` / native `KeepBest` behavior so slower runs do not replace a better time.

Optional details can include compact values such as:

```text
race rules version
Voidling cosmetic/form ID
selected stat summary/checksum
```

Do not attempt to serialize genomes into leaderboard detail integers.

## 15.3 Friends query

Valve exposes `k_ELeaderboardDataRequestFriends` for `DownloadLeaderboardEntries`; the start/end values are ignored for that request type. Use this rather than downloading global rows and filtering manually.

---

# 16. Daily friend race

## 16.1 Deterministic daily content

Use **UTC date** as the shared day boundary.

```text
DailyKey = yyyy-MM-dd (UTC)
DailySeed = StableHash("daily-race", DailyKey, DailyRulesVersion)
```

From the seed, deterministically select:

- course from an eligible daily pool;
- optional course modifiers;
- CPU opponents if the daily format contains CPUs;
- any deterministic presentation-independent random configuration.

The same game build/rules version produces the same daily race without a server.

## 16.2 One attempt

Enforce in the local save because deliberate editing is accepted.

Persist:

```csharp
public sealed class DailyRaceAttemptData
{
    public string DailyKey { get; set; } = "";
    public string RulesVersion { get; set; } = "";
    public string CreatureId { get; set; } = "";
    public DailyRaceAttemptState State { get; set; }
    public ulong SimulationSeed { get; set; }
    public int? FinishedMilliseconds { get; set; }
}
```

Mark the attempt **Started before the race begins** and save immediately.

For crash friendliness, persist enough immutable race-entry data to resume the same attempt after restart rather than granting a fresh roll. Once finished, mark it completed and upload the score.

## 16.3 Daily leaderboard

Because the requirement is friends-only, dynamically create/find one Steam leaderboard per daily key:

```text
voidling_daily_2026-08-25_v1
```

Steam supports up to 10,000 leaderboards, which is over 27 years of one board per day. Valve specifically notes `FindOrCreateLeaderboard` is appropriate when a title expects a large number of dynamically created leaderboards.

Sort: ascending milliseconds.

UI queries only the friends request.

If the daily rules change incompatibly, increment the suffix/version so incomparable clients do not share a board.

---

# 17. Persistence changes

Multiplayer adds local persistent data, but Steam lobby/session state remains transient.

Likely `GameStateData` additions:

```text
LineageArchive[]
MultiplayerWins
DailyRaceAttempts[] / bounded history
AppliedTradeTransactions[] / bounded dedupe history
PendingTradeJournal? 
```

Migration requirements follow existing rules:

- increment save version;
- derive deterministic empty/default values;
- preserve existing creatures/eggs;
- populate lineage archive from existing owned + departed creatures;
- no genome/egg rerolls;
- no Steam availability required to load/migrate a save.

Bound transaction/daily history. For example, retain recent applied trade IDs for dedupe rather than an unbounded list forever.

---

# 18. Presentation integration

## 18.1 Connected zone scene

Add a placeable Garden presentation component such as:

```text
ConnectedZone2D
├─ visual boundary
├─ connection/status indicator
└─ interaction affordance
```

It emits intents; it does not call Steam directly.

`GardenController` may coordinate the zone's placement/visual ownership because it already owns Garden presentation, but network/session logic belongs to Application services.

## 18.2 Multiplayer UI

Prefer standalone screens following existing `SettingsScreen`/`ShopScreen` patterns:

```text
Scripts/Presentation/UI/Multiplayer/
├─ ConnectedZonePanel.cs
├─ LobbyMembersPanel.cs
├─ TradePanel.cs
├─ ChallengeOfferPanel.cs
└─ FriendsLeaderboardPanel.cs
```

Screens receive presentation-ready state and emit intent.

Do not make them retrieve `Steam` singleton data themselves.

## 18.3 Remote Voidling actors

Reuse existing Voidling presentation primitives for:

- sprite grounding;
- mutation adornments;
- shadows;
- name labels.

Remote actors consume `SharedVoidlingSnapshot`/view models and must not hold mutable local `VoidlingData` ownership references.

---

# 19. Safety and robustness guardrails

These are not anti-cheat measures; they protect ordinary players and save integrity.

1. **Lobby-member allowlist**: accept Networking Messages sessions only from Steam IDs currently in the active lobby.
2. **Actual transport identity wins**: never trust sender IDs inside payloads.
3. **Protocol/build handshake** before accepting gameplay commands.
4. **Payload limits** well below Steam's absolute maximum; e.g. start with 64 KiB application limit.
5. **Per-message schema validation**.
6. **Rate limits** for spam-prone commands such as challenge offers, trade offers and transform updates.
7. **Ownership checks**: a peer cannot publish/remove another peer's session entity.
8. **Trade locks** prevent the same object being present in two simultaneous in-session trades.
9. **Transaction IDs + idempotent commit** prevent network retries from duplicating a normal trade.
10. **No arbitrary type deserialization**; map `MessageType` through an explicit registry/switch.
11. **No resource/file paths from peers** are loaded by the client.
12. **String length limits** on remote display names/flavor text.
13. **Maximum session sizes** enforced in Application even though Steam supports larger lobbies: 16 Garden members, 4 challenge players.
14. **Graceful disconnect**: remote snapshots disappear from shared presentation; no remote state is accidentally saved as owned.
15. **Offline-first failure mode**: Steam failure cannot corrupt or block normal single-player save loading.

---

# 20. Error handling and state machines

Networking should be modeled with explicit state rather than scattered booleans.

Suggested session state:

```csharp
public enum ConnectedZoneConnectionState
{
    Offline,
    CreatingLobby,
    JoiningLobby,
    Handshaking,
    Connected,
    MigratingHost,
    Leaving,
    Failed
}
```

Trade and challenge have their own independent state enums.

Application returns typed failures:

```text
SteamUnavailable
LobbyFull
LobbyJoinRejected
ProtocolMismatch
PeerDisconnected
OfferExpired
TradeAssetMissing
TradeTermsChanged
ChallengeFull
ChallengeAlreadyRunning
LeaderboardUnavailable
```

Presentation maps these to localized strings.

---

# 21. Testing strategy

## 21.1 Pure Application tests

Create deterministic in-memory fakes:

```text
FakeLobbyService
FakeMultiplayerTransport
FakeLeaderboardService
FakePlatformIdentityService
```

Test:

- host/client handshake;
- non-lobby sender rejected;
- initial full snapshot;
- publish/remove replication;
- duplicate command idempotency;
- host sequence ordering;
- host migration / authority epoch fencing;
- challenge max 4;
- challenge cancellation on disconnect;
- trade state transitions;
- stale/missing trade asset rejection;
- duplicate `TradeCommit` applies once;
- lineage transfer merge;
- daily seed is stable for UTC date;
- daily attempt cannot restart after completion;
- interrupted daily attempt resumes same immutable entry;
- leaderboard retry behavior.

## 21.2 Protocol tests

Golden JSON tests for every network message.

Verify:

- protocol version field;
- unknown fields tolerated when appropriate;
- unknown message type rejected cleanly;
- max payload enforced;
- malformed payload never mutates session/save state.

## 21.3 Multiplayer race tests

Build an in-memory two/four-peer harness around existing `RaceSimulation`.

Acceptance invariants:

- same start payload + scheduled commands = identical event logs/finish order;
- cheer commands applied at same tick produce identical outcome;
- different frame chunk sizes still produce identical result;
- state checksum matches across peers;
- VFX/presentation randomness cannot enter network simulation.

## 21.4 Godot/Steam integration tests

CI should **not** require a logged-in Steam client.

CI checks:

- project builds with Steam adapter code;
- Application/Domain remain Godot-free;
- main scene starts headless with Steam unavailable;
- Steam infrastructure reports unavailable rather than throwing.

Manual/playtest checks require two Steam accounts/machines or VMs.

GodotSteam maintainers describe two separate accounts/machines or VMs as standard multiplayer testing routes. For initial development, Valve's App ID `480` (Spacewar) can be used where appropriate; use the project's actual Steam App ID/playtest access once lobby/leaderboard configuration must be tested accurately.

Recommended manual smoke matrix:

```text
A hosts -> B joins via overlay
B disconnects/rejoins
host leaves -> B becomes host
A publishes Voidling -> B sees it
trade Voidling both directions
trade egg -> hatch retains seed/genome
4 players join challenge
same multiplayer race finish order on all clients
friend best-time leaderboard loads
one daily attempt survives restart
```

---

# 22. Observability/debug tooling

Add a debug-only multiplayer inspector rather than logging raw noise everywhere.

Useful state:

```text
Steam availability / local Steam ID
Lobby ID / lobby owner
Protocol version
Authority epoch
Lobby member list
Peer transport/session status
Packets sent/received by channel
Last packet type per peer
Current shared Voidlings
Active challenges
Active trade state
Race checksum per participant
Leaderboard operation status
```

Structured log categories:

```text
[Steam]
[Lobby]
[Net]
[Zone]
[Trade]
[Challenge]
[Leaderboard]
```

Never log full private save payloads by default.

---

# 23. Proposed file structure after implementation

Create files incrementally; this is the target shape, not a command to add empty placeholders.

```text
Scripts/
├─ Application/
│  ├─ Multiplayer/
│  │  ├─ ConnectedZoneSession.cs
│  │  ├─ ConnectedZoneCoordinator.cs
│  │  ├─ Protocol/
│  │  ├─ Trading/
│  │  │  ├─ TradeCoordinator.cs
│  │  │  └─ TradeModels.cs
│  │  ├─ Challenges/
│  │  │  ├─ ChallengeCoordinator.cs
│  │  │  ├─ IMultiplayerChallengeHandler.cs
│  │  │  └─ RaceChallengeHandler.cs
│  │  └─ Leaderboards/
│  │     ├─ FriendsLeaderboardUseCase.cs
│  │     └─ DailyRaceUseCase.cs
│  └─ Ports/
│     └─ Multiplayer/
│        ├─ IPlatformIdentityService.cs
│        ├─ ILobbyService.cs
│        ├─ IMultiplayerTransport.cs
│        └─ IFriendsLeaderboardService.cs
├─ Domain/
│  └─ Breeding/
│     └─ lineage lookup/archive refactor as required by transfers
├─ Infrastructure/
│  └─ Steam/
│     ├─ GodotSteamRuntime.cs
│     ├─ GodotSteamApi.cs
│     ├─ SteamIdentityService.cs
│     ├─ SteamLobbyService.cs
│     ├─ SteamNetworkingMessagesTransport.cs
│     ├─ SteamFriendsLeaderboardService.cs
│     └─ SteamNetworkCodec.cs
├─ Presentation/
│  └─ UI/Multiplayer/
└─ Garden/
   └─ connected-zone presentation integration

Tests/
├─ Application/Multiplayer/
├─ Domain/Lineage/
└─ Infrastructure/Steam/   # adapter contract tests where feasible
```

`GameBootstrap` composes these concrete Steam adapters and supplies them to focused Application coordinators. Do not place them into global static accessors.

---

# 24. Phased implementation

## Phase MP0 - dependency/API spike

Goal: prove the pinned integration on the current runtime.

1. Install/pin GodotSteam GDExtension **4.22 / Steamworks SDK 1.65** for Godot 4.6.1.
2. Do not mix GDExtension with GodotSteam module builds. The GodotSteam Asset Library explicitly warns against combining them.
3. Use normal Godot export templates with the GDExtension variant, as GodotSteam's current package instructions specify.
4. Add `GodotSteamRuntime` + `GodotSteamApi` only.
5. Prove:
   - Steam init;
   - Steam ID/persona;
   - callbacks;
   - create friends-only lobby max 16;
   - invite overlay;
   - second account joins;
   - send one reliable Networking Messages packet both directions;
   - graceful no-Steam startup.

**Acceptance:** two real Steam accounts exchange a typed `Hello` message; CI still launches without Steam.

## Phase MP1 - transport and protocol foundation

1. Add Application ports.
2. Implement Networking Messages adapter.
3. Implement explicit channels/delivery modes.
4. Add codec/envelopes/version handshake.
5. Lobby-member session allowlist.
6. In-memory fake transport and protocol tests.

**Acceptance:** Application multiplayer tests have zero Godot references and can simulate 16 peers in memory.

## Phase MP2 - connected Garden zone

1. Add placeable connected-zone presentation.
2. Connected zone host/join/leave state machine.
3. Publish/unpublish Voidling.
4. Full snapshot on join.
5. Low-frequency transient transform updates only if needed.
6. Host migration and authority epoch.
7. Steam overlay invite flow.

**Acceptance:** 2-16 clients converge on the same shared Voidling set after joins/leaves/host migration.

## Phase MP3 - trading + lineage foundation

1. Introduce lineage archive/migration.
2. Refactor relatedness lookup to consume ancestry independent of full live creature objects.
3. Implement transfer packages for Voidlings and eggs.
4. Trade state machine.
5. Pending transaction journal + idempotent commits.
6. Trade UI.

**Acceptance:** after exchanging a bred Voidling/egg, save/load preserves genome, seed, rare traits and ancestry needed for future inbreeding detection.

## Phase MP4 - challenge framework

1. Challenge offer/join/leave/cancel.
2. Maximum 4 participants.
3. Race challenge handler.
4. Challenge UI integrated into connected zone.

**Acceptance:** four players can opt into a challenge while remaining members of the 16-player Garden lobby.

## Phase MP5 - deterministic multiplayer races

1. Multiplayer race entry factory.
2. Canonical start-payload hashing.
3. Host-scheduled Cheer commands.
4. Race checksum diagnostics.
5. Multiplayer result use case.
6. Disconnect behavior.

**Acceptance:** same race start + network command schedule produces same event log and result across four clients.

## Phase MP6 - Steam friends leaderboards

1. Multiplayer wins leaderboard.
2. Per-course single-player best-time leaderboards.
3. Friends query UI.
4. Offline/retry behavior.

**Acceptance:** two Steam friends can see one another's win totals and course best times.

## Phase MP7 - daily friend race

1. UTC daily key/seed.
2. Daily pool selection.
3. persistent one-attempt/resume record.
4. dynamic daily Steam leaderboard.
5. friends-only results panel.

**Acceptance:** two friends on separate machines get the same daily race and can each submit only one normal local attempt.

## Phase MP8 - polish/operational hardening

1. rate limits;
2. payload caps;
3. recovery UX;
4. debug multiplayer inspector;
5. Steam launch deep-link (`+connect_lobby`) flow;
6. Playtest/beta validation;
7. localization/pseudolocalization of multiplayer screens;
8. document packaging/export setup.

---

# 25. Concrete Steam / GodotSteam API map

| Need | Steamworks native API | GodotSteam-facing API/signal |
|---|---|---|
| Create Garden lobby | `ISteamMatchmaking::CreateLobby` | `createLobby`, `lobby_created` |
| Join lobby | `ISteamMatchmaking::JoinLobby` | `joinLobby`, `lobby_joined` |
| Leave lobby | `ISteamMatchmaking::LeaveLobby` | `leaveLobby` |
| Members | `GetNumLobbyMembers`, `GetLobbyMemberByIndex` | matching camelCase wrappers |
| Current host | `GetLobbyOwner` | `getLobbyOwner` |
| Metadata | `SetLobbyData`, `GetLobbyData` | `setLobbyData`, `getLobbyData` |
| Invite UI | `ISteamFriends::ActivateGameOverlayInviteDialog` | matching GodotSteam Friends wrapper |
| Invite accepted | `GameLobbyJoinRequested_t` | `join_requested` |
| P2P send | `ISteamNetworkingMessages::SendMessageToUser` | `sendMessageToUser` |
| P2P receive | `ReceiveMessagesOnChannel` | `receiveMessagesOnChannel` |
| Incoming session | `SteamNetworkingMessagesSessionRequest_t` + `AcceptSessionWithUser` | GodotSteam networking-message callback/signal + `acceptSessionWithUser` |
| Close peer | `CloseSessionWithUser` | `closeSessionWithUser` |
| Find leaderboard | `ISteamUserStats::FindLeaderboard` | `findLeaderboard`, `leaderboard_find_result` |
| Dynamic board | `FindOrCreateLeaderboard` | `findOrCreateLeaderboard` |
| Upload | `UploadLeaderboardScore` | `uploadLeaderboardScore`, `leaderboard_score_uploaded` |
| Friends rows | `DownloadLeaderboardEntries(...Friends...)` | `downloadLeaderboardEntries`, `leaderboard_scores_downloaded` |

Exact Godot `Variant` shapes returned by GDExtension calls must be captured in adapter characterization tests during MP0. The Application layer must not depend on those raw shapes.

---

# 26. Research notes that materially affect implementation

1. **Steam lobbies are not the gameplay network.** Valve explicitly separates lobby matchmaking/membership from game networking. Use lobby data/chat for rendezvous/control metadata and Networking Messages for actual game protocol data.

2. **Lobby users are Steam-authenticated.** Valve notes users in a Steam lobby are already authenticated with Steam. We do not need a second login system for the casual P2P design.

3. **Lobby owner can arbitrate.** Valve's `SendLobbyChatMsg` documentation explicitly gives an example of using the lobby owner to make a decision and broadcast the accepted result. The Voidling host-authoritative session model applies the same idea over the higher-volume networking transport.

4. **Networking Messages is built on Networking Sockets.** It is not the deprecated old P2P transport. It provides reliable/unreliable messages, implicit P2P sessions and SDR-capable routing without explicit connection handles.

5. **Reliable ordering is channel-local.** Steam guarantees reliable messages for the same host/channel are ordered; no ordering guarantee exists across separate channels. Protocol design must not require cross-channel ordering.

6. **Incoming message sessions must be accepted.** On a remote-first connection, handle the Networking Messages session-request callback and only accept if the Steam ID belongs to the active lobby.

7. **Steam leaderboards are per-player best/current records.** They fit win totals and best times well. The daily race uses dynamic leaderboard names because Steam leaderboards do not behave like a database table that can simply be cleared each day.

8. **10,000 leaderboard limit makes daily boards viable.** One board/day remains under the title limit for decades.

9. **GodotSteam version pin matters.** API signatures and wrapper details have changed over time. Keep all raw calls in `GodotSteamApi`, pin 4.22 initially, and update that adapter intentionally when upgrading.

---

# 27. Documentation / research references

Accessed/verified in August 2026 unless otherwise stated.

### Steamworks

- Steam Matchmaking & Lobbies: https://partner.steamgames.com/doc/features/multiplayer/matchmaking
- `ISteamMatchmaking`: https://partner.steamgames.com/doc/api/ISteamMatchmaking
- `ISteamNetworkingMessages`: https://partner.steamgames.com/doc/api/ISteamNetworkingMessages
- `ISteamFriends`: https://partner.steamgames.com/doc/api/ISteamFriends
- `ISteamUserStats`: https://partner.steamgames.com/doc/api/ISteamUserStats
- Steam Leaderboards: https://partner.steamgames.com/doc/features/leaderboards

### GodotSteam

- GodotSteam documentation: https://godotsteam.com/
- GodotSteam initialization tutorial: https://godotsteam.com/tutorials/initializing/
- Current Godot Asset Library package: https://godotengine.org/asset-library/asset/2445
  - current listing at research time: GodotSteam GDExtension 4.22, Steamworks SDK 1.65, Godot 4.4+
- GodotSteam docs/news index: https://godotsteam.com/blog/category/docs/

### Repository architecture sources

- `ARCHITECTURE.md`
- `docs/architecture/MIGRATION_STATUS.md`
- `Scripts/Bootstrap/GameBootstrap.cs`
- `Scripts/Application/Game/GameStateData.cs`
- `Scripts/Domain/Creatures/VoidlingData.cs`
- `Scripts/Domain/Hatching/EggData.cs`
- `Scripts/Domain/Breeding/RelationshipService.cs`
- `Scripts/Application/Racing/RaceEntryFactory.cs`
- `Scripts/Domain/Racing/RaceParticipantSnapshot.cs`
- `Scripts/Domain/Racing/RaceSimulation.cs`

---

# 28. Final architectural rule

```text
Steam/GodotSteam tells us WHO is connected and MOVES bytes.
Application decides WHAT those bytes mean and WHO may change shared session state.
Domain decides GAME outcomes.
Presentation only SHOWS the result and emits player intent.
Local persistence remains owner truth except when a completed trade deliberately transfers ownership.
```

If a future multiplayer feature can follow that rule, it should fit the architecture without adding another networking framework or rewriting the core simulation.
