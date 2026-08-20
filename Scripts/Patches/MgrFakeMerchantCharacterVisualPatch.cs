using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Events.Custom;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MGRMod.Characters;
using STS2RitsuLib.Patching.Models;

namespace MGRMod.Patches;

/// <summary>
/// The base Fake Merchant layout creates a temporary combat visual and then
/// unconditionally starts a Spine animation. MGR uses a regular Godot scene, so
/// handle the single-player MGR case before either the Ironclad fallback or the
/// Spine-only startup can leave the custom event room half initialized.
/// </summary>
public sealed class MgrFakeMerchantCharacterVisualPatch : IPatchMethod
{
    private static readonly FieldInfo PlayersField =
        AccessTools.Field(typeof(NFakeMerchant), "_players");

    private static readonly FieldInfo CharacterContainerField =
        AccessTools.Field(typeof(NFakeMerchant), "_characterContainer");

    private static readonly FieldInfo EventField =
        AccessTools.Field(typeof(NFakeMerchant), "_event");

    private static readonly MethodInfo ShowWelcomeDialogueMethod =
        AccessTools.Method(typeof(NFakeMerchant), "ShowWelcomeDialogue");

    public static string PatchId => "mgr_fake_merchant_character_visual";
    public static string Description => "Safely initializes MGR visuals in the Fake Merchant event";

    public static ModPatchTarget[] GetTargets() =>
    [
        new(
            typeof(NFakeMerchant),
            "AfterRoomIsLoaded",
            Type.EmptyTypes)
    ];

    [HarmonyPriority(Priority.First)]
    public static bool Prefix(NFakeMerchant __instance)
    {
        List<Player> players =
            (List<Player>?)PlayersField.GetValue(__instance)
            ?? throw new InvalidOperationException("Fake Merchant player list was not initialized.");

        // Fake Merchant is a single-player event in the base game. Leave any
        // unexpected multiplayer/custom-event composition to the existing path.
        if (players.Count != 1 || players[0].Character is not MgrCharacter)
            return true;

        NCreatureVisuals? visuals = MgrCharacter.CreateStaticCreatureVisuals();
        if (visuals is null)
        {
            Entry.Logger.Warn("Could not create MGR visuals for the Fake Merchant event; using the existing fallback path.");
            return true;
        }

        Control characterContainer =
            (Control?)CharacterContainerField.GetValue(__instance)
            ?? throw new InvalidOperationException("Fake Merchant character container was not initialized.");

        characterContainer.AddChildSafely(visuals);
        characterContainer.MoveChildSafely(visuals, 0);
        visuals.Position = Vector2.Zero;

        FakeMerchant fakeMerchant =
            (FakeMerchant?)EventField.GetValue(__instance)
            ?? throw new InvalidOperationException("Fake Merchant event model was not initialized.");

        if (!fakeMerchant.StartedFight)
        {
            Task welcomeDialogue =
                (Task?)ShowWelcomeDialogueMethod.Invoke(__instance, null)
                ?? throw new InvalidOperationException("Fake Merchant welcome dialogue did not return a task.");
            TaskHelper.RunSafely(welcomeDialogue);
        }

        // The scene already displays MGR's idle frame. Suppressing the original
        // method is intentional: its next step dereferences SpineAnimation.
        return false;
    }
}
