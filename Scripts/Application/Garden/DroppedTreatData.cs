namespace Voidling.Application.Garden;

/// <summary>
/// A treat the player has put on the ground, waiting for whichever Voidling reaches it first. It is
/// owned but not yet spent: the training item leaves the satchel only when something eats it, so a
/// drop that survives a quit still costs the player nothing.
/// </summary>
public sealed class DroppedTreatData
{
    public string Id { get; set; } = string.Empty;
    public string StatId { get; set; } = string.Empty;
    public float X { get; set; }
    public float Y { get; set; }
}
