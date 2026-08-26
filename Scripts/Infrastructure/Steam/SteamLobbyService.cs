using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Voidling.Application.Ports.Multiplayer;

namespace Voidling.Infrastructure.Steam;

internal sealed class SteamLobbyService : ILobbyService
{
    private const long SteamResultOk = 1;
    private const long LobbyEnterSuccess = 1;

    private readonly GodotSteamApi _api;
    private readonly GodotSteamRuntime _runtime;
    private readonly IPlatformIdentityService _identity;

    private TaskCompletionSource<LobbyOperationResult>? _pendingCreate;
    private TaskCompletionSource<LobbyOperationResult>? _pendingJoin;
    private ulong _currentLobbyId;

    public SteamLobbyService(
        GodotSteamApi api,
        GodotSteamRuntime runtime,
        IPlatformIdentityService identity)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _identity = identity ?? throw new ArgumentNullException(nameof(identity));

        Availability = identity.Availability.IsAvailable
            ? MultiplayerAvailability.Available
            : identity.Availability;

        _runtime.LobbyCreated += OnLobbyCreated;
        _runtime.LobbyJoined += OnLobbyJoined;
        _runtime.JoinRequested += OnJoinRequested;
        _runtime.LobbyMembershipChanged += OnLobbyMembershipChanged;
    }

    public MultiplayerAvailability Availability { get; }
    public LobbySnapshot? CurrentLobby { get; private set; }

    public event Action<LobbySnapshot>? LobbyChanged;
    public event Action<LobbyJoinRequest>? JoinRequested;

    public Task<LobbyOperationResult> CreateFriendsLobbyAsync(
        int maxMembers,
        CancellationToken cancellationToken = default)
    {
        if (!Availability.IsAvailable)
            return Task.FromResult(LobbyOperationResult.Failed(Availability.Reason ?? "Steam multiplayer is unavailable."));
        if (maxMembers is < 2 or > 16)
            return Task.FromResult(LobbyOperationResult.Failed("Connected Garden lobbies support 2 to 16 members."));
        if (_pendingCreate != null)
            return Task.FromResult(LobbyOperationResult.Failed("A lobby creation request is already pending."));
        if (_currentLobbyId != 0)
            return Task.FromResult(LobbyOperationResult.Failed("Leave the current connected Garden before creating another."));

        var completion = new TaskCompletionSource<LobbyOperationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingCreate = completion;
        RegisterCancellation(completion, cancellationToken, () => _pendingCreate = null);
        _api.CreateFriendsLobby(maxMembers);
        return completion.Task;
    }

    public Task<LobbyOperationResult> JoinAsync(
        ulong lobbyId,
        CancellationToken cancellationToken = default)
    {
        if (!Availability.IsAvailable)
            return Task.FromResult(LobbyOperationResult.Failed(Availability.Reason ?? "Steam multiplayer is unavailable."));
        if (lobbyId == 0)
            return Task.FromResult(LobbyOperationResult.Failed("Lobby ID cannot be zero."));
        if (_pendingJoin != null)
            return Task.FromResult(LobbyOperationResult.Failed("A lobby join request is already pending."));
        if (_currentLobbyId != 0 && _currentLobbyId != lobbyId)
            return Task.FromResult(LobbyOperationResult.Failed("Leave the current connected Garden before joining another."));

        var completion = new TaskCompletionSource<LobbyOperationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingJoin = completion;
        RegisterCancellation(completion, cancellationToken, () => _pendingJoin = null);
        _api.JoinLobby(lobbyId);
        return completion.Task;
    }

    public Task LeaveAsync(CancellationToken cancellationToken = default)
    {
        if (_currentLobbyId != 0)
            _api.LeaveLobby(_currentLobbyId);

        _currentLobbyId = 0;
        CurrentLobby = null;
        return Task.CompletedTask;
    }

    public void OpenInviteOverlay()
    {
        if (_currentLobbyId != 0)
            _api.OpenInviteOverlay(_currentLobbyId);
    }

    private void OnLobbyCreated(long result, long lobbyId)
    {
        var pending = _pendingCreate;
        _pendingCreate = null;
        if (pending == null)
            return;

        if (result != SteamResultOk || lobbyId <= 0)
        {
            pending.TrySetResult(LobbyOperationResult.Failed($"Steam lobby creation failed with result {result}."));
            return;
        }

        _currentLobbyId = unchecked((ulong)lobbyId);
        var snapshot = RefreshSnapshot();
        pending.TrySetResult(snapshot == null
            ? LobbyOperationResult.Failed("Steam created the lobby but its membership could not be read.")
            : LobbyOperationResult.Succeeded(snapshot));
    }

    private void OnLobbyJoined(long lobbyId, long permissions, bool locked, long response)
    {
        if (response != LobbyEnterSuccess || lobbyId <= 0)
        {
            var failed = _pendingJoin;
            _pendingJoin = null;
            failed?.TrySetResult(LobbyOperationResult.Failed($"Steam lobby join failed with response {response}."));
            return;
        }

        _currentLobbyId = unchecked((ulong)lobbyId);
        var snapshot = RefreshSnapshot();

        var pending = _pendingJoin;
        _pendingJoin = null;
        if (pending != null)
        {
            pending.TrySetResult(snapshot == null
                ? LobbyOperationResult.Failed("Joined the Steam lobby but its membership could not be read.")
                : LobbyOperationResult.Succeeded(snapshot));
        }
    }

    private void OnJoinRequested(long lobbyId, long friendId)
    {
        if (lobbyId <= 0 || friendId <= 0)
            return;

        JoinRequested?.Invoke(new LobbyJoinRequest(
            unchecked((ulong)lobbyId),
            new PlatformUserId(unchecked((ulong)friendId))));
    }

    private void OnLobbyMembershipChanged()
    {
        if (_currentLobbyId != 0)
            RefreshSnapshot();
    }

    private LobbySnapshot? RefreshSnapshot()
    {
        if (_currentLobbyId == 0)
            return null;

        var ownerId = _api.GetLobbyOwner(_currentLobbyId);
        var memberCount = _api.GetNumLobbyMembers(_currentLobbyId);
        if (ownerId == 0 || memberCount <= 0)
            return null;

        var members = new List<LobbyMember>(Math.Min(memberCount, 16));
        for (var index = 0; index < memberCount && index < 16; index++)
        {
            var memberId = _api.GetLobbyMemberByIndex(_currentLobbyId, index);
            if (memberId == 0)
                continue;

            var local = _identity.LocalUser;
            var displayName = local != null && local.Id.Value == memberId
                ? local.DisplayName
                : _api.GetFriendPersonaName(memberId);
            var user = new PlatformUser(new PlatformUserId(memberId), displayName);
            members.Add(new LobbyMember(user, memberId == ownerId));
        }

        var snapshot = new LobbySnapshot(
            _currentLobbyId,
            new PlatformUserId(ownerId),
            members.AsReadOnly());
        CurrentLobby = snapshot;
        LobbyChanged?.Invoke(snapshot);
        return snapshot;
    }

    private static void RegisterCancellation(
        TaskCompletionSource<LobbyOperationResult> completion,
        CancellationToken cancellationToken,
        Action clearPending)
    {
        if (!cancellationToken.CanBeCanceled)
            return;

        cancellationToken.Register(() =>
        {
            clearPending();
            completion.TrySetCanceled(cancellationToken);
        });
    }
}
