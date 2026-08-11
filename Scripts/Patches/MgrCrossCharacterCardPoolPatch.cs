using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;
using SlayTheSpire2MGRMod.Characters;
using STS2RitsuLib.Patching.Models;

namespace SlayTheSpire2MGRMod.Patches;

/// <summary>
/// Keeps MGR's highly character-dependent cards out of cross-character
/// generation for other characters. MGR's own rewards and generation effects
/// are deliberately left untouched.
/// </summary>
public sealed class MgrCrossCharacterCombatCardPoolPatch : IPatchMethod
{
    public static string PatchId => "mgr_cross_character_combat_card_pool";
    public static string Description =>
        "Excludes MGR cards from other characters' discovery and mixed rewards";

    public static ModPatchTarget[] GetTargets() =>
    [
        new(
            typeof(CardFactory),
            nameof(CardFactory.GetDistinctForCombat),
            [
                typeof(Player),
                typeof(IEnumerable<CardModel>),
                typeof(int),
                typeof(Rng)
            ]),
        new(
            typeof(CardFactory),
            nameof(CardFactory.GetForCombat),
            [
                typeof(Player),
                typeof(IEnumerable<CardModel>),
                typeof(int),
                typeof(Rng)
            ])
    ];

    public static void Prefix(
        Player player,
        ref IEnumerable<CardModel> cards)
    {
        if (player.Character is MgrCharacter)
            return;

        cards = ExcludeMgrCards(cards);
    }

    internal static IEnumerable<CardModel> ExcludeMgrCards(
        IEnumerable<CardModel> cards) =>
        cards.Where(static card => card.Pool is not MgrCardPool);
}

public sealed class MgrCrossCharacterRewardCardPoolPatch : IPatchMethod
{
    public static string PatchId => "mgr_cross_character_reward_card_pool";
    public static string Description =>
        "Excludes MGR cards from other characters' mixed card rewards";

    public static ModPatchTarget[] GetTargets() =>
    [
        new(
            typeof(CardCreationOptions),
            nameof(CardCreationOptions.GetPossibleCards),
            [typeof(Player)])
    ];

    public static void Postfix(Player player, ref IEnumerable<CardModel> __result)
    {
        if (player.Character is MgrCharacter)
            return;

        __result = MgrCrossCharacterCombatCardPoolPatch
            .ExcludeMgrCards(__result);
    }
}

public sealed class MgrOrobasCardPoolScopePatch : IPatchMethod
{
    [ThreadStatic]
    private static int _scopeDepth;

    internal static bool IsGeneratingOptions => _scopeDepth > 0;

    internal static void EnterScope() => _scopeDepth++;

    internal static void ExitScope() =>
        _scopeDepth = Math.Max(0, _scopeDepth - 1);

    public static string PatchId => "mgr_orobas_card_pool_scope";
    public static string Description =>
        "Marks Orobas option generation for MGR character-pool exclusion";

    public static ModPatchTarget[] GetTargets() =>
    [
        new(typeof(Orobas), "GenerateInitialOptions", Type.EmptyTypes)
    ];

    public static void Prefix() => EnterScope();

    public static void Postfix() => ExitScope();
}

public sealed class MgrKaleidoscopeCardPoolScopePatch : IPatchMethod
{
    public static string PatchId => "mgr_kaleidoscope_card_pool_scope";
    public static string Description =>
        "Prevents Kaleidoscope from selecting MGR as an external card pool";

    public static ModPatchTarget[] GetTargets() =>
    [
        new(typeof(Kaleidoscope), nameof(Kaleidoscope.AfterObtained), Type.EmptyTypes)
    ];

    public static void Prefix() =>
        MgrOrobasCardPoolScopePatch.EnterScope();

    public static void Postfix() =>
        MgrOrobasCardPoolScopePatch.ExitScope();
}

public sealed class MgrScopedCharacterCardPoolsPatch : IPatchMethod
{
    public static string PatchId => "mgr_scoped_character_card_pools";
    public static string Description =>
        "Filters MGR from scoped cross-character card-pool selection";

    public static ModPatchTarget[] GetTargets() =>
    [
        new(typeof(UnlockState), "get_CharacterCardPools", Type.EmptyTypes)
    ];

    public static void Postfix(ref IEnumerable<CardPoolModel> __result)
    {
        if (!MgrOrobasCardPoolScopePatch.IsGeneratingOptions)
            return;

        __result = __result.Where(static pool => pool is not MgrCardPool);
    }
}

public sealed class MgrOrobasCharacterListPatch : IPatchMethod
{
    public static string PatchId => "mgr_orobas_character_list";
    public static string Description =>
        "Prevents Orobas from selecting MGR as another character card pool";

    public static ModPatchTarget[] GetTargets() =>
    [
        new(typeof(UnlockState), "get_Characters", Type.EmptyTypes)
    ];

    public static void Postfix(ref IEnumerable<CharacterModel> __result)
    {
        if (!MgrOrobasCardPoolScopePatch.IsGeneratingOptions)
            return;

        __result = __result.Where(static character =>
            character is not MgrCharacter);
    }
}
