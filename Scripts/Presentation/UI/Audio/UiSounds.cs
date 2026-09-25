using Godot;

namespace Voidling.Presentation.UI.Audio;

/// <summary>What just happened in a menu, as far as its sound is concerned.</summary>
public enum UiCue
{
    /// <summary>The pointer arrives on a button.</summary>
    Hover,
    /// <summary>A button goes down.</summary>
    Press,
    /// <summary>A switch turns on.</summary>
    ToggleOn,
    /// <summary>A switch turns off.</summary>
    ToggleOff,
    /// <summary>A window pops open.</summary>
    Open,
    /// <summary>A window folds away.</summary>
    Close,
    /// <summary>A purchase, placement or gift went through.</summary>
    Confirm,
    /// <summary>A reward was claimed (check-in, mission, shells).</summary>
    Reward,
    /// <summary>Something big: an egg bought or laid, a Voidling hatched.</summary>
    Celebrate,
    /// <summary>A click on something that cannot be used right now.</summary>
    Denied,
    /// <summary>One sprout of a reward landing in the purse; each lands a little higher.</summary>
    Coin
}

/// <summary>
/// The menus' sounds, from the Sprout Lands Sorry pack, played on the UI bus so the Interface
/// volume setting (and Master) controls them. Like the effect layer, the player lives under the
/// scene root and is found from any node; every call is decoration and never waits.
///
/// Small, soft cues for the constant ones (hover, press) with a little pitch wobble so repetition
/// does not grate; the brighter chimes are kept for results the player earned.
/// </summary>
public static class UiSounds
{
    // A headless run (smokes, CI) has no one to hear it, and a sound still playing when such a run
    // quits is reported as a leaked resource.
    private static readonly bool Silent = DisplayServer.GetName() == "headless";

    public static void Play(Node context, UiCue cue, int step = 0)
    {
        if (!Silent && GodotObject.IsInstanceValid(context) && context.IsInsideTree())
            UiSoundPlayer.For(context)?.Play(cue, step);
    }
}
