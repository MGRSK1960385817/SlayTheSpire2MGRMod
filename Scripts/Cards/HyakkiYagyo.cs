using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using SlayTheSpire2MGRMod.Characters;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace SlayTheSpire2MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "hyakki_yagyo")]
public sealed class HyakkiYagyo : MgrCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(10m, ValueProp.Move),
        new IntVar("NotesPerEnemy", 1m)
    ];

    protected override MgrKeywordKind KeywordKinds => MgrKeywordKind.CurseNote;

    public HyakkiYagyo() : base(
        2,
        CardType.Attack,
        CardRarity.Rare,
        TargetType.AllEnemies)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        if (CombatState is not { } combatState)
            return;

        int enemiesHit = combatState.HittableEnemies.Count;
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(combatState)
            .WithHitVfxNode(target => MgrAttackVfx.CreateGaseousImpact(
                target,
                MgrAttackVfx.CurseDarkRed,
                1.05f))
            .WithHitFx(null, null, "blunt_attack.mp3")
            .Execute(choiceContext);

        int notesToGenerate = enemiesHit * DynamicVars["NotesPerEnemy"].IntValue;
        for (int index = 0; index < notesToGenerate; index++)
            await MgrNoteSystem.ChannelNote(choiceContext, Owner, NoteKind.Curse);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
        DynamicVars["NotesPerEnemy"].UpgradeValueBy(1m);
    }
}
