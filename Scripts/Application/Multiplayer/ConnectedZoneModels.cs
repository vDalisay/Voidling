using System;
using System.Collections.Generic;
using System.Linq;
using Voidling.Application.Ports.Multiplayer;
using VoidlingGame;

namespace Voidling.Application.Multiplayer;

public readonly record struct SharedVoidlingKey(PlatformUserId OwnerId, string CreatureId);

/// <summary>
/// Minimal network-facing creature data required to render and identify a Voidling in a connected
/// Garden. This is deliberately not a save DTO and is never inserted into another player's save.
/// </summary>
public sealed record SharedVoidlingSnapshot(
    string CreatureId,
    PlatformUserId OwnerId,
    string DisplayName,
    string TintHex,
    LifeStage Stage,
    int FamilyGeneration,
    string[] RareTraitIds,
    float ZoneX,
    float ZoneY)
{
    public SharedVoidlingKey Key => new(OwnerId, CreatureId);
}

public sealed record ConnectedZoneSnapshot(
    ulong LobbyId,
    PlatformUserId HostId,
    long AuthorityEpoch,
    long Revision,
    SharedVoidlingSnapshot[] Voidlings);

public enum ZoneDeltaApplyResult
{
    Applied,
    Stale,
    RequiresSnapshot
}

public sealed record ConnectedZoneOperationResult(bool Success, string? Error)
{
    public static ConnectedZoneOperationResult Succeeded { get; } = new(true, null);

    public static ConnectedZoneOperationResult Failed(string error)
        => new(false, error);
}

/// <summary>
/// Transient replicated state for one connected Garden session. It never owns local gameplay data.
/// The host orders mutations through Revision; every peer keeps enough replicated state to become
/// the next casual session host after Steam lobby-owner migration.
/// </summary>
public sealed class ConnectedZoneState
{
    private readonly Dictionary<SharedVoidlingKey, SharedVoidlingSnapshot> _voidlings = new();

    public ulong LobbyId { get; private set; }
    public PlatformUserId HostId { get; private set; }
    public long AuthorityEpoch { get; private set; }
    public long Revision { get; private set; }
    public bool IsInitialized => LobbyId != 0;

    public IReadOnlyCollection<SharedVoidlingSnapshot> Voidlings => _voidlings.Values;

    public void Reset(LobbySnapshot lobby)
    {
        ArgumentNullException.ThrowIfNull(lobby);

        LobbyId = lobby.LobbyId;
        HostId = lobby.OwnerId;
        AuthorityEpoch = 1;
        Revision = 0;
        _voidlings.Clear();
    }

    public void Clear()
    {
        LobbyId = 0;
        HostId = default;
        AuthorityEpoch = 0;
        Revision = 0;
        _voidlings.Clear();
    }

    public void Rehost(PlatformUserId newHost)
    {
        if (!IsInitialized || newHost.Value == 0 || newHost == HostId)
            return;

        HostId = newHost;
        AuthorityEpoch++;
    }

    public long Publish(SharedVoidlingSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _voidlings[snapshot.Key] = snapshot;
        Revision++;
        return Revision;
    }

    public long Remove(PlatformUserId ownerId, string creatureId)
    {
        if (ownerId.Value == 0 || string.IsNullOrWhiteSpace(creatureId))
            return Revision;

        if (_voidlings.Remove(new SharedVoidlingKey(ownerId, creatureId)))
            Revision++;

        return Revision;
    }

    public bool RetainOwners(IReadOnlySet<PlatformUserId> allowedOwners)
    {
        ArgumentNullException.ThrowIfNull(allowedOwners);

        var removed = false;
        foreach (var key in _voidlings.Keys.ToArray())
        {
            if (allowedOwners.Contains(key.OwnerId))
                continue;

            _voidlings.Remove(key);
            removed = true;
        }

        if (removed)
            Revision++;

        return removed;
    }

