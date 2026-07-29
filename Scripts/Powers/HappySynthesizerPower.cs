using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace SlayTheSpire2MGRMod.Powers;

[RegisterPower]
public sealed class HappySynthesizerPower : ModPowerTemplate
{
    private readonly HashSet<NoteKind> _playedKindsThisTurn = [];
    private int _rewardsGrantedThisTurn;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/cards/HappySynthesizer.png",
        BigIconPath: $"{Entry.ResPath}/images/cards/HappySynthesizer.png");

    public async Task ObservePlayedNoteKind(
        PlayerChoiceContext choiceContext,
        NoteKind kind)
    {
        if (!_playedKindsThisTurn.Add(kind))
            return;

        int earnedRewards = _playedKindsThisTurn.Count / 3;
        while (_rewardsGrantedThisTurn < earnedRewards)
        {
            _rewardsGrantedThisTurn++;
            Flash();
            await PowerCmd.Apply<StrengthPower>(
                choiceContext,
                Owner,
                Amount,
                Owner,
                cardSource: null);
            await PowerCmd.Apply<DexterityPower>(
                choiceContext,
                Owner,
                Amount,
                Owner,
                cardSource: null);
        }
    }

    public override Task AfterPlayerTurnStartEarly(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        if (player.Creature == Owner)
        {
            _playedKindsThisTurn.Clear();
            _rewardsGrantedThisTurn = 0;
        }

        return Task.CompletedTask;
    }
}
