using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using Voidling.Application.Multiplayer;
using Voidling.Application.Ports.Multiplayer;

namespace Voidling.Infrastructure.Multiplayer;

/// <summary>
/// Development-only ENet transport/lobby used to exercise the real multiplayer Application stack
/// without Steam. It implements the same narrow ports as the Steam adapters.
///
/// ENet clients physically connect only to the host. Application messages carry an intended target
/// in a small LAN envelope, allowing the host to relay client-to-client traffic while preserving
/// the sender/channel contract seen by Application code.
/// </summary>
public partial class LanMultiplayerRuntime : Node, IPlatformIdentityService, ILobbyService, IMultiplayerTransport
{
    private const int ServerPeerId = 1;
    private const int MaxMembers = 16;
    private const int ApplicationChannelCount = 5;
    private const int ControlChannel = 0;
    private const int ApplicationChannelOffset = 1;
    private const int PhysicalChannelCount = ApplicationChannelOffset + ApplicationChannelCount;
    private const int ConnectTimeoutMilliseconds = 10_000;
    private const int MaxWireBytes = MultiplayerProtocol.MaxPacketBytes + 256;

    private readonly Dictionary<int, string> _memberNames = new();

    private LanMultiplayerOptions? _options;
    private ENetMultiplayerPeer? _peer;
    private MultiplayerAvailability _availability = MultiplayerAvailability.Unavailable("LAN test runtime is not configured.");
    private PlatformUser? _localUser;
    private LobbySnapshot? _currentLobby;
    private TaskCompletionSource<LobbyOperationResult>? _joinCompletion;
    private ulong _connectStartedAt;
    private bool _started;
    private int _maxMembers = MaxMembers;

    public MultiplayerAvailability Availability => _availability;
    public PlatformUser? LocalUser => _localUser;
    public LobbySnapshot? CurrentLobby => _currentLobby;

    public event Action<LobbySnapshot>? LobbyChanged;
#pragma warning disable CS0067 // LAN has no invite/deep-link source; interface parity is intentional.
    public event Action<LobbyJoinRequest>? JoinRequested;
#pragma warning restore CS0067
    public event Action<NetworkPacket>? PacketReceived;
    public event Action<PlatformUserId>? PeerSessionFailed;

    public void Configure(LanMultiplayerOptions options)
    {
        if (IsInsideTree())
            throw new InvalidOperationException("LAN runtime must be configured before entering the scene tree.");
        if (_options != null)
            throw new InvalidOperationException("LAN runtime is already configured.");
        if (options.Mode == LanMultiplayerMode.None)
            throw new ArgumentException("LAN runtime requires host or join mode.", nameof(options));

        _options = options;
        _availability = MultiplayerAvailability.Available;
    }

    public override void _Ready()
    {
        if (_options == null)
            throw new InvalidOperationException("LAN runtime must be configured before AddChild.");

        SetProcess(true);
        var started = _options.Mode == LanMultiplayerMode.Host
            ? StartHost(_maxMembers, out var error)
            : StartClient(out error);
        if (!started)
            FailRuntime(error ?? "Could not start LAN test transport.");
    }

    public override void _Process(double delta) => Poll();
    public override void _ExitTree() => Shutdown();

    public Task<LobbyOperationResult> CreateFriendsLobbyAsync(
        int maxMembers,
        CancellationToken cancellationToken = default)
    {
        if (!_availability.IsAvailable)
            return Task.FromResult(LobbyOperationResult.Failed(_availability.Reason ?? "LAN test mode is unavailable."));
        if (_options?.Mode != LanMultiplayerMode.Host)
            return Task.FromResult(LobbyOperationResult.Failed("This process was launched as a LAN client, not a LAN host."));
        if (maxMembers is < 2 or > MaxMembers)
            return Task.FromResult(LobbyOperationResult.Failed($"LAN connected Gardens support 2 through {MaxMembers} members."));
        if (_currentLobby != null)
            return Task.FromResult(LobbyOperationResult.Succeeded(_currentLobby));

        _maxMembers = maxMembers;
        return StartHost(maxMembers, out var error) && _currentLobby != null
            ? Task.FromResult(LobbyOperationResult.Succeeded(_currentLobby))
            : Task.FromResult(LobbyOperationResult.Failed(error ?? "Could not create LAN connected Garden."));
    }

