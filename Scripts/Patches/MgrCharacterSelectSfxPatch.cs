using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.CustomRun;
using MGRMod.Characters;
using STS2RitsuLib.Patching.Models;

namespace MGRMod.Patches;

/// <summary>
/// Character selection normally sends CharacterSelectSfx directly to SfxCmd, which
/// only accepts FMOD event paths. MGR ships an OGG instead of a custom FMOD bank, so
/// selection call sites route only the MGR sentinel through RitsuLib resource audio.
/// </summary>
public sealed class MgrCharacterSelectSfxPatch : IPatchMethod
{
    private static readonly MethodInfo VanillaPlay = AccessTools.Method(
        typeof(SfxCmd),
        nameof(SfxCmd.Play),
        [typeof(string), typeof(float)]);

    private static readonly MethodInfo RoutedPlay = AccessTools.Method(
        typeof(MgrCharacterSelectSfxPatch),
        nameof(PlayCharacterSelectSfx),
        [typeof(string), typeof(float)]);

    public static string PatchId => "mgr_character_select_resource_sfx";
    public static string Description => "Routes MGR character selection to its packed OGG";

    public static ModPatchTarget[] GetTargets() =>
    [
        new(
            typeof(NCharacterSelectScreen),
            "SelectCharacter",
            [typeof(NCharacterSelectButton), typeof(CharacterModel)]),
        new(
            typeof(NCharacterSelectScreen),
            "OnLocalCharacterChangedForRandom",
            [typeof(CharacterModel)]),
        new(
            typeof(NCustomRunScreen),
            "SelectCharacter",
            [typeof(NCharacterSelectButton), typeof(CharacterModel)]),
        new(
            typeof(NMultiplayerLoadGameScreen),
            "AfterMultiplayerStarted",
            Type.EmptyTypes)
    ];

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.Calls(VanillaPlay))
                instruction.operand = RoutedPlay;

            yield return instruction;
        }
    }

    public static void PlayCharacterSelectSfx(string sfx, float volume)
    {
        if (sfx == MgrAudio.CharacterSelectSfx)
        {
            MgrAudio.PlayCharacterSelect(volume);
            return;
        }

        SfxCmd.Play(sfx, volume);
    }
}