    public ZoneDeltaApplyResult ApplyPublished(
        long authorityEpoch,
        long revision,
        SharedVoidlingSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var sequence = CheckSequence(authorityEpoch, revision);
        if (sequence != ZoneDeltaApplyResult.Applied)
            return sequence;

        _voidlings[snapshot.Key] = snapshot;
        Revision = revision;
        return ZoneDeltaApplyResult.Applied;
    }

    public ZoneDeltaApplyResult ApplyRemoved(
        long authorityEpoch,
        long revision,
        PlatformUserId ownerId,
        string creatureId)
    {
        var sequence = CheckSequence(authorityEpoch, revision);
        if (sequence != ZoneDeltaApplyResult.Applied)
            return sequence;

        _voidlings.Remove(new SharedVoidlingKey(ownerId, creatureId));
        Revision = revision;
        return ZoneDeltaApplyResult.Applied;
    }

    public bool TryApplySnapshot(ConnectedZoneSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (snapshot.LobbyId == 0 || snapshot.LobbyId != LobbyId || snapshot.HostId.Value == 0)
            return false;
        if (snapshot.AuthorityEpoch < AuthorityEpoch)
            return false;
        if (snapshot.AuthorityEpoch == AuthorityEpoch && snapshot.Revision < Revision)
            return false;

        HostId = snapshot.HostId;
        AuthorityEpoch = snapshot.AuthorityEpoch;
        Revision = snapshot.Revision;
        _voidlings.Clear();

        foreach (var voidling in snapshot.Voidlings ?? Array.Empty<SharedVoidlingSnapshot>())
        {
            if (ConnectedZoneValidation.IsValidSharedVoidling(voidling))
                _voidlings[voidling.Key] = voidling;
        }

        return true;
    }

    public ConnectedZoneSnapshot ToSnapshot()
    {
        var ordered = _voidlings.Values
            .OrderBy(v => v.OwnerId.Value)
            .ThenBy(v => v.CreatureId, StringComparer.Ordinal)
            .ToArray();

        return new ConnectedZoneSnapshot(LobbyId, HostId, AuthorityEpoch, Revision, ordered);
    }

    private ZoneDeltaApplyResult CheckSequence(long authorityEpoch, long revision)
    {
        if (authorityEpoch < AuthorityEpoch ||
            (authorityEpoch == AuthorityEpoch && revision <= Revision))
        {
            return ZoneDeltaApplyResult.Stale;
        }

        if (authorityEpoch != AuthorityEpoch || revision != Revision + 1)
            return ZoneDeltaApplyResult.RequiresSnapshot;

        return ZoneDeltaApplyResult.Applied;
    }
}

public static class ConnectedZoneValidation
{
    public const int MaxCreatureIdLength = 128;
    public const int MaxDisplayNameLength = 64;
    public const int MaxTintLength = 16;
    public const int MaxRareTraits = 32;

    public static bool IsValidSharedVoidling(SharedVoidlingSnapshot? snapshot)
    {
        if (snapshot == null ||
            snapshot.OwnerId.Value == 0 ||
            string.IsNullOrWhiteSpace(snapshot.CreatureId) ||
            snapshot.CreatureId.Length > MaxCreatureIdLength ||
            string.IsNullOrWhiteSpace(snapshot.DisplayName) ||
            snapshot.DisplayName.Length > MaxDisplayNameLength ||
            string.IsNullOrWhiteSpace(snapshot.TintHex) ||
            snapshot.TintHex.Length > MaxTintLength ||
            !float.IsFinite(snapshot.ZoneX) ||
            !float.IsFinite(snapshot.ZoneY) ||
            snapshot.FamilyGeneration < 0)
        {
            return false;
        }

        var rareTraits = snapshot.RareTraitIds ?? Array.Empty<string>();
        if (rareTraits.Length > MaxRareTraits)
            return false;

        foreach (var traitId in rareTraits)
        {
            if (string.IsNullOrWhiteSpace(traitId) || traitId.Length > 128)
                return false;
        }

        return true;
    }
}
