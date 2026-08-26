# LAN Multiplayer Testing

Voidling's production multiplayer transport is Steam/GodotSteam. The LAN mode described here is a **development-only test transport** that uses Godot's ENet peer over UDP while keeping the same multiplayer Application services, protocols, Garden replication, trading, challenges, and deterministic race logic.

The purpose is to validate multiplayer behavior before debugging Steam callbacks, Steam lobbies, Steam Networking Messages, or Steam Datagram Relay.

## What LAN mode tests

LAN mode exercises the real Voidling multiplayer stack above the transport boundary:

- connected Garden membership;
- reliable shared-Voidling publish/remove replication;
- transient remote movement updates;
- Voidling and egg trade protocols and persisted two-phase commit;
- challenge offers/join/leave/cancel;
- 2-4 player deterministic race setup;
- synchronized race-start payload acknowledgement;
- lockstep Cheer scheduling;
- deterministic checksums and result consensus;
- multiplayer race reward persistence.

LAN mode does **not** emulate Steam-specific services:

- Steam friends or presence;
- Steam invite overlay/deep links;
- Steam leaderboards;
- Steam Datagram Relay/NAT traversal;
- Steam Networking Messages callback/signature behavior.

Those still require the later real two-account GodotSteam validation pass.

## Architecture

LAN mode is selected only when an explicit `--voidling-lan-host` or `--voidling-lan-join=...` argument is supplied.

```text
Application multiplayer services
        |
        +-- IPlatformIdentityService
        +-- ILobbyService
        +-- IMultiplayerTransport
        |
        +------------+----------------+
                     |                |
             LAN development      Production
                  ENet              Steam
```

Normal launches do not activate the LAN adapter. If no LAN flags are present, normal Steam detection/fallback behavior remains unchanged.

The LAN topology is host-relayed. Clients physically connect to the ENet host, but application packets retain their logical source, target, channel, and reliable/unreliable delivery mode. The Application layer therefore receives the same packet contract it uses with Steam.

Physical ENet channel `0` is reserved for LAN control/roster traffic. The five production Application channels are mapped to ENet channels `1` through `5`, so the ENet peer is created with six physical channels.

Default UDP port: **27181**.

## Recommended Windows launcher

For ordinary local testing, use the repository-root helper instead of typing development flags manually:

```bat
playgame-local-multiplayer.bat
```

It asks for:

- Host or Join mode;
- player display name;
- development save profile;
- UDP port;
- host address when joining;
- whether to build before launch.

Defaults are optimized for a two-instance same-PC test:

- Host defaults to name/profile `Host`, UDP `27181`, and builds before launch.
- Join defaults to name/profile `Client`, address `127.0.0.1`, UDP `27181`, and skips rebuilding.

When running two copies on one PC, **the save profiles must be different**. This keeps trades, ownership changes, and race rewards in separate `user://` save files.

For two PCs, run the same launcher on both machines. On the joining PC, enter the host computer's LAN IPv4 address instead of `127.0.0.1`. Choose to build on the joining PC if that checkout has not already been built.

## Command-line flags

| Flag | Meaning |
| --- | --- |
| `--voidling-lan-host` | Start this process as the development LAN host. |
| `--voidling-lan-join=<address>` | Connect this process to a LAN host, e.g. `127.0.0.1` or `192.168.1.50`. |
| `--voidling-lan-port=<port>` | Optional UDP port override. Both peers must use the same port. Default `27181`. |
| `--voidling-lan-name=<name>` | Development display name shown to the other LAN peers. |
| `--voidling-dev-profile=<profile>` | Use a separate `user://` save for this process. Strongly recommended for same-PC tests. |
| `--voidling-lan-smoke` | Automated two-peer Hello handshake probe; exits `0` on success or `2` on timeout/failure. |

`playgame.bat` forwards unknown arguments to Godot, so these flags can still be supplied directly after the normal optional `--no-build`/`-n` build flag.

## First test: two instances on one Windows PC

### 1. Start the host

Double-click or run:

```bat
playgame-local-multiplayer.bat
```

Choose **Host**. For example:

- player name: `Alice`;
- profile: `A`;
- port: `27181`;
- build: `Y`.

The host automatically opens the LAN connected-Garden session. You do not need Steam or GodotSteam for this launch.

### 2. Start the client

Open a second copy of:

```bat
playgame-local-multiplayer.bat
```

Choose **Join**. For example:

- player name: `Bob`;
- profile: `B`;
- host address: `127.0.0.1`;
- port: `27181`;
- build: `N`.

The two profiles use different save files:

```text
user://voidling_mvp_save_A.json
user://voidling_mvp_save_B.json
```

Do **not** use the same profile when running both processes on one computer if you intend to test trading or persistent race rewards. Using the same profile would point both processes at the same development save.

### 3. Verify the Connected Garden

Open **Online** in both windows. Verify:

