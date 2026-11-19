using BaseLib.Abstracts;
using SpireLab.SpireLabCode.Extensions;
using Godot;

namespace SpireLab.SpireLabCode.Character;

public class SpireLabPotionPool : CustomPotionPoolModel
{
    public override Color LabOutlineColor => SpireLab.Color;


    public override string BigEnergyIconPath => "charui/big_energy.png".ImagePath();
    public override string TextEnergyIconPath => "charui/text_energy.png".ImagePath();
}
