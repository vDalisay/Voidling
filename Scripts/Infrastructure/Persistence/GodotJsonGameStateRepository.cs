using System;
using System.Text.Json;
using Godot;
using Voidling.Application.Ports;
using Voidling.Infrastructure.Multiplayer;
using VoidlingGame;

namespace Voidling.Infrastructure.Persistence;

/// <summary>
/// Godot user-data repository with a one-generation backup. Saves are serialized completely to a
/// temporary file before replacing the primary. If the primary cannot be read/deserialized, Load
/// falls back to the last known backup and reports that recovery through IGameStateRecoveryInfo.
/// </summary>
public sealed class GodotJsonGameStateRepository : IGameStateRepository, IGameStateRecoveryInfo
{
    private readonly string _savePath;
    private readonly string _backupPath;
    private readonly string _temporaryPath;
    private readonly JsonSerializerOptions _jsonOptions;
    private bool _loadedFromBackup;

    public GodotJsonGameStateRepository(string savePath)
    {
        if (string.IsNullOrWhiteSpace(savePath))
            throw new ArgumentException("A save path is required.", nameof(savePath));

        // Development LAN testing often runs multiple game processes on one machine. Keep those
        // processes from sharing/mutating the same user:// save when an explicit profile is supplied.
        // Normal launches have no profile flag and therefore keep the exact existing save path.
        _savePath = LanMultiplayerOptions.ResolveDevelopmentSavePath(
            savePath,
            OS.GetCmdlineUserArgs());
        _backupPath = _savePath + ".bak";
        _temporaryPath = _savePath + ".tmp";
        _jsonOptions = new JsonSerializerOptions { WriteIndented = true };
    }

    public GameStateRecoveryStatus LastLoadRecoveryStatus { get; private set; }

    public GameStateData? Load()
    {
        LastLoadRecoveryStatus = GameStateRecoveryStatus.None;
        _loadedFromBackup = false;

        Exception? primaryFailure = null;
        if (FileAccess.FileExists(_savePath))
        {
            try
            {
                return LoadRequired(_savePath);
            }
            catch (Exception exception)
            {
                primaryFailure = exception;
            }
        }

        if (FileAccess.FileExists(_backupPath))
        {
            try
            {
                var recovered = LoadRequired(_backupPath);
                LastLoadRecoveryStatus = GameStateRecoveryStatus.RecoveredFromBackup;
                _loadedFromBackup = true;
                return recovered;
            }
            catch (Exception backupFailure)
            {
                if (primaryFailure != null)
                {
                    throw new InvalidOperationException(
                        "The primary save and its backup could not be loaded.",
                        new AggregateException(primaryFailure, backupFailure));
                }

                throw new InvalidOperationException("The save backup could not be loaded.", backupFailure);
            }
        }

        if (primaryFailure != null)
            throw new InvalidOperationException("The primary save could not be loaded and no backup is available.", primaryFailure);

        return null;
    }

    public void Save(GameStateData state)
    {
        ArgumentNullException.ThrowIfNull(state);

        // Serialize before touching any existing file. A serialization error therefore leaves both
        // the primary and backup byte-for-byte intact.
        var json = JsonSerializer.Serialize(state, _jsonOptions);
        WriteTemporary(json);

        try
        {
            // When Load recovered from a backup, the existing primary is known-bad. Do not overwrite
            // the valid backup with that corrupt primary during the healing save.
            if (!_loadedFromBackup && FileAccess.FileExists(_savePath))
            {
                var backupResult = DirAccess.CopyAbsolute(_savePath, _backupPath);
                if (backupResult != Error.Ok)
                    throw new InvalidOperationException($"Could not update save backup ({backupResult}).");
            }

            var replaceResult = DirAccess.RenameAbsolute(_temporaryPath, _savePath);
            if (replaceResult != Error.Ok)
                throw new InvalidOperationException($"Could not replace the save file ({replaceResult}).");

            _loadedFromBackup = false;
        }
        catch
        {
            TryRemoveTemporary();
            throw;
        }
    }

    private GameStateData LoadRequired(string path)
    {
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null)
            throw new InvalidOperationException($"Could not open save file '{path}' for reading.");

        var json = file.GetAsText();
        return JsonSerializer.Deserialize<GameStateData>(json, _jsonOptions)
               ?? throw new InvalidOperationException($"Save file '{path}' did not contain game state.");
    }

    private void WriteTemporary(string json)
    {
        TryRemoveTemporary();
        using var file = FileAccess.Open(_temporaryPath, FileAccess.ModeFlags.Write);
        if (file == null)
            throw new InvalidOperationException($"Could not open temporary save path '{_temporaryPath}' for writing.");

        file.StoreString(json);
        file.Flush();
    }

    private void TryRemoveTemporary()
    {
        if (!FileAccess.FileExists(_temporaryPath))
            return;

        var result = DirAccess.RemoveAbsolute(_temporaryPath);
        if (result != Error.Ok)
            GD.PushWarning($"Could not remove temporary save file: {result}.");
    }
}
