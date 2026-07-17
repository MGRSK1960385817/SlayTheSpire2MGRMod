using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace SlayTheSpire2MGRMod.Mechanics;

/// <summary>
/// Tracks card DynamicVar changes that explicitly last for one combat. Values
/// are restored on combat end so combat cards that share run-deck state cannot
/// leak their temporary growth into later rooms.
/// </summary>
public static class MgrCombatCardMutationState
{
    private static readonly Dictionary<(CardModel Card, string VarName), decimal> Deltas = [];

    public static decimal Increase(CardModel card, string varName, decimal amount)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (amount == 0m)
            return card.DynamicVars[varName].BaseValue;

        DynamicVar dynamicVar = card.DynamicVars[varName];
        dynamicVar.BaseValue += amount;
        var key = (card, varName);
        Deltas[key] = Deltas.TryGetValue(key, out decimal delta)
            ? delta + amount
            : amount;
        return dynamicVar.BaseValue;
    }

    public static void Clear()
    {
        foreach (((CardModel card, string varName), decimal delta) in Deltas)
        {
            if (card.DynamicVars.TryGetValue(varName, out DynamicVar? dynamicVar))
                dynamicVar.BaseValue -= delta;
        }

        Deltas.Clear();
    }
}
