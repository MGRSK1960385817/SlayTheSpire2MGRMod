using STS2RitsuLib.Audio;

namespace SlayTheSpire2MGRMod.Characters;

internal static class MgrAudio
{
    // Character profiles require an event-like identifier. Selection call sites route
    // this private sentinel to the packed OGG through RitsuLib's resource backend.
    internal const string CharacterSelectSfx = "mgr://audio/character_select";
    internal const string CharacterSelectResource = $"{Entry.ResPath}/audio/MGR_charselect.ogg";
    internal const string NoteChannelResource = $"{Entry.ResPath}/audio/NoteChannel.ogg";
    internal const string ChordResource = $"{Entry.ResPath}/audio/Chord.ogg";

    internal static void PlayCharacterSelect(float volume = 1f)
    {
        AudioPlayResult result = PlayResource(
            CharacterSelectResource,
            "MGR character select",
            volume,
            AudioLifecycleScope.Screen);

        if (!result.Succeeded)
            GameFmod.Studio.PlayOneShot("event:/sfx/characters/silent/silent_select", volume);
    }

    internal static void PlayNoteChannel(float volume = 0.2f) =>
        PlayResource(NoteChannelResource, "MGR note channel", volume, AudioLifecycleScope.Combat);

    internal static void PlayChord(float volume = 0.2f) =>
        PlayResource(ChordResource, "MGR chord", volume, AudioLifecycleScope.Combat);

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