    public Task<LobbyOperationResult> JoinAsync(
        ulong lobbyId,
        CancellationToken cancellationToken = default)
    {
        if (!_availability.IsAvailable)
            return Task.FromResult(LobbyOperationResult.Failed(_availability.Reason ?? "LAN test mode is unavailable."));
        if (_options?.Mode != LanMultiplayerMode.Join)
            return Task.FromResult(LobbyOperationResult.Failed("This process was launched as a LAN host, not a LAN client."));
        if (_currentLobby != null)
            return Task.FromResult(LobbyOperationResult.Succeeded(_currentLobby));
        if (!_started && !StartClient(out var error))
            return Task.FromResult(LobbyOperationResult.Failed(error ?? "Could not start LAN client."));

        _joinCompletion ??= new TaskCompletionSource<LobbyOperationResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        if (cancellationToken.CanBeCanceled)
            cancellationToken.Register(() => _joinCompletion?.TrySetCanceled(cancellationToken));
        return _joinCompletion.Task;
    }

    public Task LeaveAsync(CancellationToken cancellationToken = default)
    {
        Shutdown();
        _availability = MultiplayerAvailability.Available;
        return Task.CompletedTask;
    }

    public void OpenInviteOverlay()
    {
        if (_options == null)
            return;
        GD.Print(
            $"LAN test transport has no invite overlay. Join with --voidling-lan-join=<host-ip> " +
            $"--voidling-lan-port={_options.Port}.");
    }

    public bool TrySend(
        PlatformUserId peer,
        NetworkChannel channel,
        ReadOnlyMemory<byte> payload,
        DeliveryMode delivery)
    {
        if (!_availability.IsAvailable ||
            _peer == null ||
            _currentLobby == null ||
            _localUser == null ||
            payload.Length == 0 ||
            payload.Length > MultiplayerProtocol.MaxPacketBytes ||
            peer.Value == 0 ||
            peer.Value > int.MaxValue ||
            !IsLobbyMember((int)peer.Value))
        {
            return false;
        }

        var target = (int)peer.Value;
        var source = checked((int)_localUser.Id.Value);
        var frame = LanWireCodec.EncodeData(source, target, channel, delivery, payload.Span);

        if (source == ServerPeerId)
            return SendRaw(target, ToPhysicalChannel(channel), frame, delivery);

        // Clients have only one physical ENet connection. The host relays this frame onward.
        return SendRaw(ServerPeerId, ToPhysicalChannel(channel), frame, delivery);
    }

    public void Poll()
    {
        var peer = _peer;
        if (!_started || peer == null)
            return;

        if (peer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Disconnected)
        {
            if (_options?.Mode == LanMultiplayerMode.Join && _currentLobby == null)
                HandleClientConnectionFailure("LAN host disconnected or connection could not be established.");
            return;
        }

        peer.Poll();
        EnsureClientIdentityAndHandshake();
        DrainPackets();

        if (_options?.Mode == LanMultiplayerMode.Join &&
            _currentLobby == null &&
            _connectStartedAt > 0 &&
            Time.GetTicksMsec() - _connectStartedAt > ConnectTimeoutMilliseconds)
        {
            HandleClientConnectionFailure($"Timed out connecting to {_options.Address}:{_options.Port}.");
        }
    }

    public void Close(PlatformUserId peer)
    {
        if (_peer == null || peer.Value == 0 || peer.Value > int.MaxValue)
            return;

        var id = (int)peer.Value;
        try
        {
            if (_options?.Mode == LanMultiplayerMode.Host && id != ServerPeerId)
                _peer.DisconnectPeer(id);
            else if (_options?.Mode == LanMultiplayerMode.Join && id == ServerPeerId)
                _peer.Close();
        }
        catch (Exception exception)
        {
            GD.PushWarning($"LAN peer close failed for {id}: {exception.Message}");
        }
    }

