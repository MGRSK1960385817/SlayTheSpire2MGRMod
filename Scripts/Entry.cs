using System.Reflection;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using STS2RitsuLib;
using STS2RitsuLib.Interop;
using Logger = MegaCrit.Sts2.Core.Logging.Logger;

namespace SlayTheSpire2MGRMod;

[ModInitializer(nameof(Initialize))]
public partial class Entry
{
    public const string ModId = "SlayTheSpire2MGRMod";
    public const string ResPath = $"res://{ModId}";

    public static Logger Logger { get; } = new(ModId, LogType.Generic);

    public static void Initialize()
    {
        Assembly assembly = Assembly.GetExecutingAssembly();

        // Godot scene scripts and RitsuLib content attributes use separate discovery paths.
        RitsuLibFramework.EnsureGodotScriptsRegistered(assembly, Logger);
        ModTypeDiscoveryHub.RegisterModAssembly(ModId, assembly);

        Logger.Info("SlayTheSpire2MGRMod initialized.");
    }
}
