using Godot;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace SlayTheSpire2MGRMod.Characters;

/// <summary>
/// Hidden colorless-style pool for cards that exist only as choice buttons.
/// Token rarity keeps them out of ordinary MGR card rewards.
/// </summary>
[RegisterSharedCardPool]
public sealed class MgrTokenCardPool : TypeListCardPoolModel
{
    public override string Title => "MGR token";
    public override string EnergyColorName => "colorless";
    public override string CardFrameMaterialPath => "card_frame_colorless";
    public override Color DeckEntryCardColor => Colors.White;
    public override bool IsColorless => true;
}