    private bool StartHost(int maxMembers, out string? error)
    {
        error = null;
        if (_started)
            return _currentLobby != null;
        if (_options == null)
        {
            error = "LAN options are unavailable.";
            return false;
        }

        var peer = CreatePeer();
        var result = peer.CreateServer(_options.Port, Math.Max(1, maxMembers - 1), PhysicalChannelCount);
        if (result != Error.Ok)
        {
            DetachAndDispose(peer);
            error = $"ENet could not listen on UDP port {_options.Port}: {result}.";
            return false;
        }

        _peer = peer;
        _started = true;
        _maxMembers = maxMembers;
        _localUser = new PlatformUser(new PlatformUserId(ServerPeerId), _options.DisplayName);
        _memberNames.Clear();
        _memberNames[ServerPeerId] = _options.DisplayName;
        RebuildHostLobby(broadcastRoster: false);
        GD.Print(
            $"Voidling LAN host ready on UDP {_options.Port} as '{_options.DisplayName}'. " +
            "Other instances can join with --voidling-lan-join=<this-machine-ip>.");
        return true;
    }

    private bool StartClient(out string? error)
    {
        error = null;
        if (_started)
            return true;
        if (_options == null)
        {
            error = "LAN options are unavailable.";
            return false;
        }

        var peer = CreatePeer();
        var result = peer.CreateClient(_options.Address, _options.Port, PhysicalChannelCount);
        if (result != Error.Ok)
        {
            DetachAndDispose(peer);
            error = $"ENet could not connect to {_options.Address}:{_options.Port}: {result}.";
            return false;
        }

        _peer = peer;
        _started = true;
        _connectStartedAt = Time.GetTicksMsec();
        _memberNames.Clear();
        GD.Print($"Voidling LAN client connecting to {_options.Address}:{_options.Port} as '{_options.DisplayName}'.");
        return true;
    }

    private ENetMultiplayerPeer CreatePeer()
    {
        var peer = new ENetMultiplayerPeer();
        peer.PeerConnected += OnPeerConnected;
        peer.PeerDisconnected += OnPeerDisconnected;
        return peer;
    }

    private void OnPeerConnected(long peerIdValue)
    {
        if (peerIdValue <= 0 || peerIdValue > int.MaxValue || _options == null)
            return;

        var peerId = (int)peerIdValue;
        if (_options.Mode != LanMultiplayerMode.Host)
            return;

        if (_memberNames.Count >= _maxMembers)
        {
            _peer?.DisconnectPeer(peerId);
            return;
        }

        _memberNames[peerId] = $"LAN Player {peerId}";
        RebuildHostLobby(broadcastRoster: true);
    }

    private void OnPeerDisconnected(long peerIdValue)
    {
        if (peerIdValue <= 0 || peerIdValue > int.MaxValue || _options == null)
            return;

        var peerId = (int)peerIdValue;
        PeerSessionFailed?.Invoke(new PlatformUserId((ulong)peerId));

        if (_options.Mode == LanMultiplayerMode.Host)
        {
            if (_memberNames.Remove(peerId))
                RebuildHostLobby(broadcastRoster: true);
            return;
        }

        if (peerId == ServerPeerId)
            HandleClientConnectionFailure("LAN host disconnected.");
    }

    private void EnsureClientIdentityAndHandshake()
    {
        if (_options?.Mode != LanMultiplayerMode.Join || _peer == null || _localUser != null)
            return;
        if (_peer.GetConnectionStatus() != MultiplayerPeer.ConnectionStatus.Connected)
            return;

        var uniqueId = _peer.GetUniqueId();
        if (uniqueId <= ServerPeerId)
            return;

        _localUser = new PlatformUser(new PlatformUserId((ulong)uniqueId), _options.DisplayName);
        SendRaw(
            ServerPeerId,
            ControlChannel,
            LanWireCodec.EncodeName(uniqueId, _options.DisplayName),
            DeliveryMode.Reliable);
    }

    private void DrainPackets()
    {
        if (_peer == null)
            return;

        var guard = 0;
        while (_peer.GetAvailablePacketCount() > 0 && guard++ < 512)
        {
            var senderPeerId = _peer.GetPacketPeer();
            var physicalChannel = _peer.GetPacketChannel();
            var packet = _peer.GetPacket();
            if (packet.Length == 0 || packet.Length > MaxWireBytes)
                continue;

            if (physicalChannel == ControlChannel)
                HandleControlFrame(senderPeerId, packet);
            else
                HandleDataFrame(senderPeerId, physicalChannel, packet);
        }
    }

