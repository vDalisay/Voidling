namespace VoidlingGame;

/// <summary>
/// Persisted output of a successful hatch. Shell identity and visual/source metadata are kept
/// independently from the creature so future shell presentation/economy rules do not need the
/// original egg to remain in OwnedEggs.
/// </summary>
public sealed class EggShellData
{
    public string Id { get; set; } = "";
    public EggSource Source { get; set; }
    public string TintHex { get; set; } = "#F6F0C9";
}
