using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Platform;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace SlayTheSpire2MGRMod.Powers;

[RegisterPower]
public sealed class GiveItToYouPower : ModPowerTemplate
{
    private Player? _playerTarget;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new StringVar("TargetPlayer")];

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/GiveItToYouPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/GiveItToYouPower.png");

    public Player PlayerTarget
    {
        get => _playerTarget ?? throw new InvalidOperationException(
            "GiveItToYouPower was applied without a player target.");
        set
        {
            AssertMutable();
            _playerTarget = value;
            ((StringVar)DynamicVars["TargetPlayer"]).StringValue =
                PlatformUtil.GetPlayerName(
                    RunManager.Instance.NetService.Platform,
                    value.NetId);
        }
    }

    public bool HasPlayerTarget(Player player) =>
        ReferenceEquals(_playerTarget, player);

    public bool TryGetLivingTarget(out Player target)
    {
        target = _playerTarget!;
        return target is not null && target.Creature.IsAlive;
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != Owner.Side || !participants.Contains(Owner))
            return;

        await PowerCmd.Remove(this);
    }
}
