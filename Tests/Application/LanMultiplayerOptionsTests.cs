using Voidling.Infrastructure.Multiplayer;
using Xunit;

namespace Voidling.Tests.Application;

public sealed class LanMultiplayerOptionsTests
{
    [Fact]
    public void NoLanFlagsLeaveLanModeDisabled()
    {
        var args = new[] { "--some-other-flag" };

        var parsed = LanMultiplayerOptions.TryParse(args, out var options, out var error);

        Assert.True(parsed, error);
        Assert.Null(options);
        Assert.False(LanMultiplayerOptions.IsLanRequested(args));
    }

    [Fact]
    public void HostModeUsesExplicitNamePortAndProfile()
    {
        var args = new[]
        {
            "--voidling-lan-host",
            "--voidling-lan-port=32123",
            "--voidling-lan-name=Alice",
            "--voidling-dev-profile=A"
        };

        var parsed = LanMultiplayerOptions.TryParse(args, out var options, out var error);

        Assert.True(parsed, error);
        Assert.NotNull(options);
        Assert.Equal(LanMultiplayerMode.Host, options!.Mode);
        Assert.Equal(32123, options.Port);
        Assert.Equal("Alice", options.DisplayName);
        Assert.Equal("A", options.DevelopmentProfile);
    }

    [Fact]
    public void JoinModeKeepsAddressAndDefaultsPort()
    {
        var args = new[]
        {
            "--voidling-lan-join=192.168.1.42",
            "--voidling-lan-name=Bob"
        };

        var parsed = LanMultiplayerOptions.TryParse(args, out var options, out var error);

        Assert.True(parsed, error);
        Assert.NotNull(options);
        Assert.Equal(LanMultiplayerMode.Join, options!.Mode);
        Assert.Equal("192.168.1.42", options.Address);
        Assert.Equal(LanMultiplayerOptions.DefaultPort, options.Port);
        Assert.Equal("Bob", options.DisplayName);
    }

    [Fact]
    public void HostAndJoinTogetherAreRejected()
    {
        var args = new[]
        {
            "--voidling-lan-host",
            "--voidling-lan-join=127.0.0.1"
        };

        var parsed = LanMultiplayerOptions.TryParse(args, out var options, out var error);

        Assert.False(parsed);
        Assert.Null(options);
        Assert.Contains("cannot host and join", error);
    }

    [Theory]
    [InlineData("--voidling-lan-port=80")]
    [InlineData("--voidling-lan-port=70000")]
    [InlineData("--voidling-lan-port=abc")]
    public void InvalidPortIsRejected(string portArg)
    {
        var args = new[] { "--voidling-lan-host", portArg };

        var parsed = LanMultiplayerOptions.TryParse(args, out _, out var error);

        Assert.False(parsed);
        Assert.Contains("port", error, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DevelopmentProfileCreatesIndependentSavePath()
    {
        var args = new[] { "--voidling-dev-profile=Bob_2" };

        var path = LanMultiplayerOptions.ResolveDevelopmentSavePath(
            "user://voidling_mvp_save.json",
            args);

        Assert.Equal("user://voidling_mvp_save_Bob_2.json", path);
    }

    [Fact]
    public void MissingProfileKeepsProductionSavePathExactly()
    {
        var path = LanMultiplayerOptions.ResolveDevelopmentSavePath(
            "user://voidling_mvp_save.json",
            new[] { "--voidling-lan-host" });

        Assert.Equal("user://voidling_mvp_save.json", path);
    }
}
