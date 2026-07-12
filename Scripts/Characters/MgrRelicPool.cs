using Godot;
using STS2RitsuLib.Scaffolding.Content;

namespace SlayTheSpire2MGRMod.Characters;

public sealed class MgrRelicPool : TypeListRelicPoolModel
{
    public override string EnergyColorName => "MGR";
    public override Color LabOutlineColor => MgrCharacter.ThemeColor;
    public override string? BigEnergyIconPath => $"{Entry.ResPath}/images/characters/energy_big.png";
    public override string? TextEnergyIconPath => $"{Entry.ResPath}/images/characters/energy_text.png";
}
