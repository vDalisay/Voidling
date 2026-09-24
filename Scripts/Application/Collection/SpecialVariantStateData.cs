namespace Voidling.Application.Collection;

public enum SpecialVariantStatus
{
    /// <summary>Never spawned in this save.</summary>
    NotSpawned,

    /// <summary>Its egg exists and is waiting to hatch on an unused special environment.</summary>
    Egg,

    Alive,

    /// <summary>Died or was said goodbye to; a respawn egg can be bought.</summary>
    Departed
}

/// <summary>Where one special variant stands in this save. At most one of each exists at a time.</summary>
public sealed class SpecialVariantStateData
{
    public string VariantId { get; set; } = "";

    /// <summary>The one-time spawn through breeding has happened; later ones come from respawn eggs.</summary>
    public bool BreedingSpawnUsed { get; set; }

    public SpecialVariantStatus Status { get; set; } = SpecialVariantStatus.NotSpawned;

    /// <summary>The egg's ID while <see cref="SpecialVariantStatus.Egg"/>; the creature's (same ID) once hatched.</summary>
    public string CreatureId { get; set; } = "";
}
