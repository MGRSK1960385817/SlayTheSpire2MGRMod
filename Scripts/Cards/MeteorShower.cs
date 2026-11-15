using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using MGRMod.Characters;
using MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace MGRMod.Cards;

[RegisterCard(typeof(MgrCardPool), StableEntryStem = "meteor_shower")]
public sealed class MeteorShower : MgrCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(3m, ValueProp.Move),
        new IntVar("Hits", 2m),
        new CalculationBaseVar(2m),
        new CalculationExtraVar(1m),
        new CalculatedVar("TotalHits").WithMultiplier(
            static (card, _) =>
                card.CombatState is not null &&
                MgrCombatStateStore.TryGet(card.Owner, out MgrCombatState state)
                    ? state.StarryNotesGeneratedThisCombat
                    : 0m)
    ];

    public override bool IsStarryCard => true;

    public MeteorShower() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        int hitCount = (int)((CalculatedVar)DynamicVars["TotalHits"]).Calculate(null);
        await MgrSignatureVfx.PlayMeteorShower(this, cardPlay.Target, hitCount);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitCount(hitCount)
            .WithHitVfxNode(target =>
                MgrAttackVfx.CreateStarryImpact(
                    target,
                    MgrAttackVfx.StarPurple,
                    0.9f))
            .WithHitFx(null, null, "blunt_attack.mp3")
            .OnlyPlayAnimOnce()
            .Execute(choiceContext);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(1m);
}
