namespace VoidlingGame;

/// <summary>
/// Persisted Chao Garden style progress for one stat. Every stat levels 0-99 whatever its rank;
/// <see cref="Progress"/> fills the bar toward the next level and each level-up adds stat
/// <see cref="Points"/>, which is what races read. The rank only changes how many points a
/// level-up is worth.
/// </summary>
public sealed class StatProgressData
{
    public int Level { get; set; }
    public int Progress { get; set; }
    public int Points { get; set; }
}
