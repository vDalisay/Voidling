# Steam multiplayer connectivity spike

This is a developer-only integration test for the multiplayer foundation. It is not production multiplayer UI.

## Offline-first invariant

Do **not** install Steam/GodotSteam merely to run or develop the singleplayer game. If the GodotSteam singleton is absent or Steam initialization fails, Voidling composes offline adapters and starts normally.

## Steam test prerequisites

For a real two-account test:

1. Install the pinned GodotSteam GDExtension version described in `docs/MULTIPLAYER_IMPLEMENTATION_PLAN.md`.
2. Configure the Voidling Steam App ID through GodotSteam project settings, `SteamAppId`/`SteamGameId`, or the normal `steam_appid.txt` development mechanism.
3. Run Steam and log into two different Steam accounts on two machines/OS sessions.
4. Build/run the `multiplayer-implementation` branch on both clients.

Do not commit a developer-specific `steam_appid.txt` containing a private/unreleased App ID.

## Host

Launch with:

```text
--voidling-mp-host
```

To immediately open the Steam invite overlay after lobby creation:

```text
--voidling-mp-host --voidling-mp-invite
```

To also publish the first locally owned Voidling into the connected Garden replication model:

```text
--voidling-mp-host --voidling-mp-publish-first
```

Expected log:

```text
[multiplayer-probe] host succeeded for lobby <id>
[multiplayer-probe] share lobby id <id> with the second account or use Steam invite.
[multiplayer-probe] published <name> (<creature-id>) into the connected zone
```

The friends-only lobby is capped at 16 members.

## Join by lobby ID

Launch the second account with:

```text
--voidling-mp-join=<lobby-id>
```

To make the second player publish their first locally owned Voidling as well:

```text
--voidling-mp-join=<lobby-id> --voidling-mp-publish-first
```

Expected log on both peers after the join/hello exchange and full-state synchronization:

```text
[multiplayer-probe] lobby <id>, owner <steam-id>, members 2
[multiplayer-probe] hello from <persona> (<steam-id>)
[multiplayer-probe] zone lobby <id>, host <steam-id>, epoch 1, revision <n>, shared Voidlings <count>
[multiplayer-probe] shared <name> (<creature-id>) owner <steam-id> at <x>,<y>
```

Accepting a Steam invite while the probe is active also uses the `join_requested` callback and joins the requested lobby.

## Connected Garden replication behavior under test

The current spike implements a transient, host-coordinated connected-zone model:

- the Steam lobby owner orders reliable Garden mutations;
- each published Voidling remains owned only by its source save;
- network snapshots are presentation/session data and are never inserted into another player's `GameStateData`;
- clients request a compact full snapshot when joining/rejoining;
- the host broadcasts canonical publish/remove events with a monotonically increasing revision;
- clients request another full snapshot if they detect a revision gap;
- embedded sender IDs must match the Steam Networking Messages transport identity;
- a player cannot publish/remove a session entity while claiming another Steam user's ownership;
- repeated command IDs are ignored by the host;
- when Steam changes the lobby owner, the replicated state is retained and the authority epoch increments;
- departed lobby members' shared Voidlings are purged by the host and a new full snapshot is broadcast.

This is intentionally low-frequency state replication. Ordinary remote idle/wander animation is not streamed frame-by-frame.

## What this spike now proves

- optional GodotSteam initialization;
- friends-only Steam lobby creation/join;
- lobby owner/member discovery;
- Steam invite callback routing;
- Networking Messages session acceptance for lobby members;
- reliable, versioned `Hello` message transport;
- sender-ID validation against the transport identity;
- full connected-Garden snapshots for late joiners;
- host-ordered Voidling publish/remove replication;
- revision-gap recovery path;
- basic lobby-owner migration/session epoch handling;
- no Steam requirement for normal singleplayer startup/CI.

## What it deliberately does not prove yet

- production connected-zone placement UI or remote Garden actors;
- low-frequency transient position/facing corrections;
- trade prepare/commit journal and lineage transfer;
- race/challenge synchronization;
- leaderboards;
- daily friend race;
- production multiplayer UI.

Those are subsequent implementation phases. The next product-facing step is to bind the replicated `ConnectedZoneSnapshot` to a placeable Garden-zone presentation without making remote Voidlings part of the local save/simulation aggregate.
