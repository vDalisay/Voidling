namespace Voidling.Presentation.Garden;

public enum GardenDecorationSize
{
    Small,
    Medium,
    Large
}

/// <summary>
/// The size a decoration is sold as. Menus draw decoration art at its true pixel size so it stays
/// crisp, which hides the Garden scale that tells a small tree from a large one; this names it
/// instead. Plain C#, so it is tested without the engine.
/// </summary>
public static class GardenDecorationSizing
{
    public static GardenDecorationSize For(float scale)
        => scale < 0.9f ? GardenDecorationSize.Small
            : scale > 1.1f ? GardenDecorationSize.Large
            : GardenDecorationSize.Medium;
}
