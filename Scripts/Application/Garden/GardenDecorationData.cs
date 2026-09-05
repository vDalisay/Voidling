namespace Voidling.Application.Garden;

/// <summary>
/// Persisted cosmetic Garden object. Decorations are deliberately separate from functional land
/// modules and never carry training/stat fields.
/// </summary>
public sealed class GardenDecorationData
{
    public string Id { get; set; } = string.Empty;
    public string TypeId { get; set; } = string.Empty;
    public float X { get; set; }
    public float Y { get; set; }
}
