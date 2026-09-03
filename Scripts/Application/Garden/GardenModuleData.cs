namespace Voidling.Application.Garden;

/// <summary>
/// Persisted semantic Garden module. SlotIndex is a logical placement slot rather than scene or
/// pixel geometry so authored Garden layouts can change without save-shape churn.
/// </summary>
public sealed class GardenModuleData
{
    public string Id { get; set; } = "";
    public string StatId { get; set; } = "";
    public int Level { get; set; } = 1;
    public int SlotIndex { get; set; } = -1;
}
