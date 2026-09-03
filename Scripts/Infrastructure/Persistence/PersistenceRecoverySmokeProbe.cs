using System;
using Godot;
using Voidling.Application.Ports;
using VoidlingGame;

namespace Voidling.Infrastructure.Persistence;

/// <summary>
/// Headless regression probe for the real user:// repository. It proves that a corrupt primary
/// falls back to the previous backup and that the healing save does not copy the corrupt primary
/// over that valid backup.
/// </summary>
public partial class PersistenceRecoverySmokeProbe : Node
{
    private const string SmokePath = "user://voidling_persistence_recovery_smoke.json";
    private const string SuccessMarker = "PERSISTENCE_RECOVERY_SMOKE_SUCCESS";

    public override void _Ready()
        => Callable.From(Run).CallDeferred();

    private void Run()
    {
        try
        {
            Cleanup();

            var repository = new GodotJsonGameStateRepository(SmokePath);
            repository.Save(new GameStateData { Coins = 111, SeedCounter = 11 });
            repository.Save(new GameStateData { Coins = 222, SeedCounter = 22 });
            CorruptPrimary();

            var recovered = repository.Load()
                ?? throw new InvalidOperationException("Recovery returned no game state.");
            if (recovered.Coins != 111 || repository.LastLoadRecoveryStatus != GameStateRecoveryStatus.RecoveredFromBackup)
                throw new InvalidOperationException("Repository did not recover the expected backup state.");

            repository.Save(recovered);

            var verificationRepository = new GodotJsonGameStateRepository(SmokePath);
            var verified = verificationRepository.Load()
                ?? throw new InvalidOperationException("Healed primary could not be loaded.");
            if (verified.Coins != 111 || verificationRepository.LastLoadRecoveryStatus != GameStateRecoveryStatus.None)
                throw new InvalidOperationException("Recovered state was not healed back into the primary save.");

            GD.Print(SuccessMarker);
            Cleanup();
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"Persistence recovery smoke failed: {exception}");
            Cleanup();
            GetTree().Quit(1);
        }
    }

    private static void CorruptPrimary()
    {
        using var file = FileAccess.Open(SmokePath, FileAccess.ModeFlags.Write);
        if (file == null)
            throw new InvalidOperationException("Could not open smoke primary for corruption step.");
        file.StoreString("{ definitely-not-valid-json");
        file.Flush();
    }

    private static void Cleanup()
    {
        RemoveIfExists(SmokePath);
        RemoveIfExists(SmokePath + ".bak");
        RemoveIfExists(SmokePath + ".tmp");
    }

    private static void RemoveIfExists(string path)
    {
        if (!FileAccess.FileExists(path))
            return;
        var result = DirAccess.RemoveAbsolute(path);
        if (result != Error.Ok)
            GD.PushWarning($"Persistence smoke cleanup could not remove {path}: {result}.");
    }
}
