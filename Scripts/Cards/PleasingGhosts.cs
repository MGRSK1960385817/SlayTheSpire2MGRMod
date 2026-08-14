using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Vfx.Cards;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "pleasing_ghosts")]
public sealed class PleasingGhosts : MgrCard
{
    protected override IEnumerable<string> ExtraRunAssetPaths =>
        NNightmareHandsVfx.AssetPaths;

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<IntangiblePower>()
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        base.CanonicalKeywords.Concat(
            [CardKeyword.Ethereal, CardKeyword.Exhaust]);

    public override NoteKind? NoteOverride => NoteKind.Ghost;

    public PleasingGhosts() : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // The original texture already contains dark violet shading. Multiplying
        // it by near-black made the hands almost invisible, so retain its native
        // colors and full opacity here.
        MgrSignatureVfx.SpawnNightmareHands(Owner);

        for (int index = 0; index < 2; index++)
        {
            await MgrCurseUtils.AddRandomCurseToCombat(
                Owner,
                PileType.Discard,
                pilePreviewDuration: 1.05f,
                pilePreviewWait: 0.32f);
        }
    }

    protected override void OnUpgrade()
    {
        RemoveKeyword(CardKeyword.Ethereal);
    }
}
