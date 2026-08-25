using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Voidling.Infrastructure.Multiplayer;

public enum LanMultiplayerMode
{
    None,
    Host,
    Join
}

/// <summary>
/// Development-only LAN transport settings. These flags deliberately never activate unless an
/// explicit host/join mode is present, so ordinary launches keep the Steam/offline composition.
/// </summary>
public sealed record LanMultiplayerOptions(
    LanMultiplayerMode Mode,
    string Address,
    int Port,
    string DisplayName,
    string? DevelopmentProfile)
{
    public const int DefaultPort = 27181;

    public static bool IsLanRequested(IReadOnlyList<string> args)
        => args.Any(arg =>
            string.Equals(arg, "--voidling-lan-host", StringComparison.OrdinalIgnoreCase) ||
            arg.StartsWith("--voidling-lan-join=", StringComparison.OrdinalIgnoreCase));

    public static bool TryParse(
        IReadOnlyList<string> args,
        out LanMultiplayerOptions? options,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(args);
        options = null;
        error = null;

        var host = args.Any(arg =>
            string.Equals(arg, "--voidling-lan-host", StringComparison.OrdinalIgnoreCase));
        var joinValues = args
            .Where(arg => arg.StartsWith("--voidling-lan-join=", StringComparison.OrdinalIgnoreCase))
            .Select(arg => arg[(arg.IndexOf('=') + 1)..].Trim())
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (!host && joinValues.Length == 0)
            return true;
        if (host && joinValues.Length > 0)
        {
            error = "LAN test mode cannot host and join in the same process.";
            return false;
        }
        if (joinValues.Length > 1)
        {
            error = "Only one --voidling-lan-join address may be supplied.";
            return false;
        }

        var port = DefaultPort;
        var portArg = args.LastOrDefault(arg =>
            arg.StartsWith("--voidling-lan-port=", StringComparison.OrdinalIgnoreCase));
        if (portArg != null)
        {
            var rawPort = portArg[(portArg.IndexOf('=') + 1)..].Trim();
            if (!int.TryParse(rawPort, NumberStyles.None, CultureInfo.InvariantCulture, out port) ||
                port is < 1024 or > 65535)
            {
                error = "--voidling-lan-port must be an integer from 1024 through 65535.";
                return false;
            }
        }

        var profile = ReadOptionalValue(args, "--voidling-dev-profile=");
        if (profile != null && !IsSafeProfile(profile))
        {
            error = "--voidling-dev-profile may contain only letters, numbers, '-' and '_'.";
            return false;
        }

        var name = ReadOptionalValue(args, "--voidling-lan-name=");
        if (string.IsNullOrWhiteSpace(name))
            name = profile ?? (host ? "LAN Host" : "LAN Player");
        name = name.Trim();
        if (name.Length > 40)
        {
            error = "--voidling-lan-name must be 40 characters or fewer.";
            return false;
        }

        options = new LanMultiplayerOptions(
            host ? LanMultiplayerMode.Host : LanMultiplayerMode.Join,
            host ? "0.0.0.0" : joinValues[0],
            port,
            name,
            profile);
        return true;
    }

    public static string ResolveDevelopmentSavePath(
        string defaultPath,
        IReadOnlyList<string> args)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultPath);
        ArgumentNullException.ThrowIfNull(args);

        var profile = ReadOptionalValue(args, "--voidling-dev-profile=");
        if (profile == null || !IsSafeProfile(profile))
            return defaultPath;

        var dot = defaultPath.LastIndexOf('.');
        return dot > defaultPath.LastIndexOf('/')
            ? defaultPath[..dot] + "_" + profile + defaultPath[dot..]
            : defaultPath + "_" + profile;
    }

    private static string? ReadOptionalValue(IReadOnlyList<string> args, string prefix)
    {
        var arg = args.LastOrDefault(value =>
            value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        if (arg == null)
            return null;

        var value = arg[prefix.Length..].Trim();
        return value.Length == 0 ? null : value;
    }

    private static bool IsSafeProfile(string value)
    {
        if (value.Length is < 1 or > 32)
            return false;

        foreach (var character in value)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_')
                return false;
        }

        return true;
    }
}
