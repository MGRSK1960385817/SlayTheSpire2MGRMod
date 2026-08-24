using MegaCrit.Sts2.Core.Commands;
using MGRMod.Settings;
using STS2RitsuLib.Audio;

namespace MGRMod.Characters;

internal static class MgrAudio
{
    // Shared linear gain for every MGR event except character selection.
    internal const float EventVolumeGain = 1.8f;

    // Linear gain applied only when MGR is selected on a character screen.
    // This is independent from EventVolumeGain so the UI sound can be tuned
    // without changing NoteChannel, Chord, Writing, or Glitch.
    // Player feedback on the real v0.107.1 client showed that only the
    // character-select event needed an additional ~4.1 dB over its old value.
    internal const float CharacterSelectVolumeGain = 7f;

    // Linear playback ratio for Writing.ogg while Imagine/Create owns its card
    // selection screen. Raise/lower this value to tune only that ambience.
    internal const float ImagineCreateWritingLoopVolume = 2f;
    // Linear playback ratio for Glitch.ogg while Failure Girl owns its card
    // selection screen. Raise/lower this value to tune only that loop.
    internal const float FlawedGirlGlitchLoopVolume = 2f;

    internal const string BankResource = $"{Entry.ResPath}/audio/MGR.bank";
    internal const string GuidMappingsResource = $"{Entry.ResPath}/audio/GUIDs.txt";

    internal const string CharacterSelectSfx = "event:/MGR/sfx/MGR_charselect";
    internal const string NoteChannelSfx = "event:/MGR/sfx/NoteChannel";
    internal const string ChordSfx = "event:/MGR/sfx/Chord";
    internal const string WritingSfx = "event:/MGR/sfx/Writing";
    internal const string GlitchSfx = "event:/MGR/sfx/Glitch";

    internal static void RegisterBank()
    {
        FmodStudioDeferredBankRegistration.RegisterBank(BankResource);
        FmodStudioDeferredBankRegistration.RegisterStudioGuidMappings(GuidMappingsResource);
        Entry.Logger.Info(
            $"Registered deferred MGR FMOD bank '{BankResource}' and GUID mappings '{GuidMappingsResource}'.");
    }

    internal static void PlayGeneratedNote(float volume = 1.4f)
    {
        if (MgrVisualSettings.ShouldPlayNoteSounds)
            PlayEvent(NoteChannelSfx, volume);
    }

    internal static void PlayChord(float volume = 1.5f)
    {
        if (MgrVisualSettings.ShouldPlayNoteSounds)
            PlayEvent(ChordSfx, volume);
    }

    // Some card-specific impacts deliberately reuse the packed NoteChannel
    // asset without representing note generation. Keep those cues independent
    // from the setting whose scope is note generation and Chord triggering.
    internal static void PlayNoteChannelCue(float volume = 1.4f) =>
        PlayEvent(NoteChannelSfx, volume);

    /// <summary>
    /// Starts the writing ambience used while Imagine/Create owns its grayscale
    /// selection screen. The caller owns the returned handle and must stop and
    /// release it together with that screen's lease.
    /// </summary>
    internal static IAudioHandle? BeginWritingLoop(
        float volume = ImagineCreateWritingLoopVolume)
    {
        float effectiveVolume = ApplyEventVolumeGain(WritingSfx, volume);
        return BeginEventLoop(
            WritingSfx,
            "MGR Imagine/Create writing loop",
            effectiveVolume);
    }

    /// <summary>
    /// Starts the glitch ambience for Failure Girl's choice screen. The filter
    /// lease owns this handle and stops it as soon as the choice closes.
    /// </summary>
    internal static IAudioHandle? BeginGlitchLoop(
        float volume = FlawedGirlGlitchLoopVolume)
    {
        float effectiveVolume = ApplyEventVolumeGain(GlitchSfx, volume);
        return BeginEventLoop(
            GlitchSfx,
            "MGR Failure Girl glitch loop",
            effectiveVolume);
    }

    internal static bool IsMgrEvent(string? eventPath) =>
        eventPath?.StartsWith("event:/MGR/", StringComparison.Ordinal) == true;

    internal static float GetEventVolumeGain(string eventPath) =>
        string.Equals(eventPath, CharacterSelectSfx, StringComparison.Ordinal)
            ? CharacterSelectVolumeGain
            : EventVolumeGain;

    internal static float ApplyEventVolumeGain(string eventPath, float volume) =>
        Math.Max(0f, volume * GetEventVolumeGain(eventPath));

    private static IAudioHandle? BeginEventLoop(
        string eventPath,
        string debugName,
        float volume)
    {
        IAudioHandle? handle = GameFmod.Playback.PlayLoop(
            AudioSource.Event(eventPath),
            new AudioPlaybackOptions
            {
                Volume = volume,
                Scope = AudioLifecycleScope.Combat,
                // The MGR Studio events do not define the vanilla convention's
                // named "loop" parameter. Their timeline/instrument controls
                // whether they repeat; the retained handle still stops them.
                UsesLoopParameter = false,
                AllowFadeOutOnStop = false,
                DebugName = debugName
            });

        if (handle is null)
        {
            Entry.Logger.Warn($"Could not start {debugName}.");
        }

        return handle;
    }

    private static void PlayEvent(string eventPath, float volume)
    {
        // Follow the same public entry point used by the base game. RitsuLib's
        // GUID mapping patch resolves this mod-bank event before the native
        // strings bank is queried.
        SfxCmd.Play(eventPath, volume);
    }
}
