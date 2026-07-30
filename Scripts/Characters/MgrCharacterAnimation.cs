using STS2RitsuLib.Scaffolding.Visuals;
using STS2RitsuLib.Scaffolding.Visuals.Definition;

namespace SlayTheSpire2MGRMod.Characters;

/// <summary>
/// Central definition for MGR's texture-sequence character animation.
/// Keep timing and resource layout here so future animation sets can be replaced
/// without editing the character registration or the Godot scene.
/// </summary>
internal static class MgrCharacterAnimation
{
    internal const string IdleCue = "idle";
    internal const int IdleFrameCount = 100;
    // Keep every exported source frame and play them at the preferred 24 FPS.
    internal const float IdleFramesPerSecond = 24f;

    private const string AnimationRoot =
        $"{MgrCharacter.ImageRoot}/animations";

    internal static VisualCueSet CombatCues { get; } = BuildCombatCues();

    private static VisualCueSet BuildCombatCues()
    {
        VisualCueSetBuilder cues = ModVisualCues.CueSet();
        cues.Sequence(IdleCue, sequence =>
        {
            for (int frame = 1; frame <= IdleFrameCount; frame++)
            {
                sequence.Frame(
                    $"{AnimationRoot}/idle/idle_{frame:000}.png",
                    1f / IdleFramesPerSecond);
            }

            sequence.Loop();
        });

        return cues.Build();
    }
}
