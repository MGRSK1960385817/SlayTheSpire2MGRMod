using Godot;
using STS2RitsuLib.Scaffolding.Content;

namespace MGRMod.Characters;

public sealed class MgrPotionPool : TypeListPotionPoolModel
{
    public override string EnergyColorName => "MGR";
    public override Color LabOutlineColor => MgrCharacter.ThemeColor;
    public override string? BigEnergyIconPath => $"{Entry.ResPath}/images/characters/energy_big.png";
    public override string? TextEnergyIconPath => $"{Entry.ResPath}/images/characters/energy_text.png";
}
