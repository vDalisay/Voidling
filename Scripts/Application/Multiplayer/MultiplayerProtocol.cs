using System;
using System.Text.Json;
using Voidling.Application.Ports.Multiplayer;

namespace Voidling.Application.Multiplayer;

public static class MultiplayerProtocol
{
    public const int CurrentVersion = 1;
    public const int MaxPacketBytes = 64 * 1024;
    public const string HelloMessageType = "hello";

    private sealed record Envelope(
        int ProtocolVersion,
        string MessageType,
        string MessageId,
        ulong SenderId,
        JsonElement Payload);

    private sealed record HelloPayload(string DisplayName);

    public static byte[] EncodeHello(PlatformUser sender)
    {
        ArgumentNullException.ThrowIfNull(sender);

        var payload = JsonSerializer.SerializeToElement(new HelloPayload(sender.DisplayName));
        var envelope = new Envelope(
            CurrentVersion,
            HelloMessageType,
            Guid.NewGuid().ToString("N"),
            sender.Id.Value,
            payload);

        return JsonSerializer.SerializeToUtf8Bytes(envelope);
    }

    public static bool TryDecodeHello(
        ReadOnlySpan<byte> bytes,
        PlatformUserId transportSender,
        out PlatformUser sender)
    {
        sender = default!;
        if (bytes.Length == 0 || bytes.Length > MaxPacketBytes)
            return false;

        try
        {
            var envelope = JsonSerializer.Deserialize<Envelope>(bytes);
            if (envelope == null ||
                envelope.ProtocolVersion != CurrentVersion ||
                !string.Equals(envelope.MessageType, HelloMessageType, StringComparison.Ordinal) ||
                envelope.SenderId != transportSender.Value)
            {
                return false;
            }

            var hello = envelope.Payload.Deserialize<HelloPayload>();
            if (hello == null || string.IsNullOrWhiteSpace(hello.DisplayName))
                return false;

            sender = new PlatformUser(transportSender, hello.DisplayName.Trim());
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
