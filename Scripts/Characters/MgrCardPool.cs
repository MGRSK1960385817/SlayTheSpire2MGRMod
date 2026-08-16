using Godot;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Utils;

namespace MGRMod.Characters;

public sealed class MgrCardPool : TypeListCardPoolModel
{
    private static readonly Material? FrameTint =
        MaterialUtils.CreateReplaceHueShaderMaterial(1f, 0.43f, 0f);

    public override string Title => "MGR";
    public override string EnergyColorName => "MGR";
    public override string? BigEnergyIconPath => $"{Entry.ResPath}/images/characters/energy_big.png";
    public override string? TextEnergyIconPath => $"{Entry.ResPath}/images/characters/energy_text.png";
    public override Color DeckEntryCardColor => MgrCharacter.ThemeColor;
    public override Color EnergyOutlineColor => new(0.32f, 0.08f, 0.02f);
    public override Material? PoolFrameMaterial => FrameTint;
    public override bool IsColorless => false;
}
