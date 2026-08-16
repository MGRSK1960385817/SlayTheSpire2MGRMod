using STS2RitsuLib.Audio;

namespace MGRMod.Characters;

internal static class MgrAudio
{
    private const float CharacterSelectVolumeMultiplier = 0.8f;
    // Linear playback ratio for Writing.ogg while Imagine/Create owns its card
    // selection screen. Raise/lower this value to tune only that ambience.
    internal const float ImagineCreateWritingLoopVolume = 0.30f;
    // Linear playback ratio for Glitch.ogg while Failure Girl owns its card
    // selection screen. Raise/lower this value to tune only that loop.
    internal const float FlawedGirlGlitchLoopVolume = 0.30f;

    // Character profiles require an event-like identifier. Selection call sites route
    // this private sentinel to the packed OGG through RitsuLib's resource backend.
    internal const string CharacterSelectSfx = "mgr://audio/character_select";
    internal const string CharacterSelectResource = $"{Entry.ResPath}/audio/MGR_charselect.ogg";
    internal const string NoteChannelResource = $"{Entry.ResPath}/audio/NoteChannel.ogg";
    internal const string ChordResource = $"{Entry.ResPath}/audio/Chord.ogg";
    internal const string WritingResource = $"{Entry.ResPath}/audio/Writing.ogg";
    internal const string GlitchResource = $"{Entry.ResPath}/audio/Glitch.ogg";

    internal static void PlayCharacterSelect(float volume = 1f)
    {
        float effectiveVolume = Math.Clamp(
            volume * CharacterSelectVolumeMultiplier,
            0f,
            1f);
        AudioPlayResult result = PlayResource(
            CharacterSelectResource,
            "MGR character select",
            effectiveVolume,
            AudioLifecycleScope.Screen);

        if (!result.Succeeded)
            GameFmod.Studio.PlayOneShot(
                "event:/sfx/characters/silent/silent_select",
                effectiveVolume);
    }

    internal static void PlayNoteChannel(float volume = 0.2f) =>
        PlayResource(NoteChannelResource, "MGR note channel", volume, AudioLifecycleScope.Combat);

    internal static void PlayChord(float volume = 0.2f) =>
        PlayResource(ChordResource, "MGR chord", volume, AudioLifecycleScope.Combat);

    /// <summary>
    /// Starts the writing ambience used while Imagine/Create owns its grayscale
    /// selection screen. The caller owns the returned handle and must stop and
    /// release it together with that screen's lease.
    /// </summary>
    internal static IAudioHandle? BeginWritingLoop(
        float volume = ImagineCreateWritingLoopVolume)
    {
        return BeginResourceLoop(
            WritingResource,
            "MGR Imagine/Create writing loop",
            volume);
    }

    /// <summary>
    /// Starts the glitch ambience for Failure Girl's choice screen. The filter
    /// lease owns this handle and stops it as soon as the choice closes.
    /// </summary>
    internal static IAudioHandle? BeginGlitchLoop(
        float volume = FlawedGirlGlitchLoopVolume)
    {
        return BeginResourceLoop(
            GlitchResource,
            "MGR Failure Girl glitch loop",
            volume);
    }

    private static IAudioHandle? BeginResourceLoop(
        string resource,
        string debugName,
        float volume)
    {
        IAudioHandle? handle = GameFmod.Playback.PlayLoop(
            AudioSource.StreamingResourceMusic(resource),
            new AudioPlaybackOptions
            {
                Volume = volume,
                Scope = AudioLifecycleScope.Combat,
                AllowFadeOutOnStop = false,
                DebugName = debugName
            });

        if (handle is null)
            Entry.Logger.Warn($"Could not start {debugName}.");

        return handle;
    }

    private static AudioPlayResult PlayResource(
        string resource,
        string debugName,
        float volume,
        AudioLifecycleScope scope)
    {
        AudioPlayResult result = GameFmod.Playback.PlayOneShot(
            AudioSource.ResourceFile(resource),
            new AudioPlaybackOptions
            {
                Volume = volume,
                Scope = scope,
                DebugName = debugName
            });

        if (!result.Succeeded)
        {
            Entry.Logger.Warn(
                $"Could not play {debugName} ({result.Status}: {result.Message}).");
        }

        return result;
    }
}
