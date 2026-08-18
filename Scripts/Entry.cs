using System.Reflection;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MGRMod.Characters;
using STS2RitsuLib;
using STS2RitsuLib.Interop;
using STS2RitsuLib.Patching.Core;
using MGRMod.Patches;
using MGRMod.Settings;
using MGRMod.Telemetry;
using Logger = MegaCrit.Sts2.Core.Logging.Logger;

namespace MGRMod;

[ModInitializer(nameof(Initialize))]
public partial class Entry
{
    public const string ModId = "MGRMod";
    public const string ResPath = $"res://{ModId}";

    public static Logger Logger { get; } = new(ModId, LogType.Generic);

    private static ModPatcher? _runtimePatcher;
    public static bool IsModActive { get; private set; }

    public static void Initialize()
    {
        if (IsModActive)
            return;

        Assembly assembly = Assembly.GetExecutingAssembly();

        // Godot scene scripts and RitsuLib content attributes use separate discovery paths.
        RitsuLibFramework.EnsureGodotScriptsRegistered(assembly, Logger);
        MgrVisualSettings.Register();
        MgrAudio.RegisterBank();

        _runtimePatcher ??= RitsuLibFramework.CreatePatcher(ModId, "runtime", "runtime integration");
        _runtimePatcher.RegisterPatch<MgrAudioVolumePatch>();
        _runtimePatcher.RegisterPatch<MgrPerformanceDescriptionPatch>();
        _runtimePatcher.RegisterPatch<MgrPerformancePowerCardVfxPatch>();
        _runtimePatcher.RegisterPatch<MgrHoverTipOrderPatch>();
        _runtimePatcher.RegisterPatch<MgrManimaniTargetPreviewPatch>();
        _runtimePatcher.RegisterPatch<MgrCrossCharacterCombatCardPoolPatch>();
        _runtimePatcher.RegisterPatch<MgrCrossCharacterRewardCardPoolPatch>();
        _runtimePatcher.RegisterPatch<MgrOrobasCardPoolScopePatch>();
        _runtimePatcher.RegisterPatch<MgrOrobasCharacterListPatch>();
        _runtimePatcher.RegisterPatch<MgrKaleidoscopeCardPoolScopePatch>();
        _runtimePatcher.RegisterPatch<MgrScopedCharacterCardPoolsPatch>();
        if (!RitsuLibFramework.ApplyRequiredPatcher(
                _runtimePatcher,
                () => IsModActive = false,
                "MGR runtime patches failed; initialization aborted."))
            return;

        ModTypeDiscoveryHub.RegisterModAssembly(ModId, assembly);
        MgrTelemetry.Register();

        IsModActive = true;
        Logger.Info("MGRMod initialized.");
    }
}
