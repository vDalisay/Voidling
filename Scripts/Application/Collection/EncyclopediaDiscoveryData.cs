namespace Voidling.Application.Collection;

/// <summary>The first time a journal entry was discovered in this save.</summary>
public sealed class EncyclopediaDiscoveryData
{
    public string EntryId { get; set; } = "";

    /// <summary>1 for the first entry ever discovered, 2 for the next, and so on.</summary>
    public int Order { get; set; }

    /// <summary>The Voidling that unlocked it, as it was named then.</summary>
    public string CreatureName { get; set; } = "";

    /// <summary>That Voidling's palette hue, so the journal shows the one the player actually had.</summary>
    public float PaletteHue { get; set; } = -1.0f;
}
