namespace Voidling.Application.Ports;

public enum GameStateRecoveryStatus
{
    None,
    RecoveredFromBackup
}

/// <summary>
/// Optional diagnostic companion to IGameStateRepository. Gameplay callers only depend on the
/// narrow repository contract; the lifetime shell may inspect this after Load to surface a
/// user-friendly recovery notice without learning file-system details.
/// </summary>
public interface IGameStateRecoveryInfo
{
    GameStateRecoveryStatus LastLoadRecoveryStatus { get; }
}
