using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Vfx.Cards;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "pale_dread")]
public sealed class PaleDread : MgrCard
{
    protected override IEnumerable<string> ExtraRunAssetPaths =>
        NNightmareHandsVfx.AssetPaths;

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromCard<Pale>()
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(2)
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Exhaust];

    public PaleDread() : base(
        1,
        CardType.Skill,
        CardRarity.Uncommon,
        TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner.Creature.CombatState is not { } combatState)
            return;

        // A shorter, softer Nightmare gesture distinguishes this uncommon card
        // from Pleasing Ghosts' full two-second screen encirclement.
        MgrSignatureVfx.SpawnNightmareHands(
            Owner,
            visibleSeconds: 0.72f,
            initialAlpha: 0.74f);

        List<CardModel> paleCards = [];
        for (int index = 0; index < DynamicVars.Cards.IntValue; index++)
            paleCards.Add(combatState.CreateCard<Pale>(Owner));

        await CardPileCmd.AddGeneratedCardsToCombat(
            paleCards,
            PileType.Hand,
            Owner);
        await Cmd.Wait(0.1f);

        await MgrCurseUtils.AddRandomCurseToCombat(
            Owner,
            PileType.Discard,
            CardPilePosition.Random);
    }

    protected override void OnUpgrade() =>
        DynamicVars.Cards.UpgradeValueBy(1m);
}
