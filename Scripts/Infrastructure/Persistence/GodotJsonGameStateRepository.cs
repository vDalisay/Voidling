using System;
using System.Text.Json;
using Godot;
using Voidling.Application.Ports;
using Voidling.Infrastructure.Multiplayer;
using VoidlingGame;

namespace Voidling.Infrastructure.Persistence;

public sealed class GodotJsonGameStateRepository : IGameStateRepository
{
    private readonly string _savePath;
    private readonly JsonSerializerOptions _jsonOptions;

    public GodotJsonGameStateRepository(string savePath)
    {
        if (string.IsNullOrWhiteSpace(savePath))
            throw new ArgumentException("A save path is required.", nameof(savePath));

        // Development LAN testing often runs multiple game processes on one machine. Keep those
        // processes from sharing/mutating the same user:// save when an explicit profile is supplied.
        // Normal launches have no profile flag and therefore keep the exact existing save path.
        _savePath = LanMultiplayerOptions.ResolveDevelopmentSavePath(
            savePath,
            OS.GetCmdlineArgs());
        _jsonOptions = new JsonSerializerOptions { WriteIndented = true };
    }

    public GameStateData? Load()
    {
        if (!FileAccess.FileExists(_savePath))
            return null;

        using var file = FileAccess.Open(_savePath, FileAccess.ModeFlags.Read);
        if (file == null)
            return null;

        var json = file.GetAsText();
        return JsonSerializer.Deserialize<GameStateData>(json, _jsonOptions);
    }

    public void Save(GameStateData state)
    {
        ArgumentNullException.ThrowIfNull(state);

        using var file = FileAccess.Open(_savePath, FileAccess.ModeFlags.Write);
        if (file == null)
            throw new InvalidOperationException($"Could not open save path '{_savePath}' for writing.");

        file.StoreString(JsonSerializer.Serialize(state, _jsonOptions));
    }
}