    private void HandleControlFrame(int actualSenderPeerId, byte[] packet)
    {
        if (_options == null)
            return;

        if (_options.Mode == LanMultiplayerMode.Host &&
            LanWireCodec.TryDecodeName(packet, out var claimedPeerId, out var displayName))
        {
            if (actualSenderPeerId != claimedPeerId ||
                actualSenderPeerId <= ServerPeerId ||
                !_memberNames.ContainsKey(actualSenderPeerId))
            {
                return;
            }

            _memberNames[actualSenderPeerId] = displayName;
            RebuildHostLobby(broadcastRoster: true);
            return;
        }

        if (_options.Mode == LanMultiplayerMode.Join &&
            actualSenderPeerId == ServerPeerId &&
            LanWireCodec.TryDecodeRoster(packet, out var roster))
        {
            ApplyClientRoster(roster);
        }
    }

    private void HandleDataFrame(int actualSenderPeerId, int physicalChannel, byte[] packet)
    {
        if (_options == null ||
            !LanWireCodec.TryDecodeData(packet, out var frame) ||
            frame.Payload.Length == 0 ||
            frame.Payload.Length > MultiplayerProtocol.MaxPacketBytes ||
            ToPhysicalChannel(frame.Channel) != physicalChannel)
        {
            return;
        }

        if (_options.Mode == LanMultiplayerMode.Host)
        {
            if (frame.SourcePeerId != actualSenderPeerId ||
                !IsLobbyMember(actualSenderPeerId) ||
                !IsLobbyMember(frame.TargetPeerId))
            {
                return;
            }

            if (frame.TargetPeerId == ServerPeerId)
            {
                PacketReceived?.Invoke(new NetworkPacket(
                    new PlatformUserId((ulong)frame.SourcePeerId),
                    frame.Channel,
                    frame.Payload));
                return;
            }

            SendRaw(frame.TargetPeerId, ToPhysicalChannel(frame.Channel), packet, frame.Delivery);
            return;
        }

        if (actualSenderPeerId != ServerPeerId ||
            _localUser == null ||
            frame.TargetPeerId != (int)_localUser.Id.Value ||
            !IsLobbyMember(frame.SourcePeerId))
        {
            return;
        }

        PacketReceived?.Invoke(new NetworkPacket(
            new PlatformUserId((ulong)frame.SourcePeerId),
            frame.Channel,
            frame.Payload));
    }

    private bool SendRaw(int targetPeerId, int physicalChannel, byte[] payload, DeliveryMode delivery)
    {
        if (_peer == null ||
            payload.Length == 0 ||
            payload.Length > MaxWireBytes ||
            physicalChannel is < 0 or >= PhysicalChannelCount)
        {
            return false;
        }

        try
        {
            _peer.SetTargetPeer(targetPeerId);
            _peer.TransferChannel = physicalChannel;
            _peer.TransferMode = delivery == DeliveryMode.Reliable
                ? MultiplayerPeer.TransferModeEnum.Reliable
                : MultiplayerPeer.TransferModeEnum.Unreliable;
            var result = _peer.PutPacket(payload);
            if (result == Error.Ok)
                return true;

            GD.PushWarning($"LAN ENet send to peer {targetPeerId} failed: {result}.");
            PeerSessionFailed?.Invoke(new PlatformUserId((ulong)Math.Max(1, targetPeerId)));
            return false;
        }
        catch (Exception exception)
        {
            GD.PushWarning($"LAN ENet send to peer {targetPeerId} threw: {exception.Message}");
            PeerSessionFailed?.Invoke(new PlatformUserId((ulong)Math.Max(1, targetPeerId)));
            return false;
        }
    }

    private void RebuildHostLobby(bool broadcastRoster)
    {
        if (_options?.Mode != LanMultiplayerMode.Host || _localUser == null)
            return;

        var members = _memberNames
            .OrderBy(pair => pair.Key)
            .Select(pair => new LobbyMember(
                new PlatformUser(new PlatformUserId((ulong)pair.Key), pair.Value),
                pair.Key == ServerPeerId))
            .ToArray();
        _currentLobby = new LobbySnapshot(
            (ulong)_options.Port,
            new PlatformUserId(ServerPeerId),
            members);
        LobbyChanged?.Invoke(_currentLobby);

        if (!broadcastRoster || _peer == null)
            return;

        var payload = LanWireCodec.EncodeRoster(members);
        foreach (var member in members)
        {
            var id = (int)member.User.Id.Value;
            if (id != ServerPeerId)
                SendRaw(id, ControlChannel, payload, DeliveryMode.Reliable);
        }
    }

