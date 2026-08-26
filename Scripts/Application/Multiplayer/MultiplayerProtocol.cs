using System;
using System.Text.Json;
using Voidling.Application.Ports.Multiplayer;

namespace Voidling.Application.Multiplayer;

public static class MultiplayerProtocol
{
    public const int CurrentVersion = 1;
    public const int MaxPacketBytes = 64 * 1024;

    public const string HelloMessageType = "hello";
    public const string ZoneSnapshotRequestMessageType = "zone.snapshot.request";
    public const string ZoneSnapshotMessageType = "zone.snapshot";
    public const string PublishVoidlingCommandMessageType = "zone.voidling.publish.command";
    public const string VoidlingPublishedEventMessageType = "zone.voidling.published";
    public const string RemoveVoidlingCommandMessageType = "zone.voidling.remove.command";
    public const string VoidlingRemovedEventMessageType = "zone.voidling.removed";

    private sealed record Envelope(
        int ProtocolVersion,
        string MessageType,
        string MessageId,
        ulong SenderId,
        JsonElement Payload);

    private sealed record HelloPayload(string DisplayName);
    private sealed record ZoneSnapshotRequestPayload(ulong LobbyId);
    private sealed record ZoneSnapshotPayload(ConnectedZoneSnapshot Snapshot);
    private sealed record PublishVoidlingPayload(SharedVoidlingSnapshot Voidling);
    private sealed record VoidlingPublishedPayload(
        long AuthorityEpoch,
        long Revision,
        SharedVoidlingSnapshot Voidling);
    private sealed record RemoveVoidlingPayload(PlatformUserId OwnerId, string CreatureId);
    private sealed record VoidlingRemovedPayload(
        long AuthorityEpoch,
        long Revision,
        PlatformUserId OwnerId,
        string CreatureId);

    public static byte[] EncodeHello(PlatformUser sender)
        => Encode(HelloMessageType, sender, new HelloPayload(sender.DisplayName));

    public static bool TryDecodeHello(
        ReadOnlySpan<byte> bytes,
        PlatformUserId transportSender,
        out PlatformUser sender)
    {
        sender = default!;
        if (!TryDecode(
                bytes,
                transportSender,
                HelloMessageType,
                out _,
                out HelloPayload? hello) ||
            hello == null ||
            string.IsNullOrWhiteSpace(hello.DisplayName))
        {
            return false;
        }

        sender = new PlatformUser(transportSender, hello.DisplayName.Trim());
        return true;
    }

    public static byte[] EncodeZoneSnapshotRequest(PlatformUser sender, ulong lobbyId)
        => Encode(ZoneSnapshotRequestMessageType, sender, new ZoneSnapshotRequestPayload(lobbyId));

    public static bool TryDecodeZoneSnapshotRequest(
        ReadOnlySpan<byte> bytes,
        PlatformUserId transportSender,
        out Guid messageId,
        out ulong lobbyId)
    {
        lobbyId = 0;
        if (!TryDecode(
                bytes,
                transportSender,
                ZoneSnapshotRequestMessageType,
                out messageId,
                out ZoneSnapshotRequestPayload? payload) ||
            payload == null ||
            payload.LobbyId == 0)
        {
            return false;
        }

        lobbyId = payload.LobbyId;
        return true;
    }

    public static byte[] EncodeZoneSnapshot(PlatformUser sender, ConnectedZoneSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return Encode(ZoneSnapshotMessageType, sender, new ZoneSnapshotPayload(snapshot));
    }

    public static bool TryDecodeZoneSnapshot(
        ReadOnlySpan<byte> bytes,
        PlatformUserId transportSender,
        out Guid messageId,
        out ConnectedZoneSnapshot snapshot)
    {
        snapshot = default!;
        if (!TryDecode(
                bytes,
                transportSender,
                ZoneSnapshotMessageType,
                out messageId,
                out ZoneSnapshotPayload? payload) ||
            payload?.Snapshot == null)
        {
            return false;
        }

        snapshot = payload.Snapshot;
        return true;
    }

    public static byte[] EncodePublishVoidlingCommand(PlatformUser sender, SharedVoidlingSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return Encode(PublishVoidlingCommandMessageType, sender, new PublishVoidlingPayload(snapshot));
    }

    public static bool TryDecodePublishVoidlingCommand(
        ReadOnlySpan<byte> bytes,
        PlatformUserId transportSender,
        out Guid messageId,
        out SharedVoidlingSnapshot snapshot)
    {
        snapshot = default!;
        if (!TryDecode(
                bytes,
                transportSender,
                PublishVoidlingCommandMessageType,
                out messageId,
                out PublishVoidlingPayload? payload) ||
            payload?.Voidling == null ||
            !ConnectedZoneValidation.IsValidSharedVoidling(payload.Voidling))
        {
            return false;
        }

        snapshot = payload.Voidling;
        return true;
    }

    public static byte[] EncodeVoidlingPublishedEvent(
        PlatformUser sender,
        long authorityEpoch,
        long revision,
        SharedVoidlingSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return Encode(
            VoidlingPublishedEventMessageType,
            sender,
            new VoidlingPublishedPayload(authorityEpoch, revision, snapshot));
    }

    public static bool TryDecodeVoidlingPublishedEvent(
        ReadOnlySpan<byte> bytes,
        PlatformUserId transportSender,
        out Guid messageId,
        out long authorityEpoch,
        out long revision,
        out SharedVoidlingSnapshot snapshot)
    {
        authorityEpoch = 0;
        revision = 0;
        snapshot = default!;
        if (!TryDecode(
                bytes,
                transportSender,
                VoidlingPublishedEventMessageType,
                out messageId,
                out VoidlingPublishedPayload? payload) ||
            payload == null ||
            payload.AuthorityEpoch < 1 ||
            payload.Revision < 1 ||
            !ConnectedZoneValidation.IsValidSharedVoidling(payload.Voidling))
        {
            return false;
        }

        authorityEpoch = payload.AuthorityEpoch;
        revision = payload.Revision;
        snapshot = payload.Voidling;
        return true;
    }

