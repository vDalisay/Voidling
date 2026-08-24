using System;
using Godot;
using Voidling.Application.Multiplayer;
using Voidling.Application.Ports.Multiplayer;

namespace Voidling.Infrastructure.Steam;

internal sealed class SteamNetworkingMessagesTransport : IMultiplayerTransport
{
    private const int MaxMessagesPerChannelPerPoll = 64;

    private readonly GodotSteamApi _api;
    private readonly GodotSteamRuntime _runtime;
    private readonly ILobbyService _lobbies;

    public SteamNetworkingMessagesTransport(
        GodotSteamApi api,
        GodotSteamRuntime runtime,
        ILobbyService lobbies)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _lobbies = lobbies ?? throw new ArgumentNullException(nameof(lobbies));

        Availability = lobbies.Availability.IsAvailable
            ? MultiplayerAvailability.Available
            : lobbies.Availability;

        _runtime.NetworkingMessagesSessionRequested += OnSessionRequested;
    }

    public MultiplayerAvailability Availability { get; }

    public event Action<NetworkPacket>? PacketReceived;
    public event Action<PlatformUserId>? PeerSessionFailed;

    public bool TrySend(
        PlatformUserId peer,
        NetworkChannel channel,
        ReadOnlyMemory<byte> payload,
        DeliveryMode delivery)
    {
        if (!Availability.IsAvailable ||
            payload.Length == 0 ||
            payload.Length > MultiplayerProtocol.MaxPacketBytes ||
            !IsCurrentLobbyMember(peer))
        {
            return false;
        }

        try
        {
            return _api.SendMessageToUser(
                peer.Value,
                payload.ToArray(),
                delivery == DeliveryMode.Reliable,
                (int)channel);
        }
        catch (Exception exception)
        {
            GD.PushWarning($"Steam message send failed for {peer}: {exception.Message}");
            PeerSessionFailed?.Invoke(peer);
            return false;
        }
    }

    public void Poll()
    {
        if (!Availability.IsAvailable || _lobbies.CurrentLobby == null)
            return;

        PollChannel(NetworkChannel.Session);
        PollChannel(NetworkChannel.Challenge);
        PollChannel(NetworkChannel.Trade);
    }

    public void Close(PlatformUserId peer)
    {
        if (!Availability.IsAvailable)
            return;

        try
        {
            _api.CloseSessionWithUser(peer.Value);
        }
        catch (Exception exception)
        {
            GD.PushWarning($"Steam message session close failed for {peer}: {exception.Message}");
        }
    }

    private void OnSessionRequested(long remoteSteamId)
    {
        if (remoteSteamId <= 0)
            return;

        var peer = new PlatformUserId(unchecked((ulong)remoteSteamId));
        if (!IsCurrentLobbyMember(peer))
            return;

        try
        {
            _api.AcceptSessionWithUser(peer.Value);
        }
        catch (Exception exception)
        {
            GD.PushWarning($"Steam message session accept failed for {peer}: {exception.Message}");
            PeerSessionFailed?.Invoke(peer);
        }
    }

    private void PollChannel(NetworkChannel channel)
    {
        Variant messages;
        try
        {
            messages = _api.ReceiveMessagesOnChannel((int)channel, MaxMessagesPerChannelPerPoll);
        }
        catch (Exception exception)
        {
            GD.PushWarning($"Steam receive failed on channel {(int)channel}: {exception.Message}");
            return;
        }

        if (messages.VariantType != Variant.Type.Array)
            return;

        foreach (var messageValue in messages.AsGodotArray())
        {
            if (messageValue.VariantType != Variant.Type.Dictionary)
                continue;

            var message = messageValue.AsGodotDictionary();
            if (!message.ContainsKey("payload"))
                continue;

            var payloadVariant = message["payload"];
            if (payloadVariant.VariantType != Variant.Type.PackedByteArray)
                continue;

            var payload = payloadVariant.AsByteArray();
            if (payload.Length == 0 || payload.Length > MultiplayerProtocol.MaxPacketBytes)
                continue;

            if (!TryReadSender(message, out var sender) || !IsCurrentLobbyMember(sender))
                continue;

            PacketReceived?.Invoke(new NetworkPacket(sender, channel, payload));
        }
    }

    private bool IsCurrentLobbyMember(PlatformUserId peer)
    {
        var lobby = _lobbies.CurrentLobby;
        if (lobby == null)
            return false;

        foreach (var member in lobby.Members)
        {
            if (member.User.Id == peer)
                return true;
        }

        return false;
    }

    private static bool TryReadSender(Godot.Collections.Dictionary message, out PlatformUserId sender)
    {
        sender = default;

        foreach (var key in new[] { "remote_steam_id", "steam_id", "identity_peer", "identity", "peer" })
        {
            if (!message.ContainsKey(key))
                continue;

            var value = message[key];
            if (TryReadSteamId(value, out var id))
            {
                sender = new PlatformUserId(id);
                return true;
            }
        }

        return false;
    }

    private static bool TryReadSteamId(Variant value, out ulong steamId)
    {
        steamId = 0;

        if (value.VariantType == Variant.Type.Int)
        {
            var raw = value.AsInt64();
            if (raw > 0)
            {
                steamId = unchecked((ulong)raw);
                return true;
            }
        }

        if (value.VariantType != Variant.Type.Dictionary)
            return false;

        var identity = value.AsGodotDictionary();
        foreach (var key in new[] { "steam_id", "steam_id64", "steamID64", "id" })
        {
            if (!identity.ContainsKey(key))
                continue;

            var raw = identity[key].AsInt64();
            if (raw <= 0)
                continue;

            steamId = unchecked((ulong)raw);
            return true;
        }

        return false;
    }
}
