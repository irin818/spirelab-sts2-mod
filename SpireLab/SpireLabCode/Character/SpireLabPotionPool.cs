using BaseLib.Abstracts;
using Godot;

namespace SpireLab.SpireLabCode.Character;

public class SpireLabPotionPool : CustomPotionPoolModel
{
    public override Color LabOutlineColor => SpireLab.Color;


    public override string BigEnergyIconPath => $"{MainFile.ResPath}/images/surveyor/energy.png";
    public override string TextEnergyIconPath => $"{MainFile.ResPath}/images/surveyor/energy.png";
}