    private void ApplyClientRoster(IReadOnlyList<LobbyMember> members)
    {
        if (_options?.Mode != LanMultiplayerMode.Join || _localUser == null)
            return;
        if (members.Count == 0 ||
            members.Count > MaxMembers ||
            !members.Any(member => member.User.Id == _localUser.Id) ||
            !members.Any(member => member.User.Id.Value == ServerPeerId && member.IsOwner))
        {
            return;
        }

        var parsedNames = new Dictionary<int, string>();
        foreach (var member in members)
        {
            if (member.User.Id.Value is 0 or > int.MaxValue)
                return;
            parsedNames[(int)member.User.Id.Value] = member.User.DisplayName;
        }

        _memberNames.Clear();
        foreach (var pair in parsedNames)
            _memberNames[pair.Key] = pair.Value;

        _currentLobby = new LobbySnapshot(
            (ulong)_options.Port,
            new PlatformUserId(ServerPeerId),
            members.ToArray());
        LobbyChanged?.Invoke(_currentLobby);
        _joinCompletion?.TrySetResult(LobbyOperationResult.Succeeded(_currentLobby));
        _joinCompletion = null;
    }

    private bool IsLobbyMember(int peerId)
        => peerId > 0 && _memberNames.ContainsKey(peerId);

    private void HandleClientConnectionFailure(string reason)
    {
        if (_options?.Mode != LanMultiplayerMode.Join)
            return;

        _joinCompletion?.TrySetResult(LobbyOperationResult.Failed(reason));
        _joinCompletion = null;
        _currentLobby = null;
        _memberNames.Clear();
        _localUser = null;
        _connectStartedAt = 0;
        DetachAndDispose(_peer);
        _peer = null;
        _started = false;
        GD.PushWarning(reason);
    }

    private void FailRuntime(string reason)
    {
        Shutdown();
        _availability = MultiplayerAvailability.Unavailable(reason + " Single-player remains available.");
        GD.PushWarning(_availability.Reason);
    }

    private void Shutdown()
    {
        _joinCompletion?.TrySetResult(LobbyOperationResult.Failed("LAN session was closed."));
        _joinCompletion = null;
        DetachAndDispose(_peer);
        _peer = null;
        _started = false;
        _connectStartedAt = 0;
        _currentLobby = null;
        _localUser = null;
        _memberNames.Clear();
    }

    private static void DetachAndDispose(ENetMultiplayerPeer? peer)
    {
        if (peer == null)
            return;
        try
        {
            peer.Close();
        }
        catch
        {
            // Best-effort development transport cleanup.
        }
        peer.Dispose();
    }

    private static int ToPhysicalChannel(NetworkChannel channel)
        => checked((int)channel + ApplicationChannelOffset);

    private sealed record LanDataFrame(
        int SourcePeerId,
        int TargetPeerId,
        NetworkChannel Channel,
        DeliveryMode Delivery,
        byte[] Payload);

    private static class LanWireCodec
    {
        private const uint Magic = 0x4E414C56;
        private const byte Version = 1;
        private const byte KindName = 1;
        private const byte KindRoster = 2;
        private const byte KindData = 3;
        private const int MaxNameBytes = 160;

        public static byte[] EncodeName(int peerId, string displayName)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
            WriteHeader(writer, KindName);
            writer.Write(peerId);
            WriteString(writer, displayName);
            writer.Flush();
            return stream.ToArray();
        }

        public static bool TryDecodeName(ReadOnlySpan<byte> bytes, out int peerId, out string displayName)
        {
            peerId = 0;
            displayName = string.Empty;
            try
            {
                using var reader = CreateReader(bytes, KindName);
                if (reader == null)
                    return false;
                peerId = reader.ReadInt32();
                return TryReadString(reader, out displayName) &&
                       peerId > ServerPeerId &&
                       displayName.Length is > 0 and <= 40;
            }
            catch
            {
                return false;
            }
        }