1. both `Alice` and `Bob` appear in the member list;
2. Alice is shown as host;
3. Steam Friend Boards are unavailable in LAN mode, without affecting the Garden;
4. ordinary single-player systems continue functioning in both windows.

### 4. Verify shared Voidlings

On Alice:

1. select a local Voidling;
2. open **Online**;
3. choose **Share Selected**;
4. move/observe the Voidling in the Garden.

On Bob verify:

- the remote Voidling appears;
- it moves smoothly from transient updates;
- it is not inserted into Bob's local roster or save;
- stopping sharing on Alice removes the remote actor on Bob.

Repeat in the opposite direction.

### 5. Verify trading

Use **Online -> Trades**:

1. Alice offers one Voidling or egg to Bob;
2. Bob sees the incoming offer even if the Trades modal was closed when it arrived;
3. test decline;
4. test a gift with no return asset;
5. test a two-sided trade;
6. close and reopen each game after a completed trade and verify ownership persisted correctly.

### 6. Verify multiplayer racing

Use **Online -> Challenges**:

1. Alice offers a Race challenge;
2. Bob joins;
3. both players open Race Setup and lock a Voidling;
4. the creator/host starts the race;
5. verify both clients enter the synchronized race;
6. use Cheer on both players at different moments;
7. verify both clients finish with the same ordering;
8. return to each local Garden;
9. verify the winner's local multiplayer-win progress persists.

## Direct same-PC commands

The interactive launcher is preferred, but the equivalent direct commands remain useful for debugging.

Host:

```bat
playgame.bat --no-build --voidling-lan-host --voidling-lan-name=Alice --voidling-dev-profile=A
```

Client:

```bat
playgame.bat --no-build --voidling-lan-join=127.0.0.1 --voidling-lan-name=Bob --voidling-dev-profile=B
```

## Automated same-machine socket smoke

The smoke mode uses the same typed `MultiplayerProtocol` Hello message as production Steam networking. Run the host and client in separate terminals after a successful build/import.

Host:

```bat
playgame.bat --no-build --voidling-lan-host --voidling-lan-name=SmokeHost --voidling-dev-profile=smoke_host --voidling-lan-smoke
```

Client:

```bat
playgame.bat --no-build --voidling-lan-join=127.0.0.1 --voidling-lan-name=SmokeClient --voidling-dev-profile=smoke_client --voidling-lan-smoke
```

Each process should print:

```text
[multiplayer-probe] LAN_SMOKE_SUCCESS
```

and exit successfully. The GitHub Actions workflow runs the same two-process handshake headlessly on every PR build. The CI step always prints both process logs and exit codes before reporting a smoke failure so ENet/socket regressions remain diagnosable.

## Two computers on the same LAN

On both computers, run:

```bat
playgame-local-multiplayer.bat
```

Choose **Host** on the first computer. Find that computer's LAN IPv4 address, for example `192.168.1.50`.

Choose **Join** on the second computer and enter that LAN IPv4 address. Both machines must use the same UDP port.

The equivalent direct commands are:

Host:

```bat
playgame.bat --no-build --voidling-lan-host --voidling-lan-name=Alice --voidling-dev-profile=A
```

Join:

```bat
playgame.bat --no-build --voidling-lan-join=192.168.1.50 --voidling-lan-name=Bob --voidling-dev-profile=B
```

If the connection times out:

- confirm both machines are on the same network;
- allow Godot/Voidling through the host OS firewall;
- allow **UDP 27181** on the host, or the custom port supplied with `--voidling-lan-port`;
- verify the client is using the host's LAN IPv4 address, not a public address.

For a custom port, specify the same value on both processes:

```bat
--voidling-lan-port=32123
```

## Expected development limitations

- LAN mode is not advertised or supported as a shipping multiplayer option.
- Steam leaderboards and Steam friend discovery remain unavailable in LAN mode.
- The development LAN host is fixed as the session host. If it exits, clients lose the LAN session. Production Steam lobby-owner migration is tested separately with Steam.
- LAN mode does not attempt internet NAT traversal. It is intended for loopback and local networks only.
- The casual trust model remains unchanged: this is a consistency test harness, not an anti-cheat system.

## Recommended validation order before merging multiplayer

1. GitHub Actions two-process LAN Hello smoke.
2. Same-PC Connected Garden sharing/movement using `playgame-local-multiplayer.bat`.
3. Same-PC trading, including restart persistence.
4. Same-PC 2-player deterministic race + Cheer.
5. Two-PC LAN repetition of the same flows.
6. Disconnect/error testing on LAN.
7. Install/pin GodotSteam and repeat the same scenarios with two Steam accounts.
8. Validate Steam friends/invites/leaderboards/SDR specifically.

The LAN pass is intentionally a transport-independent confidence step. Passing it does not remove the requirement for the final Steam integration pass.
