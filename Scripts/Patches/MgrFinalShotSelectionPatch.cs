using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MGRMod.Cards;
using STS2RitsuLib.Patching.Models;

namespace MGRMod.Patches;

/// <summary>Keep the synchronized hand picker and disallow confirming a single attack.</summary>
public sealed class MgrFinalShotSelectionPatch : IPatchMethod
{
    public static string PatchId => "mgr_final_shot_selection";
    public static string Description => "Allows Final Beat to confirm zero or exactly two attack cards";

    public static ModPatchTarget[] GetTargets() =>
        [new(typeof(NPlayerHand), "RefreshSelectModeConfirmButton")];

    public static void Postfix(
        CardSelectorPrefs ____prefs,
        List<CardModel> ____selectedCards,
        NConfirmButton ____selectModeConfirmButton)
    {
        if ((____prefs.Prompt?.LocEntryKey is FinalShot.RepeatSelectionKey or FinalShot.TitleKey) &&
            ____selectedCards.Count == 1)
        {
            ____selectModeConfirmButton.Disable();
        }
    }
}