        public static byte[] EncodeRoster(IReadOnlyList<LobbyMember> members)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
            WriteHeader(writer, KindRoster);
            writer.Write(members.Count);
            foreach (var member in members)
            {
                writer.Write(checked((int)member.User.Id.Value));
                writer.Write(member.IsOwner);
                WriteString(writer, member.User.DisplayName);
            }
            writer.Flush();
            return stream.ToArray();
        }

        public static bool TryDecodeRoster(ReadOnlySpan<byte> bytes, out IReadOnlyList<LobbyMember> members)
        {
            members = Array.Empty<LobbyMember>();
            try
            {
                using var reader = CreateReader(bytes, KindRoster);
                if (reader == null)
                    return false;
                var count = reader.ReadInt32();
                if (count is < 1 or > MaxMembers)
                    return false;

                var parsed = new List<LobbyMember>(count);
                var seen = new HashSet<int>();
                for (var i = 0; i < count; i++)
                {
                    var peerId = reader.ReadInt32();
                    var isOwner = reader.ReadBoolean();
                    if (peerId <= 0 || !seen.Add(peerId) || !TryReadString(reader, out var name))
                        return false;
                    parsed.Add(new LobbyMember(
                        new PlatformUser(new PlatformUserId((ulong)peerId), name),
                        isOwner));
                }

                if (parsed.Count(member => member.IsOwner) != 1 ||
                    parsed.Single(member => member.IsOwner).User.Id.Value != ServerPeerId)
                {
                    return false;
                }

                members = parsed;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static byte[] EncodeData(
            int sourcePeerId,
            int targetPeerId,
            NetworkChannel channel,
            DeliveryMode delivery,
            ReadOnlySpan<byte> payload)
        {
            using var stream = new MemoryStream(payload.Length + 32);
            using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
            WriteHeader(writer, KindData);
            writer.Write(sourcePeerId);
            writer.Write(targetPeerId);
            writer.Write((byte)channel);
            writer.Write((byte)delivery);
            writer.Write(payload.Length);
            writer.Write(payload);
            writer.Flush();
            return stream.ToArray();
        }

        public static bool TryDecodeData(ReadOnlySpan<byte> bytes, out LanDataFrame frame)
        {
            frame = null!;
            try
            {
                using var reader = CreateReader(bytes, KindData);
                if (reader == null)
                    return false;

                var source = reader.ReadInt32();
                var target = reader.ReadInt32();
                var channelRaw = reader.ReadByte();
                var deliveryRaw = reader.ReadByte();
                var payloadLength = reader.ReadInt32();
                if (source <= 0 ||
                    target <= 0 ||
                    !Enum.IsDefined(typeof(NetworkChannel), (int)channelRaw) ||
                    !Enum.IsDefined(typeof(DeliveryMode), (int)deliveryRaw) ||
                    payloadLength is <= 0 or > MultiplayerProtocol.MaxPacketBytes ||
                    reader.BaseStream.Length - reader.BaseStream.Position != payloadLength)
                {
                    return false;
                }

                frame = new LanDataFrame(
                    source,
                    target,
                    (NetworkChannel)channelRaw,
                    (DeliveryMode)deliveryRaw,
                    reader.ReadBytes(payloadLength));
                return frame.Payload.Length == payloadLength;
            }
            catch
            {
                return false;
            }
        }

        private static void WriteHeader(BinaryWriter writer, byte kind)
        {
            writer.Write(Magic);
            writer.Write(Version);
            writer.Write(kind);
        }

        private static BinaryReader? CreateReader(ReadOnlySpan<byte> bytes, byte expectedKind)
        {
            if (bytes.Length < 6 || bytes.Length > MaxWireBytes)
                return null;

            var reader = new BinaryReader(
                new MemoryStream(bytes.ToArray(), writable: false),
                Encoding.UTF8,
                leaveOpen: false);
            if (reader.ReadUInt32() == Magic &&
                reader.ReadByte() == Version &&
                reader.ReadByte() == expectedKind)
            {
                return reader;
            }

            reader.Dispose();
            return null;
        }

        private static void WriteString(BinaryWriter writer, string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value.Trim());
            if (bytes.Length is < 1 or > MaxNameBytes)
                throw new ArgumentOutOfRangeException(nameof(value));
            writer.Write((ushort)bytes.Length);
            writer.Write(bytes);
        }

        private static bool TryReadString(BinaryReader reader, out string value)
        {
            value = string.Empty;
            var length = reader.ReadUInt16();
            if (length is < 1 or > MaxNameBytes ||
                reader.BaseStream.Length - reader.BaseStream.Position < length)
            {
                return false;
            }

            var bytes = reader.ReadBytes(length);
            if (bytes.Length != length)
                return false;
            value = Encoding.UTF8.GetString(bytes).Trim();
            return value.Length is > 0 and <= 40;
        }
    }
}
