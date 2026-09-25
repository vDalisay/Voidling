using System.Collections.Generic;
using Godot;

namespace Voidling.Presentation.UI.Audio;

/// <summary>
/// The pool of voices behind <see cref="UiSounds"/>: a few players on the UI bus, round-robin, with
/// a minimum gap per cue so a sweep across a grid of buttons does not machine-gun.
/// </summary>
public partial class UiSoundPlayer : Node
{
    private const string NodeName = "UiSounds";
    private const string Folder = "res://Assets/Sprout Sorry pack/Audio/";
    private const int Voices = 8;

    private readonly record struct Sound(string File, float VolumeDb, float Pitch, float Wobble, ulong MinGapMsec);

    // The pack's files are imported trimmed and normalised (see their .import files), so these
    // volumes set the balance between cues.
    private static readonly Dictionary<UiCue, Sound> Sounds = new()
    {
        [UiCue.Hover] = new("blup_1.wav", -20.0f, 1.18f, 0.05f, 45),
        [UiCue.Press] = new("blup_2.wav", -12.0f, 1.0f, 0.04f, 30),
        [UiCue.ToggleOn] = new("squick_1.wav", -10.0f, 1.08f, 0.0f, 30),
        [UiCue.ToggleOff] = new("squick_2.wav", -10.0f, 0.92f, 0.0f, 30),
        [UiCue.Open] = new("bip_1.wav", -15.0f, 1.0f, 0.0f, 60),
        [UiCue.Close] = new("bip_1.wav", -17.0f, 0.82f, 0.0f, 60),
        [UiCue.Confirm] = new("bing_1.wav", -10.0f, 1.0f, 0.0f, 60),
        [UiCue.Reward] = new("flute_1.wav", -9.0f, 1.0f, 0.0f, 120),
        [UiCue.Celebrate] = new("flute_3.wav", -8.0f, 1.0f, 0.0f, 120),
        [UiCue.Denied] = new("boo_1.wav", -14.0f, 1.0f, 0.0f, 120),
        [UiCue.Coin] = new("blup_2.wav", -13.0f, 1.1f, 0.0f, 0)
    };

    private readonly List<AudioStreamPlayer> _voices = new();
    private readonly Dictionary<string, AudioStream?> _streams = new();
    private readonly Dictionary<UiCue, ulong> _lastPlayed = new();
    private int _next;

    private static UiSoundPlayer? _joining;

    public static UiSoundPlayer? For(Node context)
    {
        var root = context.GetTree().Root;
        if (root.GetNodeOrNull<UiSoundPlayer>(NodeName) is { } existing)
            return existing.IsInsideTree() ? existing : null;
        if (_joining != null && GodotObject.IsInstanceValid(_joining))
            return null;

        // Joins the root at the end of the frame (the root may be busy adding children now); the
        // very first cue is skipped rather than delayed.
        _joining = new UiSoundPlayer { Name = NodeName };
        root.CallDeferred(Node.MethodName.AddChild, _joining);
        return null;
    }

    public override void _Ready()
    {
        _joining = null;
        ProcessMode = ProcessModeEnum.Always;
        for (var index = 0; index < Voices; index++)
        {
            var voice = new AudioStreamPlayer { Bus = "UI" };
            AddChild(voice);
            _voices.Add(voice);
        }
    }

    public void Play(UiCue cue, int step)
    {
        if (!Sounds.TryGetValue(cue, out var sound))
            return;

        var now = Time.GetTicksMsec();
        if (_lastPlayed.TryGetValue(cue, out var last) && now - last < sound.MinGapMsec)
            return;
        // A press right after a hover covers it; a list rebuilt under the pointer should not blip.
        if (cue == UiCue.Hover && _lastPlayed.TryGetValue(UiCue.Press, out var pressed) && now - pressed < 150)
            return;
        _lastPlayed[cue] = now;

        var stream = Stream(sound.File);
        if (stream == null)
            return;

        var voice = _voices[_next];
        _next = (_next + 1) % _voices.Count;
        voice.Stream = stream;
        voice.VolumeDb = sound.VolumeDb;
        var wobble = sound.Wobble <= 0.0f ? 1.0f : 1.0f + (float)GD.RandRange(-sound.Wobble, sound.Wobble);
        voice.PitchScale = sound.Pitch * wobble * (1.0f + 0.06f * Mathf.Clamp(step, 0, 10));
        voice.Play();
    }

    private AudioStream? Stream(string file)
    {
        if (!_streams.TryGetValue(file, out var stream))
        {
            stream = GD.Load<AudioStream>(Folder + file);
            _streams[file] = stream;
        }
        return stream;
    }
}
