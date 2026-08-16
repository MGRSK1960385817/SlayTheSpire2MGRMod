using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MGRMod.Characters;
using MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "living_dream")]
public sealed class LivingDream : MgrCard
{
    public LivingDream() : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        MgrAbilityVfx.SpawnCastBurst(
            Owner.Creature,
            MgrAbilityVfxStyle.Mirage,
            1.04f);
        MgrAbilityVfx.SpawnCastBurst(
            Owner.Creature,
            MgrAbilityVfxStyle.Galaxy,
            0.72f);

        IReadOnlyList<MgrNote> notes = MgrNoteSystem.RemoveAllNotes(Owner);
        foreach (MgrNote note in notes)
        {
            CardModel? card = MgrNoteCardFactory.CreateRandomCard(Owner, note.Kind, IsUpgraded);
            if (card is null)
                continue;

            // Follow Sculpting Strike's native keyword mutation path. Applying
            // the keywords to the mutable combat instance before it enters the
            // hand also makes the standard card renderer append and refresh the
            // Exhaust/Ethereal rules text and hover tips automatically.
            CardCmd.ApplyKeyword(
                card,
                CardKeyword.Exhaust,
                CardKeyword.Ethereal);

            // Match Blade Dance's fast hand-generation presentation: let the
            // native hand add animate the card and avoid the separate 1.2s
            // centre-screen pile preview.
            await CardPileCmd.AddGeneratedCardToCombat(
                card,
                PileType.Hand,
                Owner);
            await Cmd.Wait(0.1f);
        }
    }

    protected override void OnUpgrade()
    {
    }
}
