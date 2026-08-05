using System.Reflection;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using STS2RitsuLib;
using STS2RitsuLib.Interop;
using STS2RitsuLib.Patching.Core;
using SlayTheSpire2MGRMod.Patches;
using SlayTheSpire2MGRMod.Telemetry;
using Logger = MegaCrit.Sts2.Core.Logging.Logger;

namespace SlayTheSpire2MGRMod;

[ModInitializer(nameof(Initialize))]
public partial class Entry
{
    public const string ModId = "SlayTheSpire2MGRMod";
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

        _runtimePatcher ??= RitsuLibFramework.CreatePatcher(ModId, "runtime", "runtime integration");
        _runtimePatcher.RegisterPatch<MgrCharacterSelectSfxPatch>();
        _runtimePatcher.RegisterPatch<MgrPerformanceDescriptionPatch>();
        if (!RitsuLibFramework.ApplyRequiredPatcher(
                _runtimePatcher,
                () => IsModActive = false,
                "MGR runtime patches failed; initialization aborted."))
            return;

        ModTypeDiscoveryHub.RegisterModAssembly(ModId, assembly);
        MgrTelemetry.Register();

        IsModActive = true;
        Logger.Info("SlayTheSpire2MGRMod initialized.");
    }
}