    public static byte[] EncodeRemoveVoidlingCommand(
        PlatformUser sender,
        PlatformUserId ownerId,
        string creatureId)
        => Encode(RemoveVoidlingCommandMessageType, sender, new RemoveVoidlingPayload(ownerId, creatureId));

    public static bool TryDecodeRemoveVoidlingCommand(
        ReadOnlySpan<byte> bytes,
        PlatformUserId transportSender,
        out Guid messageId,
        out PlatformUserId ownerId,
        out string creatureId)
    {
        ownerId = default;
        creatureId = string.Empty;
        if (!TryDecode(
                bytes,
                transportSender,
                RemoveVoidlingCommandMessageType,
                out messageId,
                out RemoveVoidlingPayload? payload) ||
            payload == null ||
            payload.OwnerId.Value == 0 ||
            string.IsNullOrWhiteSpace(payload.CreatureId) ||
            payload.CreatureId.Length > ConnectedZoneValidation.MaxCreatureIdLength)
        {
            return false;
        }

        ownerId = payload.OwnerId;
        creatureId = payload.CreatureId;
        return true;
    }

    public static byte[] EncodeVoidlingRemovedEvent(
        PlatformUser sender,
        long authorityEpoch,
        long revision,
        PlatformUserId ownerId,
        string creatureId)
        => Encode(
            VoidlingRemovedEventMessageType,
            sender,
            new VoidlingRemovedPayload(authorityEpoch, revision, ownerId, creatureId));

    public static bool TryDecodeVoidlingRemovedEvent(
        ReadOnlySpan<byte> bytes,
        PlatformUserId transportSender,
        out Guid messageId,
        out long authorityEpoch,
        out long revision,
        out PlatformUserId ownerId,
        out string creatureId)
    {
        authorityEpoch = 0;
        revision = 0;
        ownerId = default;
        creatureId = string.Empty;
        if (!TryDecode(
                bytes,
                transportSender,
                VoidlingRemovedEventMessageType,
                out messageId,
                out VoidlingRemovedPayload? payload) ||
            payload == null ||
            payload.AuthorityEpoch < 1 ||
            payload.Revision < 1 ||
            payload.OwnerId.Value == 0 ||
            string.IsNullOrWhiteSpace(payload.CreatureId) ||
            payload.CreatureId.Length > ConnectedZoneValidation.MaxCreatureIdLength)
        {
            return false;
        }

        authorityEpoch = payload.AuthorityEpoch;
        revision = payload.Revision;
        ownerId = payload.OwnerId;
        creatureId = payload.CreatureId;
        return true;
    }

    private static byte[] Encode<T>(string messageType, PlatformUser sender, T payload)
        => EncodeMessage(messageType, sender, payload);

    private static bool TryDecode<T>(
        ReadOnlySpan<byte> bytes,
        PlatformUserId transportSender,
        string expectedMessageType,
        out Guid messageId,
        out T? payload)
        => TryDecodeMessage(bytes, transportSender, expectedMessageType, out messageId, out payload);

    internal static byte[] EncodeMessage<T>(string messageType, PlatformUser sender, T payload)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageType);

        var payloadElement = JsonSerializer.SerializeToElement(payload);
        var envelope = new Envelope(
            CurrentVersion,
            messageType,
            Guid.NewGuid().ToString("N"),
            sender.Id.Value,
            payloadElement);

        return JsonSerializer.SerializeToUtf8Bytes(envelope);
    }

    internal static bool TryDecodeMessage<T>(
        ReadOnlySpan<byte> bytes,
        PlatformUserId transportSender,
        string expectedMessageType,
        out Guid messageId,
        out T? payload)
    {
        messageId = default;
        payload = default;
        if (bytes.Length == 0 || bytes.Length > MaxPacketBytes || transportSender.Value == 0)
            return false;

        try
        {
            var envelope = JsonSerializer.Deserialize<Envelope>(bytes);
            if (envelope == null ||
                envelope.ProtocolVersion != CurrentVersion ||
                !string.Equals(envelope.MessageType, expectedMessageType, StringComparison.Ordinal) ||
                envelope.SenderId != transportSender.Value ||
                !Guid.TryParseExact(envelope.MessageId, "N", out messageId))
            {
                return false;
            }

            payload = envelope.Payload.Deserialize<T>();
            return payload != null;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    /// <summary>
    /// Safely inspects an envelope for protocol routing without interpreting its payload. This lets
    /// multiple typed sub-protocols share one transport channel without treating each other as malformed.
    /// </summary>
    internal static bool TryPeekMessageType(ReadOnlySpan<byte> bytes, out string messageType)
    {
        messageType = string.Empty;
        if (bytes.Length == 0 || bytes.Length > MaxPacketBytes)
            return false;

        try
        {
            var envelope = JsonSerializer.Deserialize<Envelope>(bytes);
            if (envelope == null ||
                envelope.ProtocolVersion != CurrentVersion ||
                string.IsNullOrWhiteSpace(envelope.MessageType) ||
                envelope.MessageType.Length > 128 ||
                string.IsNullOrWhiteSpace(envelope.MessageId) ||
                !Guid.TryParseExact(envelope.MessageId, "N", out _) ||
                envelope.SenderId == 0)
            {
                return false;
            }

            messageType = envelope.MessageType;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }
}
