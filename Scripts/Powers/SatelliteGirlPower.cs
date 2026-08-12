using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace SlayTheSpire2MGRMod.Powers;

[RegisterPower]
public sealed class SatelliteGirlPower : ModPowerTemplate
{
    private bool _triggeredThisTurn;

    /// <summary>
    /// Read by MGR's character-local aura. Keeping the visual derived from the
    /// power state avoids persistent combat-overlay nodes and save-state data.
    /// </summary>
    public bool IsAvailableThisTurn => !_triggeredThisTurn;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/powers/SatelliteGirlPower.png",
        BigIconPath: $"{Entry.ResPath}/images/powers/SatelliteGirlPower.png");

    public override Task AfterPlayerTurnStart(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        if (player.Creature == Owner)
            _triggeredThisTurn = false;

        return Task.CompletedTask;
    }

    public override async Task AfterEnergySpent(CardModel card, int amount)
    {
        if (_triggeredThisTurn ||
            amount <= 0 ||
            card.Owner.Creature != Owner ||
            card.Owner.PlayerCombatState?.Energy != 0)
        {
            return;
        }

        int notes = Math.Max(0, (int)Amount);
        if (notes == 0)
            return;

        _triggeredThisTurn = true;
        Flash();
        var choiceContext = new ThrowingPlayerChoiceContext();
        for (int index = 0; index < notes; index++)
            await MgrNoteSystem.ChannelNote(choiceContext, card.Owner, NoteKind.Starry);
    }
}
