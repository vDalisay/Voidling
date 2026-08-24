# Steam multiplayer connectivity spike

This is a developer-only test for the first multiplayer foundation. It is not production multiplayer UI.

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

Expected log:

```text
[multiplayer-probe] host succeeded for lobby <id>
[multiplayer-probe] share lobby id <id> with the second account or use Steam invite.
```

The friends-only lobby is capped at 16 members.

## Join by lobby ID

Launch the second account with:

```text
--voidling-mp-join=<lobby-id>
```

Expected log on both peers after the join/hello exchange:

```text
[multiplayer-probe] lobby <id>, owner <steam-id>, members 2
[multiplayer-probe] hello from <persona> (<steam-id>)
```

Accepting a Steam invite while the probe is active also uses the `join_requested` callback and joins the requested lobby.

## What this spike proves

- optional GodotSteam initialization;
- friends-only Steam lobby creation/join;
- lobby owner/member discovery;
- Steam invite callback routing;
- Networking Messages session acceptance for lobby members;
- reliable, versioned `Hello` message transport;
- sender-ID validation against the transport identity;
- no Steam requirement for normal singleplayer startup/CI.

## What it deliberately does not prove yet

- connected Garden Voidling replication;
- host migration/session epochs;
- trade prepare/commit journal;
- race/challenge synchronization;
- leaderboards;
- daily friend race;
- production multiplayer UI.

Those are subsequent implementation phases after this connectivity boundary is validated with two real Steam accounts.
