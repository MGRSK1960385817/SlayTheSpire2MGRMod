using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MGRMod.Characters;
using MGRMod.Mechanics;
using MGRMod.Powers;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "watching_u")]
public sealed class WatchingU : MgrCard
{
    protected override MgrKeywordKind KeywordKinds => MgrKeywordKind.StatusNote;

    public WatchingU() : base(
        2,
        CardType.Power,
        CardRarity.Rare,
        TargetType.Self)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        MgrSignatureVfx.SpawnWatchingEyes();
        await PowerCmd.Apply<WatchingUPower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            this);

        if (IsUpgraded)
            await ChannelNote(choiceContext, NoteKind.Status);
    }

    protected override void OnUpgrade()
    {
    }
}
