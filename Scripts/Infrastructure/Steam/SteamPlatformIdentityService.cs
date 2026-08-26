using Voidling.Application.Ports.Multiplayer;

namespace Voidling.Infrastructure.Steam;

internal sealed class SteamPlatformIdentityService : IPlatformIdentityService
{
    public SteamPlatformIdentityService(GodotSteamApi api)
    {
        var steamId = api.GetSteamId();
        var name = api.GetPersonaName();

        if (steamId == 0)
        {
            Availability = MultiplayerAvailability.Unavailable("Steam initialized, but no local Steam user is available.");
            LocalUser = null;
            return;
        }

        Availability = MultiplayerAvailability.Available;
        LocalUser = new PlatformUser(new PlatformUserId(steamId), name);
    }

    public MultiplayerAvailability Availability { get; }
    public PlatformUser? LocalUser { get; }
}
