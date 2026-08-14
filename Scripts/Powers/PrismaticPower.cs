using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using SlayTheSpire2MGRMod.Mechanics;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace SlayTheSpire2MGRMod.Powers;

[RegisterPower]
public sealed class PrismaticPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/cards/Prismatic.png",
        BigIconPath: $"{Entry.ResPath}/images/cards/Prismatic.png");

    public async Task OnChordTriggered(
        PlayerChoiceContext choiceContext,
        IReadOnlyList<MgrNote> notes)
    {
        if (notes.Select(note => note.Kind).Distinct().Take(3).Count() < 3)
            return;

        Flash();
        MgrAbilityVfx.SpawnCastBurst(
            Owner,
            MgrAbilityVfxStyle.Prism,
            0.74f);
        MgrSignatureVfx.SpawnRainbowStarRing(Owner);
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
